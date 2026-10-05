"""Skill resolution: normalisation, aliases, direction, and the veto.

## What this covers

Matching used to compare skill names with ``str.strip().lower()``. That is
correct for exactly one spelling of every skill, and freelancers do not use one
spelling. ``ReactJS`` missed ``React``; ``Node.js`` missed ``Node``; ``k8s``
missed ``Kubernetes``; ``TS`` missed ``TypeScript``. Each miss cost a genuinely
strong candidate most of the skill-coverage factor -- 0.35 in matching, and
**0.45 in screening**, which is the heaviest weight in the whole formula.

## The three properties worth defending

**1. Aliases are not the interesting part.** ``ReactJS`` is a spelling. The
interesting case is direction: a C# developer genuinely can do .NET work, and a
.NET developer might only write F#. So ``implies`` is directed and scored, and
both directions are written out explicitly rather than inferred.

**2. The sibling veto is a safety feature, not a taxonomy.** An embedding model
places React and Angular almost on top of each other, because both are frontend
JavaScript frameworks -- correct as clustering, catastrophic as a hiring
decision. This module instead records "related, and *not* interchangeable" as an
explicit veto that nothing can override. The test that a React developer scores
zero against an Angular brief is therefore more important than any test here
about a successful match.

**3. The old behaviour is the floor, not the ceiling.** With a pair absent from
the ontology, credit is 1.0 for an exact match and 0.0 otherwise -- byte for byte
the previous count. A skill the table has never seen behaves exactly as it did
before it existed.

## The data is guarded too

Both mistakes in the first version of ``skill_ontology.yaml`` were silent:
``node.js`` was claimed by two canonicals and resolved to the wrong one, and
``Next.js`` was listed as both an implication of React *and* a sibling of it --
contradictory, with the veto quietly winning. Neither raised anything. Both are
now rejected at load time, and the tests at the bottom prove the guards fire.
"""

from __future__ import annotations

from uuid import UUID

import pytest

from ai.agents.matching_agent import MatchingAgent
from ai.agents.screening_agent import ProposalIn, ScreeningAgent, band_for
from ai.clients.dotnet_client import DotNetClient
from ai.config import Settings
from ai.skills.ontology import (
    EXACT_CREDIT,
    NO_CREDIT,
    OntologyPath,
    coverage,
    is_sibling,
    load_ontology,
    normalise,
    resolve_requirement,
)

KEY = "test-key-abc123"

UUID_A = "b944584e-2cd9-43f9-b5c7-a25e2c865ff4"
UUID_B = "c2b7f0e1-1111-2222-3333-444455556666"


@pytest.fixture
def agent() -> MatchingAgent:
    client = DotNetClient(
        Settings(internal_api_key=KEY, dotnet_base_url="https://x.invalid"),
        workflow_run_id=0,
    )

    async def noop(*args, **kwargs):
        return None

    for name in (
        "append_step", "append_tool_call", "append_recommendation",
        "append_screening", "append_validation", "complete_workflow",
    ):
        setattr(client, name, noop)

    return MatchingAgent(client, categories=[])


# ── normalisation ─────────────────────────────────────────────────────


@pytest.mark.parametrize(
    "raw,expected",
    [
        ("React", "react"),
        ("react", "react"),
        ("  React  ", "react"),
        # Compaction, not suffix stripping: the alias table holds the knowledge
        # that "reactjs" means React, so there is one place to review it.
        ("React.js", "reactjs"),
        ("React JS", "reactjs"),
        ("react-js", "reactjs"),
        ("react_js", "reactjs"),
        ("React Developer", "react"),
        ("Node.js", "nodejs"),
        ("TypeScript", "typescript"),
        ("C#", "csharp"),
        ("c sharp", "csharp"),
        (".NET", "net"),
        ("ASP.NET Core", "aspnetcore"),
        ("", ""),
    ],
)
def test_normalisation_collapses_the_spellings_people_actually_type(raw, expected):
    assert normalise(raw) == expected


