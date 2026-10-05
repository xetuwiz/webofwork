"""Measure Agentic AI latency, per stage, for the performance report.

## Why this exists

Section 12 asks for performance evidence covering "concurrent requests,
response time, success/failure rate, database response and **Agentic AI
latency**". The other four are measured in the .NET suite against a real
PostgreSQL container. This covers the fifth, and it is the one that behaves
nothing like the others: an agentic stage is not a request handler, it is a
sequence of internal reads, a model call and a set of deterministic rules.

## What it reports, and what it does not

It reports wall-clock latency per stage: median, p95, max. Run against the
**fixture** provider by default, which isolates the system's own cost -- the
internal reads, the validation rules, the persistence -- from a third party's
availability. That is the number worth knowing, because it is the part this
project controls.

With `--provider` set to a real provider it also measures the model call, and the
output says so explicitly. A figure that mixes our cost with a provider's is
worse than no figure, because it cannot be compared against anything.

**These are in-process measurements with no network hop to the .NET API**, so
they exclude TLS, routing and real database round-trips. The internal reads here
are served from an in-memory stub. Treat the numbers as the floor, not the
production latency.

## Usage

    # system cost only, deterministic, no keys needed
    python -m ai.scripts.measure_latency

    # including a real model call
    python -m ai.scripts.measure_latency --provider groq --iterations 20

    # machine-readable, for pasting into the report
    python -m ai.scripts.measure_latency --json
"""

from __future__ import annotations

import argparse
import asyncio
import json
import os
import statistics
import sys
import time
from dataclasses import dataclass, field
from decimal import Decimal
from typing import Any
from uuid import UUID

from ..config import Settings, _ENV_FILE


# ── a stand-in for the .NET internal API ──────────────────────────────────────
#
# Only the reads the stages perform are implemented. Timings against this stub
# are the floor described above -- they measure our own work with the network
# removed, which is the only way to get a figure that does not change when
# someone else's provider is slow.

FREELANCERS: list[dict[str, Any]] = [
    {
        "id": "b944584e-2cd9-43f9-b5c7-a25e2c865ff4",
        "skills": ["React", "TypeScript", "Node"],
        "portfolio": [
            {"title": "Design system", "description": "React component library"},
            {"title": "Dashboard", "description": "Analytics front end"},
        ],
        "packages": [{"priceUsd": 2500.0, "deliveryDays": 14}],
        "averageRating": 4.9,
        "reviewCount": 40,
        "availability": True,
        "isActive": True,
    },
    {
        "id": "c2b7f0e1-1111-2222-3333-444455556666",
        "skills": ["React", "TypeScript"],
        "portfolio": [{"title": "Storefront", "description": "Next.js commerce"}],
        "packages": [{"priceUsd": 1800.0, "deliveryDays": 10}],
        "averageRating": 4.6,
        "reviewCount": 22,
        "availability": True,
        "isActive": True,
    },
    {
        "id": "d3c8a1f2-3333-4444-5555-666677778888",
        "skills": ["Vue", "JavaScript"],
        "portfolio": [],
        "packages": [{"priceUsd": 1200.0, "deliveryDays": 7}],
        "averageRating": 4.2,
        "reviewCount": 9,
        "availability": True,
        "isActive": True,
    },
]

PROJECT: dict[str, Any] = {
    "id": 7,
    "title": "Build a React design system",
    "description": "A component library for an existing product.",
    "skills": ["React", "TypeScript"],
    "pricingType": "fixed",
    "fixedPrice": 2500.0,
    "expectedDays": 21,
    "commissionPercentage": "10",
}

#: Categories, commission and the client's partial text, all taken from the test
#: suite's own examples rather than restated here.
#:
#: Restating them looked harmless and cost an afternoon: the draft prompt embeds
#: the category list, the fixture is keyed by `sha256` of that prompt, and a
#: two-entry list produces a different prompt, a different hash, and a
#: `FixtureMissingError` that says nothing about why. There is one definition of
#: each of these, and this module uses it.
from ..tests.examples import CATEGORIES, COMMISSION, DRAFT_PARTIAL_TEXT


