"""Grounding: real catalogue rows survive, invented ones do not.

## What this file is for

``DraftAgent._ground`` is the last thing standing between a language model's
output and a posted project. Its whole job is to drop ids the model invented --
an invented ``skill_id`` on a live project is a broken browse page.

It is also, until recently, where two real bugs lived. Both were invisible for
the same reason: **the method dropped things it should have kept, quietly, and
the result still looked like a plausible draft.** Neither raised an error. A
reviewer reading the JSON would not know the skills had been thrown away.

1. It matched skill names against the **category** list, and validated skill ids
   against **category** ids. The catalogue is nested -- each category carries its
   own ``skills`` -- so a genuine skill id could never survive.

2. It iterated ``skill_names``. A model returning ids only, which is the
   *expected* shape once it can look the catalogue up itself, matched nothing and
   lost every id.

Both were found by running the live agent, not by reading the code. Both are now
pinned below, along with the property they were both breaking: a draft grounded
against a real catalogue keeps its real ids.
"""

from __future__ import annotations

import pytest

from ai.agents.draft_agent import DraftAgent, DraftOutput

#: A catalogue shaped the way the endpoint actually returns it: nested.
CATEGORIES = [
    {
        "id": 1,
        "name": "Development",
        "skills": [
            {"id": 11, "name": "React"},
            {"id": 12, "name": "TypeScript"},
            {"id": 13, "name": "PostgreSQL"},
        ],
    },
    {
        "id": 2,
        "name": "Design",
        "skills": [{"id": 21, "name": "Figma"}],
    },
]

CATEGORY_IDS = {1, 2}


def _draft(**overrides) -> DraftOutput:
    base = dict(
        title="Warehouse stock tracking app",
        description="A description long enough to clear the minimum length check.",
        requirements="Requirements that are also comfortably long enough.",
        expected_deliverables="Deliverables that are long enough as well.",
        category_id=1,
        skill_ids=[],
        skill_names=[],
    )
    base.update(overrides)
    return DraftOutput(**base)


@pytest.fixture
def agent() -> DraftAgent:
    # No client needed: grounding is pure and takes only the catalogue.
    return DraftAgent(client=None)  # type: ignore[arg-type]


def ground(agent: DraftAgent, draft: DraftOutput) -> DraftOutput:
    names = {str(c.get("name", "")).lower(): c for c in CATEGORIES if "id" in c}
    return agent._ground(draft, CATEGORIES, CATEGORY_IDS, names)


# ── the properties that were broken ─────────────────────────────────────


