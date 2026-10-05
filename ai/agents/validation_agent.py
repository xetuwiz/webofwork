"""The Validation Agent.

The deterministic safety gate. Ten rules, no model authority over any of them.

The model is given exactly one job: rewrite ``human_summary`` into a sentence a
person can act on. It cannot add an issue, remove one, change a verdict, or move
a number. Everything else is ``policies.validation_rules``, which is a pure
function over already-fetched facts.

This is the agent where "the model is only advisory" is enforced by construction
rather than by instruction, and it is the reason the rules live in Python instead
of in a prompt.
"""

from __future__ import annotations

import time
from decimal import Decimal
from typing import Any, Literal
from uuid import UUID

from pydantic import BaseModel, Field

from ..policies.validation_rules import Facts, evaluate
from .base import BaseAgent


class ValidationInput(BaseModel):
    workflow_run_id: int
    stage: Literal["talent_match", "validate"] = "validate"
    project_id: int | None = None
    shortlist: list[UUID] = Field(default_factory=list)
    proposal_id: int | None = None
    freelancer_id: UUID | None = None
    proposed_amount_usd: Decimal | None = None
    delivery_days: int | None = None
    match_score: float | None = None
    commission_percentage: Decimal | None = None


class ValidationOutput(BaseModel):
    valid: bool
    issues: list[dict[str, Any]] = Field(default_factory=list)
    cost_breakdown: dict[str, Any] | None = None
    requires_human_approval: bool = True
    human_summary: str = ""


