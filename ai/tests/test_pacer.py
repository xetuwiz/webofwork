"""Does the pacer space requests, or only appear to?

## Why

A live draft retried one Gemini key four times at 1.7s, 3.0s, 7.2s and 14.5s.
Every delay was longer than the last and every one ended in another 429, because
backoff is reactive: it starts once the provider has already refused. Nothing in
the system put a floor on how often the provider could be *asked*.

The tests below pin the three properties that make it a floor rather than a
delay-after-the-fact, and one property that is easy to get backwards.
"""

from __future__ import annotations

import time

from ai.llm.chain import Pacer, get_pacer


class TestSpacing:
    async def test_the_first_call_to_a_provider_is_not_delayed(self):
        # Otherwise every run pays the floor before it has learned anything.
        pacer = Pacer(min_interval=5.0)

        assert await pacer.wait("gemini") == 0.0

    async def test_a_second_call_waits_for_the_remainder_of_the_floor(self):
        pacer = Pacer(min_interval=0.30)
        await pacer.wait("gemini")

        started = time.monotonic()
        slept = await pacer.wait("gemini")
        elapsed = time.monotonic() - started

        assert slept > 0.0
        assert elapsed >= 0.25

    async def test_the_actual_gap_meets_the_floor(self):
        # The sleep and the wall clock are checked separately above. This is the
        # one that matters: it measures the interval between the two dispatches,
        # which is what a provider's rate limiter counts.
        floor = 0.25
        pacer = Pacer(min_interval=floor)
        await pacer.wait("gemini")

        dispatched: list[float] = []
        for _ in range(3):
            await pacer.wait("gemini")
            dispatched.append(time.monotonic())

        gaps = [b - a for a, b in zip(dispatched, dispatched[1:])]
        assert all(gap >= floor - 0.02 for gap in gaps), gaps

    async def test_a_zero_floor_never_waits(self):
        # The escape hatch, and the difference between "unset" and "chose none".
        pacer = Pacer(min_interval=0.0)
        await pacer.wait("gemini")

        assert await pacer.wait("gemini") == 0.0


class TestIsolation:
    async def test_one_busy_provider_does_not_delay_another(self):
        # The whole point of pacing per provider rather than globally. A draft
        # falling back from a hammered gemini to a cold cerebras must not wait
        # behind gemini's floor, or the fallback costs as much as the thing it
        # is falling back from.
        pacer = Pacer(min_interval=30.0)
        await pacer.wait("gemini")

        started = time.monotonic()
        await pacer.wait("cerebras")

        assert time.monotonic() - started < 0.05

    async def test_a_per_provider_override_beats_the_global_floor(self):
        pacer = Pacer(min_interval=10.0, overrides={"gemini": 0.0})
        await pacer.wait("gemini")

        assert await pacer.wait("gemini") == 0.0

    async def test_an_override_can_also_make_one_provider_slower(self):
        # A 50/day allowance needs more spacing than a per-minute one, which is
        # why the floor is per provider rather than a single number.
        pacer = Pacer(min_interval=0.0, overrides={"openrouter": 0.20})
        await pacer.wait("openrouter")

        started = time.monotonic()
        await pacer.wait("openrouter")

        assert time.monotonic() - started >= 0.15


class TestFailuresStillConsumeTheirSlot:
    async def test_a_429_does_not_hand_back_the_turn(self):
        # The subtle one. `wait()` stamps before sleeping rather than after the
        # response, so a provider that refuses *every* call is still spaced. If it
        # stamped on success instead, a permanently-429ing provider would be
        # retried instantly forever -- reintroducing the exact storm the floor
        # exists to prevent, while passing every test that only looks at the
        # successful path.
        floor = 0.20
        pacer = Pacer(min_interval=floor)
        await pacer.wait("gemini")

        dispatched: list[float] = []
        for _ in range(3):
            await pacer.wait("gemini")
            dispatched.append(time.monotonic())
            # No success. Every one of these is a 429 as far as the caller cares.

        gaps = [b - a for a, b in zip(dispatched, dispatched[1:])]
        assert all(gap >= floor - 0.02 for gap in gaps), gaps


class TestAccounting:
    async def test_waits_are_visible_rather_than_an_unexplained_gap(self):
        # A pause in the timeline that nothing logs reads as a hang.
        pacer = Pacer(min_interval=0.15)
        await pacer.wait("gemini")
        await pacer.wait("gemini")

        assert pacer.waits == 1
        assert pacer.waited_seconds > 0.0

    async def test_no_wait_is_still_counted_as_no_wait(self):
        pacer = Pacer(min_interval=5.0)
        await pacer.wait("gemini")

        assert pacer.waits == 0
        assert pacer.waited_seconds == 0.0


class TestBuiltFromSettings:
    def test_unset_provider_overrides_inherit_rather_than_silence(self):
        # The default-value footgun. An override defaulting to 0.0 would resolve
        # to "no wait" for every provider at once and quietly disable the feature
        # it exists to configure. `None` is what makes inheritance mean anything.
        from ai.config import get_settings

        settings = get_settings()
        overrides = {
            name: value
            for name, value in (
                (p, getattr(settings, f"llm_min_interval_{p}", None))
                for p in ("gemini", "cerebras", "groq")
            )
            if value is not None
        }

        assert overrides == {}, "unset overrides should not be collected at all"

    def test_the_floor_is_read_from_settings(self, monkeypatch):
        from ai.config import reload_settings
        import ai.llm.chain as chain

        settings = reload_settings().model_copy(update={"llm_min_interval_seconds": 7.5})
        chain._pacer = None
        try:
            assert get_pacer(settings).interval_for("gemini") == 7.5
        finally:
            chain._pacer = None

    def test_an_explicit_zero_is_honoured_as_a_real_choice(self):
        # The pair that makes the `None` default meaningful: a provider explicitly
        # set to zero waits not at all, and one left unset inherits the floor.
        # If these both resolved to the global value, "I chose none" and "I did
        # not choose" would be indistinguishable -- and every unset override in a
        # config would silently mean "no pacing".
        import ai.llm.chain as chain

        try:
            pacer = chain.Pacer(min_interval=5.0, overrides={"gemini": 0.0})
            assert pacer.interval_for("gemini") == 0.0
            assert pacer.interval_for("groq") == 5.0
        finally:
            chain._pacer = None