class TestGroundingKeepsRealRows:
    @pytest.mark.parametrize("skill_id", [11, 12, 13])
    def test_a_real_skill_id_survives(self, agent, skill_id):
        """Bug 1: skill ids were validated against *category* ids.

        Every real skill id therefore failed, and the draft came back with notes
        saying "Skill id 11 is not a real row and was dropped" for an id that
        existed. The catalogue is nested, so the skill rows were never read.
        """
        draft = ground(agent, _draft(skill_ids=[skill_id]))

        assert draft.skill_ids == [skill_id], (
            f"skill id {skill_id} is a real catalogue row in this category and "
            f"must be kept; notes were {draft.notes}"
        )

    def test_a_real_skill_from_ANOTHER_category_is_dropped(self, agent):
        """Being a real row is not sufficient. Belonging to *this* category is.

        This parametrised case used to include id 21, which is Figma -- a real
        skill, in category 2. The draft under test is category 1, Development.
        The old assertion said a Design tool was acceptable on a Development
        brief, which is the whole defect: grounding asked "is this id real?" and
        never "is this id the thing the model asked for?". A live draft asked for
        a responsive website and was handed ClickHouse, Snowflake and BigQuery --
        every one a real row.
        """
        draft = ground(agent, _draft(skill_ids=[21]))

        assert draft.skill_ids == [], (
            "Figma is a real skill, but it is a Design skill and this draft is "
            f"Development; notes were {draft.notes}"
        )
        assert any("21" in n for n in draft.notes), (
            "and the drop has to be explained, so the client knows to pick again"
        )

    def test_a_name_from_another_category_is_dropped_too(self, agent):
        """Scoping must not depend on which half of the pair was supplied."""
        draft = ground(agent, _draft(skill_names=["Figma"]))

        assert draft.skill_ids == []
        assert any("Figma" in n for n in draft.notes)

    def test_a_name_and_id_that_disagree_are_dropped_not_relabelled(self, agent):
        """The exact failure, with the exact numbers.

        A live draft for a responsive website came back with
        ``skills = ["ClickHouse", "Snowflake", "BigQuery"]`` and a note reading
        "Skill IDs are placeholders for Frontend Development, UI/UX Design, and
        Web Performance." None of those three wanted skills are in the catalogue.

        What the model actually returned was ``skill_ids = [101, 102, 103]`` with
        ``skill_names = ["Frontend Development", "UI/UX Design", "Web
        Performance"]``. Ids 101-103 are real rows -- ClickHouse, Snowflake and
        BigQuery -- so the old code kept them and then *renamed them from the
        pool*, overwriting the model's stated intent and reporting success.

        So the check passed because it only ever asked "is this id real?" and
        never "is this id the thing the model named?". A name that does not
        resolve is the evidence that it is not.
        """
        draft = ground(agent, _draft(
            skill_ids=[11, 12, 13],
            skill_names=["Frontend Development", "UI/UX Design", "Web Performance"],
        ))

        assert draft.skill_ids == [], (
            "every name is absent from the catalogue, so nothing survives; "
            f"notes were {draft.notes}"
        )
        assert draft.skill_names == []
        # And it must be said out loud, because the client is the one who has to
        # choose real skills next.
        for wanted in ("Frontend Development", "UI/UX Design", "Web Performance"):
            assert any(wanted in n for n in draft.notes), (
                f"{wanted!r} was dropped without explanation; notes were "
                f"{draft.notes}"
            )

    def test_an_ids_only_response_is_grounded_rather_than_discarded(self, agent):
        """Bug 2: the loop iterated ``skill_names``, so ids alone matched nothing.

        This is the *expected* shape once the model can call
        ``list_skills_by_category`` itself: it looks up real ids and returns
        them. Discarding those would throw away exactly the runs that did the
        most work.
        """
        draft = ground(agent, _draft(skill_ids=[11, 12, 13], skill_names=[]))

        assert draft.skill_ids == [11, 12, 13]
        assert not [n for n in draft.notes if "dropped" in n], (
            f"nothing was dropped, so nothing should be reported as dropped: "
            f"{draft.notes}"
        )

    def test_names_are_resolved_to_their_real_ids(self, agent):
        draft = ground(agent, _draft(skill_names=["React", "PostgreSQL"]))

        assert draft.skill_ids == [11, 13]
        assert draft.skill_names == ["React", "PostgreSQL"]

    def test_name_matching_is_case_insensitive(self, agent):
        draft = ground(agent, _draft(skill_names=["react"]))

        assert draft.skill_ids == [11]

    def test_a_wrong_name_beats_a_right_id_when_the_name_matches_something(self, agent):
        """A name that resolves is honoured, even if the paired id disagrees.

        This is the opposite of "the id is ground truth", and deliberately so.
        When the model returns both, the *name* is the more specific claim: a
        name that matches a real catalogue row is evidence, whereas an id in the
        right position could simply be the model reusing the id from the
        neighbouring element of a list.

        So "id 11 + name TypeScript" resolves to TypeScript (12). It is a
        different claim from "id 11 + no name at all", which is what
        ``test_an_ids_only_response_is_grounded_rather_than_discarded`` covers
        and where the id does survive.
        """
        draft = ground(agent, _draft(skill_ids=[11], skill_names=["TypeScript"]))

        assert draft.skill_ids == [12]
        assert draft.skill_names == ["TypeScript"]


# ── the property that must not regress ──────────────────────────────────


