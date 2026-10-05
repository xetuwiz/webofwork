"""The catalogue has to be reachable from the agent, in one spelling.

## The failure this file exists for

`list_skills_by_category` returned `[]` for every category, and category grounding
rejected the model's own correct answer with "Category id 1 is not a real
category" -- on the category the request had just listed. Two separate causes,
both silent, both total:

1. The categories ride in on the request body, so they never cross `DotNetClient`
   and were never normalised. They arrived PascalCase (`{"Id": 1, "Name": ...}`)
   while every reader looked for `"id"`.
2. The tool never asked the platform for anything. It filtered the injected
   category list and returned a *category row* -- an answer about categories to a
   question about skills -- and once the caller stopped nesting skills into that
   list (nesting all 654 overflowed the provider's request limit), there was
   nothing left to find.

The symptom in a live run was a model calling `list_skills_by_category` seven
times, once per category, looking for skills that were never going to arrive.

Neither cause raises. Both return a well-formed empty answer, which is the
hardest kind of bug to notice and the easiest to ship.
"""

from __future__ import annotations

import pytest

from ai.agents.draft_agent import DraftAgent
from ai.clients.dotnet_client import DotNetClient
from ai.clients.normalize import normalise, to_camel
from ai.config import Settings
from ai.graph.dispatcher import StageDispatcher
from ai.tools.registry import ToolBox


@pytest.fixture
def agent() -> DraftAgent:
    # Grounding is pure: it takes only the catalogue, so it needs no client.
    return DraftAgent(client=None)  # type: ignore[arg-type]


class _StubClient(DotNetClient):
    """A real client with the HTTP call replaced. No network, no audit writes."""

    def __init__(self, rows: object = "default") -> None:
        super().__init__(Settings())
        self.reads: list[str] = []
        self.params: list[dict] = []
        self._rows = rows

    async def _get(self, path: str, **params):  # type: ignore[override]
        self.reads.append(path)
        self.params.append(params)
        if self._rows == "default":
            category_id = int(path.rstrip("/skills").rsplit("/", 1)[-1])
            return [
                {"id": 11, "name": "React", "categoryId": category_id, "isCustom": False},
                {"id": 12, "name": "TypeScript", "categoryId": category_id, "isCustom": False},
            ]
        return self._rows


class TestToCamelHandlesBothSpellings:
    """`to_camel` is the whole reason the rest of this file's first fix works."""

    @pytest.mark.parametrize(
        ("raw", "expected"),
        [
            ("min_price_usd", "minPriceUsd"),  # snake_case, the original case for it
            ("Id", "id"),                       # PascalCase, the anonymous projection
            ("Name", "name"),
            ("CategoryId", "categoryId"),
            ("minPriceUsd", "minPriceUsd"),    # already camel: untouched
            ("ID", "id"),                       # a whole-word acronym
            ("IDNumber", "idNumber"),           # acronym then a word
        ],
    )
    def test_both_spellings_reach_one_convention(self, raw, expected):
        assert to_camel(raw) == expected

    def test_a_lowered_pascal_key_does_not_double_up(self):
        """`iDNUMBER` is what a naive `key[0].lower()` produces.

        Not a style point. An acronym-initial key arrives in a `_INT_FIELDS` or
        money-field lookup by name, so a mangled key silently stops being coerced
        and the value arrives as a string -- the same class of bug as the missing
        `minPriceUsd` entry that made money arrive unparsed.
        """
        assert to_camel("IDNumber") == "idNumber"
        assert to_camel("ID") != "iD"


# ── 1. the injected categories must be normalised ─────────────────────────


class TestInjectedCategoriesAreNormalised:
    def test_the_dispatcher_normalises_the_context_categories(self):
        """PascalCase in, camelCase out -- before any agent sees them.

        `normalise` is applied at `DotNetClient`'s HTTP boundary for every other
        .NET payload. These categories come in on the request body instead, so
        nothing normalised them. That asymmetry is the whole bug: two paths into
        the same subsystem, two conventions, and only one of them documented.
        """
        # What ASP.NET serialises `new { c.Id, c.Name }` into.
        raw = [{"Id": 1, "Name": "Development"}]

        assert raw[0].get("id") is None, "precondition: PascalCase really is the raw shape"
        assert normalise(raw)[0]["id"] == 1

    @pytest.mark.asyncio
    async def test_category_ids_are_readable_after_dispatch(self, monkeypatch):
        """The observable consequence, at the layer that consumed it.

        Asserted through `StageDispatcher.run` rather than through `normalise`
        directly, because the unit under test is "does a request reach an agent in
        a usable shape", not "does this function convert keys". The dispatch itself
        is stubbed -- it is not what is under test, and running it would need a
        model and a database.
        """
        dispatcher = StageDispatcher(
            _request_with_categories([{"Id": 1, "Name": "Development"}])
        )
        seen: list[dict] = []

        async def fake_dispatch():
            seen.extend(dispatcher._categories)
            return "ok", "", {}

        monkeypatch.setattr(dispatcher, "_dispatch", fake_dispatch)

        await dispatcher.run()

        assert seen, "the dispatcher passed no categories to the stage"
        assert seen[0]["id"] == 1, (
            "an agent reading this row with `row['id']` found nothing, and "
            f"concluded the category did not exist: {seen}"
        )
        assert seen[0]["name"] == "Development"


def _request_with_categories(categories: list[dict]):
    from ai.graph.dispatcher import AgentChatRequest

    return AgentChatRequest(
        message="Build a Modern Responsive Website",
        execution_mode="recorded_replay",
        workflow_run_id=1,
        stage="draft",
        context={"partial_text": "responsive website", "categories": categories},
    )


# ── 2. the tool must ask the platform ─────────────────────────────────────


