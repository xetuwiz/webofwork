"""Resolving whether a freelancer has a skill a project needs.

## The problem this exists to solve

Skill matching used to be string equality after lowercasing. So a freelancer who
wrote ``ReactJS`` did not match a project asking for ``React``, ``Node.js`` did
not match ``Node``, and ``k8s`` did not match ``Kubernetes``. Those are not
edge cases -- they are the same skill written the way people actually write it,
and each miss costs a genuinely strong candidate most of the skill-coverage
factor.

The original plan called for a fourth resolution, ``llm_equivalent``, which
would ask a model. That was never built. This module is the part that needs no
model at all, and it turns out to cover almost everything real.

## Three tiers, cheapest first

1. **Normalisation** -- strip separators, unify spacing, drop redundant
   qualifiers. Pure function, no I/O.
2. **Alias table** -- a reviewed, versioned mapping of the spellings people use.
3. **A typed ontology** -- directional ``implies`` edges, plus ``sibling`` vetoes
   for skills that are related but *not* interchangeable.

A model would only ever handle the residue, and the residue turns out to be small
enough that it is not worth a network call per run.

## Why not ask a model

For skill equivalence specifically, a model is the *wrong* tool, and this is the
non-obvious part. Embedding models place React and Angular very close together --
both are frontend JavaScript frameworks -- which is correct as clustering and
catastrophic as a hiring decision. A closed, checkable fact ("is React the same
as Angular?") should not be answered by a distance function.

The ``sibling`` edge is how that fact is recorded: an explicit veto, checked
before anything else, that no inference can override. The human decides what
must not match; the code honours it.

## The contract this module offers callers

``resolve_requirement`` returns a :class:`SkillResolution` with a **credit**
between 0 and 1 and a **resolution** naming which rule produced it. Callers sum
credits; they never infer anything themselves. And every resolution carries the
candidate skill that produced it, so the reason can be shown to a client --
"C# matched .NET" is a defensible sentence, an unexplained match is not.

With this module disabled or a pair absent from it, credit is 1.0 for an exact
match and 0.0 otherwise, which is exactly the previous behaviour. The old system
is the floor, not the ceiling.
"""

from __future__ import annotations

import functools
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Literal

import yaml

DataDir = Path(__file__).resolve().parent.parent / "data"
OntologyPath = DataDir / "skill_ontology.yaml"

#: Every resolution a credit can carry. A closed set on purpose: a caller
#: switching on this cannot be handed a value nobody anticipated.
Resolution = Literal[
    "exact",
    "case_insensitive",
    "alias",
    "implies",
    "weakly_implies",
    "sibling_blocked",
    "missing",
]

#: Credit for an unresolved pairing. Binary, so the floor is the old behaviour.
EXACT_CREDIT = 1.0
NO_CREDIT = 0.0

#: What an implication is worth read backwards.
#:
#: ``aspnetcore implies csharp`` is true. Its converse is not: someone with C# may
#: be writing desktop software or games, so they do not satisfy an ASP.NET Core
#: brief. Discounting the reverse rather than reusing the edge's own weight is
#: what keeps a directional claim directional -- without it the resolver matched a
#: C# developer to an ASP.NET job at full credit.
REVERSE_DISCOUNT = 0.6

# Separators people use interchangeably inside one skill name.
_SEPARATORS = re.compile(r"[\s_\-/]+")

#: Words that add length without meaning. Removed while word boundaries still
#: exist, which is why they come before compaction.
_NOISE = re.compile(
    r"\b(developer|development|dev|programming|programmer|basic|advanced|modern)\b"
)


