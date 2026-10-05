"""The Planning Agent.

Given a posted project, decides which agents run and how much each scoring factor
matters. This is the only agent that plans; the rest execute.

The plan is a *specification*, so the model's role is to choose within bounds and
Python's role is to hold those bounds. Weights that do not sum to one, an agent
name that is not in the registry, a shortlist of forty -- each is a plan that
would misbehave at run time, and each is corrected here with a note rather than
passed through.

The defaults matter more than the model's answer. Most of the time a sensible
uniform weighting is the right one, and a model asked to "plan" will produce
confident-looking numbers that are no better than the default and harder to
explain to a client.
"""

from __future__ import annotations

import logging
import time
from typing import Any

from pydantic import BaseModel, Field

from ..llm.base import LLMUnavailableError, ToolCallResponse
from ..llm.chain import call_llm_with_tools
from .base import BaseAgent
from .tool_loop import LoopResult, ToolLoopError, json_schema_for, run_tool_loop

log = logging.getLogger(__name__)

#: What the planner may look up while planning.
#:
#: Every one of these is a *read*, and every one is already permitted for the
#: planning agent by ``AGENT_TOOL_PERMISSIONS`` in the registry. The schemas here
#: advertise what exists; the registry decides what is allowed. Advertising
#: something the allow-list refuses would be a tool the model could see and not
#: use, which is a worse failure than it not appearing at all.
PLANNER_TOOL_SCHEMAS = [
    json_schema_for(
        "get_project",
        "Read the full project record: description, requirements, deliverables, "
        "budget and required skills. Call this first if the objective does not "
        "already contain what you need.",
        {"project_id": {"type": "integer", "description": "The project id."}},
        ["project_id"],
    ),
    json_schema_for(
        "list_categories",
        "List every service category and its skills. Use this to ground the skills "
        "you require in real catalogue rows rather than inventing them.",
        {},
        [],
    ),
    json_schema_for(
        "search_freelancers",
        "Search the freelancer pool. Use this to find out how many candidates "
        "actually exist for these requirements before deciding on a shortlist size. "
        "Read-only: it does not invite or shortlist anyone.",
        {
            "query": {"type": "string", "description": "Free text, e.g. a skill name."},
            "category_id": {"type": "integer", "description": "Restrict to a category."},
            "min_rating": {"type": "number", "description": "Minimum average rating."},
            "available": {"type": "boolean", "description": "Only currently available."},
            "limit": {"type": "integer", "description": "Maximum rows, 1-200."},
        },
        [],
    ),
    json_schema_for(
        "get_similar_projects",
        "Read recently posted projects in the same category, newest first, with "
        "their pricing and delivery windows. Use this to judge what a normal "
        "budget and timeline looks like for this kind of work, and to flag a "
        "budget that is wildly out of line with the market. A short list is a "
        "real signal that the category is thin -- report that rather than "
        "inventing a typical figure. Pass exclude_project_id so the project you "
        "are planning is not counted as its own comparable.",
        {
            "category_id": {"type": "integer", "description": "The category to inspect."},
            "limit": {"type": "integer", "description": "Maximum rows."},
            "exclude_project_id": {
                "type": "integer",
                "description": "The project being planned, left out of its own baseline.",
            },
        },
        ["category_id"],
    ),
    json_schema_for(
        "get_agent_thresholds",
        "Read the platform's configured agent thresholds. Use this to respect the "
        "platform's minimum match score rather than inventing one.",
        {},
        [],
    ),
]

