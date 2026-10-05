"""The Draft Agent.

Turns whatever a client has typed into a structured project draft whose
``category_id`` and ``skill_ids`` are **real rows**.

The model chooses; Python verifies. The most likely failure of a language model
asked to fill in a database id is to invent one, and an invented id on a posted
project is a broken browse page. So every id in the output is checked against the
category list the .NET side supplied with the request, and a hallucinated one is
dropped and noted rather than passed through.

The draft never becomes a project. The client reviews it, edits it, and submits
through the existing endpoint, which re-validates.
"""

from __future__ import annotations

import logging
import re
import time
from decimal import Decimal
from typing import Any, Literal

from pydantic import BaseModel, Field

from ..llm.base import LLMUnavailableError, ToolCallResponse
from ..llm.chain import call_llm_with_tools
from .base import AgentError, BaseAgent
from .tool_loop import (
    MAX_TOOL_RESULT_CHARS,
    LoopResult,
    ToolLoopError,
    json_schema_for,
    run_tool_loop,
)

log = logging.getLogger(__name__)

#: What the drafter may look up.
#:
#: Every name here is already permitted for the draft agent by
#: ``AGENT_TOOL_PERMISSIONS``, and a test asserts the two sets agree. That check
#: exists because they disagreed once for the planner -- five schemas advertised,
#: three permitted -- and the loop turns every refusal into an observation, so the
#: agent carried on working and nobody noticed it had been working blind.
DRAFTER_TOOL_SCHEMAS = [
    json_schema_for(
        "list_categories",
        "List every service category with its id, name and skills. Call this "
        "first when grounding a category or skill id: only ids from this list "
        "survive validation.",
        {},
        [],
    ),
    json_schema_for(
        "list_skills_by_category",
        "List the real skills in one category, with their ids. Use once you have "
        "chosen a category and need genuine skill ids for it rather than "
        "invented ones. Call it with no `search`: every category's full list "
        "fits, and you choose from all of it. Leave `search` alone unless the "
        "result is explicitly marked TRUNCATED.",
        {
            "category_id": {"type": "integer", "description": "The category id."},
            "search": {
                "type": "string",
                "description": (
                    "A last resort, for a result explicitly marked TRUNCATED. Do "
                    "NOT set this from a word in the brief. It matches skill "
                    "NAMES by substring, and a brief's vocabulary is not a "
                    "catalogue's: searching 'website' matches only Webflow, "
                    "Webpack and WebSockets, hiding CSS3, HTML5 and JavaScript. "
                    "For a website brief the answer is usually the unsearched "
                    "list."
                ),
            },
        },
        ["category_id"],
    ),
    json_schema_for(
        "get_project",
        "Re-read the existing project: description, requirements and "
        "deliverables. Use when the brief refers to work already posted and you "
        "need that detail.",
        {"project_id": {"type": "integer", "description": "The project id."}},
        ["project_id"],
    ),
    json_schema_for(
        "get_platform_commission",
        "Read the platform commission percentage. Use only if you intend to "
        "suggest a price, so the suggestion accounts for the platform's cut.",
        {},
        [],
    ),
    json_schema_for(
        "get_price_bands",
        "Advertised reference price bands for comparable work: deliverable, "
        "experience level, and a low-high range in USD, each with the source it "
        "came from. Call this whenever you are about to set a price and have not "
        "looked one up yet -- guessing a number is worse than quoting a range you "
        "can attribute. These are reference bands, not quotes: observed freelancer "
        "rates run well outside them at both ends, so present the range as a "
        "starting point, never as what the work costs.",
        {
            "search": {
                "type": "string",
                "description": "Optional word to match deliverable names against.",
            },
            "category_id": {
                "type": "integer",
                "description": "Optional category id, to narrow the list.",
            },
        },
        [],
    ),
]

#: The operating manual.
#:
#: Three rules, each preventing a specific failure: hallucinating an id (the
#: grounding rule), over-fetching (the budget rule), and never terminating (the
#: stop rule). Lecture 07's phrasing applies -- "Multi-agent bugs are usually
#: missing policy, not missing intelligence."
DRAFTER_SYSTEM = """\
You are the drafting agent for a freelance marketplace. You turn a client's rough
brief into a structured project that other freelancers will be matched against.

How to work:
1. Ground every id before you use it. `category_id` and `skill_ids` MUST come from
   the catalogue you looked up. An id you invent is deleted by validation and the
   draft is weaker for it. If you have not looked up the categories, do that now.
2. Attach at least 3 skills. A project with fewer than 3 will not submit: the
   platform needs three real skills to match it to freelancers. Pick the ones a
   freelancer would actually list for this brief -- fewer than three is the most
   common reason a draft comes back unusable.
3. Write for the freelancer who will read it. The description, requirements and
   expected deliverables are what someone decides whether to apply on. Be specific
   about the outcome, not vague about intent.
4. Price from the reference bands, not from memory. Call `get_price_bands` before
   you set a price you did not get from the client. These are advertised bands
   from a marketplace's public pages, and observed freelancer rates run far
   outside them at both ends -- so quote the range, attribute it, and put the
   judgement (where inside the band this brief sits, and why) in your own words.
   A number with no source behind it is the one thing worse than no number.
5. Call each tool once. A second call with different arguments repeats an answer
   you already have.
6. Stop as soon as you can draft. Two or three lookups beat six.
7. Reply with ONE JSON object and nothing else -- no explanation before it, no
   code fence around it.

If the client's text is too thin to draft from, set confidence below 0.4 and put
one specific question in clarification_question. Do not invent the brief.
"""

