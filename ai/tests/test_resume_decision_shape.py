"""Does an approval survive the trip across the .NET boundary?

## The failure these exist for

``WorkflowRun.ResumeValue`` is a jsonb column stored as ``str``. Embedding it in
the outgoing ``agent/chat`` payload by value sent it as a JSON *string*:

    "resume_value": "{\\"freelancerIds\\":[\\"…\\"]}"

The reader guarded with ``isinstance(resume, dict)``, found a string, and fell
through to an empty list. So the client approved a shortlist, the outreach stage
ran, **zero invitations were created**, and the run reported success. Nothing
errored anywhere.

.NET now parses the column before sending (``AsJsonValue``), but a consumer that
quietly degrades on a shape mismatch is a trap for the next producer -- so both
the parsing and the refusal-to-succeed-silently are pinned here.
"""

from __future__ import annotations

import asyncio
import json

from ai.graph.dispatcher import (
    AgentChatRequest,
    approved_ids_from,
)

#: The decision exactly as .NET wrote it into the jsonb column.
DECISION = {
    "freelancerIds": ["aaaa-1111", "bbbb-2222"],
    "approvedBy": "c2f0f0f0-0000-0000-0000-000000000000",
    "approvedAt": "2026-10-04T12:00:00Z",
}


class TestTheDecisionIsRead:
    def test_as_an_object(self):
        assert approved_ids_from({"resume_value": DECISION}) == [
            "aaaa-1111",
            "bbbb-2222",
        ]

    def test_as_the_column_text(self):
        # The shape that shipped: the jsonb forwarded as a quoted string.
        assert approved_ids_from(
            {"resume_value": json.dumps(DECISION)}
        ) == ["aaaa-1111", "bbbb-2222"]

    def test_snake_case_keys(self):
        assert approved_ids_from(
            {"resume_value": {"freelancer_ids": ["cccc-3333"]}}
        ) == ["cccc-3333"]

    def test_camel_case_context_key(self):
        assert approved_ids_from({"resumeValue": DECISION}) == [
            "aaaa-1111",
            "bbbb-2222",
        ]

    def test_ids_are_stringified(self):
        # The ids reach the agent and .NET as strings. Leaving them as whatever
        # JSON produced would serialise a bare number for a non-Guid id.
        assert approved_ids_from(
            {"resume_value": {"freelancerIds": [1, 2]}}
        ) == ["1", "2"]

    def test_preserves_order(self):
        # The client ticked them in a visible order; the invitations should not
        # be shuffled relative to it.
        assert approved_ids_from(
            {"resume_value": {"freelancerIds": ["z", "a", "m"]}}
        ) == ["z", "a", "m"]


class TestAnUnreadableDecisionYieldsNothing:
    """The reader is honest. What happens next is the dispatcher's job."""

    def test_unparseable_text(self):
        assert approved_ids_from({"resume_value": "{not json"}) == []

    def test_a_bare_number(self):
        assert approved_ids_from({"resume_value": 7}) == []

    def test_no_decision_at_all(self):
        assert approved_ids_from({}) == []

    def test_a_decision_with_no_ids(self):
        assert approved_ids_from({"resume_value": {"approvedBy": "x"}}) == []


class TestOutreachDoesNotSucceedHavingContactedNobody:
    """The reader returning ``[]`` is only safe because of this.

    An approval that contacts nobody must not report success: the client has been
    told these people were invited, and an empty result is indistinguishable from
    a legitimately empty approval unless something refuses to call it one.
    """

    @staticmethod
    def _outreach(context: dict) -> tuple[str, str, object]:
        from ai.graph.dispatcher import StageDispatcher

        request = AgentChatRequest(
            message="Find talent",
            workflow_run_id=25,
            stage="talent_match",
            project_id=11,
            context=context,
        )
        dispatcher = StageDispatcher(request)
        return asyncio.run(dispatcher._outreach())

    def test_an_unparseable_decision_fails_the_stage(self):
        status, summary, _ = self._outreach({"resume_value": "{not json"})

        assert status == "failed"
        assert "nobody was contacted" in summary

    def test_a_decision_with_no_ids_fails_the_stage(self):
        status, summary, _ = self._outreach(
            {"resume_value": {"freelancerIds": []}}
        )

        assert status == "failed"
        assert "nobody was contacted" in summary

    def test_the_failure_says_the_approval_can_be_retried(self):
        # A dead end with no route out of it is worse than the original bug: the
        # client is stuck with a run they cannot finish.
        _, summary, _ = self._outreach({"resume_value": "{not json"})

        assert "Re-approving" in summary

    def test_an_absent_decision_is_not_reported_as_corruption(self):
        # Nothing to resume from and a decision that cannot be read are different
        # faults. Collapsing them would make the run's error text useless for
        # telling them apart, and would fail a resume that never had a decision.
        _, summary, _ = self._outreach({})

        assert "could not be read" not in summary