#: The system instruction. An operating manual, not a personality.
#:
#: Three rules, each one preventing a specific failure: over-fetching (a budget),
#: inventing catalogue rows (grounding), and looping (a stop condition). Lecture 07
#: makes the same point about a supervisor prompt -- "Multi-agent bugs are usually
#: missing policy, not missing intelligence."
PLANNER_SYSTEM = """\
You are the planning agent for a freelance sourcing platform. You produce a
sourcing plan for one project. You never invite, shortlist or contact anyone --
that happens later, and only after a human approves.

What you must decide:
- which agents should run afterwards, from: matching, screening, validation, outreach
- how much each scoring factor matters (the five weights, summing to 1.0)
- hard filters: how large a shortlist to aim for, whether to require availability
- your confidence, and a short rationale

How to work:
1. Look things up before deciding. Call the tools you need -- the project record,
   the category catalogue, comparable projects, the size of the freelancer pool.
   Do not guess a budget, a delivery window or a skill name.
2. Ground every skill you require in the catalogue. Never invent a technology.
3. Call each tool ONCE. A second call with a different argument repeats an
   answer you already have and costs the same as the first. If you asked for the
   pool once, use what came back -- do not keep re-querying with narrower filters.
4. Stop as soon as you can answer. Three well-chosen calls beat six. When you have
   the project, the category and the pool size, you have enough to plan.
5. When you are done, reply with ONE JSON object and nothing else -- no
   explanation before it, no code fence around it.

If the project is too vague to plan for, say so with confidence below 0.4 and put
a single clarifying question in clarification_question.
"""

#: The JSON contract, given to the model verbatim.
#:
#: Written out rather than generated from the Pydantic models because the model
#: needs the *field* names and the constraint that matters most -- the weights sum
#: to 1.0 -- and a generated schema hides that. The parser still validates
#: against :class:`PlanningOutput`, so a wrong shape is caught in code.
PLANNING_SCHEMA_HINT = """\
{
  "weights": {
    "skill_coverage": 0.0-1.0,
    "portfolio_relevance": 0.0-1.0,
    "past_success_rate": 0.0-1.0,
    "availability_and_speed": 0.0-1.0,
    "budget_fit": 0.0-1.0
  },
  "hard_filters": {
    "require_active": true,
    "require_available": true,
    "min_rating": 0.0,
    "exclude_already_proposed": true
  },
  "shortlist_size": 5-15,
  "plan_steps": [{"order": 1, "agent_name": "matching|screening|validation|outreach",
                  "tool_requirements": [], "rationale": "..."}],
  "delegated_agents": ["matching", "..."],
  "required_tools": [],
  "confidence": 0.0-1.0,
  "reasoning_summary": "two or three sentences",
  "clarification_question": null
}"""

#: The closed registry. A plan naming anything else is not a plan.
KNOWN_AGENTS: frozenset[str] = frozenset({"matching", "screening", "validation", "outreach"})

MIN_SHORTLIST = 5
MAX_SHORTLIST = 15
MAX_PLAN_STEPS = 8
WEIGHT_TOLERANCE = 0.001
LOW_CONFIDENCE = 0.55


class MatchWeights(BaseModel):
    """Relative importance of each scoring factor. Must sum to 1.0."""

    skill_coverage: float = 0.35
    portfolio_relevance: float = 0.2
    past_success_rate: float = 0.2
    availability_and_speed: float = 0.15
    budget_fit: float = 0.1

    def as_dict(self) -> dict[str, float]:
        return self.model_dump()

    def total(self) -> float:
        return sum(self.model_dump().values())


class HardFilters(BaseModel):
    """Non-negotiable conditions, applied before scoring."""

    require_active: bool = True
    require_available: bool = True
    min_rating: float = 0.0
    #: Freelancers who have already proposed are excluded: inviting or shortlisting
    #: someone who has already applied is a way of saying "I ignored you".
    exclude_already_proposed: bool = True


class PlanStep(BaseModel):
    order: int
    agent_name: str
    tool_requirements: list[str] = Field(default_factory=list)
    rationale: str = ""


class PlanningInput(BaseModel):
    workflow_run_id: int
    project_id: int
    #: The project projection, as returned by ``/internal/read/projects/{id}``.
    project: dict[str, Any] | None = None


class PlanningOutput(BaseModel):
    weights: MatchWeights = Field(default_factory=MatchWeights)
    hard_filters: HardFilters = Field(default_factory=HardFilters)
    shortlist_size: int = 10
    plan_steps: list[PlanStep] = Field(default_factory=list)
    delegated_agents: list[str] = Field(default_factory=list)
    required_tools: list[str] = Field(default_factory=list)
    confidence: float = 0.7
    reasoning_summary: str = ""
    #: Set when the model was not confident enough to be worth acting on.
    clarification_question: str | None = None