class TestGroundingStillRejectsInventedIds:
    @pytest.mark.parametrize("invented", [999, 0, -1, 5])
    def test_an_invented_id_is_dropped_and_reported(self, agent, invented):
        """The original purpose. Still true after both fixes."""
        draft = ground(agent, _draft(skill_ids=[invented]))

        assert draft.skill_ids == []
        assert any("dropped" in n for n in draft.notes), (
            "a dropped id must be reported, because the client is told what was "
            f"assumed; notes were {draft.notes}"
        )

    def test_an_invented_name_is_dropped_and_reported(self, agent):
        draft = ground(agent, _draft(skill_names=["Cobol"]))

        assert draft.skill_ids == []
        assert any("Cobol" in n for n in draft.notes)

    def test_real_and_invented_survive_apart(self, agent):
        draft = ground(agent, _draft(skill_ids=[11, 999, 13]))

        assert draft.skill_ids == [11, 13]
        assert any("999" in n for n in draft.notes)

    def test_duplicates_are_collapsed(self, agent):
        draft = ground(agent, _draft(skill_ids=[11, 11, 12]))

        assert draft.skill_ids == [11, 12]

    def test_a_category_id_is_not_a_skill_id(self, agent):
        """Categories and skills have separate id spaces.

        1 is a real category row. Using it as a skill id would attach the wrong
        row to a project, which is the failure grounding exists to prevent.
        """
        draft = ground(agent, _draft(skill_ids=[1]))

        assert draft.skill_ids == []


# ── category grounding, unchanged ───────────────────────────────────────


class TestCategoryGrounding:
    def test_a_real_category_id_is_kept(self, agent):
        draft = ground(agent, _draft(category_id=2))

        assert draft.category_id == 2
        assert draft.category_name == "Design"

    def test_an_invented_category_id_is_dropped_and_reported(self, agent):
        draft = ground(agent, _draft(category_id=404))

        assert draft.category_id is None
        assert any("404" in n for n in draft.notes)

    def test_a_category_hint_does_not_survive_being_wrong(self, agent):
        """The name fallback only applies to an *unmatched id*, not a cleared one.

        ``_ground`` sets ``category_name = None`` when the id does not resolve,
        before the name fallback reads it. That is deliberate: a hint that
        disagrees with a real catalogue row is not a hint the system should
        trust, and falling back to it would re-admit exactly the invented value
        the id check just rejected.

        Pinned because it looks like an oversight and is not.
        """
        draft = ground(agent, _draft(category_id=404, category_name="Design"))

        assert draft.category_id is None
        assert draft.category_name is None

    def test_a_name_alone_resolves_when_no_id_was_proposed(self, agent):
        draft = ground(
            agent, _draft(category_id=None, category_name="Design")
        )

        assert draft.category_id == 2, (
            "a correct name with no competing id is good evidence and should win"
        )


# ── shape tolerance ────────────────────────────────────────────────────


class TestCatalogueShapes:
    def test_a_flat_skill_row_list_is_accepted_when_it_identifies_its_category(self, agent):
        """A flat skill row is only usable if it says which category it is in.

        `category_id` is what distinguishes a skill row from a category row. Both
        are `{id, name}`, so without that field the two are indistinguishable --
        and guessing wrong is not a degradation, it is a wrong answer that looks
        right. Which is exactly what happened in production.
        """
        flat = [
            {"id": 31, "name": "React", "category_id": 3},
            {"id": 32, "name": "Figma", "category_id": 3},
        ]

        draft = agent._ground(_draft(skill_ids=[31, 32]), flat, {31, 32}, {})

        assert draft.skill_ids == [31, 32]
        assert draft.skill_names == ["React", "Figma"]

    def test_a_bare_id_and_name_list_is_treated_as_categories_not_skills(self, agent):
        """The regression that shipped.

        The server sent `{id, name}` category rows with no skills, and this branch
        promoted all of them into the skill pool. A live draft came back with
        `skills: ["Development", "Design"]` -- the two category names it had been
        shown -- out of 654 real skills it had never seen.

        So a bare `{id, name}` list now grounds nothing, and says so. Dropping the
        skills is recoverable: the client picks from the real catalogue in the
        form. Reporting category names as matched skills is not.
        """
        categories_only = [{"id": 1, "name": "Development"}, {"id": 2, "name": "Design"}]

        draft = agent._ground(
            _draft(skill_ids=[1, 2]), categories_only, {1, 2}, {}
        )

        assert draft.skill_ids == []
        assert draft.skill_names == []
        # The client is told why, rather than left to wonder why the agent that
        # claims to have matched skills came back with none.
        #
        # Matched on meaning rather than wording. This note used to say "the
        # catalogue arrived without its skills", which blamed the platform for
        # our own bug: the skills had been fetched and then discarded one
        # instruction earlier, so the note described a cause that had not
        # happened. What must not change is that the client is told, and told
        # something true.
        assert any(
            "pick skills" in n.lower() for n in draft.notes
        ), f"the client must be told what to do; notes were {draft.notes}"
        assert not any(
            "not a real row" in n for n in draft.notes
        ), (
            "an empty pool says nothing about whether an id is a real row, and "
            f"claiming otherwise sent the reader hunting a seed-data bug: {draft.notes}"
        )

    def test_the_category_names_are_never_reported_as_skills(self, agent):
        """Named directly, because this is the exact failure.

        The model's skill_names will frequently be category names -- it was given
        categories, and a name it recognises is a name it will use. Those must be
        dropped, not silently accepted as skills.
        """
        draft = agent._ground(
            _draft(skill_ids=[], skill_names=["Development", "Design"]),
            [{"id": 1, "name": "Development"}, {"id": 2, "name": "Design"}],
            {1, 2},
            {},
        )

        assert "Development" not in draft.skill_names
        assert "Design" not in draft.skill_names
        # Matched on meaning rather than wording: this note has been reworded
        # twice, because it used to blame the catalogue for a problem that was
        # ours. What must not change is that the client is told, and told
        # something true.
        assert any(
            "pick skills" in n.lower() for n in draft.notes
        ), f"the client must be told what to do; notes were {draft.notes}"
        assert not any(
            "not a real row" in n for n in draft.notes
        ), (
            "an empty pool says nothing about whether an id is a real row, and "
            f"claiming otherwise sent the reader hunting a seed-data bug: {draft.notes}"
        )

    def test_an_empty_catalogue_drops_everything_without_raising(self, agent):
        draft = agent._ground(
            _draft(skill_ids=[11], category_id=1), [], set(), {}
        )

        assert draft.skill_ids == []
        assert draft.category_id is None

    def test_malformed_skill_rows_are_skipped_not_fatal(self, agent):
        broken = [
            {"id": 1, "name": "Development", "skills": [
                {"id": 11, "name": "React"},
                {"id": "not-an-int", "name": "Broken"},
                {"name": "No id at all"},
                "not even a dict",
            ]},
        ]

        draft = agent._ground(_draft(skill_ids=[11]), broken, {1}, {"development": broken[0]})

        assert draft.skill_ids == [11], "one malformed row must not lose the good one"