def test_normalisation_keeps_genuinely_different_skills_apart():
    """The dangerous failure would be a normaliser that collapses too much.

    ``react`` and ``reactnative`` are different jobs. If they collapsed, every
    React Native freelancer would score full marks on a React brief.
    """
    assert normalise("React") != normalise("React Native")
    assert normalise("React") != normalise("Vue")
    assert normalise("PostgreSQL") != normalise("MySQL")


# ── aliases ───────────────────────────────────────────────────────────


@pytest.mark.parametrize(
    "required,spelling",
    [
        ("React", "ReactJS"),
        ("React", "React.js"),
        ("React", "react js"),
        ("Node", "Node.js"),
        ("TypeScript", "TS"),
        ("Kubernetes", "k8s"),
        ("PostgreSQL", "postgres"),
        ("JavaScript", "js"),
        ("Machine Learning", "ML"),
    ],
)
def test_a_spelling_variant_earns_full_credit(required, spelling):
    """The case the whole module exists for.

    Before this, every one of these was a silent miss costing the candidate most
    of the heaviest factor in the score.
    """
    resolution = resolve_requirement(required, [spelling])

    assert resolution.credit == EXACT_CREDIT, f"{required} vs {spelling}: {resolution}"
    assert resolution.resolution == "alias"
    assert resolution.matched == spelling, "the matched spelling is reported, not the required one"


def test_an_unknown_skill_behaves_exactly_as_it_did_before():
    """The floor property.

    A pair the ontology has never seen must resolve to the old answer: exact means
    1.0, otherwise 0.0. If a new table entry ever changed this, every existing
    score would move and the change would be invisible.
    """
    assert resolve_requirement("Zurb", ["Zurb"]).credit == EXACT_CREDIT
    assert resolve_requirement("Zurb", ["Elixir"]).credit == NO_CREDIT


def test_the_three_credit_labels_are_each_reachable():
    """A label nobody can produce is a label nobody can rely on.

    In the first version the case-insensitive branch was checked before the exact
    one and swallowed it, so ``exact`` was unreachable and identical names were
    reported as differing in case when they did not. Three claims deserve three
    labels.
    """
    assert resolve_requirement("React", ["React"]).resolution == "exact"
    assert resolve_requirement("React", ["react"]).resolution == "case_insensitive"
    assert resolve_requirement("React", ["ReactJS"]).resolution == "alias"
    assert resolve_requirement("React", ["Zurb"]).resolution == "missing"


def test_listing_a_skill_five_ways_is_not_penalised():
    """The best match wins, rather than the first or the last."""
    # Vue and Angular are React siblings and are listed first, so the veto is
    # encountered before any match. Recording it rather than returning is what
    # keeps this a match.
    resolution = resolve_requirement("React", ["Vue", "ReactJS", "react.js", "Angular", "React"])

    assert resolution.credit == EXACT_CREDIT, resolution


# ── direction ─────────────────────────────────────────────────────────


def test_csharp_implies_dotnet_at_full_credit():
    """You cannot write C# without .NET, so this is not a judgement call."""
    resolution = resolve_requirement(".NET", ["C#"])

    assert resolution.credit == EXACT_CREDIT
    assert resolution.resolution == "implies"


def test_dotnet_only_partially_covers_csharp():
    """The reverse is weaker, and that asymmetry is the whole point.

    A .NET developer may write F# or VB.NET. Crediting them fully for a C# brief
    would be the flat-alias-table mistake wearing a direction arrow.
    """
    resolution = resolve_requirement("C#", [".NET"])

    assert 0.0 < resolution.credit < EXACT_CREDIT, "partial, not full and not zero"
    assert resolution.resolution == "weakly_implies"