def normalise(skill: str) -> str:
    """Reduce a skill name to a comparable form.

    A pure function, so it is trivially testable and identical on every run --
    which is the property that lets the rest of this module be deterministic.

    It deliberately does *not* try to strip ``.js`` or ``.ts``. Doing so in a
    regex is how ``reactjs`` becomes ``reac`` or ``node`` becomes ``""``, and
    the two cases it looks like it would fix -- ``React.js`` and ``React JS`` --
    both compact to ``reactjs`` anyway, which the alias table already knows.
    Keeping normalisation dumb and letting the table hold the knowledge means
    one place to review, rather than a rule in code.

    >>> normalise("React.js")
    'reactjs'
    >>> normalise("React JS")
    'reactjs'
    >>> normalise("React Developer")
    'react'
    >>> normalise("C#")
    'csharp'
    """
    if not skill:
        return ""

    text = skill.strip().lower()

    # Separators first, so "node js", "node-js" and "node_js" become one shape
    # while the word boundaries still exist.
    text = _SEPARATORS.sub(" ", text).strip()

    # Noise words next, and *before* compaction -- after compaction
    # "react developer" is a single token and the word boundary no longer
    # matches, so the word silently survives.
    text = _NOISE.sub("", text)

    # Then the characters that carry no meaning for comparison.
    text = text.replace("#", "sharp").replace("+", "plus")
    text = text.replace(".", "")

    # Compact last, so the aliases are written against one readable form.
    return text.replace(" ", "").strip()


@functools.lru_cache(maxsize=1)
def load_ontology() -> tuple[
    dict[str, str], dict[tuple[str, str], tuple[str, float]], frozenset[frozenset[str]]
]:
    """Read and index the ontology file.

    Cached because it is read once per process and never changes at runtime --
    it is versioned in git like any other source.

    Returns ``(canonical_by_normalised, directed_edges, sibling_pairs)``.
    """
    with OntologyPath.open(encoding="utf-8") as handle:
        raw = yaml.safe_load(handle) or {}

    # 1. Alias -> canonical, keyed by the normalised form of both sides so
    #    lookups never have to normalise twice.
    #
    # A spelling claimed by two canonicals is a contradiction, not a
    # last-one-wins race. It was one, and `node.js` silently resolved to
    # `javascript_backend` rather than `node` -- so a Node.js developer stopped
    # matching Node, which is the exact failure this module exists to remove.
    # Two files claiming the same word cannot both be right, and guessing which
    # wins is how a hiring decision changes without anyone editing the code.
    canonical: dict[str, str] = {}
    claimed_by: dict[str, str] = {}

    def claim(spelling: str, owner: str) -> None:
        key = normalise(spelling)
        if not key:
            return

        existing = claimed_by.get(key)
        if existing is not None and existing != owner:
            raise ValueError(
                f"{OntologyPath.name}: {spelling!r} is claimed by both "
                f"{existing!r} and {owner!r}. One spelling cannot mean two skills."
            )

        claimed_by[key] = owner
        canonical[key] = owner

    for key, spellings in (raw.get("aliases") or {}).items():
        canonical_key = key.strip().lower()

        claim(canonical_key, canonical_key)
        for spelling in spellings or []:
            claim(spelling, canonical_key)

    # 2. Directed edges, keyed by (normalised from, normalised to).
    edges: dict[tuple[str, str], tuple[str, float]] = {}

    for edge in raw.get("edges") or []:
        kind = edge.get("type")
        source = normalise(edge.get("from", ""))
        target = normalise(edge.get("to", ""))

        if kind not in {"implies", "weakly_implies"}:
            # `same_as` belongs in the alias table; a sibling is a veto, not a
            # credit. Anything else is a typo and is ignored rather than
            # silently treated as a match.
            continue

        if not source or not target:
            continue

        weight = float(edge.get("weight", 1.0))

        # A weak edge must stay weaker than the strong one it shares a
        # direction with, or the two entries would disagree.
        if kind == "weakly_implies":
            weight = min(weight, 0.9)
        else:
            weight = max(weight, 0.9)

        edges[(source, target)] = (kind, weight)

    # 3. Sibling vetoes, as unordered pairs.
    siblings: set[frozenset[str]] = set()

    for pair in raw.get("siblings") or []:
        if len(pair) != 2:
            continue

        left, right = normalise(pair[0]), normalise(pair[1])
        if not left or not right or left == right:
            # A skill cannot be a sibling of itself; that entry would veto a
            # genuine exact match.
            continue

        siblings.add(frozenset({left, right}))

    # A pair cannot be both a credit edge and a veto. If it were, the veto would
    # always win (it is checked first) and the edge would be dead -- a rule that
    # looks active and never fires. Next.js and React were in both lists at once,
    # which is exactly the failure this catches.
    for source, target in edges:
        pair = frozenset({source, target})
        if pair in siblings:
            raise ValueError(
                f"{OntologyPath.name}: {source!r} and {target!r} are listed both "
                "as an implication and as siblings. One of the two is a mistake."
            )

    # Every endpoint must name a skill the alias table knows about.
    #
    # A sibling or edge naming an unknown skill parses fine, loads fine, and then
    # silently never fires -- because the pair it builds can never be produced by
    # anything a candidate actually wrote. That is not hypothetical: the sibling
    # list named vue, angular, aws, mysql, flask, django and a dozen more that
    # appeared nowhere in `aliases`, so `is_sibling("VueJS", "React")` was False
    # because "VueJS" normalised to `vuejs` while the veto held `vue`. Every one of
    # those vetoes was decorative -- it fired for the two literal strings in the
    # file and for nothing else, which is the least useful case a rule can have.
    #
    # Failing at load time is the only version of this check that helps. The
    # alternative -- a veto that looks active and never fires -- is how a hiring
    # decision changes without anyone editing the code.
    known = set(canonical.values())
    for source, target in edges:
        for endpoint in (source, target):
            if endpoint not in known:
                raise ValueError(
                    f"{OntologyPath.name}: the edge {source!r} -> {target!r} names "
                    f"{endpoint!r}, which has no entry in `aliases`. Add it, or the "
                    "edge can never fire."
                )

    for pair in siblings:
        for endpoint in pair:
            if endpoint not in known:
                raise ValueError(
                    f"{OntologyPath.name}: the sibling pair {sorted(pair)!r} names "
                    f"{endpoint!r}, which has no entry in `aliases`. A veto against "
                    "a skill the table cannot recognise never fires for the "
                    "spellings people actually type."
                )

    return canonical, edges, frozenset(siblings)