# ── grounding source: what the model actually looked up ─────────────────
#
# The drafter is now model-directed, so grounding has two possible sources: rows
# the model fetched itself, or a fallback fetch when it fetched none. These pin
# which is preferred, because getting it wrong is silent either way.


class _StubClient:
    """Swallows the step record. Grounding is what's under test here."""

    def __init__(self) -> None:
        self.steps: list[dict] = []

    async def append_step(self, *args, **kwargs) -> None:
        self.steps.append({"args": args, "kwargs": kwargs})


class _StubBox:
    """Records what was called. Raises on anything unexpected, so a silent miss
    becomes a test failure rather than a passing test that proved nothing."""

    def __init__(self, categories: list[dict]) -> None:
        self.categories = categories
        self.calls: list[str] = []

    async def call(self, tool: str, **kwargs) -> object:
        self.calls.append(tool)
        if tool == "list_categories":
            return self.categories
        raise AssertionError(f"unexpected tool {tool}")


def _loop_returning(answer: str):
    """A completed loop result, standing in for a model that has finished."""
    from ai.agents.tool_loop import LoopResult

    return LoopResult(answer=answer, steps=(), stop_reason="answered")


ANSWER = (
    '{"title": "Warehouse stock tracking app",'
    ' "description": "A description long enough to clear the minimum check.",'
    ' "requirements": "Requirements that are also comfortably long enough.",'
    ' "expected_deliverables": "Deliverables that are long enough as well.",'
    ' "category_id": 1, "skill_ids": [11], "pricing_type": "range",'
    ' "min_budget_usd": 4000, "max_budget_usd": 8000, "delivery_days": 42,'
    ' "confidence": 0.6, "notes": []}'
)