#: The JSON contract, given verbatim.
#:
#: Written out rather than generated so the model sees the field names and the
#: `"fixed"|"range"` constraint a generated schema would hide. The parser still
#: validates against :class:`DraftOutput`.
DRAFT_SCHEMA_HINT = """\
{
  "title": "short and specific",
  "description": "what the client wants, in their terms",
  "requirements": "what must be true when it is done",
  "expected_deliverables": "what is handed over",
  "category_id": <int from the catalogue>,
  "skill_ids": [<ints from the catalogue>],
  "pricing_type": "fixed" | "range",
  "fixed_price_usd": <number or null>,
  "min_budget_usd": <number or null>,
  "max_budget_usd": <number or null>,
  "delivery_days": <int>,
  "confidence": 0.0-1.0,
  "notes": ["anything you had to assume"]
}"""

#: Bound on the client's typed text, matching ``AiDraftController``'s own cap.
#: Two limits in two places is deliberate: the controller's rejects a bad request
#: cheaply, and this one stops a text that arrived by some other route.
MAX_PARTIAL_TEXT = 8000

MIN_TITLE_LENGTH = 5
MIN_DESCRIPTION_LENGTH = 20
MIN_DELIVERY_DAYS = 1
MAX_DELIVERY_DAYS = 365


class DraftInput(BaseModel):
    workflow_run_id: int
    partial_text: str = Field(..., max_length=MAX_PARTIAL_TEXT)
    title_hint: str | None = None
    category_hint: str | None = None
    budget_hint_usd: Decimal | None = None
    delivery_days_hint: int | None = None


class DraftOutput(BaseModel):
    """What the client is shown. Every field is editable."""

    title: str
    description: str
    requirements: str
    expected_deliverables: str
    category_id: int | None = None
    category_name: str | None = None
    skill_ids: list[int] = Field(default_factory=list)
    skill_names: list[str] = Field(default_factory=list)
    pricing_type: Literal["fixed", "range"] = "fixed"
    fixed_price_usd: Decimal | None = None
    min_budget_usd: Decimal | None = None
    max_budget_usd: Decimal | None = None
    delivery_days: int = 14
    confidence: float = 0.5
    notes: list[str] = Field(default_factory=list)
    #: Set when the input was too thin to draft from. The client is asked this
    #: rather than shown a confident guess.
    clarification_question: str | None = None


#: Fewest skills a project may carry.
#:
#: The source of truth is the client's own schema --
#: `client/src/features/client/projectManagement/projectSchemas.js`, `skills:
#: z.array(...).min(3)`. The server does **not** enforce it (`ProjectService`
#: iterates whatever ids it is given), so this number and that schema are the only
#: two places it exists, and nothing at runtime links them.
#:
#: That gap had a visible consequence: a live draft supplied two real skills,
#: "Fill the form" merged them, and the form then refused to submit with a raw
#: zod message. The draft looked complete and was not submittable.
#:
#: `tests/test_skill_minimum.py` reads the JS and fails if the two disagree, which
#: is the only automated link available without a cross-language build step.
MIN_REQUIRED_SKILLS = 3


def _skill_terms(draft: DraftOutput) -> set[str]:
    """Words from the draft's own prose, for judging skill relevance.

    Deliberately the draft's own words and not the category name: "responsive
    website" and the Development category share no vocabulary, so a category-name
    match would pad every brief with the same few skills regardless of what was
    asked for.
    """
    blob = " ".join(
        part
        for part in (
            draft.title,
            draft.description,
            draft.requirements,
            draft.expected_deliverables,
        )
        if isinstance(part, str)
    ).lower()
    # Two characters minimum, so "c" and "go" do not match half the catalogue.
    return {w for w in re.findall(r"[a-z0-9+#]{2,}", blob)}


def _top_up_to_minimum(
    draft: DraftOutput,
    skill_pool: dict[int, str],
    ids: list[int],
    names: list[str],
) -> tuple[list[int], list[str], list[str]]:
    """Bring a short skill list up to the platform minimum, or explain why not.

    The model cannot be relied on to count, and this is a validity rule rather
    than a matter of taste -- so it is enforced here, in the same place every
    other validity decision already is. `_ground` decides which ids are real;
    this decides whether there are enough of them.

    Padding is by *relevance to the brief*, not by taking the head of the
    catalogue. Padding with arbitrary rows would make the draft worse: a
    responsive-website brief would acquire "Ansible" and "Assembly" purely
    because they sort early. Only skills whose name shares a word with what the
    client actually wrote are eligible.

    If nothing relevant exists, the list is left short and the note says so. An
    honest "add one more" is better than a form that submits with skills nobody
    would have chosen, and the client is already looking at this form.
    """
    missing = MIN_REQUIRED_SKILLS - len(ids)
    if missing <= 0 or not skill_pool:
        return ids, names, []

    chosen = set(ids)
    terms = _skill_terms(draft)

    scored: list[tuple[int, str, int]] = []
    for skill_id, skill_name in skill_pool.items():
        if skill_id in chosen:
            continue
        skill_terms = {w for w in re.findall(r"[a-z0-9+#]{2,}", skill_name.lower())}
        overlap = terms & skill_terms
        if not overlap:
            continue
        # More shared words first, then by id so the result is stable across runs
        # -- a draft that reshuffles its skills on every call is unreadable.
        scored.append((-len(overlap), skill_id, skill_name))

    if not scored:
        return ids, names, [
            f"The platform needs at least {MIN_REQUIRED_SKILLS} skills for a "
            "project to be matched to freelancers. Only "
            f"{len(ids)} fitted this brief, so add at least "
            f"{missing} more from the list before posting."
        ]

    scored.sort()
    added = scored[:missing]
    new_ids = [*ids, *(entry[1] for entry in added)]
    new_names = [*names, *(entry[2] for entry in added)]
    return new_ids, new_names, [
        f"Added {' and '.join(repr(entry[2]) for entry in added)} to reach the "
        f"minimum of {MIN_REQUIRED_SKILLS} skills, because the brief names them "
        "and fewer than the platform minimum were suggested. Remove any that do "
        "not apply."
    ]