def canonical_name(skill: str) -> str | None:
    """The ontology's name for a skill, or None if it has never heard of it."""
    canonical, _, _ = load_ontology()
    return canonical.get(normalise(skill))


@dataclass(frozen=True)
class SkillResolution:
    """How one required skill was satisfied, and how strongly."""

    required: str
    credit: float
    resolution: Resolution
    #: The candidate skill that produced the credit, or None.
    matched: str | None = None

    @property
    def matched_anything(self) -> bool:
        return self.credit > 0


def is_sibling(left: str, right: str) -> bool:
    """Whether these two skills are related but explicitly not interchangeable.

    Both sides are canonicalised first, not merely normalised.

    This function used to normalise only, which meant it disagreed with
    :func:`resolve_requirement` -- the resolver canonicalises, so it correctly
    blocked a Vue freelancer from satisfying a React brief, while this reported
    ``False`` for the same pair because it was comparing ``vuejs`` against a veto
    written as ``vue``. A public predicate that answers "no" while the code it
    documents answers "yes" is worse than no predicate at all.
    """
    canonical, _, siblings = load_ontology()

    left_key = normalise(left)
    right_key = normalise(right)
    if not left_key or not right_key:
        return False

    return frozenset(
        {
            canonical.get(left_key, left_key),
            canonical.get(right_key, right_key),
        }
    ) in siblings


