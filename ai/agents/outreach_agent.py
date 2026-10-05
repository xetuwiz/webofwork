"""The Outreach Agent.

Sends invitations to the shortlist a client approved.

The model contributes nothing here. It writes a sentence describing what happened,
and even that is optional -- the action itself is a single call into .NET, which
re-checks the approval, the stage, and the shortlist membership before it creates
anything. This is deliberate: the one step in the pipeline that contacts a real
person should have the least machinery between it and a deterministic check.

If the model is asked for anything beyond a summary here, that is a bug, and the
agent has no code path that would let it.
"""

from __future__ import annotations

import time
from typing import Any
from uuid import UUID

from pydantic import BaseModel, Field

from .base import BaseAgent

#: Ceiling on one outreach call. Matches ``AiWorkflowService.MaxShortlistApprovals``
#: on the .NET side, which enforces the same bound. Two enforcers, because only
#: each can see its own count, and this one stops a runaway before the request is
#: even made.
MAX_SHORTLIST = 20


class OutreachInput(BaseModel):
    workflow_run_id: int
    project_id: int
    #: What the client approved, from the run's recorded decision.
    approved_freelancer_ids: list[UUID] = Field(default_factory=list)


class OutreachOutput(BaseModel):
    invitation_ids: list[int] = Field(default_factory=list)
    skipped: list[str] = Field(default_factory=list)
    errors: list[str] = Field(default_factory=list)
    human_summary: str = ""


class OutreachAgent(BaseAgent[OutreachInput, OutreachOutput]):
    name = "outreach"

    async def run(self, payload: OutreachInput) -> OutreachOutput:
        started = time.monotonic()
        tools = self._toolbox(payload.workflow_run_id)

        result = OutreachOutput()

        # ── preconditions ──────────────────────────────────────────────
        # Checked here as well as on the .NET side. This one gives a clear reason
        # in the run's log; that one is the authority. Belt and braces, because
        # the failure this prevents -- emailing people a client never approved --
        # is not a recoverable one.
        if not payload.approved_freelancer_ids:
            result.errors.append("No freelancer ids were supplied to invite.")
            result.human_summary = "Nothing was sent: the approval contained no freelancers."
            await self._record_step(
                workflow_run_id=payload.workflow_run_id,
                step_name="outreach",
                input_summary={"project_id": payload.project_id, "approved": 0},
                output_summary={"refused": "empty_approval"},
                status="refused",
                started=started,
            )
            return result

        if len(payload.approved_freelancer_ids) > MAX_SHORTLIST:
            result.errors.append(
                f"{len(payload.approved_freelancer_ids)} freelancers were approved, "
                f"above the limit of {MAX_SHORTLIST}."
            )
            result.human_summary = "Nothing was sent: the approval was too large."
            await self._record_step(
                workflow_run_id=payload.workflow_run_id,
                step_name="outreach",
                input_summary={"project_id": payload.project_id, "approved": len(payload.approved_freelancer_ids)},
                output_summary={"refused": "oversized_approval"},
                status="refused",
                started=started,
            )
            return result

        # Deduplicated before the call, so a repeated id does not become a second
        # invitation request that .NET then has to reject.
        unique = list(dict.fromkeys(str(f) for f in payload.approved_freelancer_ids))

        # ── the action ──────────────────────────────────────────────────
        try:
            response = await tools.call(
                "send_invitations",
                project_id=payload.project_id,
                freelancer_ids=unique,
            )
        except Exception as exc:  # noqa: BLE001
            # Reported, not raised. A failed outreach should leave the run
            # finished-with-errors so a person can see it and retry, rather than
            # a stack trace in a background job nobody reads.
            result.errors.append(f"{type(exc).__name__}: {exc}")
            result.human_summary = (
                f"Invitations could not be sent: {exc}. Nothing was contacted."
            )
            await self._record_step(
                workflow_run_id=payload.workflow_run_id,
                step_name="outreach",
                input_summary={"project_id": payload.project_id, "approved": len(unique)},
                output_summary={"error": str(exc)},
                status="failed",
                started=started,
            )
            return result

        # Parsed one at a time, with the failures kept.
        #
        # This used to be `[int(i) for i in ...]`, and the comprehension is outside
        # the `try` above -- so a single non-numeric id raised `ValueError` out of
        # `run()` and discarded the whole result. The invitations that *had* been
        # created were real: the client's freelancers had been contacted, and the
        # run said nothing had happened. A malformed id in a response is not worth
        # losing that.
        for raw_id in (response or {}).get("invitation_ids", []) or []:
            try:
                result.invitation_ids.append(int(raw_id))
            except (TypeError, ValueError):
                result.errors.append(
                    f"send-invitations returned an invitation id that is not a "
                    f"number: {raw_id!r}. That invitation cannot be recorded here, "
                    f"though it was created."
                )

        for item in (response or {}).get("skipped", []):
            # The reason is kept, because "we did not contact two of your five"
            # is a question a client will ask and needs an answer.
            if isinstance(item, dict):
                result.skipped.append(
                    f"{item.get('freelancer_id', 'unknown')}: {item.get('reason', 'no reason given')}"
                )
            else:
                result.skipped.append(str(item))

        if result.errors:
            status = "failed"
        elif result.invitation_ids:
            status = "ok"
        else:
            # No error but nobody invited. Possible if .NET reported everything as
            # skipped, which is worth flagging separately from an outright failure.
            status = "partial" if result.skipped else "failed"

        result.human_summary = self._summarise(result)

        await self._record_step(
            workflow_run_id=payload.workflow_run_id,
            step_name="outreach",
            input_summary={"project_id": payload.project_id, "approved": len(unique)},
            output_summary={
                "invited": len(result.invitation_ids),
                "skipped": len(result.skipped),
                "errors": result.errors[:3],
            },
            status=status,
            started=started,
        )
        return result

    @staticmethod
    def _summarise(result: OutreachOutput) -> str:
        """Plain statement of what happened.

        Written here rather than by the model. There is no judgement involved --
        this is a count -- and a model asked to phrase it could only add
        optimism.
        """
        if result.errors:
            return (
                f"Outreach failed: {result.errors[0]}. "
                "No invitations were sent."
            )

        invited = len(result.invitation_ids)
        skipped = len(result.skipped)

        if invited == 0:
            return "No invitations were sent; every approved freelancer was skipped."

        base = f"Sent {invited} invitation{'s' if invited != 1 else ''}."
        if skipped:
            base += (
                f" {skipped} could not be invited: "
                + "; ".join(result.skipped[:2])
            )
        return base