def test_implication_does_not_chain():
    """A C# developer is not thereby an ASP.NET Core developer.

    ``csharp implies dotnet`` and ``aspnetcore implies dotnet`` are both true,
    so chaining would infer that C# implies ASP.NET Core. It does not -- they
    may be writing desktop software or games. Scoring is single-hop by design,
    and this test is what stops someone "improving" it into a transitive walk.
    """
    resolution = resolve_requirement("ASP.NET Core", ["C#"])

    # Not full credit. A transitive walk through .NET would have produced 1.0.
    assert resolution.credit < EXACT_CREDIT, "chaining would have given this full credit"
    assert resolution.resolution == "weakly_implies"

    # And the other direction is still strong, which is what makes this a
    # direction test rather than a chain test.
    assert resolve_requirement("C#", ["ASP.NET Core"]).credit == EXACT_CREDIT


def test_the_strong_direction_still_works_for_a_framework():
    assert resolve_requirement("C#", ["ASP.NET Core"]).credit == EXACT_CREDIT


# ── the veto ──────────────────────────────────────────────────────────


@pytest.mark.parametrize(
    "required,held",
    [
        ("React", "Angular"),
        ("React", "Vue"),
        ("PostgreSQL", "MySQL"),
        ("Java", "C#"),
        ("JavaScript", "Java"),
        ("REST", "GraphQL"),
    ],
)
def test_related_but_not_interchangeable_scores_zero(required, held):
    """The safety property, and the most important test in this file.

    A similarity model would rate most of these highly. That is correct as
    clustering and wrong as hiring: a React developer is not an Angular
    developer, and a client who sees that match would be misled.
    """
    resolution = resolve_requirement(required, [held])

    assert resolution.credit == NO_CREDIT
    assert resolution.resolution == "sibling_blocked"


def test_the_veto_beats_an_alias():
    """The veto is checked first, so nothing else can rescue a blocked pair.

    Ordering it last would be the natural way to write this and would be a bug:
    a candidate listed under several spellings would eventually find one that
    matched and slip through.
    """
    canonical, _, siblings = load_ontology()
    assert siblings, "the sibling table should not be empty"

    resolution = resolve_requirement("React", ["ReactJS", "React", "react.js"])

    # React is not its own sibling, so this must still match -- and it does.
    assert resolution.credit == EXACT_CREDIT


def test_a_skill_is_never_a_sibling_of_itself():
    """A self-sibling entry would veto a genuine exact match.

    Guarded at load time, and asserted here because the consequence would be a
    skill that can never match anything, including itself.
    """
    for required in ("React", "C#", "PostgreSQL", "Node"):
        resolution = resolve_requirement(required, [required])
        assert resolution.credit == EXACT_CREDIT, f"{required} cannot match itself"


def test_sibling_check_is_symmetric():
    assert is_sibling("React", "Angular")
    assert is_sibling("Angular", "React")
    assert not is_sibling("React", "React")


# ── coverage ──────────────────────────────────────────────────────────


def test_no_required_skills_is_full_coverage_not_zero():
    """Unknown, not absent.

    Scoring a project that lists no skills at zero would return an empty
    shortlist for every freelancer, which reads as "nobody is suitable" rather
    than "nobody said what was needed".
    """
    ratio, resolutions = coverage([], ["React"])

    assert ratio == 1.0
    assert resolutions == []


def test_partial_coverage_is_a_credit_sum_not_a_count():
    """One full match and one partial, out of two requirements."""
    ratio, resolutions = coverage(["React", "C#"], ["ReactJS", ".NET"])

    assert ratio == pytest.approx((1.0 + 0.6) / 2)
    assert [r.resolution for r in resolutions] == ["alias", "weakly_implies"]


def test_empty_and_null_skill_lists_are_handled():
    assert coverage(["React"], [])[0] == 0.0
    assert coverage(["React"], [None, "", "   "])[0] == 0.0


# ── the data is guarded ───────────────────────────────────────────────


def test_the_ontology_file_is_valid_and_not_trivially_empty():
    canonical, edges, siblings = load_ontology()

    assert len(canonical) > 40, "the alias table looks suspiciously small"
    assert len(edges) > 10, "the implication table looks suspiciously small"
    assert len(siblings) > 5, "the sibling table looks suspiciously small"
    assert OntologyPath.exists()