class StubClient:
    """Answers the internal reads. Writes are discarded, as they are in tests."""

    def __init__(self) -> None:
        self.reads = 0
        self.writes = 0

    async def get_project(self, project_id: int) -> dict[str, Any]:
        self.reads += 1
        return PROJECT

    async def search_freelancers(self, **kwargs: Any) -> list[dict[str, Any]]:
        self.reads += 1
        return list(FREELANCERS)

    async def get_freelancer(self, freelancer_id: str) -> dict[str, Any] | None:
        self.reads += 1
        return next((f for f in FREELANCERS if f["id"] == str(freelancer_id)), None)

    async def get_freelancer_packages(self, freelancer_id: str) -> list[dict[str, Any]]:
        self.reads += 1
        return []

    async def get_freelancer_reviews(self, freelancer_id: str) -> list[dict[str, Any]]:
        self.reads += 1
        return []

    async def get_commission(self) -> dict[str, Any]:
        self.reads += 1
        # The recorded shape, not an equivalent one. The commission is part of the
        # draft prompt, and the fixture is keyed by `sha256` of that prompt -- so
        # returning `{"percentage": "10"}` instead produces a prompt hash with no
        # recorded response, and the stage fails for a reason that has nothing to
        # do with latency.
        return dict(COMMISSION)

    async def get_agent_thresholds(self) -> dict[str, Any]:
        self.reads += 1
        return {"min_rating": 4.0, "min_reviews": 0}

    async def get_contract_disputes(self, contract_id: int) -> list[Any]:
        self.reads += 1
        return []

    async def get_recommended_freelancer_ids(self, workflow_run_id: int) -> list[str]:
        self.reads += 1
        return [f["id"] for f in FREELANCERS]

    async def list_categories(self) -> list[dict[str, Any]]:
        self.reads += 1
        return [{"id": 1, "name": "Web Development"}]

    async def list_skills_by_category(self, category_id: int) -> list[dict[str, Any]]:
        self.reads += 1
        return [
            {"id": 1, "name": "React"},
            {"id": 2, "name": "TypeScript"},
            {"id": 3, "name": "Node"},
        ]

    async def get_similar_projects(self, category_id: int, limit: int = 10) -> list[Any]:
        self.reads += 1
        return []

    async def send_invitations(self, workflow_run_id: int, project_id: int, freelancer_ids: list[str]) -> dict[str, Any]:
        self.writes += 1
        return {"invitation_ids": list(range(1, len(freelancer_ids) + 1)), "skipped": [], "errors": []}

    async def _noop(self, *args: Any, **kwargs: Any) -> None:
        self.writes += 1
        return None

    append_step = _noop
    append_tool_call = _noop
    append_recommendation = _noop
    append_screening = _noop
    append_validation = _noop
    complete_workflow = _noop


# ── measurement ──────────────────────────────────────────────────────────────


@dataclass
class Sample:
    stage: str
    durations: list[float] = field(default_factory=list)
    errors: list[str] = field(default_factory=list)

    @property
    def median(self) -> float:
        return statistics.median(self.durations) if self.durations else 0.0

    @property
    def p95(self) -> float:
        if not self.durations:
            return 0.0
        ordered = sorted(self.durations)
        index = min(len(ordered) - 1, int(round(0.95 * len(ordered))) - 1)
        return ordered[max(0, index)]

    @property
    def maximum(self) -> float:
        return max(self.durations) if self.durations else 0.0

    def as_dict(self) -> dict[str, Any]:
        return {
            "stage": self.stage,
            "iterations": len(self.durations),
            "median_ms": round(self.median, 1),
            "p95_ms": round(self.p95, 1),
            "max_ms": round(self.maximum, 1),
            "errors": len(self.errors),
            "first_error": self.errors[0] if self.errors else None,
        }


async def _run_stage(stage: str, client: StubClient) -> None:
    """One stage, end to end through its agent."""
    if stage == "draft":
        from ..agents.draft_agent import DraftAgent, DraftInput

        # `DRAFT_PARTIAL_TEXT` is the text the fixture was recorded against.
        await DraftAgent(client, categories=CATEGORIES).run(
            DraftInput(workflow_run_id=1, partial_text=DRAFT_PARTIAL_TEXT)
        )
        return

    if stage == "talent_match":
        from ..agents.matching_agent import MatchingAgent, MatchingInput
        from ..agents.validation_agent import ValidationAgent, ValidationInput

        matching = MatchingAgent(client, categories=[])
        shortlist = await matching.run(
            MatchingInput(workflow_run_id=1, project_id=7, project=PROJECT)
        )

        validation = ValidationAgent(client, categories=[])
        await validation.run(
            ValidationInput(
                workflow_run_id=1,
                stage="talent_match",
                project_id=7,
                shortlist=[UUID(str(r.freelancer_id)) for r in shortlist.recommendations],
            )
        )
        return

    if stage == "screen":
        from ..agents.screening_agent import ProposalIn, ScreeningAgent, ScreeningInput

        proposals = [
            ProposalIn(
                proposal_id=1,
                freelancer_id=UUID(FREELANCERS[0]["id"]),
                bid_amount_usd=Decimal("2400"),
                delivery_days=14,
                cover_letter="I have built three design systems.",
            ),
            ProposalIn(
                proposal_id=2,
                freelancer_id=UUID(FREELANCERS[2]["id"]),
                bid_amount_usd=Decimal("900"),
                delivery_days=3,
                cover_letter="Fast and cheap.",
            ),
        ]

        await ScreeningAgent(client, categories=[]).run(
            ScreeningInput(workflow_run_id=1, project_id=7, project=PROJECT, proposals=proposals)
        )
        return

    if stage == "validate":
        from ..agents.validation_agent import ValidationAgent, ValidationInput

        await ValidationAgent(client, categories=[]).run(
            ValidationInput(
                workflow_run_id=1,
                stage="validate",
                project_id=7,
                freelancer_id=UUID(FREELANCERS[0]["id"]),
                proposed_amount_usd=Decimal("2500"),
                delivery_days=14,
                commission_percentage=Decimal("10"),
            )
        )
        return

    raise ValueError(f"unknown stage: {stage}")