def compact_reference_rows(rows: list[dict]) -> str:
    """Render catalogue rows for the model, without the JSON's own weight.

    ## Why this exists

    A live run asked for `list_skills_by_category(1, search="web")` and got back
    exactly three rows: Webflow, Webpack, WebSockets. It picked all three, because
    `MIN_REQUIRED_SKILLS` is three and three was all it had ever seen. HTML5, CSS3,
    JavaScript and Responsive Design were all in that category the whole time and
    were never shown to it. So the drafter proposed a build-toolchain for a
    marketing site.

    The model did nothing wrong here. The tool misled it.

    ## The cause was our own framing, not the provider

    Development is 199 skills. Serialised as JSON that is 12,443 characters, and
    `MAX_TOOL_RESULT_CHARS` is 4,000 -- so the result was cut, and cut
    *alphabetically*, which removes everything from the middle of the alphabet
    onwards. `search` existed to reach past that cut. So the model reached for it,
    and reached for it with the one word its brief supplied: "website".

    `search` matches skill *names* by substring. "web" matches Webflow, Webpack
    and WebSockets, and nothing else. It cannot match CSS3 or JavaScript, because
    those names do not contain the word the client used to describe the work. The
    vocabulary of a brief and the vocabulary of a catalogue are different
    vocabularies, and a name-substring search bridges them only by accident.

    ## The fix

    Every category fits comfortably once the JSON stops padding itself:

        Development (199 skills)   12,443 chars as JSON  ->  2,689 compact
        category 2     (127)        8,636 chars as JSON  ->  2,410 compact
        category 3     (110)        7,172 chars as JSON  ->  1,779 compact

    Nothing is truncated, so nothing needs `search`, so the model chooses from the
    real 199 rather than from whatever three its own wording happened to match.

    ## Why a string and not a list of dicts

    Grounding needs the rows, and `_skills_seen` captures them before this is
    applied. Returning the compact form keeps the ids and names the model must
    quote while the structured rows stay available to the code that checks them.
    """
    parts: list[str] = []
    for row in rows:
        if not isinstance(row, dict):
            continue
        # Both wire spellings, for the same reason `_row_category` accepts both.
        name = row.get("name") or row.get("Name") or ""
        rid = row.get("id", row.get("Id"))
        if name == "":
            continue
        parts.append(f"{rid} {name}" if rid is not None else str(name))

    if not parts:
        return "No rows."

    body = ", ".join(parts)
    if len(body) <= MAX_TOOL_RESULT_CHARS:
        return body

    # Kept because a compact format is not a guarantee: a future category large
    # enough to overflow it must still say so rather than appear complete.
    kept: list[str] = []
    for part in parts:
        if sum(len(p) + 2 for p in kept) + len(part) > MAX_TOOL_RESULT_CHARS - 160:
            break
        kept.append(part)
    omitted = len(parts) - len(kept)
    return (
        f"{', '.join(kept)}.\n\n"
        f"TRUNCATED: {omitted} further skill(s) were cut to fit. Names are listed "
        "alphabetically, so the ones omitted are not absent from the platform -- "
        f"call again with `search` for a specific skill. Omitted: {omitted}."
    )


def _row_category(row: dict) -> int | None:
    """The category a catalogue row belongs to, in either wire spelling.

    Two row shapes have to be read here, and they do not identify their category
    the same way. A category row's `id` *is* its category; a flat skill row's `id`
    is the skill's own, and its category is a separate field. Scoping or
    discriminating on `id` alone therefore treats every flat skill row as a
    category row -- which deletes it from the pool, or promotes it into one.

    Both key spellings are accepted because both are real. The boundary
    normalises .NET's PascalCase to camelCase (`categoryId`), while the agent
    contracts are written snake_case (`category_id`). Reading only one silently
    drops the other shape.

    Module level because two places need this and they must not disagree: the
    scoping filter here, and the decision in `run` about whether a re-fetch is
    needed. When they disagreed, the second asked for a fetch the first had
    already satisfied.
    """
    raw = row.get("categoryId", row.get("category_id"))
    if raw is None:
        # A category row, not a skill row.
        return row.get("id")
    try:
        return int(raw)
    except (TypeError, ValueError):
        return None