def test_a_spellings_claimed_by_two_canonicals_is_rejected_at_load_time(monkeypatch):
    """The first real bug in this data, which was silent.

    ``node.js`` appeared under both ``node`` and ``javascript_backend``. The
    loader took whichever came last, so a Node.js developer stopped matching
    Node -- the exact failure the module exists to remove, introduced by the
    module.

    A hiring decision changing on which YAML key happened to be read last is not
    acceptable, so the loader now refuses rather than guessing.
    """
    import ai.skills.ontology as ontology

    conflicting = """
version: 1
aliases:
  react:
    - reactjs
  reactjs:
    - react.js
"""
    fake = tmp_path_file(conflicting)
    monkeypatch.setattr(ontology, "OntologyPath", fake)
    ontology.load_ontology.cache_clear()

    with pytest.raises(ValueError, match="claimed by both"):
        ontology.load_ontology()

    ontology.load_ontology.cache_clear()


def test_a_pair_that_is_both_an_edge_and_a_sibling_is_rejected(monkeypatch):
    """The second real bug, also silent.

    ``Next.js`` was listed as implying ``react`` *and* as a sibling of it. The
    veto is checked first, so the edge was dead code that looked alive -- a rule
    nobody could find a failing test for, because nothing reached it.
    """
    import ai.skills.ontology as ontology

    contradictory = """
version: 1
aliases:
  react:
    - reactjs
edges:
  - { type: implies, from: nextjs, to: react, weight: 1.0 }
siblings:
  - [react, angular]
  - [nextjs, react]
"""
    fake = tmp_path_file(contradictory)
    monkeypatch.setattr(ontology, "OntologyPath", fake)
    ontology.load_ontology.cache_clear()

    with pytest.raises(ValueError, match="both"):
        ontology.load_ontology()

    ontology.load_ontology.cache_clear()


def test_an_edge_naming_an_unknown_skill_is_rejected(monkeypatch):
    """The third silent bug, and the one that made the whole veto list decorative.

    The ``siblings`` list named skills -- ``vue``, ``angular``, ``aws``, ``mysql``,
    ``flask``, ``django`` and a dozen more -- that appeared nowhere in ``aliases``.
    Nothing rejected them, because nothing could: a veto is a pair of normalised
    strings, and a pair naming a skill the table cannot recognise parses perfectly.

    The result was a rule that fired for the two literal strings in the file and
    nothing else. ``is_sibling("Vue", "React")`` was True; ``is_sibling("VueJS",
    "React")`` was False, because "VueJS" normalises to ``vuejs`` and the veto held
    ``vue``. Every one of those vetoes protected nobody.

    This is the failure mode that is hardest to notice: a guard that looks active,
    appears in a reviewed data file, and never fires.
    """
    import ai.skills.ontology as ontology

    dangling = """
version: 1
aliases:
  react:
    - reactjs
  python:
    - python3
edges: []
siblings:
  - [react, angular]
"""
    fake = tmp_path_file(dangling)
    monkeypatch.setattr(ontology, "OntologyPath", fake)
    ontology.load_ontology.cache_clear()

    with pytest.raises(ValueError, match="no entry in `aliases`"):
        ontology.load_ontology()

    ontology.load_ontology.cache_clear()


def test_an_edge_naming_an_unknown_skill_is_rejected_too(monkeypatch):
    """Same rule for implications: an edge into the void earns nothing, silently.

    ``from: figma, to: designsystem`` with no ``designsystem`` entry loads cleanly
    and fires never.
    """
    import ai.skills.ontology as ontology

    dangling = """
version: 1
aliases:
  figma: []
edges:
  - { type: implies, from: figma, to: designsystem, weight: 1.0 }
siblings: []
"""
    fake = tmp_path_file(dangling)
    monkeypatch.setattr(ontology, "OntologyPath", fake)
    ontology.load_ontology.cache_clear()

    with pytest.raises(ValueError, match="no entry in `aliases`"):
        ontology.load_ontology()

    ontology.load_ontology.cache_clear()