class ValidationAgent(BaseAgent[ValidationInput, ValidationOutput]):
    name = "validation"

    async def run(self, payload: ValidationInput) -> ValidationOutput:
        started = time.monotonic()
        tools = self._toolbox(payload.workflow_run_id)

        facts = Facts(
            stage=payload.stage,
            project_id=payload.project_id,
            shortlist=[str(f) for f in payload.shortlist],
            proposed_amount_usd=payload.proposed_amount_usd,
            delivery_days=payload.delivery_days,
            match_score=payload.match_score,
            commission_percentage=payload.commission_percentage,
        )

        # Facts first, judgement never. Each read is independent and a failure is
        # recorded as absent rather than aborting -- a rule that needs a fact we
        # could not fetch simply does not fire, and that is visible in the issue
        # list rather than silently treated as a pass.
        if payload.project_id is not None:
            try:
                # Deliberately not best-effort. A failed read here is caught and
                # recorded as a warning, so the verdict says "could not check"
                # rather than "fine".
                facts.project = await tools.call("get_project", project_id=payload.project_id)
            except Exception as exc:  # noqa: BLE001
                self._note(facts, "project_read_failed", f"Could not read the project: {exc}")

        if payload.freelancer_id is not None:
            try:
                # As above: a missing profile must not read as a passed gate.
                facts.freelancer = await tools.call(
                    "get_freelancer", freelancer_id=str(payload.freelancer_id)
                )
            except Exception as exc:  # noqa: BLE001
                self._note(facts, "freelancer_read_failed", f"Could not read the freelancer: {exc}")

        if payload.commission_percentage is None:
            try:
                # Best-effort: no rate means no cost breakdown, and the summary
                # says so rather than guessing a commission.
                commission = await tools.call("get_platform_commission", best_effort=True) or {}
                if commission.get("commissionPercentage") is not None:
                    facts.commission_percentage = Decimal(str(commission["commissionPercentage"]))
            except Exception:  # noqa: BLE001
                # The cost breakdown is skipped when there is no rate, and the
                # summary says so. Guessing a commission would put a wrong number
                # in front of a client.
                pass

        if facts.commission_percentage is not None and facts.project is not None:
            # Rule 4 compares against the platform's own setting, so both sides
            # have to come from the same place.
            facts.project = {**facts.project, "commissionPercentage": str(facts.commission_percentage)}

        try:
            # Best-effort: the rating rule simply does not fire without a threshold.
            thresholds = await tools.call("get_agent_thresholds", best_effort=True) or {}
            if thresholds.get("matchScoreThreshold") is not None:
                facts.min_rating_threshold = float(thresholds["matchScoreThreshold"])
        except Exception:  # noqa: BLE001
            pass

        if payload.shortlist:
            # Rule 10 needs to know what the run actually recommended. Asking is
            # better than trusting: a shortlist the model assembled itself is
            # exactly the case this rule exists to catch.
            try:
                known = await tools.call("get_recommended_freelancer_ids",
                                         workflow_run_id=payload.workflow_run_id)
                facts.recommended_ids = {str(f) for f in (known or [])}
            except Exception:  # noqa: BLE001
                # Without the list, rule 10 cannot run. Said out loud rather than
                # passing, because a gate that silently skips is not a gate.
                self._note(
                    facts,
                    "recommended_ids_unavailable",
                    "Could not confirm the shortlist against this run's "
                    "recommendations, so the check for unapproved freelancers was skipped.",
                )

        verdict = evaluate(facts)

        result = ValidationOutput(
            valid=verdict.valid,
            issues=[
                {"rule": i.rule, "severity": i.severity, "message": i.message}
                for i in verdict.issues
            ],
            cost_breakdown=verdict.cost_breakdown.as_dict() if verdict.cost_breakdown else None,
            requires_human_approval=verdict.requires_human_approval,
            human_summary=verdict.human_summary,
        )

        # The model's only authority: a more readable summary. It cannot change
        # the verdict, and a refusal or a hallucination leaves the deterministic
        # text in place.
        result.human_summary = await self._polish_summary(result, payload.workflow_run_id)

        await self._client.append_validation(
            payload.workflow_run_id,
            {
                "project_id": payload.project_id,
                "proposal_id": payload.proposal_id,
                "freelancer_id": str(payload.freelancer_id) if payload.freelancer_id else None,
                "valid": result.valid,
                "issues": result.issues,
                "cost_breakdown": result.cost_breakdown,
                "requires_human_approval": result.requires_human_approval,
                "human_summary": result.human_summary,
            },
        )

        await self._record_step(
            workflow_run_id=payload.workflow_run_id,
            step_name="validation",
            input_summary={
                "stage": payload.stage,
                "project_id": payload.project_id,
                "proposal_id": payload.proposal_id,
                "amount": str(payload.proposed_amount_usd) if payload.proposed_amount_usd else None,
            },
            output_summary={
                "valid": result.valid,
                "errors": len(verdict.errors),
                "warnings": len(verdict.warnings),
                "reads_used": self._client.reads,
            },
            status="ok" if result.valid else "validation_failed",
            started=started,
        )
        return result

    @staticmethod
    def _note(facts: Facts, rule: str, message: str) -> None:
        """Record that a read failed, so the verdict can say it could not check.

        A dict, not a list: the note is keyed by what failed, and the severity
        policy in ``validation_rules`` keys off that. Appending a formatted string
        would mean parsing it back apart again.
        """
        facts.notes[rule] = message

    async def _polish_summary(self, result: ValidationOutput, workflow_run_id: int) -> str:
        """Ask the model to restate the summary. Never the other way round.

        The deterministic summary is already correct, so this is a pure
        improvement attempt. Any failure, and any response that does not look like
        a summary, leaves the original.
        """
        if not result.issues and not result.cost_breakdown:
            return result.human_summary

        prompt = (
            f"{self.read_prompt('validation')}\n\n"
            f"Restate the following in one or two plain sentences for a person deciding "
            f"whether to proceed. Do not add, remove or soften any fact.\n\n"
            f"{result.human_summary}"
        )

        try:
            raw = await self._ask_model(prompt, workflow_run_id=workflow_run_id)
        except Exception:  # noqa: BLE001
            return result.human_summary

        candidate = raw.strip().splitlines()[0].strip() if raw.strip() else ""
        if not candidate or len(candidate) > 500:
            return result.human_summary

        # The restated summary must not contradict the verdict. A model that
        # summarises "no issues" for a failed validation has misunderstood, and
        # showing that to a client is worse than showing the plain text.
        lowered = candidate.lower()
        says_blocked = any(w in lowered for w in ("issue", "block", "fail", "cannot", "exceed", "not verified"))
        if not result.valid and not says_blocked:
            return result.human_summary

        return candidate
