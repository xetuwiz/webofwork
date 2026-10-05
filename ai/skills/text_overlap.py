"""Does this portfolio entry show work like the work being commissioned?

## Why this exists

``portfolio_relevance`` was computed as::

    evidence = len(portfolio) * 2 + len(packages)

and then reported to clients under the name *relevance*. That is a
misrepresentation, not a weak signal: a freelancer with five unrelated projects
outscores one with two that are exactly the work the client wants. The field
promises relevance and delivered a count, and nothing in the system flagged the
discrepancy because the number was internally consistent.

## What this does instead

Compare the text that describes the commission against the text that describes
the evidence:

- the brief side is ``Project.Requirements`` and ``Project.ExpectedDeliverables``
  -- these are what the client says they need delivered, which is the closest
  thing the system has to a statement of fit;
- the evidence side is each ``FreelancerPortfolio`` entry's ``Title`` and
  ``Description``.

Both sides are tokenised, stopworded, and reduced through
:mod:`ai.skills.ontology` so that a portfolio entry saying "ReactJS" and a
requirement saying "React" are recognised as the same term rather than as two
unrelated words. The result is a per-term credit in ``[0, 1]``, so a partial
overlap earns partial relevance -- which is what relevance actually is.

## What this deliberately does not read

- ``FreelancerProfile.Bio`` and ``Tagline``. These are marketing copy. Scoring
  them rewards listing every technology you have ever touched, which is the
  resume-keyword problem: the more advertising a profile contains, the higher it
  scores, regardless of whether the person can do the work.
- ``Project.Description`` and ``Project.Title``. These are prose written to be
  read by a human. They are retrieval signals -- useful for *finding* candidate
  projects -- and mixing them into a fit score rewards a brief that happens to
  share vocabulary with a portfolio entry.
- The model. Overlap is a closed, checkable computation. A model would make the
  number nondeterministic across runs for no gain, and would make a 20% scoring
  factor depend on an API being up.

## Determinism

No network, no model, no randomness. The same project and the same freelancer
always produce the same number, which is the property that lets the score be
audited and the tests assert exact values.
"""

from __future__ import annotations

import re
from dataclasses import dataclass

from .ontology import canonical_name, normalise

#: Words that carry no evidence of fit. Kept small and reviewed rather than
#: pulled from a general-purpose list: an aggressive stopword list starts removing
#: the words that describe deliverables ("design", "build", "test"), which are
#: precisely the ones worth matching on.
STOPWORDS = frozenset(
    {
        "a",
        "an",
        "and",
        "any",
        "are",
        "as",
        "at",
        "be",
        "been",
        "but",
        "by",
        "can",
        "do",
        "does",
        "for",
        "from",
        "has",
        "have",
        "how",
        "i",
        "if",
        "in",
        "into",
        "is",
        "it",
        "its",
        "me",
        "my",
        "of",
        "on",
        "or",
        "our",
        "out",
        "so",
        "that",
        "the",
        "their",
        "them",
        "then",
        "there",
        "these",
        "they",
        "this",
        "to",
        "up",
        "us",
        "was",
        "we",
        "were",
        "what",
        "when",
        "which",
        "who",
        "will",
        "with",
        "you",
        "your",
    }
)

#: Term lengths outside this range are not evidence of fit. One or two characters
#: are noise ("js", "ui"); very long runs are usually a sentence fragment that
#: slipped past tokenisation rather than a technology or a deliverable.
MIN_TERM_LENGTH = 2
MAX_TERM_LENGTH = 28

_TERM = re.compile(r"[a-z0-9+#.]+")