class DraftAgent(BaseAgent[DraftInput, DraftOutput]):
    name = "draft"

    #: The last loop trajectory, as plain data. See :class:`PlanningAgent` for
    #: why this is an attribute and not a return value -- it is written into the
    #: step record, which is the durable place the evaluation reads it from.
    _last_trajectory: list[dict[str, Any]] = []

    #: Categories and skills the model actually looked up during the last run.
    #: Reset at the start of every run so two drafts in one process cannot see
    #: each other's lookups.
    _categories_seen: list[dict] = []
    _skills_seen: list[dict] = []

    async def run(self, payload: DraftInput) -> DraftOutput:
        started = time.monotonic()
        tools = self._toolbox(payload.workflow_run_id)

        # ── the clarification path ────────────────────────────────────────
        # Checked before spending a model call. A single word is not a brief, and
        # the honest answer is a question rather than an invention.
        self._last_trajectory = []
        self._categories_seen = []
        self._skills_seen = []

        if not payload.partial_text.strip():
            result = DraftOutput(
                title=payload.title_hint or "",
                description="",
                requirements="",
                expected_deliverables="",
                confidence=0.0,
                clarification_question=(
                    "What should be built? A sentence or two about the work, the "
                    "outcome you want, and roughly when you need it is enough to start."
                ),
            )
            await self._record_step(
                workflow_run_id=payload.workflow_run_id,
                step_name="draft",
                input_summary={"partial_text_chars": 0, "path": "empty_input"},
                output_summary={"clarification_requested": True},
                status="pending_clarification",
                started=started,
            )
            return result

        # ── the judgement call, via a model-directed tool loop ────────────
        #
        # Previously every fact was fetched first and the model received one
        # pre-formatted prompt. That made drafting a single-shot classification:
        # the model could not decide it needed to look at the category catalogue
        # again, or re-read the project, or check what skills exist in the
        # category it had just picked.
        #
        # Those are real unknowns. A client says "I need something like that
        # dashboard we discussed" and the agent has to go and find out what
        # dashboards exist here before it can draft. That is the same
        # unknown-unknown discovery that justifies the planning loop, and it is
        # the reason this agent is autonomous.
        #
        # Everything downstream is unchanged and still deterministic: the model
        # chooses *tools*, never the output's validity. `_ground` still drops any
        # id the model invented, and `_validate` still rejects a malformed draft.
        loop = await self._draft_with_tools(
            payload=payload, toolbox=tools, workflow_run_id=payload.workflow_run_id
        )
        self._last_trajectory = loop.trace() if loop else []

        if loop is None or not loop.completed:
            # No provider could do tool calling, or the loop ran out of steps.
            # Distinct from a failed draft: the client gets a clarification
            # request rather than an empty project, because a half-draft is worse
            # than an honest question.
            result = DraftOutput(
                title=payload.title_hint or "",
                # The three prose fields are required by the contract, and this
                # branch omitted all of them.
                #
                # That made the fallback unreachable: constructing the
                # "ask a question instead" draft raised ValidationError, so the
                # run died with three missing-field errors instead of returning
                # the clarification the client was meant to see. The graceful path
                # only ever existed on paper.
                #
                # They echo what the client actually sent rather than being empty,
                # so the form they land in is not blanked out. `confidence=0.0` and
                # the question are what mark this as not a real draft.
                description=payload.partial_text,
                requirements=payload.partial_text,
                expected_deliverables=payload.partial_text,
                confidence=0.0,
                clarification_question=(
                    "I could not draft this reliably. Please tell me what should be "
                    "built, the outcome you want, and roughly when you need it."
                ),
                notes=["Drafting was unavailable; a question was asked instead."],
            )
            await self._record_step(
                workflow_run_id=payload.workflow_run_id,
                step_name="draft",
                input_summary={"path": "tool_loop_unavailable"},
                output_summary={
                    "clarification_requested": True,
                    "trajectory": self._last_trajectory,
                },
                status="pending_clarification",
                started=started,
            )
            return result

        try:
            draft = await self._parse(
                loop.answer, DraftOutput, workflow_run_id=payload.workflow_run_id
            )
        except AgentError:
            # A model that cannot produce parseable output gets a second,
            # deliberately bare attempt rather than a failure. The first prompt
            # carries the whole context; the retry says only "JSON, this schema".
            retry = (
                "Return ONLY a JSON object matching this schema, no prose:\n"
                '{"title": str, "description": str, "requirements": str, '
                '"expected_deliverables": str, "category_id": int, "skill_ids": [int], '
                '"pricing_type": "fixed"|"range", "fixed_price_usd": number|null, '
                '"min_budget_usd": number|null, "max_budget_usd": number|null, '
                '"delivery_days": int, "confidence": number, "notes": [str]}\n\n'
                f"Client text:\n{payload.partial_text[:2000]}"
            )
            raw = await self._ask_model(retry, workflow_run_id=payload.workflow_run_id)
            draft = await self._parse(raw, DraftOutput, workflow_run_id=payload.workflow_run_id)

        # ── grounding ─────────────────────────────────────────────────────
        # Categories for the id/name resolution, then skills for the chosen
        # category.
        #
        # Two lookups rather than one, and deliberately: the server sends
        # categories WITHOUT their skills, because nesting all 654 of them on
        # every draft overflowed the request limit on the primary provider
        # ("413 Payload Too Large", twice, mid-draft) and gave the model a
        # 654-row list to choose from instead of the ~200 that apply.
        #
        # BOTH sources, concatenated. This used to be `self._categories_seen or
        # self._skills_seen`, which reads like a preference and behaves like a
        # trap: a model that calls `list_categories` first populates
        # `_categories_seen`, so the `or` short-circuits and discards
        # `_skills_seen` -- the only source that carries skills.
        #
        # A live draft did exactly that. The model called
        # `list_skills_by_category(1)` twice and got the real Development skills,
        # and the draft came back with `skill_ids: []` and three notes: the
        # catalogue "arrived without its skills", and skill ids 1 and 2 "are not
        # a real row". Ids 1 and 2 are C# and React -- real rows in the category
        # the model had just looked up. The rows were thrown away one instruction
        # before they were needed.
        #
        # Order is preserved and duplicates are harmless: `_ground_skills` builds
        # a pool keyed by skill id, and a category appearing twice contributes
        # the same ids twice.
        categories: list[dict] = [*self._categories_seen, *self._skills_seen]
        if not categories:
            try:
                categories = await tools.call("list_categories", best_effort=True) or []
            except Exception as exc:  # noqa: BLE001
                log.info("draft category read failed, grounding will be lenient: %s", exc)
                categories = []

        category_ids = {c["id"] for c in categories if "id" in c}
        category_names = {str(c.get("name", "")).lower(): c for c in categories if "id" in c}

        # Resolve the category FIRST, then fetch that category's skills, then
        # ground the skills against them.
        #
        # The order is the fix. Grounding both at once meant the skill pool was
        # whatever the model happened to have looked at, which is how a
        # "responsive website" brief ended up carrying ClickHouse, Snowflake and
        # BigQuery: those were real rows in the pool, so they validated, and the
        # model's actual request -- recorded in its own notes as "Frontend
        # Development, UI/UX Design, Web Performance" -- was overwritten by
        # whatever the ids happened to point at.
        draft = self._ground_category(draft, categories, category_ids, category_names)

        if draft.category_id is not None:
            # `_row_category`, not `c.get("id")`: on a flat skill row the id is the
            # skill's, so this test could never see skills the model had already
            # fetched and would re-read the catalogue on every single run.
            scoped = [c for c in categories if _row_category(c) == draft.category_id]
            if not any(
                isinstance(c.get("skills"), list) and c["skills"] for c in scoped
            ) and not any(
                "categoryId" in c or "category_id" in c for c in scoped
            ):
                try:
                    fetched = await tools.call(
                        "list_skills_by_category",
                        category_id=draft.category_id,
                        best_effort=True,
                    )
                except Exception as exc:  # noqa: BLE001
                    log.info("draft skill read failed: %s", exc)
                    fetched = None
                if fetched:
                    categories = list(categories) + [
                        c for c in fetched if isinstance(c, dict)
                    ]

        draft = self._ground_skills(draft, categories)

        # ── deterministic validation ──────────────────────────────────────
        self._validate(draft, payload)

        await self._record_step(
            workflow_run_id=payload.workflow_run_id,
            step_name="draft",
            input_summary=self._summary(payload),
            output_summary={
                "category_id": draft.category_id,
                "skill_count": len(draft.skill_ids),
                "pricing_type": draft.pricing_type,
                "delivery_days": draft.delivery_days,
                "confidence": draft.confidence,
                "notes": draft.notes[:5],
                # The trajectory, so evaluation can assert the model actually
                # discovered something rather than drafting blind.
                "trajectory": self._last_trajectory,
                "stop_reason": loop.stop_reason,
                "tools_called": list(loop.tools_called),
            },
            status="ok",
            started=started,
        )
        return draft

    async def _draft_with_tools(
        self,
        *,
        payload: DraftInput,
        toolbox: Any,
        workflow_run_id: int,
    ) -> LoopResult | None:
        """Run the model-directed drafting loop.

        Returns ``None`` when no provider can do tool calling, which is the
        signal to ask the client a question instead. Distinct from a loop that
        ran and failed: one is a configuration gap, the other is a model that
        could not finish.
        """
        # Reset per run, not per instance state carried between runs: two drafts
        # in one process must not see each other's category lookups.
        self._categories_seen = []

        objective = (
            f"Turn this client's rough brief into a structured project draft.\n\n"
            f"Client text:\n{payload.partial_text[:2000]}\n"
            f"{'Title hint: ' + payload.title_hint if payload.title_hint else ''}\n\n"
            f"Look up whatever you need before drafting -- the categories and their "
            f"real skill ids, the platform commission if you intend to suggest a "
            f"price, the existing project if there is one to build on.\n\n"
            # Stated here as well as in the system prompt because the objective is
            # the last thing the model reads before it decides what to do, and
            # because a tool that is never named in the request is a tool that is
            # never called. The symptom of leaving this out is not an error: the
            # draft simply comes back with no price and a note saying there was no
            # budget, which reads as a considered decision rather than a missing
            # capability.
            f"If the client gave no budget, call `get_price_bands` and price from "
            f"the bands it returns rather than leaving the price unset. Pick the "
            f"band whose deliverable and level match this brief, then set the "
            f"price inside that range -- and say in `notes` which band you used "
            f"and why. If nothing in the catalogue fits, say so plainly instead of "
            f"inventing a band.\n\n"
            f"When you are done, reply with ONE JSON object and nothing else:\n"
            f"{DRAFT_SCHEMA_HINT}"
        )

        async def ask(messages: list[dict], tools: list[dict]) -> ToolCallResponse:
            return await call_llm_with_tools(
                messages, tools, agent=self.name, workflow_run_id=workflow_run_id
            )

        async def execute(name: str, arguments: dict, run_id: int) -> str:
            result = await toolbox.call(name, **arguments)

            # Remembered so grounding can use whatever the model actually looked
            # at, instead of a second blind fetch.
            #
            # The rows are captured *before* compaction, because grounding needs
            # the ids and the category each belongs to and the compact form is
            # for the model's eyes only. Compacted output is what stops the
            # 199-skill Development list from being cut at 4,000 characters --
            # see `compact_reference_rows`.
            if name in ("list_categories", "list_skills_by_category"):
                rows = result if isinstance(result, list) else []
                if name == "list_categories" and rows:
                    self._categories_seen = rows
                elif rows:
                    self._skills_seen.extend(rows)
                return compact_reference_rows(rows)

            return result

        try:
            return await run_tool_loop(
                ask=ask,
                system=DRAFTER_SYSTEM,
                objective=objective,
                tools=DRAFTER_TOOL_SCHEMAS,
                execute=execute,
                workflow_run_id=workflow_run_id,
                max_steps=6,
            )
        except (ToolLoopError, LLMUnavailableError) as exc:
            log.info("draft tool loop unavailable: %s", exc)
            return None

    # ── grounding ─────────────────────────────────────────────────────────

    def _ground(
        self,
        draft: DraftOutput,
        categories: list[dict],
        category_ids: set[int],
        category_names: dict[str, dict],
    ) -> DraftOutput:
        """Drop anything the model invented.

        This is the agent's most important function. A model asked for an id will
        produce a plausible number, and a plausible number that is not a row is
        worse than no number at all -- it looks right.
        """
        draft = self._ground_category(draft, categories, category_ids, category_names)
        draft = self._ground_skills(draft, categories)
        return draft

    def _ground_category(
        self,
        draft: DraftOutput,
        categories: list[dict],
        category_ids: set[int],
        category_names: dict[str, dict],
    ) -> DraftOutput:
        """Resolve `category_id`, preferring the client's own hint.

        Split out from skill grounding because the skills have to be grounded
        against the category this returns. Doing both in one pass, in either
        order, means grounding skills against a category that has not been
        resolved yet -- which is how a warehouse skill validates on a website
        brief.
        """
        notes: list[str] = list(draft.notes)

        # Category. A hint from the client is preferred over the model's choice,
        # because the client knows something the model does not.
        resolved_category: int | None = None

        if draft.category_id in category_ids:
            resolved_category = draft.category_id
        else:
            if draft.category_id is not None:
                notes.append(
                    f"Category id {draft.category_id} is not a real category and was ignored."
                )
                # The hint is cleared *only* when an id was rejected. Clearing it
                # when the model proposed no id at all discarded a correct name
                # and made the name fallback below unreachable for the one case
                # it exists to serve.
                draft.category_name = None
            draft.category_id = None

        # Try the client's own hint by name before giving up.
        if resolved_category is None:
            name = (draft.category_name or "").strip().lower()
            match = category_names.get(name)
            if match:
                resolved_category = match["id"]
                notes.append(f"Category resolved to {match['name']!r} by name.")
            else:
                notes.append(
                    "No category could be matched. Pick one before posting -- the "
                    "wrong category is worse than none, because it decides who sees "
                    "the project."
                )

        draft.category_id = resolved_category
        if resolved_category is not None:
            # The catalogue's own spelling wins, because this is the string the
            # client reads next to a dropdown and the two can differ in case.
            draft.category_name = next(
                (str(c.get("name")) for c in categories if c.get("id") == resolved_category),
                draft.category_name,
            )

        # Written back here, not by the caller. Returning the list and letting the
        # caller assign it lost these notes entirely, because the next phase reads
        # `draft.notes` -- so a dropped category was never explained to anyone.
        draft.notes = notes
        return draft

    def _ground_skills(
        self,
        draft: DraftOutput,
        categories: list[dict],
    ) -> DraftOutput:
        """Resolve `skill_ids`/`skill_names` against the catalogue.

        Called only after the category is resolved, and given rows scoped to it.
        """
        notes: list[str] = list(draft.notes)

        # Skills.
        #
        # The catalogue is *nested*: each category carries its own `skills` array
        # of `{id, name}`. This used to match skill names against the **category**
        # list and validate against **category** ids -- so a real skill id could
        # never survive, and every grounded skill was reported as dropped.
        #
        # It was invisible because the wire format the model saw mostly carried
        # skill *names*, and a name matching no category happened to fall through
        # to the same "drop it" branch, so the outcome looked right for the wrong
        # reason. It surfaced the moment the model could call
        # `list_skills_by_category` and return ids it had genuinely looked up: a
        # live draft came back with notes saying "Skill id 11 is not a real row and
        # was dropped", for ids 11, 12 and 13, which were real.
        #
        # Flattened here, and the nested shape is what the endpoint actually
        # returns. A flat list of skill rows is accepted too, so a change to the
        # endpoint shape degrades to "no skills grounded" rather than an exception.
        skill_pool: dict[int, str] = {}

        # Scoped to the resolved category when there is one.
        #
        # Without this, any real skill anywhere in the catalogue validates -- so a
        # warehouse tool is an acceptable answer to "what skills does a responsive
        # website need", which is not a judgement anyone wants the grounding pass
        # making. A skill that is real but belongs to Data & Analytics does not
        # belong on a Development brief, and the check is the only place that can
        # say so.
        scoped_rows = [
            c for c in categories
            if draft.category_id is None
            or _row_category(c) == draft.category_id
        ]

        for category in scoped_rows:
            if not isinstance(category, dict):
                continue
            nested = category.get("skills")
            if isinstance(nested, list):
                for skill in nested:
                    if isinstance(skill, dict) and "id" in skill:
                        try:
                            skill_pool[int(skill["id"])] = str(skill.get("name", ""))
                        except (TypeError, ValueError):
                            continue
            elif "id" in category and (
                "category_id" in category or "categoryId" in category
            ):
                # Already a flat skill row.
                #
                # `category_id` is the discriminator, and it is load-bearing. A
                # category row and a flat skill row are otherwise identical --
                # both carry `id` and `name` -- so keying this branch on `id`
                # alone promoted every CATEGORY into the skill pool.
                #
                # That is how a live draft returned `skills: ["Development",
                # "Design"]`: the agent had been sent 7 categories and no skills
                # (the .NET side projected `{Id, Name}` only), so it named the only
                # two labels it had, and grounding compared those labels against
                # the category list, matched them, and reported success. Every id
                # was real, every id was a category, and the check passed because
                # it was validating categories against categories.
                try:
                    skill_pool[int(category["id"])] = str(category.get("name", ""))
                except (TypeError, ValueError):
                    continue
            elif "id" in category:
                # A category row with no nested skills. Not promoted to a skill --
                # that is how "Development" and "Design" once came back as a
                # draft's skills.
                #
                # Nothing is recorded here. This used to set a flag that chose
                # between "the catalogue arrived without its skills" and "the
                # model named skills that are not in it", and the flag is gone
                # because that distinction is no longer answerable from the row
                # alone: the server sends categories unexpanded by design, so
                # every category looks like this and the flag was always true.
                # `not skill_pool` below is the real signal; what went wrong is
                # decided from the model's own answer instead.
                continue

        if not skill_pool:
            # Only one explanation, and it has to be the true one.
            #
            # These two branches used to be "the catalogue had no skills" versus
            # "the names were wrong", chosen by whether any unnested row was seen.
            # That is no longer a usable signal: the server sends categories
            # without skills by design, so *every* category row is unnested and
            # the first branch always won. A draft whose skills were dropped
            # because the model looked them up and then had them discarded was
            # reported as "the catalogue arrived without its skills" -- which
            # blamed the platform for the model's mistake and told the client to
            # do something that would not help.
            if draft.skill_ids or draft.skill_names:
                notes.append(
                    "None of the suggested skills are in this category's catalogue, "
                    "so none were attached. Pick skills from the list before posting."
                )
            else:
                notes.append(
                    "No skills could be matched. Pick skills from the list before "
                    "posting."
                )

        good_ids: list[int] = []
        good_names: list[str] = []
        seen: set[int] = set()

        def keep(skill_id: int, label: str) -> None:
            if skill_id in seen:
                return
            seen.add(skill_id)
            good_ids.append(skill_id)
            good_names.append(label)

        # Walk the pairs positionally, so a model that returns both names and ids
        # is read as two halves of one statement rather than two candidates.
        #
        # A name and an id that DISAGREE are a contradiction, and the name wins
        # by being the thing the model actually meant. See the drop branch below
        # for why this is not negotiable.
        pairs = max(len(draft.skill_names), len(draft.skill_ids))
        for index in range(pairs):
            skill_name = draft.skill_names[index] if index < len(draft.skill_names) else None
            candidate = draft.skill_ids[index] if index < len(draft.skill_ids) else None
            key = str(skill_name or "").strip().lower()

            if key:
                match = next(
                    (sid for sid, label in skill_pool.items()
                     if label.strip().lower() == key),
                    None,
                )
                if match is not None:
                    keep(match, skill_pool[match])
                    continue

                # The model named a skill the catalogue does not have.
                #
                # The old code fell through to the id here, and that is how a live
                # draft asked for a "responsive website" and was handed ClickHouse,
                # Snowflake and BigQuery. The model returned
                # `skill_names = ["Frontend Development", "UI/UX Design",
                # "Web Performance"]` with `skill_ids = [101, 102, 103]`, wrote a
                # note confessing they were placeholders, and grounding kept all
                # three -- because 101/102/103 *are* real rows. `keep()` then
                # relabelled them from the pool, so the client's draft said
                # "ClickHouse" while the model had asked for "Frontend
                # Development" and the note said so in as many words.
                #
                # The check passed because it only ever asked "is this id real?"
                # and never "is this id the thing the model named?". A name that
                # does not resolve is the evidence that it is not, so the pair is
                # dropped rather than silently reinterpreted.
                notes.append(
                    f"Skill {skill_name!r} is not in the catalogue and was dropped. "
                    "Pick skills from the list rather than inventing them."
                )
                continue

            # Ids with NO name at all. This is the legitimate use of the id path: a
            # model that looked the catalogue up and returned ids only is the
            # expected shape once it can call the tools itself, and there is no
            # stated intent for the id to contradict.
            if isinstance(candidate, int) and candidate in skill_pool:
                keep(candidate, skill_pool[candidate])
                continue

            if key:
                # Unreachable: every `key` path above ends in `continue`. Kept as
                # a guard rather than deleted, so a future edit that removes one
                # of those `continue`s cannot silently drop a skill with no
                # explanation at all.
                notes.append(f"Skill {skill_name!r} is not in the catalogue and was dropped.")
            elif isinstance(candidate, int):
                # "not a real row" was a claim this code could not support.
                #
                # It fires whenever an id is absent from the pool -- including
                # when the pool is empty because the lookup never reached us. A
                # live draft said "Skill id 1 is not a real row and was dropped"
                # for id 1, which is C#, a real Development row the model had
                # just looked up and that we then discarded. The note named a
                # database fact it had not checked and sent the reader looking
                # for a seed-data problem that did not exist.
                if skill_pool:
                    notes.append(
                        f"Skill id {candidate} is not one of this category's "
                        "skills and was dropped."
                    )
                # With an empty pool there is nothing to say about the id, so say
                # nothing. The "no skills could be matched" note above is the
                # truthful explanation and it has already been added.

        good_ids, good_names, pad_notes = _top_up_to_minimum(
            draft, skill_pool, good_ids, good_names
        )
        notes.extend(pad_notes)

        draft.skill_ids = good_ids
        draft.skill_names = good_names
        draft.notes = notes
        return draft

    # ── validation ────────────────────────────────────────────────────────

    def _validate(self, draft: DraftOutput, payload: DraftInput) -> None:
        """Check the bounds a model will get wrong, and correct what is safe.

        Clamping beats rejecting: a delivery time of 400 days is obviously a
        slip, and the client can fix it in the form. Failing the whole draft over
        it would be pedantry.
        """
        if len(draft.title.strip()) < MIN_TITLE_LENGTH:
            if not payload.title_hint:
                raise AgentError(
                    f"Draft title is shorter than {MIN_TITLE_LENGTH} characters and "
                    "no title hint was supplied to fall back on.",
                    agent=self.name,
                )
            draft.title = payload.title_hint.strip()[:200]
            draft.notes.append("Title was too short to post; used the client's hint instead.")

        if len(draft.description.strip()) < MIN_DESCRIPTION_LENGTH:
            # The description is the thing freelancers read first. Padding it
            # would post a hollow project, so this is left for the client to fix
            # and reported.
            draft.notes.append(
                f"Description is under {MIN_DESCRIPTION_LENGTH} characters. "
                "Freelancers screen on this, so it is worth a sentence more."
            )

        if draft.delivery_days < MIN_DELIVERY_DAYS or draft.delivery_days > MAX_DELIVERY_DAYS:
            clamped = max(MIN_DELIVERY_DAYS, min(MAX_DELIVERY_DAYS, draft.delivery_days))
            draft.notes.append(
                f"Delivery time of {draft.delivery_days} days was outside "
                f"{MIN_DELIVERY_DAYS}-{MAX_DELIVERY_DAYS}; using {clamped}."
            )
            draft.delivery_days = clamped

        if draft.pricing_type == "fixed":
            if draft.fixed_price_usd is None or draft.fixed_price_usd <= 0:
                budget = payload.budget_hint_usd
                if budget is not None and budget > 0:
                    draft.fixed_price_usd = budget
                    draft.notes.append("No usable price; used the budget the client gave.")
                else:
                    draft.notes.append(
                        "No price could be determined. Set one before posting."
                    )
                    # Cleared, not just reported. A negative price left in place
                    # reaches a form whose schema is `z.number().positive()`, so
                    # the draft becomes un-postable while the field still *looks*
                    # filled -- and the client is left with a number they did not
                    # choose that they must notice and delete themselves.
                    draft.fixed_price_usd = None
            # A range price on a fixed-price project is a category error, not a
            # default the client would notice.
            draft.min_budget_usd = None
            draft.max_budget_usd = None
        else:
            if draft.min_budget_usd is None or draft.min_budget_usd <= 0:
                draft.notes.append("Range pricing needs a minimum budget.")
                draft.min_budget_usd = None

            if (
                draft.max_budget_usd is not None
                and draft.min_budget_usd is not None
                and draft.max_budget_usd <= draft.min_budget_usd
            ):
                # The maximum is cleared rather than set equal to the minimum.
                #
                # It used to be set to the minimum, which is *also* rejected: the
                # form's schema is `min_budget < max_budget`, so equal values
                # fail exactly as an inverted pair does. A "correction" that
                # produces a still-invalid value is worse than none -- it looks
                # handled.
                #
                # The minimum is kept because it is the number the model was more
                # likely to get right, and one filled field with a clear note is
                # more useful than two invented ones.
                draft.notes.append(
                    f"Maximum budget {draft.max_budget_usd} is not above the minimum "
                    f"{draft.min_budget_usd}, so the maximum was cleared. Set it above "
                    "the minimum before posting."
                )
                draft.max_budget_usd = None
            draft.fixed_price_usd = None

        # Confidence is clamped rather than trusted: a model reporting 1.4 is
        # miscalibrated, and that number is shown to the client as reassurance.
        draft.confidence = max(0.0, min(1.0, draft.confidence))

    # ── prompt ────────────────────────────────────────────────────────────

    def _compose_prompt(
        self,
        payload: DraftInput,
        categories: list[dict],
        commission: dict,
    ) -> str:
        template = self.read_prompt("draft")

        category_lines = "\n".join(
            f"  {c['id']}: {c.get('name', '')}" for c in categories[:60]
        ) or "  (no categories are configured)"

        commission_line = ""
        if commission:
            rate = commission.get("commissionPercentage")
            if rate is not None:
                commission_line = (
                    f"\nThe platform takes {rate}% commission. For fixed-price work the "
                    "client pays your price plus that; price accordingly, and do not "
                    "quote a total."
                )

        return (
            f"{template}\n\n"
            f"## Available categories and skills (use these ids and names exactly)\n"
            f"{category_lines}{commission_line}\n\n"
            f"## Client's notes\n{payload.partial_text}\n"
        )


import logging  # noqa: E402  (after class body, used inside run())

log = logging.getLogger(__name__)
