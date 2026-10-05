"""A draft must be submittable, and the minimum is defined in another language.

## The failure

A live draft returned `skill_ids: [31, 48]` -- CSS3 and Accessibility, both real
Development rows. Grounding kept both, correctly. The client's form then refused
to submit:

    Required Skills
    Too small: expected array to have >=3 items

So "Help me draft" produced a draft that filled the form and left it invalid. The
draft panel looked complete. Nothing in the AI subsystem was wrong: the model was
asked for skills, returned two real ones, and code accepted them.

The rule lives in exactly one place --
`client/src/features/client/projectManagement/projectSchemas.js` --
and the server does not enforce it at all (`ProjectService` iterates whatever
skill ids it is given). So this constant and that schema are two unlinked
statements of the same fact, and nothing at runtime checks they agree.

That is why `test_the_minimum_matches_the_client_schema` exists and reads the
JavaScript. It is the only automated link available without a cross-language
build step, and it is the test that would have caught this before a client saw it.
"""

from __future__ import annotations

import re
from pathlib import Path

import pytest

from ai.agents.draft_agent import (
    MIN_REQUIRED_SKILLS,
    DraftAgent,
    DraftOutput,
    _top_up_to_minimum,
)

CLIENT_SCHEMA = (
    Path(__file__).resolve().parents[2]
    / "client/src/features/client/projectManagement/projectSchemas.js"
)


@pytest.fixture
def agent() -> DraftAgent:
    return DraftAgent(client=None)  # type: ignore[arg-type]


def _draft(**kw) -> DraftOutput:
    base = {
        "title": "Build a Modern Responsive Website",
        "description": "A modern, responsive, user-friendly website.",
        "requirements": "Must work across desktop, tablet and mobile.",
        "expected_deliverables": "Source code and a deployed site.",
        "category_id": 1,
        "skill_ids": [],
        "skill_names": [],
        "pricing_type": "fixed",
        "fixed_budget_usd": 1500,
        "min_budget_usd": None,
        "max_budget_usd": None,
        "delivery_days": 14,
        "confidence": 0.7,
        "notes": [],
    }
    base.update(kw)
    return DraftOutput(**base)


# ── the two definitions must agree ─────────────────────────────────────────


def test_the_minimum_matches_the_client_schema():
    """Read the JavaScript. Do not trust that someone remembered to update it.

    The Python constant is a copy of a number that lives in a `.js` file in a
    different build. Nothing links them, so this reads the source and compares.
    If this test is ever awkward to run in some environment, the honest fix is a
    shared generated constant -- not deleting the check.
    """
    assert CLIENT_SCHEMA.exists(), (
        f"the client schema moved: {CLIENT_SCHEMA} is gone, so the skill minimum "
        "this file mirrors could not be verified"
    )

    source = CLIENT_SCHEMA.read_text(encoding="utf-8")

    # The array's own `.min(N)` is the LAST one chained in the skills block, and
    # taking the first was wrong twice over.
    #
    # This block contains two `.min()` calls: `.min(1)` on each element (the blank
    # rule) and `.min(3)` on the array (the platform minimum). Both earlier
    # patterns picked the wrong one -- a non-greedy `.*?\.min\((\d+)` grabs the
    # first, and anchoring on `.array(...)` fails too because `.trim()` closes
    # immediately before `.min(1)`.
    #
    # Taking the last is a structural claim, not a guess: the element schema is
    # nested inside `.array(...)`, and the array's own constraint is chained after
    # it. The assertion on the count below is the guard on that claim -- if a
    # future edit adds or removes a `.min()` here, this test says so instead of
    # quietly reading the wrong number.
    block = re.search(r"skills:[\s\S]*?pricing_type:", source)
    assert block, (
        f"could not find the skills block in {CLIENT_SCHEMA.name}; the schema's "
        "shape changed and this check no longer verifies anything"
    )

    minimums = re.findall(r"\.min\((\d+)", block.group(0))
    assert len(minimums) >= 1, "the skills field has no .min() at all"
    assert len(minimums) <= 2, (
        f"expected at most two .min() calls in the skills block (one per element, "
        f"one for the array) but found {len(minimums)}: {minimums}. The 'last one "
        "is the array's' assumption no longer holds, so this test would read the "
        "wrong number without saying so."
    )

    client_minimum = int(minimums[-1])

    assert client_minimum == MIN_REQUIRED_SKILLS, (
        f"the client requires {client_minimum} skills but the drafter pads to "
        f"{MIN_REQUIRED_SKILLS}. A draft built to this constant would be "
        "rejected by the form."
    )


def test_the_server_does_not_enforce_the_minimum_we_are_mirroring():
    """Confirms *why* the Python side has to enforce it.

    If the API ever starts validating skill counts, the drafter's padding is
    redundant and this should be revisited -- but until then the form is the only
    gate, and a draft that fails it is a draft that cannot be posted.
    """
    service = (
        Path(__file__).resolve().parents[2]
        / "server/WebOfWork.Api/Services/ProjectService.cs"
    )
    if not service.exists():
        pytest.skip("server sources not present in this checkout")

    body = service.read_text(encoding="utf-8")
    skill_block = re.search(r"if \(request\.SkillIds != null\).*?\n\s*\}", body, re.DOTALL)

    assert skill_block, "could not locate the SkillIds handling to inspect"
    assert ".Count" not in skill_block.group(0), (
        "ProjectService now validates skill count; the drafter's minimum and "
        "padding can be reconsidered against it"
    )