def _canonical(term: str) -> str:
    """The ontology's name for a term, falling back to its normalised form.

    :func:`~ai.skills.ontology.normalise` on its own is *not* enough. It maps
    ``React.js`` and ``ReactJS`` to the same string, but it does not know that
    ``reactjs`` and ``react`` are the same technology -- that knowledge lives in
    the alias table, reached through
    :func:`~ai.skills.ontology.canonical_name`.

    An earlier version of this module normalised only, and the consequence was
    concrete: a portfolio entry saying "ReactJS" failed to satisfy a brief asking
    for "React", so a genuinely relevant freelancer lost most of this factor on
    a spelling difference.

    Falling back to the normalised form matters for the majority of terms, which
    are ordinary English words rather than catalogue entries -- "responsive",
    "checkout", "invoice". ``canonical_name`` returns ``None`` for those, and
    dropping them would leave almost every brief with no terms at all.
    """
    return canonical_name(term) or normalise(term)


def terms(text: str | None) -> set[str]:
    """Reduce free text to the comparable terms it contains.

    Each surviving term is resolved through the ontology, so "React.js",
    "ReactJS" and "React" all yield ``react`` and match each other, and "Node"
    matches "Node.js".

    >>> sorted(terms("Built a React.js front end"))
    ['built', 'end', 'front', 'react']
    >>> sorted(terms("The best solution for this project"))
    ['best', 'solution']
    """
    if not text:
        return set()

    found: set[str] = set()
    for raw in _TERM.findall(text.lower()):
        if raw in STOPWORDS:
            continue
        if len(raw) < MIN_TERM_LENGTH or len(raw) > MAX_TERM_LENGTH:
            continue

        canonical = _canonical(raw)
        if not canonical or canonical in STOPWORDS:
            continue

        found.add(canonical)

    return found


def _significant(tokens: set[str]) -> list[str]:
    """Order tokens so overlap is compared on the same terms every run.

    Sets are unordered in Python, and iterating one directly would make the
    "best matching term" chosen for a given requirement depend on hash order.
    Sorting is what makes the resulting detail reproducible.
    """
    return sorted(tokens)


@dataclass(frozen=True)
class TermOverlap:
    """How much of the brief one body of text actually addresses."""

    #: Fraction of significant brief terms the text covers, in ``[0, 1]``.
    ratio: float
    #: Brief terms with no counterpart in the text, most significant first.
    missing: tuple[str, ...]
    #: The matched term per covered brief term, as ``brief_term -> matched_term``.
    matched: dict[str, str]

    @property
    def covered(self) -> int:
        return len(self.matched)


def overlap(brief_text: str | None, evidence_text: str | None) -> TermOverlap:
    """Fraction of one text's terms that another text addresses.

    Terms are matched exactly after normalisation. There is no partial-credit
    fuzziness here on purpose: "responsive" and "responsibility" share a prefix
    and nothing else, and a scorer that rewards shared prefixes will happily
    report a match that does not exist.
    """
    return _overlap_terms(terms(brief_text), evidence_text)


def _overlap_terms(brief: set[str], evidence_text: str | None) -> TermOverlap:
    """Overlap of a caller-supplied brief term set against one evidence text."""
    evidence = terms(evidence_text)
    ordered = _significant(brief)

    if not ordered:
        # No brief terms, so there is nothing to be missing. Ratio 1.0 with
        # nothing matched -- "full coverage of nothing" -- which is the honest
        # reading for this helper.
        #
        # `portfolio_relevance` does not rely on this: it checks for an empty brief
        # first and returns None, because "nothing was asked" and "everything was
        # shown" must not produce the same number.
        return TermOverlap(ratio=1.0, missing=(), matched={})

    if not evidence:
        # A brief was stated and nothing answered it. Every term is missing, which
        # is different from the empty-brief case above.
        return TermOverlap(ratio=0.0, missing=tuple(ordered), matched={})

    matched: dict[str, str] = {t: t for t in ordered if t in evidence}
    missing = tuple(t for t in ordered if t not in evidence)

    return TermOverlap(
        ratio=len(matched) / len(ordered),
        missing=missing,
        matched=matched,
    )