class TestListSkillsByCategoryActuallyReads:
    @pytest.mark.asyncio
    async def test_it_reads_the_platform_rather_than_filtering_memory(self):
        client = _StubClient()
        box = ToolBox("draft", 1, client, categories=[])

        rows = await box.call("list_skills_by_category", category_id=1)

        assert client.reads == ["/internal/read/categories/1/skills"], (
            "the tool must go to the platform; filtering the injected category "
            f"list answers a question about categories, not skills ({client.reads})"
        )
        assert [r["name"] for r in rows] == ["React", "TypeScript"]

    @pytest.mark.asyncio
    async def test_an_empty_category_list_no_longer_means_no_skills(self):
        """The specific regression: categories empty, skills still reachable.

        `categories=[]` is what every non-draft agent passes, and it is what the
        draft gets too whenever the caller sends none. Before the fix that made
        the tool return nothing at all, which reads to the model as "this category
        has no skills" -- a claim about the catalogue that was simply false.
        """
        box = ToolBox("draft", 1, _StubClient(), categories=[])

        rows = await box.call("list_skills_by_category", category_id=7)

        assert rows, "an empty injected catalogue must not disable the lookup"

    @pytest.mark.asyncio
    async def test_the_model_can_narrow_a_category_too_large_to_return(self):
        """A category with no way to filter is a category whose tail does not exist.

        Development is 199 skills, about 12KB of JSON, and the tool loop cuts
        results at 4000 characters -- landing mid-list alphabetically. So
        JavaScript, React and TypeScript were never shown to the model, which
        then picked Bootstrap and CSS3 from the head of the alphabet and wrote in
        its notes that HTML and JavaScript "were not attached rather than guessed".

        Being told the list was truncated is not enough on its own. Without a
        search parameter the model has nothing to do with that information except
        repeat the same oversized call.
        """
        client = _StubClient()
        box = ToolBox("draft", 1, client, categories=[])

        await box.call("list_skills_by_category", category_id=1, search="JavaScript")

        assert client.reads == ["/internal/read/categories/1/skills"], (
            f"the search must reach the platform; reads were {client.reads}"
        )
        assert client.params[-1].get("search") == "JavaScript", (
            "the search term was dropped before the request: "
            f"{client.params[-1]}"
        )

    @pytest.mark.asyncio
    async def test_the_search_is_advertised_in_the_schema(self):
        """A parameter the model cannot see is a parameter it will not use."""
        from ai.agents.draft_agent import DRAFTER_TOOL_SCHEMAS

        schema = next(
            s for s in DRAFTER_TOOL_SCHEMAS
            if s["function"]["name"] == "list_skills_by_category"
        )
        props = schema["function"]["parameters"]["properties"]

        assert "search" in props, (
            f"the model has no way to narrow a truncated list: {sorted(props)}"
        )

    @pytest.mark.asyncio
    async def test_a_non_list_answer_is_treated_as_no_rows(self):
        """Never returns None into the loop.

        A `None` here would reach the tool loop as a tool result and be rendered
        to the model as the string "None", which is not a refusal to answer -- it
        is an answer, and a wrong one.
        """
        box = ToolBox("draft", 1, _StubClient(rows=None), categories=[])

        assert await box.call("list_skills_by_category", category_id=1) == []


# ── 3. flat skill rows must survive grounding ────────────────────────────


class TestFlatSkillRowsGround:
    """The shape the platform now returns: flat rows, each carrying its category."""

    def test_a_camel_case_category_id_is_a_valid_discriminator(self, agent):
        """`categoryId`, not `category_id`.

        The boundary contract is camelCase -- `normalize.py` states it in its own
        docstring, and `DECIMAL_FIELDS` is written that way. Grounding checked for
        the snake_case spelling only, so every real skill row fell through to the
        "bare category row" branch and was refused. The skills were fetched,
        correct, and thrown away by a spelling.
        """
        rows = [{"id": 11, "name": "React", "categoryId": 1}]

        draft = agent._ground(_draft(category_id=1, skill_ids=[11]), rows, {1}, {})

        assert draft.skill_ids == [11]
        assert draft.skill_names == ["React"]

    def test_a_flat_row_is_still_scoped_to_its_category(self, agent):
        """Scoping must not be what deletes it.

        The filter read `c["id"] == category_id`, which is right for a category row
        and wrong for a flat skill row -- there `id` is the skill's own id, so no
        skill row ever matched and the pool was emptied the moment a category was
        resolved. Which is always: the category is resolved before skills ground.
        """
        rows = [
            {"id": 11, "name": "React", "categoryId": 1},
            {"id": 90, "name": "Snowflake", "categoryId": 5},
        ]

        draft = agent._ground(_draft(category_id=1, skill_ids=[11, 90]), rows, {1}, {})

        assert draft.skill_ids == [11], (
            f"a skill from another category must not validate; got {draft.skill_ids}"
        )
        assert "Snowflake" not in draft.skill_names

    def test_a_category_row_is_still_not_mistaken_for_a_skill(self, agent):
        """The original bug, restated so the fix cannot undo it."""
        draft = agent._ground(
            _draft(category_id=1, skill_ids=[1, 2]),
            [{"id": 1, "name": "Development"}, {"id": 2, "name": "Design"}],
            {1, 2},
            {},
        )

        assert draft.skill_ids == []
        assert "Development" not in draft.skill_names
        assert "Design" not in draft.skill_names


def _draft(**kw):
    from ai.agents.draft_agent import DraftOutput

    base = {
        "title": "Build a Modern Responsive Website",
        "description": "A description long enough to satisfy the output contract.",
        "requirements": "Requirements long enough to satisfy the output contract.",
        "expected_deliverables": "Deliverables long enough to satisfy the contract.",
        "category_id": None,
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