# ── the padding behaviour itself ───────────────────────────────────────────


class TestPaddingReachesTheMinimum:
    def test_a_short_list_is_padded_with_skills_the_brief_names(self):
        """The live case: CSS3 and Accessibility, on a responsive-website brief."""
        pool = {31: "CSS3", 48: "Accessibility", 8: "JavaScript", 47: "Responsive Design"}
        draft = _draft(skill_ids=[31, 48], skill_names=["CSS3", "Accessibility"])

        ids, names, notes = _top_up_to_minimum(draft, pool, [31, 48], ["CSS3", "Accessibility"])

        assert len(ids) == MIN_REQUIRED_SKILLS
        assert set(ids) == {31, 48, 47}, f"padded with the wrong rows: {ids}"
        assert "Responsive Design" in names
        assert notes, "padding must be disclosed; the client did not choose these"

    def test_the_padding_is_disclosed_by_name(self):
        """The client is looking at this form and can delete chips.

        A silent addition would be indistinguishable from something the model
        decided, which is the one thing the AI-filled badge is supposed to mean.
        """
        pool = {31: "CSS3", 48: "Accessibility", 47: "Responsive Design"}
        draft = _draft(skill_ids=[31], skill_names=["CSS3"])

        _, _, notes = _top_up_to_minimum(draft, pool, [31], ["CSS3"])

        assert len(notes) == 1
        assert "Responsive Design" in notes[0]

    def test_it_never_pads_with_skills_the_brief_did_not_refer_to(self):
        """Padding must improve the draft, not dilute it.

        Taking the head of the catalogue would attach "Ansible" to a website
        brief purely because it sorts early. Only rows sharing a word with what
        the client wrote are eligible.
        """
        pool = {900: "Ansible", 901: "Assembly", 902: "COBOL"}
        draft = _draft(skill_ids=[31], skill_names=["CSS3"])

        ids, names, notes = _top_up_to_minimum(draft, pool, [31], ["CSS3"])

        assert ids == [31], f"irrelevant skills were attached: {names}"
        assert "3" in notes[0], "the client must be told the form will not submit"

    def test_a_full_list_is_left_alone(self):
        pool = {31: "CSS3", 48: "Accessibility", 8: "JavaScript", 47: "Responsive Design"}
        draft = _draft()

        ids, names, notes = _top_up_to_minimum(
            draft, pool, [31, 48, 8], ["CSS3", "Accessibility", "JavaScript"]
        )

        assert ids == [31, 48, 8]
        assert notes == [], "a satisfied minimum must produce no note"

    def test_nothing_is_padded_when_the_catalogue_is_unreachable(self):
        """No pool means no claim about what exists.

        Padding from an empty pool is impossible by construction; this asserts the
        function reports the shortfall instead of inventing ids.
        """
        draft = _draft(skill_ids=[31], skill_names=["CSS3"])

        ids, names, notes = _top_up_to_minimum(draft, {}, [31], ["CSS3"])

        assert ids == [31]
        assert notes == [], (
            "with no catalogue there is nothing truthful to say about a minimum"
        )

    def test_padding_is_stable_across_calls(self):
        """A draft that reshuffles its skills on every call is unreadable."""
        pool = {47: "Responsive Design", 8: "JavaScript", 30: "HTML5"}
        draft = _draft(skill_ids=[31], skill_names=["CSS3"])

        runs = [
            _top_up_to_minimum(draft, pool, [31], ["CSS3"])[:2] for _ in range(5)
        ]

        assert all(r == runs[0] for r in runs), f"padding was not deterministic: {runs}"


class TestGroundingEnforcesItEndToEnd:
    @pytest.mark.asyncio
    async def test_a_two_skill_draft_comes_out_of_grounding_submittable(self, agent):
        """Through `_ground`, not just the helper.

        The helper is easy to wire up wrongly -- called before the ids are
        resolved, or not called at all on some path. This is the path the live
        failure went through.
        """
        pool_rows = [
            {"id": 31, "name": "CSS3", "categoryId": 1},
            {"id": 48, "name": "Accessibility", "categoryId": 1},
            {"id": 47, "name": "Responsive Design", "categoryId": 1},
            {"id": 900, "name": "Ansible", "categoryId": 1},
        ]

        draft = agent._ground(
            _draft(skill_ids=[31, 48], skill_names=["CSS3", "Accessibility"]),
            pool_rows,
            {1},
            {"development": {"id": 1, "name": "Development"}},
        )

        assert len(draft.skill_ids) >= MIN_REQUIRED_SKILLS, (
            f"the drafter produced a form that cannot submit: {draft.skill_ids}"
        )
        assert all(isinstance(i, int) for i in draft.skill_ids), (
            "padding must only ever attach real catalogue ids"
        )
        assert 900 not in draft.skill_ids, "Ansible was attached to a website brief"