def brief_terms(*texts: str | None) -> set[str]:
    """Union of the terms across every field that states what is needed.

    Requirements and expected deliverables are the two fields that say what the
    client wants produced. Both count: a deliverable named only in the
    deliverables list is still a requirement.
    """
    combined: set[str] = set()
    for text in texts:
        combined |= terms(text)
    return combined


def portfolio_relevance(
    *,
    requirements: str | None,
    expected_deliverables: str | None,
    portfolio: list[dict] | None,
    packages: list[dict] | None = None,
) -> tuple[float | None, list[TermOverlap]]:
    """Score how much of the brief a freelancer's portfolio addresses.

    Returns the percentage in ``[0, 100]`` and the per-entry detail, so the reason
    can be shown to a client rather than asserted.

    Returns ``None`` as the score when the brief carries no usable text, meaning
    "relevance could not be measured" -- which is different from "irrelevant", and
    different again from "perfect". The caller drops the factor rather than
    guessing a value for it.

    ## How entries combine

    Each portfolio entry is scored against the whole brief, and the candidate
    takes the **best** entry rather than the average. Averaging punishes a
    freelancer for having a broad portfolio, which is not a fault: someone who
    has done the exact work once has demonstrated it, and that is the evidence
    the client asked to see. The worst entry would be the wrong extreme, for the
    opposite reason.

    ## Packages

    A package description states what the freelancer is *offering*, which is
    evidence about intent rather than evidence about delivered work. It is
    included as a fallback so that a freelancer with a package but an empty
    portfolio is not scored as though they had shown nothing at all, but it is
    scored on its own and only used when there is no portfolio to judge.
    """
    brief = brief_terms(requirements, expected_deliverables)
    entries = [e for e in (portfolio or []) if isinstance(e, dict)]

    if not brief:
        # No brief text, so there is no way to know whether any entry is
        # relevant. Return ``None`` rather than a number in either direction.
        #
        # This started as "abstain with full marks", mirroring how
        # ``_score_skills`` treats an empty requirement list. That was wrong, and
        # a golden case caught it: a freelancer with an *empty* portfolio scored
        # 100.0 because there was nothing to miss, which is the exact inversion of
        # the intended meaning -- it rewarded having shown no work at all, and
        # pushed candidates who should have been excluded over the
        # ``MIN_USABLE_SCORE`` line.
        #
        # The caller treats ``None`` as "this factor could not be measured" and
        # drops it from the weighted sum, so an unmeasurable factor cannot inflate
        # a score in either direction.
        return None, []  # type: ignore[return-value]

    if entries:
        # Each entry is measured against the brief term set directly rather than
        # against concatenated text, so one entry cannot "borrow" the terms of
        # another and a freelancer is not credited for a portfolio whose parts
        # only make sense together.
        details = [_overlap_terms(brief, _entry_text(entry)) for entry in entries]

        best = max(details, key=lambda d: (d.ratio, -len(d.missing)))
        return 100.0 * best.ratio, details

    package_entries = [p for p in (packages or []) if isinstance(p, dict)]
    if package_entries:
        detail = _overlap_terms(brief, _package_text(package_entries))
        return 100.0 * detail.ratio, [detail]

    return 0.0, []


def _entry_text(entry: dict) -> str:
    """The text of one portfolio entry.

    Only Title and Description. ImageUrl is deliberately excluded: a filename
    like ``react-dashboard.png`` is a label, not a claim about delivered work,
    and including it would let a well-named file stand in for evidence.
    """
    parts = [str(entry.get("title") or ""), str(entry.get("description") or "")]
    return " ".join(p for p in parts if p.strip())


def _package_text(entries: list[dict]) -> str:
    parts = [
        " ".join(
            p
            for p in (
                str(e.get("packageType") or ""),
                str(e.get("description") or ""),
            )
            if p.strip()
        )
        for e in entries
    ]
    return " ".join(p for p in parts if p.strip())