STAGES = ("draft", "talent_match", "screen", "validate")


async def measure(iterations: int) -> list[Sample]:
    samples = {stage: Sample(stage) for stage in STAGES}

    for stage in STAGES:
        for _ in range(iterations):
            client = StubClient()
            started = time.perf_counter()

            try:
                await _run_stage(stage, client)
            except Exception as exc:  # noqa: BLE001 - a failure is a measurement
                samples[stage].errors.append(f"{type(exc).__name__}: {exc}")

            samples[stage].durations.append((time.perf_counter() - started) * 1000)

    return [samples[stage] for stage in STAGES]


def report(samples: list[Sample], as_json: bool, provider: str) -> None:
    if as_json:
        print(
            json.dumps(
                {
                    "provider": provider,
                    "includes_model_call": provider not in {"", "fixture"},
                    "network_to_dotnet": False,
                    "note": "in-process; internal reads served from a stub",
                    "stages": [s.as_dict() for s in samples],
                },
                indent=2,
            )
        )
        return

    print("Agentic AI latency, per stage")
    print(f"  provider chain     : {provider}")
    print(f"  model call included: {'yes' if provider not in {'', 'fixture'} else 'no'}")
    print("  internal reads     : served from an in-memory stub (no network)")
    print("  figures are a floor, not production latency")
    print()

    header = f"  {'stage':<14}{'n':>4}{'median':>10}{'p95':>10}{'max':>10}{'errors':>9}"
    print(header)
    print("  " + "-" * (len(header) - 2))

    for sample in samples:
        print(
            f"  {sample.stage:<14}{len(sample.durations):>4}"
            f"{sample.median:>9.1f}ms{sample.p95:>9.1f}ms{sample.maximum:>9.1f}ms"
            f"{len(sample.errors):>9}"
        )

    # A grep-friendly line, so the numbers can be lifted from CI output into the
    # report without being retyped -- retyped numbers are how a report ends up
    # quoting figures that were never measured.
    print()
    for sample in samples:
        payload = sample.as_dict()
        print(
            f"  PERF|{payload['stage']}|median={payload['median_ms']}|p95={payload['p95_ms']}"
            f"|max={payload['max_ms']}|errors={payload['errors']}"
        )

    failed = [s for s in samples if s.errors]
    if failed:
        print()
        print("  Stages that failed, with the first error:")
        for sample in failed:
            print(f"    {sample.stage}: {sample.errors[0]}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--iterations", type=int, default=25)
    parser.add_argument("--provider", default="fixture")
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()

    # Settings read ai/.env, so these must be set before the config is built.
    os.environ["INTERNAL_API_KEY"] = os.environ.get("INTERNAL_API_KEY", "measure-latency-not-a-real-key")
    os.environ["LLM_PROVIDER_CHAIN"] = args.provider
    os.environ["LLM_ALLOW_FIXTURE_FALLBACK"] = "true"

    if args.provider not in {"", "fixture"} and not any(
        key.startswith(f"LLM_{args.provider.upper()}_API_KEYS") for key in os.environ
    ):
        # Not fatal: the provider may be configured in ai/.env, which Settings
        # loads. Only worth saying out loud, because otherwise a "live" run
        # silently measures the fixture path.
        print(
            f"  note: no LLM_{args.provider.upper()}_API_KEYS in the environment; "
            f"falling back to {Settings(_env_file=_ENV_FILE).llm_provider_chain}",
            file=sys.stderr,
        )

    samples = asyncio.run(measure(args.iterations))
    report(samples, args.json, args.provider)

    return 1 if any(s.errors for s in samples) else 0


if __name__ == "__main__":
    raise SystemExit(main())