@pytest.fixture
def wired(monkeypatch):
    """An agent with a stub client and a stub toolbox, and the loop stubbed out.

    No model, no network. The stub records catalogue lookups on the agent
    exactly as the real loop's `execute` callback does, so the two paths below
    are genuinely distinguished rather than simulated.
    """

    def build(
        *,
        records: tuple[str, list[dict]] | None = None,
        also: tuple[str, list[dict]] | None = None,
    ):
        agent = DraftAgent(client=_StubClient())  # type: ignore[arg-type]
        box = _StubBox(CATEGORIES)
        agent._toolbox = lambda run_id: box  # type: ignore[method-assign]

        async def fake_loop(*, payload, toolbox, workflow_run_id):
            # Assignment, not `extend`: the loop's callback overwrites
            # `_categories_seen` and only accumulates `_skills_seen`, and
            # conflating the two is exactly the kind of stub lie that makes
            # a test pass without testing anything.
            #
            # `also` seeds the second source, which is what a model that lists
            # the categories and then looks up one of them leaves behind.
            for pair in (records, also):
                if pair is None:
                    continue
                attr, rows = pair
                getattr(agent, attr)[:] = rows
            return _loop_returning(ANSWER)

        monkeypatch.setattr(agent, "_draft_with_tools", fake_loop)
        return agent, box

    return build


async def _run(agent) -> DraftOutput:
    from ai.agents.draft_agent import DraftInput

    return await agent.run(
        DraftInput(
            workflow_run_id=1, partial_text="A warehouse stock tracking app."
        )
    )


class TestGroundingUsesWhatTheModelLookedUp:
    @pytest.mark.asyncio
    async def test_no_second_fetch_when_the_model_looked_the_catalogue_up(self, wired):
        agent, box = wired(records=("_categories_seen", CATEGORIES))

        draft = await _run(agent)

        assert draft.skill_ids == [11]
        assert "list_categories" not in box.calls, (
            "the model already looked the catalogue up; re-fetching spends a "
            f"round trip to learn nothing (calls were {box.calls})"
        )

    @pytest.mark.asyncio
    async def test_a_fallback_fetch_happens_when_the_model_looked_up_nothing(self, wired):
        """The other direction: no silent leniency when the model fetched nothing.

        A model that drafts from memory has seen no catalogue, so every id it
        returns is unverified. Grounding has to fetch regardless, or the draft
        goes out with invented ids attached -- the failure this method exists to
        prevent.
        """
        agent, box = wired()  # records nothing

        draft = await _run(agent)

        assert "list_categories" in box.calls, (
            "nothing was looked up during the loop, so grounding must fetch"
        )
        assert draft.skill_ids == [11]

    @pytest.mark.asyncio
    async def test_both_lookups_survive_the_list_categories_call(self, wired):
        """The bug that shipped, and it is a one-character bug.

        A model that lists the categories and then looks up one of them leaves
        behind two sources: `_categories_seen` (bare `{id, name}`, because that
        is the shape the server sends -- nesting all 654 skills overflowed the
        provider) and `_skills_seen` (one row with its skills still nested).

        The line reading

            categories = self._categories_seen or self._skills_seen

        looks like a preference. It is a short-circuit: the bare list is
        non-empty, so the row carrying the skills is thrown away, and grounding
        runs against a catalogue with no skills in it.

        A live draft hit this exactly. The model called
        `list_skills_by_category(1)` twice and got the real Development skills;
        the draft came back `skill_ids: []` with the note "Skill id 1 is not a
        real row". Id 1 is C#. The rows were discarded one instruction before
        they were needed.

        So the sources must be concatenated, not selected between.
        """
        agent, box = wired(
            records=("_categories_seen", [{"id": 1, "name": "Development"}]),
            also=("_skills_seen", [CATEGORIES[0]]),
        )

        draft = await _run(agent)

        assert draft.category_id == 1
        assert draft.skill_ids == [11], (
            "the skills were fetched during the loop and must survive into "
            f"grounding; got {draft.skill_ids} with notes {draft.notes}"
        )
        assert not any("not a real row" in n for n in draft.notes), (
            "an empty pool says nothing about whether an id is a real row: "
            f"{draft.notes}"
        )

    @pytest.mark.asyncio
    async def test_rows_from_the_narrower_lookup_alone_are_enough(self, wired):
        """A model that only called `list_skills_by_category` has still seen enough.

        That tool returns one category row with its skills still nested inside,
        which is the shape `_ground` reads. Re-fetching the full catalogue would
        spend a round trip to learn nothing -- and the narrower call is the one a
        model is likelier to make once it has already chosen a category.
        """
        agent, box = wired(records=("_skills_seen", [CATEGORIES[0]]))

        draft = await _run(agent)

        assert box.calls == [], f"no fetch was needed, but got {box.calls}"
        assert draft.category_id == 1
        assert draft.skill_ids == [11]