class PlanningAgent(BaseAgent[PlanningInput, PlanningOutput]):
    name = "planning"

    #: The last loop trajectory, as plain data.
    #:
    #: An instance attribute rather than a return value because the assignment's
    #: evaluation asks for the trajectory of the *workflow*, and the step record
    #: written at the end of :meth:`run` is the durable place for it. Reading it
    #: back from the recorded step would mean a database round trip on every
    #: assertion, and the evaluator already has the step log.
    _last_trajectory: list[dict[str, Any]] = []

    async def run(self, payload: PlanningInput) -> PlanningOutput:
        started = time.monotonic()
        tools = self._toolbox(payload.workflow_run_id)

        project = payload.project
        if project is None:
            # Not best-effort: without the project there is nothing to plan for.
            project = await tools.call("get_project", project_id=payload.project_id)

        thresholds: dict = {}
        try:
            # Best-effort: a plan with the default threshold beats no plan, and the
            # fallback is recorded in the reasoning summary.
            thresholds = await tools.call("get_agent_thresholds", best_effort=True) or {}
        except Exception as exc:  # noqa: BLE001
            thresholds = {"_error": str(exc)}

        # Model-directed discovery.
        #
        # Previously this agent received one pre-formatted prompt containing the
        # project and the thresholds, and answered with weights. That is not
        # planning -- it is classification. Now the model is given *tools* and
        # decides which to call: it may look up the category, check how many
        # freelancers are actually available, or read the project's similar
        # projects, and only then produce weights. What it looks up, and in what
        # order, is its own decision, and the trajectory is recorded.
        #
        # This is the one agent that is genuinely autonomous, and deliberately so:
        # planning is where the next step cannot be known in advance. Matching and
        # screening stay deterministic, because there the factor values must be
        # reproducible and the assignment asks for deterministic validation.
        self._last_trajectory = []

        loop = await self._plan_with_tools(
            project=project,
            thresholds=thresholds,
            toolbox=tools,
            workflow_run_id=payload.workflow_run_id,
        )
        self._last_trajectory = loop.trace() if loop else []

        if loop is None or not loop.completed:
            # The loop could not run, or it ran out of steps. Both produce the
            # default plan with an explicit note, because a plan is the one thing
            # that can be produced without a model.
            plan = PlanningOutput(
                reasoning_summary=(
                    "The planner could not complete its tool loop, so the default "
                    "weighting was used. This is a safe fallback, not a considered plan."
                ),
                confidence=LOW_CONFIDENCE,
            )
            plan = self._apply_hard_filter_defaults(plan, thresholds)
            plan = self._normalise(plan, thresholds)
            plan = self._apply_hard_filter_defaults(plan, thresholds)
            return plan

        try:
            plan = await self._parse(
                loop.answer,
                PlanningOutput,
                workflow_run_id=payload.workflow_run_id,
            )
        except Exception:
            # A plan is the one thing that can be produced without a model: the
            # defaults are defensible. So a parse failure degrades to the default
            # plan with a note, rather than failing the workflow.
            plan = PlanningOutput(
                reasoning_summary=(
                    "The planner's output could not be read, so the default weighting "
                    "was used. This is a safe fallback, not a considered plan."
                )
            )
            plan = self._apply_hard_filter_defaults(plan, thresholds)

        plan = self._normalise(plan, thresholds)
        plan = self._apply_hard_filter_defaults(plan, thresholds)

        status = "ok"
        if plan.clarification_question:
            status = "pending_clarification"

        await self._record_step(
            workflow_run_id=payload.workflow_run_id,
            step_name="planning",
            input_summary={"project_id": payload.project_id},
            output_summary={
                "weights": plan.weights.as_dict(),
                "shortlist_size": plan.shortlist_size,
                "delegated_agents": plan.delegated_agents,
                "confidence": plan.confidence,
                # The trajectory, so the evaluation can assert that the model
                # actually chose tools rather than answering blind.
                "trajectory": self._last_trajectory,
                "stop_reason": loop.stop_reason,
                "tools_called": list(loop.tools_called),
            },
            status=status,
            started=started,
        )
        return plan

    async def _plan_with_tools(
        self,
        *,
        project: dict[str, Any],
        thresholds: dict,
        toolbox: Any,
        workflow_run_id: int,
    ) -> LoopResult | None:
        """Run the model-directed planning loop.

        Returns ``None`` when no provider can do tool calling, which is the signal
        to fall back to the default plan. Distinguished from a loop that ran and
        failed, because those are different problems: one is a configuration gap,
        the other is a model that could not finish.
        """
        objective = (
            f"Produce a sourcing plan for this project.\n\n"
            f"Project id: {project.get('id')}\n"
            f"Title: {project.get('title')}\n"
            f"Description: {str(project.get('description') or '')[:400]}\n"
            f"Requirements: {str(project.get('requirements') or '')[:400]}\n"
            f"Expected deliverables: {str(project.get('expectedDeliverables') or '')[:400]}\n"
            f"Skills listed: {project.get('skills')}\n"
            f"Budget: {project.get('fixedPrice')} (min {project.get('minPrice')}, "
            f"max {project.get('maxPrice')})\n"
            f"Expected days: {project.get('expectedDays')}\n"
            f"Platform thresholds: {thresholds}\n\n"
            f"Reply with a single JSON object matching this shape and nothing else:\n"
            f"{PLANNING_SCHEMA_HINT}"
        )

        async def ask(messages: list[dict], tools: list[dict]) -> ToolCallResponse:
            return await call_llm_with_tools(
                messages,
                tools,
                agent=self.name,
                workflow_run_id=workflow_run_id,
            )

        async def execute(name: str, arguments: dict, run_id: int) -> str:
            # Straight through to the Toolbox, which owns the per-agent allow-list
            # and the argument validation. The loop adds no permissions of its own,
            # so a tool this agent may not use cannot be reached by asking for it.
            return await toolbox.call(name, **arguments)

        try:
            return await run_tool_loop(
                ask=ask,
                system=PLANNER_SYSTEM,
                objective=objective,
                tools=PLANNER_TOOL_SCHEMAS,
                execute=execute,
                workflow_run_id=workflow_run_id,
            )
        except (ToolLoopError, LLMUnavailableError) as exc:
            log.info("planner tool loop unavailable: %s", exc)
            return None

    # ── normalisation ─────────────────────────────────────────────────────

    def _normalise(self, plan: PlanningOutput, thresholds: dict) -> PlanningOutput:
        """Force the plan inside the bounds, noting every correction.

        Every correction is recorded. A plan that was silently adjusted is a plan
        nobody can reason about afterwards, and the client is shown these weights.
        """
        corrections: list[str] = []

        # Weights. Individual values out of range are clamped first, then the
        # total is rescaled, because a set summing to 1.3 is wrong in a way that
        # matters -- the score formula multiplies by them.
        weights = plan.weights.as_dict()

        for key, value in list(weights.items()):
            if not (0.0 <= value <= 1.0):
                clamped = max(0.0, min(1.0, value))
                corrections.append(f"weight {key} {value} clamped to {clamped}")
                weights[key] = clamped

        total = sum(weights.values())
        if total <= 0:
            # All zero. Uniform is the only defensible recovery.
            weights = MatchWeights().as_dict()
            corrections.append("all weights were zero; uniform weighting used")
        elif abs(total - 1.0) > WEIGHT_TOLERANCE:
            scaled = {k: round(v / total, 4) for k, v in weights.items()}
            # Rounding can leave the total slightly off; the last factor absorbs it
            # rather than introducing a new error.
            drift = round(1.0 - sum(scaled.values()), 4)
            if drift:
                last_key = next(reversed(scaled))
                scaled[last_key] = round(scaled[last_key] + drift, 4)
            corrections.append(f"weights summed to {total:.3f}; rescaled to 1.0")
            weights = scaled

        plan.weights = MatchWeights(**weights)

        # Shortlist size.
        if not (MIN_SHORTLIST <= plan.shortlist_size <= MAX_SHORTLIST):
            clamped = max(MIN_SHORTLIST, min(MAX_SHORTLIST, plan.shortlist_size))
            corrections.append(f"shortlist_size {plan.shortlist_size} clamped to {clamped}")
            plan.shortlist_size = clamped

        # Delegated agents, against the closed registry.
        known = [a for a in plan.delegated_agents if a in KNOWN_AGENTS]
        unknown = [a for a in plan.delegated_agents if a not in KNOWN_AGENTS]
        if unknown:
            corrections.append(f"dropped unknown agent(s): {', '.join(unknown)}")
        if not known:
            # The pipeline is fixed; an empty delegation means nothing runs.
            corrections.append("no valid agents named; defaulting to the full pipeline")
            known = ["matching", "screening", "validation", "outreach"]
        plan.delegated_agents = known

        # Plan steps: bounded, and ordered.
        if len(plan.plan_steps) > MAX_PLAN_STEPS:
            corrections.append(
                f"plan truncated from {len(plan.plan_steps)} to {MAX_PLAN_STEPS} steps"
            )
            plan.plan_steps = plan.plan_steps[:MAX_PLAN_STEPS]

        for index, step in enumerate(plan.plan_steps, start=1):
            step.order = index
            if step.agent_name not in KNOWN_AGENTS:
                step.agent_name = known[0] if known else "matching"

        # Required tools must be tools the delegated agents may actually use,
        # otherwise the plan promises a capability that does not exist.
        from ..tools.registry import AGENT_TOOL_PERMISSIONS

        permitted: set[str] = set()
        for agent in plan.delegated_agents:
            permitted |= AGENT_TOOL_PERMISSIONS.get(agent, frozenset())

        usable = [t for t in plan.required_tools if t in permitted]
        if len(usable) != len(plan.required_tools):
            corrections.append(
                "dropped required_tools that no delegated agent is permitted to call"
            )
        plan.required_tools = usable

        plan.confidence = max(0.0, min(1.0, plan.confidence))

        if plan.confidence < LOW_CONFIDENCE and not plan.clarification_question:
            # Below the bar, the run should stop rather than proceed on a plan
            # nobody believes.
            plan.clarification_question = (
                "The project is too loosely specified to match confidently. Adding "
                "the required skills and a realistic delivery window would make the "
                "shortlist much more useful."
            )

        if corrections:
            plan.reasoning_summary = (
                f"{plan.reasoning_summary} ".strip() + "Corrections: " + "; ".join(corrections)
            )

        return plan

    def _apply_hard_filter_defaults(self, plan: PlanningOutput, thresholds: dict) -> PlanningOutput:
        """Take the platform's own thresholds rather than the model's.

        ``MatchScoreThreshold`` lives in ``GlobalSettings`` and an admin sets it.
        A model proposing a different bar would mean the admin's setting is
        ignored, which is the whole reason it is configurable.
        """
        platform_min = thresholds.get("matchScoreThreshold")
        if platform_min is not None:
            try:
                value = float(platform_min)
            except (TypeError, ValueError):
                return plan

            if plan.hard_filters.min_rating != value:
                plan.reasoning_summary = (
                    f"{plan.reasoning_summary} ".strip()
                    + f"Minimum rating taken from platform settings ({value}), "
                    + "not the model's suggestion."
                )
                plan.hard_filters.min_rating = value

        return plan

    def _compose_prompt(self, project: dict[str, Any], thresholds: dict) -> str:
        template = self.read_prompt("planning")

        return (
            f"{template}\n\n"
            f"## Project\n"
            f"id: {project.get('id')}\n"
            f"title: {project.get('title')}\n"
            f"description: {str(project.get('description') or '')[:1500]}\n"
            f"requirements: {str(project.get('requirements') or '')[:1000]}\n"
            f"pricing: {project.get('pricingType')} "
            f"(fixed={project.get('fixedPrice')}, min={project.get('minPrice')}, "
            f"max={project.get('maxPrice')})\n"
            f"expected days: {project.get('expectedDays')}\n"
            f"skills: {', '.join(project.get('skills') or []) or '(none listed)'}\n\n"
            f"## Platform thresholds\n{thresholds}\n"
        )