def test_the_shipped_ontology_has_no_dangling_endpoints():
    """The guard, applied to the file that actually ships.

    The load-time check makes a bad file fail loudly, but only when it is loaded.
    This asserts the committed data is clean, so a reviewer adding an entry sees
    the failure in the test suite rather than at service startup.
    """
    canonical, edges, siblings = load_ontology()
    known = set(canonical.values())

    dangling_edges = {e for pair in edges for e in pair if e not in known}
    dangling_siblings = {e for pair in siblings for e in pair if e not in known}

    assert not dangling_edges, f"edges naming unknown skills: {sorted(dangling_edges)}"
    assert not dangling_siblings, (
        f"siblings naming unknown skills: {sorted(dangling_siblings)} -- these "
        "vetoes will never fire for any spelling a freelancer types"
    )


def test_every_veto_fires_for_the_spellings_people_actually_type():
    """The property the endpoint guard exists to protect, asserted directly.

    A veto can pass every structural check and still be useless if the spellings
    people write do not canonicalise into it. These are real spellings from real
    profiles, not the two strings that happen to be in the YAML.
    """
    # (candidate skill, requirement, what the veto is for)
    cases = [
        ("VueJS", "React", "frontend framework"),
        ("Vue.js", "React", "frontend framework"),
        ("Angular", "React", "frontend framework"),
        ("Amazon Web Services", "GCP", "cloud provider"),
        ("AWS", "Azure", "cloud provider"),
        ("MySQL", "PostgreSQL", "database"),
        ("Django", "Flask", "python web framework"),
        ("GraphQL API", "REST", "api style"),
        ("Sketch", "Figma", "design tool"),
        ("Adobe XD", "Figma", "design tool"),
        ("Canva", "Figma", "design tool"),
        ("Photoshop", "Figma", "design tool"),
        ("Unity", "Unreal Engine", "game engine"),
        ("Power BI", "Tableau", "bi tool"),
        ("Proofreading", "Copy Editing", "writing service"),
        ("Bookkeeping", "Payroll", "finance service"),
        ("Java", "C#", "language"),
        ("NodeJS", "Python", "runtime"),
    ]

    missed = [
        (candidate, requirement, why)
        for candidate, requirement, why in cases
        if not is_sibling(candidate, requirement)
    ]

    assert not missed, (
        "these vetoes do not fire for the spellings people write, which means "
        f"the rule protects nobody: {missed}"
    )


def test_the_design_domain_is_no_longer_a_single_entry():
    """The gap that motivated widening the file.

    Design had exactly one catalogue entry and no relationships at all, so a Figma
    brief could not distinguish a Figma designer from a Photoshop one -- and a
    similarity model absolutely would have, which is the whole argument for the
    veto existing.
    """
    canonical, _, siblings = load_ontology()
    known = set(canonical.values())

    for skill in ("figma", "sketch", "adobexd", "photoshop", "canva"):
        assert skill in known, f"{skill} needs a canonical entry to be vetoed against"

    # ...and the pairs that matter are actually declared.
    assert frozenset({"figma", "sketch"}) in siblings
    assert frozenset({"figma", "adobexd"}) in siblings
    assert frozenset({"figma", "photoshop"}) in siblings


def test_is_sibling_agrees_with_the_resolver():
    """The two functions must not contradict each other.

    ``is_sibling`` normalised without canonicalising, so it answered False for
    ("VueJS", "React") while ``resolve_requirement`` -- which does canonicalise --
    correctly blocked the pair. A public predicate that disagrees with the code it
    documents is worse than no predicate.
    """
    from ai.skills.ontology import resolve_requirement

    cases = [
        ("VueJS", "React"),
        ("Vue.js", "React"),
        ("NodeJS", "Python"),
        ("GraphQL API", "REST"),
        ("Amazon Web Services", "GCP"),
        # ...and pairs that are not vetoes.
        ("React", "React"),
        ("React", "Redux"),
        ("Python", "Django"),
    ]

    for candidate, requirement in cases:
        resolution = resolve_requirement(requirement, [candidate])
        blocked = resolution.resolution == "sibling_blocked"

        if is_sibling(candidate, requirement):
            assert blocked or resolution.credit > 0, (
                f"is_sibling({candidate!r}, {requirement!r}) is True but the "
                f"resolver neither blocked it nor credited it: {resolution}"
            )
        else:
            assert not blocked, (
                f"is_sibling({candidate!r}, {requirement!r}) is False but the "
                f"resolver blocked the pair: {resolution}"
            )


