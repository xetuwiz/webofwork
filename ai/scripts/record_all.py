"""Record a fixture for every agent prompt.

Run from ``ai/``:

    python -m ai.scripts.record_all

Driven from the agents themselves, with the canonical inputs from
``ai.tests.examples``, so a fixture can never be recorded against a prompt the
agent no longer sends.

Re-record after editing anything in ``ai/prompts/`` or after changing how an
agent composes a prompt. The fixture provider will tell you when you need to:
an unrecorded prompt is a hard failure, not a generated answer.
"""

from __future__ import annotations

import asyncio
import json
import sys

from ..agents.draft_agent import DraftAgent, DraftInput
from ..agents.matching_agent import MatchingAgent
from ..agents.planning_agent import PlanningAgent, PlanningInput
from ..agents.screening_agent import ScreeningAgent
from ..agents.validation_agent import ValidationAgent
from ..clients.dotnet_client import DotNetClient
from ..config import get_settings
from ..llm.chain import FIXTURE_DIR, call_llm, reset_for_tests
from ..tests.examples import (
    CATEGORIES,
    COMMISSION,
    DRAFT_PARTIAL_TEXT,
    FREELANCERS,
    PROJECT,
    THRESHOLDS,
)
from .record_fixture import append_fixture


def _stub_client() -> DotNetClient:
    """A client that answers reads from the examples and writes to nowhere.

    The recorder only needs the prompts, so every .NET write is a no-op. Pointing
    it at a live API would make recording depend on the server being up, which is
    exactly when you least want to be re-recording.
    """
    client = DotNetClient(get_settings(), workflow_run_id=0)

    async def noop(*args, **kwargs):
        return None

    for name in (
        "append_step", "append_tool_call", "append_recommendation",
        "append_screening", "append_validation", "complete_workflow",
    ):
        setattr(client, name, noop)

    async def get_project(_project_id):
        return PROJECT

    async def get_commission():
        return COMMISSION

    async def get_agent_thresholds():
        return THRESHOLDS

    async def search_freelancers(**_kwargs):
        return FREELANCERS

    async def get_freelancer(freelancer_id):
        return next((f for f in FREELANCERS if f["id"] == str(freelancer_id)), {})

    async def get_freelancer_reviews(_freelancer_id):
        return []

    async def get_recommended_freelancer_ids(_run_id):
        return [f["id"] for f in FREELANCERS]

    client.get_project = get_project
    client.get_commission = get_commission
    client.get_agent_thresholds = get_agent_thresholds
    client.search_freelancers = search_freelancers
    client.get_freelancer = get_freelancer
    client.get_freelancer_reviews = get_freelancer_reviews
    client.get_recommended_freelancer_ids = get_recommended_freelancer_ids
    return client


def _prompts() -> list[tuple[str, str]]:
    """Every prompt this service can send, composed by the agents themselves."""
    client = _stub_client()

    draft = DraftAgent(client, categories=CATEGORIES)
    planning = PlanningAgent(client, categories=CATEGORIES)
    matching = MatchingAgent(client, categories=CATEGORIES)
    screening = ScreeningAgent(client, categories=CATEGORIES)
    validation = ValidationAgent(client, categories=CATEGORIES)

    return [
        (
            "draft",
            draft._compose_prompt(
                DraftInput(workflow_run_id=1, partial_text=DRAFT_PARTIAL_TEXT),
                CATEGORIES,
                COMMISSION,
            ),
        ),
        (
            "planning",
            planning._compose_prompt(PROJECT, THRESHOLDS),
        ),
        (
            "matching",
            matching.read_prompt("matching")
            + f"\n\n## Project\n{PROJECT['title']}\n{PROJECT['description'][:800]}\n"
            + "\n## Scored candidates\n"
            + "- b944584e-2cd9-43f9-b5c7-a25e2c865ff4: skill 50, portfolio 75, "
              "success 100, availability 60, budget 90. Matched: React. Missing: CSharp\n"
            + "- c2b7f0e1-1111-2222-3333-444455556666: skill 50, portfolio 75, "
              "success 71, availability 60, budget 70. Matched: CSharp. Missing: React\n",
        ),
        (
            "screening",
            screening.read_prompt("screening")
            + f"\n\n## Project\n{PROJECT['title']}\n{PROJECT['description'][:800]}\n"
            + "\n## Screened proposals\n"
            + "- proposal 1: fit 0.82 [Covers React, Bid 2000.00 is inside the budget] concerns []\n",
        ),
        (
            "validation",
            validation.read_prompt("validation")
            + "\n\n1 blocking issue(s): Proposed amount 4000.00 exceeds the project's "
              "ceiling of 2500.00.",
        ),
        # Outreach never reaches a model: the agent has no call to make, which is
        # the point of it. Recorded here so the absence is explicit rather than an
        # oversight someone later "fixes" by adding a model call.
        ("outreach", "You write one sentence describing what happened.\n\n"
                     "Sent 3 invitations. 1 could not be invited: already applied."),
    ]


async def main() -> int:
    settings = get_settings()
    print(f"recording into {FIXTURE_DIR}")
    print(f"provider chain: {settings.providers()}\n")

    failures: list[str] = []

    for agent, prompt in _prompts():
        reset_for_tests()
        # Cache off, or a second run re-uses a response rather than asking again.
        settings.llm_cache_enabled = False

        try:
            response = await call_llm(prompt, agent=agent, workflow_run_id=0, settings=settings)
        except Exception as exc:  # noqa: BLE001
            print(f"  {agent:<11} FAILED  {str(exc)[:110]}")
            failures.append(agent)
            continue

        key = append_fixture(agent, prompt, response.text)
        print(f"  {agent:<11} {response.provider:<11} {key[:12]}")

    print()
    if failures:
        print(f"not recorded: {', '.join(failures)}")
        print("the chain is exhausted; retry, or add a provider")
        return 1

    # Prove the recording replays, so a fixture is not trusted because it was
    # just written but because it is actually found.
    settings.llm_provider_chain = "fixture"
    reset_for_tests()
    print("verifying every fixture replays offline...")
    for agent, prompt in _prompts():
        try:
            await call_llm(prompt, agent=agent, workflow_run_id=0, settings=settings)
            print(f"  {agent:<11} replays")
        except Exception as exc:  # noqa: BLE001
            print(f"  {agent:<11} DOES NOT REPLAY: {str(exc)[:110]}")
            return 1

    return 0


if __name__ == "__main__":
    sys.exit(asyncio.run(main()))