def resolve_requirement(required: str, candidate_skills: list[str]) -> SkillResolution:
    """Decide how much credit one required skill earns.

    The order is the contract, and it matters more than any individual rule:

    1. **Sibling veto first.** Checked before anything else, so no alias and no
       ontology edge can rescue a pair a human decided must not match. Putting
       this last would be the natural way to write it and would be wrong.
    2. **Exact**, then alias, through the canonical table.
    3. **Directed edges** -- a candidate skill that implies the requirement earns
       full credit; the reverse earns partial credit.
    4. **Missing.**

    The best available credit wins across the candidate's skills, so a
    freelancer is not penalised for listing a skill five different ways.
    """
    normalised_required = normalise(required)
    canonical, edges, _ = load_ontology()

    best = SkillResolution(required, NO_CREDIT, "missing")

    if not normalised_required:
        return best

    required_canonical = canonical.get(normalised_required, normalised_required)
    required_stripped = required.strip()
    required_folded = required.strip().lower()

    #: A sibling pair was seen among this candidate's skills. Whether it *decides*
    #: anything depends on whether anything else matched.
    saw_sibling = False

    for raw in candidate_skills:
        normalised_candidate = normalise(raw)
        if not normalised_candidate:
            continue

        candidate_canonical = canonical.get(normalised_candidate, normalised_candidate)

        # 1. The veto, recorded rather than returned.
        #
        #    Returning here would mean a freelancer who lists Vue *and* React
        #    scores zero on a React brief purely because Vue came first in their
        #    profile -- which is not what "related but not interchangeable" means.
        #    The rule exists to stop a sibling being *inferred* as a match, not to
        #    disqualify someone who actually has the skill.
        if is_sibling(candidate_canonical, required_canonical):
            saw_sibling = True
            continue

        # 2. Identical, then case-only, then punctuation-only, then an alias.
        #
        #    These are three distinct claims and collapsing them would make one of
        #    the three labels unreachable -- which is how `exact` became dead code
        #    in the first version: the case-insensitive branch was checked first
        #    and swallowed it.
        if raw.strip() == required_stripped:
            if EXACT_CREDIT > best.credit:
                best = SkillResolution(required, EXACT_CREDIT, "exact", raw)
            continue

        if raw.strip().lower() == required_folded:
            if EXACT_CREDIT > best.credit:
                best = SkillResolution(required, EXACT_CREDIT, "case_insensitive", raw)
            continue

        if normalised_candidate == normalised_required:
            # Same words, different punctuation or spacing: "Node JS" / "node js".
            if EXACT_CREDIT > best.credit:
                best = SkillResolution(required, EXACT_CREDIT, "exact", raw)
            continue

        if candidate_canonical == required_canonical:
            # A genuinely different spelling the alias table knows about.
            if EXACT_CREDIT > best.credit:
                best = SkillResolution(required, EXACT_CREDIT, "alias", raw)
            continue

        # 3. Directed edges, never chained.
        forward = edges.get((candidate_canonical, required_canonical))
        if forward is not None:
            kind, weight = forward
            if weight > best.credit:
                best = SkillResolution(required, weight, kind, raw)  # type: ignore[arg-type]
            continue

        # The converse. An implication does not run backwards: "ASP.NET Core
        # implies C#" is true, so having C# does *not* mean having ASP.NET Core.
        # Reading the edge in reverse without discounting is what made a C#
        # developer score full marks for an ASP.NET Core brief -- a false
        # positive produced by the very rule meant to be careful.
        backward = edges.get((required_canonical, candidate_canonical))
        if backward is not None:
            _, weight = backward
            reverse = weight * REVERSE_DISCOUNT

            if reverse > best.credit:
                best = SkillResolution(required, reverse, "weakly_implies", raw)

    # The veto decides only when nothing else did. A candidate who has both React
    # and Vue matches a React brief; a candidate who has only Vue does not, and
    # gets told why.
    if best.credit == NO_CREDIT and saw_sibling:
        return SkillResolution(required, NO_CREDIT, "sibling_blocked")

    return best


def resolve_all(required: list[str], candidate_skills: list[str]) -> list[SkillResolution]:
    """Resolve every requirement. Order matches the input, for reporting."""
    return [resolve_requirement(skill, candidate_skills) for skill in required]


def coverage(required: list[str], candidate_skills: list[str]) -> tuple[float, list[SkillResolution]]:
    """Coverage as a 0..1 credit ratio, with the resolutions behind it.

    A project that lists no required skills returns ``1.0`` -- "unknown", not
    "absent" -- because scoring it zero would bury every freelancer whose skills
    happen to be stored under different names.
    """
    if not required:
        return 1.0, []

    resolutions = resolve_all(required, candidate_skills)
    earned = sum(r.credit for r in resolutions)

    return earned / len(required), resolutions