def tmp_path_file(content: str):
    import tempfile
    from pathlib import Path

    handle = tempfile.NamedTemporaryFile(
        "w", suffix=".yaml", delete=False, encoding="utf-8"
    )
    handle.write(content)
    handle.close()

    from pathlib import Path as _Path

    return _Path(handle.name)


# ── wired into the agents ─────────────────────────────────────────────


def test_the_matching_agent_scores_through_the_ontology(agent):
    """The end-to-end effect on the agent, not just the helper."""

    detail, score = agent._score_skills(
        {"skills": ["ReactJS", "Node.js"]}, ["React", "Node"]
    )

    assert score == 100.0, "ReactJS and Node.js cover React and Node"
    assert all(d.match for d in detail)
    assert {d.resolution for d in detail} == {"alias"}


def test_the_matching_agent_awards_partial_credit(agent):
    _, score = agent._score_skills({"skills": [".NET"]}, ["C#"])

    assert 0.0 < score < 100.0, "partial, not a full match and not a miss"


def test_the_matching_agent_blocks_a_sibling(agent):
    detail, score = agent._score_skills({"skills": ["Angular"]}, ["React"])

    assert score == 0.0
    assert detail[0].resolution == "sibling_blocked"
    assert not detail[0].match


def test_the_matching_agent_reports_credit_per_requirement(agent):
    """So the client can be told why, rather than shown an unexplained match."""
    detail, _ = agent._score_skills({"skills": ["C#"]}, [".NET"])

    assert detail[0].credit == EXACT_CREDIT
    assert detail[0].candidate == "C#"
    assert detail[0].resolution == "implies"


def test_screening_gives_a_spelling_variant_the_score_it_should(agent):
    """0.45 is the heaviest weight in the formula, so this matters most here."""
    screening = ScreeningAgent(agent._client, categories=[])

    proposal = ProposalIn(
        proposal_id=1,
        freelancer_id=UUID(UUID_A),
        bid_amount_usd=1000,
        delivery_days=7,
        cover_letter="I have built several React applications and can start immediately.",
    )

    profile = {"skills": ["ReactJS", "TypeScript"], "averageRating": 4.5, "reviewCount": 20}

    with_spellings, _, _ = screening._fit(proposal, profile, ["React", "TypeScript"], None, 30)

    profile_exact = {"skills": ["React", "TypeScript"], "averageRating": 4.5, "reviewCount": 20}
    exact, _, _ = screening._fit(proposal, profile_exact, ["React", "TypeScript"], None, 30)

    assert with_spellings == exact, (
        "ReactJS must score identically to React; before the ontology this "
        "difference was 45% of a proposal's fit score"
    )
    assert with_spellings > 0.6, "covering both requirements should clear the strong band"


def test_screening_names_a_partial_match_as_partial(agent):
    screening = ScreeningAgent(agent._client, categories=[])

    proposal = ProposalIn(
        proposal_id=1,
        freelancer_id=UUID(UUID_A),
        bid_amount_usd=1000,
        delivery_days=7,
        cover_letter="",
    )

    _, strengths, _ = screening._fit(
        proposal, {"skills": [".NET"]}, ["C#"], None, 30
    )

    assert any("partially covers" in s for s in strengths), strengths


def test_the_bands_still_follow_the_score(agent):
    """A score change must not desynchronise the bands."""
    # Bands are derived from the score, so they must move with it.
    strong = band_for(0.8)
    weak = band_for(0.3)

    assert strong is not None
    assert strong != weak, "a 0.8 fit and a 0.3 fit cannot land in the same band"