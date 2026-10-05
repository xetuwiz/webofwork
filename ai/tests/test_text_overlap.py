"""Tests for :mod:`ai.skills.text_overlap`.

The module replaces a factor that reported itself as *relevance* while counting
rows. These tests pin down what it reads, what it refuses to read, and that it
stays deterministic -- the three properties that make it defensible in a viva
rather than merely plausible.
"""

from __future__ import annotations

import pytest

from ai.skills.text_overlap import (
    STOPWORDS,
    TermOverlap,
    brief_terms,
    overlap,
    portfolio_relevance,
    terms,
)


# ── tokenisation ────────────────────────────────────────────────────────


class TestTerms:
    def test_stopwords_are_dropped(self):
        assert "the" not in terms("The best solution")
        assert "and" not in terms("cats and dogs")

    def test_case_and_punctuation_do_not_matter(self):
        assert terms("React, Node.JS; PostgreSQL!") == terms(
            "react node.js postgresql"
        )

    def test_technology_spellings_collapse_together(self):
        """The ontology alias table, not just character normalisation.

        ``normalise`` maps "React.js" and "ReactJS" to the same string but does
        not know either is "React"; only the alias table knows that. An earlier
        version of this module normalised only, and lost real matches on it.
        """
        assert terms("ReactJS") == terms("React")
        assert terms("Node.js") == terms("Node")

    def test_a_term_absent_from_the_ontology_still_survives(self):
        """Ordinary words are not catalogue entries and must not be dropped.

        Most of what a brief says is plain English. Falling back to the
        normalised form is what keeps "checkout" and "invoice" in the comparison.
        """
        assert "checkout" in terms("A checkout and invoice system")
        assert "responsive" in terms("Responsive layout")

    def test_empty_input_is_empty_output(self):
        assert terms("") == set()
        assert terms(None) is not None and terms(None) == set()

    def test_words_the_ontology_strips_do_not_survive(self):
        """"developer" normalises to the empty string and is dropped.

        A real behaviour of the ontology that this module depends on. If it ever
        changes, a term silently appears in or disappears from every comparison,
        so it is pinned rather than left implicit.
        """
        assert "developer" not in terms("developer")
        assert "" not in terms("developer manager engineer")

    def test_plain_english_survives_even_when_the_ontology_has_no_entry(self):
        """`canonical_name` returns None for ordinary words; they must not vanish.

        Most of a brief is plain English. If the fallback to `normalise` were
        removed, these words would all disappear and almost every brief would
        reduce to a handful of technology names.
        """
        found = terms("A responsive checkout with invoice reminders")
        for word in ("responsive", "checkout", "reminders"):
            assert word in found, f"'{word}' is brief vocabulary and must be compared"

    def test_a_plain_word_can_be_canonicalised_to_its_catalogue_discipline(self):
        """"invoice" resolving to `invoicing` is intended, not a bug.

        The ontology maps a noun to the discipline a client would commission. The
        property that matters is that the word survives the round trip -- what must
        not happen is `checkout` or `reminders` being dropped because they have no
        catalogue entry.
        """
        assert "invoicing" in terms("An invoice reminder system")
        assert terms("invoicing system") == terms("invoice system")

    def test_deliverable_words_are_not_stopworded_away(self):
        """An aggressive stopword list would remove the words worth matching.

        "build", "design" and "test" describe deliverables. They are exactly the
        terms a brief and a portfolio entry need to agree on.
        """
        found = terms("Build a design system with test coverage")
        for verb in ("build", "design", "test"):
            assert verb in found, f"'{verb}' describes a deliverable and must survive"

    @pytest.mark.parametrize("junk", ["a", "js", "x", ""])
    def test_noise_is_dropped(self, junk):
        assert junk not in terms(f"real work {junk} more work")

    def test_result_is_deterministic(self):
        text = "React front end with Node.js API and PostgreSQL"
        assert len({frozenset(terms(text)) for _ in range(20)}) == 1


# ── single-text overlap ─────────────────────────────────────────────────


class TestOverlap:
    def test_full_coverage(self):
        result = overlap("React and PostgreSQL", "React and PostgreSQL work")
        assert result.ratio == 1.0
        assert result.missing == ()

    def test_no_coverage(self):
        result = overlap("React and PostgreSQL", "Logo and stationery design")
        assert result.ratio == 0.0
        assert set(result.missing) == {"react", "postgresql"}

    def test_partial_coverage_is_proportional(self):
        result = overlap("React PostgreSQL Docker", "React work")
        assert result.ratio == pytest.approx(1 / 3)

    def test_empty_brief_abstains_rather_than_scoring_zero(self):
        """Nothing was asked, so nothing can be missed.

        Scoring zero here would empty every shortlist and read as "no one is
        suitable", which is a different and wrong statement.
        """
        result = overlap("", "React and everything else")
        assert result.ratio == 1.0
        assert result.missing == ()

    def test_empty_evidence_scores_zero(self):
        result = overlap("React PostgreSQL", "")
        assert result.ratio == 0.0
        assert len(result.missing) == 2

    def test_shared_prefixes_are_not_treated_as_matches(self):
        """"responsive" and "responsibility" are different words.

        A scorer that rewards string similarity reports matches that do not
        exist, which is worse than reporting none.
        """
        assert overlap("responsive", "responsibility").ratio == 0.0
        assert overlap("running", "runner").ratio == 0.0

    def test_missing_terms_are_ordered_deterministically(self):
        first = overlap("React PostgreSQL Docker Redis", "nothing here")
        second = overlap("React PostgreSQL Docker Redis", "nothing here")
        assert first.missing == second.missing == tuple(sorted(first.missing))

    def test_covered_counts_matches(self):
        result = overlap("React PostgreSQL", "React")
        assert result.covered == 1


# ── brief assembly ─────────────────────────────────────────────────────


class TestBriefTerms:
    def test_requirements_and_deliverables_both_count(self):
        """A deliverable named only in the deliverables list is still required."""
        combined = brief_terms("React front end", "automated test suite")
        assert "react" in combined
        assert "test" in combined or "suite" in combined or "automated" in combined

    def test_absent_fields_do_not_raise(self):
        assert brief_terms(None, None) == set()
        assert brief_terms("React", None) == terms("React")


# ── the factor itself ──────────────────────────────────────────────────


def _entry(title: str, description: str = "") -> dict:
    return {"title": title, "description": description}


BRIEF = {
    "requirements": "React front end with a Node.js API and PostgreSQL storage",
    "expectedDeliverables": "Deployed application with an automated test suite",
}


class TestPortfolioRelevance:
    def test_a_relevant_entry_scores_high(self):
        score, _ = portfolio_relevance(
            requirements=BRIEF["requirements"], expected_deliverables=BRIEF["expectedDeliverables"],
            portfolio=[
                _entry(
                    "Storefront",
                    "React front end, Node.js API, PostgreSQL storage, deployed "
                    "with an automated test suite",
                )
            ],
        )
        assert score > 80.0

    def test_an_unrelated_entry_scores_zero(self):
        score, _ = portfolio_relevance(
            requirements=BRIEF["requirements"], expected_deliverables=BRIEF["expectedDeliverables"],
            portfolio=[_entry("Logo design", "Brand identity work")],
        )
        assert score == 0.0

    def test_quantity_alone_does_not_score(self):
        """The original defect, isolated.

        Five unrelated entries used to score 100.0. If this test fails, the
        factor has regressed to counting rows.
        """
        score, _ = portfolio_relevance(
            requirements=BRIEF["requirements"], expected_deliverables=BRIEF["expectedDeliverables"],
            portfolio=[_entry(f"Thing {i}", "Unrelated work") for i in range(5)],
        )
        assert score == 0.0

    def test_the_best_entry_wins_not_the_average(self):
        """A broad portfolio is not a fault.

        Averaging would punish someone who has done the exact work once alongside
        unrelated projects, which is precisely the evidence the client asked to
        see.
        """
        score, _ = portfolio_relevance(
            requirements=BRIEF["requirements"], expected_deliverables=BRIEF["expectedDeliverables"],
            portfolio=[
                _entry("Logo", "Brand identity"),
                _entry(
                    "Storefront",
                    "React front end, Node.js API, PostgreSQL storage, deployed "
                    "with an automated test suite",
                ),
                _entry("Brochure", "Print design"),
            ],
        )
        assert score > 80.0

    def test_entries_do_not_pool_their_terms(self):
        """One entry cannot satisfy a term only a different entry mentions."""
        score, _ = portfolio_relevance(
            requirements=BRIEF["requirements"], expected_deliverables=BRIEF["expectedDeliverables"],
            portfolio=[
                _entry("Front end", "React and Node.js work"),
                _entry("Storage", "PostgreSQL and a deployed test suite"),
            ],
        )
        assert score < 100.0

    def test_no_brief_is_unmeasurable_rather_than_perfect(self):
        """None, not 100.0.

        An earlier version returned full marks on the reasoning that nothing was
        required so nothing was missed. A golden case caught the consequence: a
        freelancer with an *empty* portfolio scored 100.0 for having shown no work
        at all, which is the exact inverse of the intended meaning. "Nothing was
        asked" cannot distinguish "relevant" from "irrelevant", so the honest
        answer is that it is unmeasurable.
        """
        score, details = portfolio_relevance(
            requirements=None,
            expected_deliverables=None,
            portfolio=[_entry("Anything", "At all")],
        )
        assert score is None
        assert details == []

    def test_a_whitespace_only_brief_is_also_unmeasurable(self):
        score, _ = portfolio_relevance(
            requirements="   \n  ",
            expected_deliverables="",
            portfolio=[_entry("Anything", "At all")],
        )
        assert score is None

    def test_an_unmeasurable_brief_does_not_mask_an_empty_portfolio(self):
        """Two different statements, kept apart.

        "No brief to answer" and "answered nothing" must not produce the same
        number, or a caller cannot tell them apart.
        """
        no_brief, _ = portfolio_relevance(
            requirements=None,
            expected_deliverables=None,
            portfolio=[_entry("Anything", "At all")],
        )
        empty_evidence, _ = portfolio_relevance(
            requirements="React front end",
            expected_deliverables=None,
            portfolio=[],
        )
        assert no_brief is None
        assert empty_evidence == 0.0

    def test_no_portfolio_and_no_packages_is_zero(self):
        score, _ = portfolio_relevance(requirements=BRIEF["requirements"], expected_deliverables=BRIEF["expectedDeliverables"], portfolio=[], packages=[])
        assert score == 0.0

    def test_packages_are_used_only_when_there_is_no_portfolio(self):
        """A package is evidence of intent, not of delivered work."""
        described = {"packageType": "standard", "description": "React front end"}

        with_portfolio, _ = portfolio_relevance(
            requirements=BRIEF["requirements"], expected_deliverables=BRIEF["expectedDeliverables"], portfolio=[_entry("Unrelated", "Nothing relevant")],
            packages=[described],
        )
        without_portfolio, _ = portfolio_relevance(
            requirements=BRIEF["requirements"], expected_deliverables=BRIEF["expectedDeliverables"], portfolio=[], packages=[described]
        )

        assert with_portfolio == 0.0, "a portfolio must be judged on its own merits"
        assert without_portfolio > 0.0, "a package is better than nothing at all"

    def test_malformed_entries_are_ignored_rather_than_crashing(self):
        score, _ = portfolio_relevance(
            requirements=BRIEF["requirements"], expected_deliverables=BRIEF["expectedDeliverables"],
            portfolio=[
                None,
                "not a dict",
                _entry(None, None),
                _entry("Storefront", "React front end and Node.js API"),
            ],
        )
        assert score > 0.0

    def test_score_is_bounded(self):
        for portfolio in ([], [{}], [_entry("x", "y")] * 3):
            score, _ = portfolio_relevance(requirements=BRIEF["requirements"], expected_deliverables=BRIEF["expectedDeliverables"], portfolio=portfolio)
            assert 0.0 <= score <= 100.0

    def test_repeated_runs_are_identical(self):
        """Determinism was the original reason this factor was a count.

        It is now computed, so reproducibility is no longer free.
        """
        portfolio = [
            _entry("B", "Node.js API"),
            _entry("A", "React front end"),
            _entry("C", "PostgreSQL storage"),
        ]
        runs = {
            portfolio_relevance(requirements=BRIEF["requirements"], expected_deliverables=BRIEF["expectedDeliverables"], portfolio=list(portfolio))[0]
            for _ in range(30)
        }
        assert len(runs) == 1, f"relevance varied between runs: {runs}"

    def test_detail_lists_what_is_missing(self):
        """A client is told what was not evidenced, not just given a number."""
        _, details = portfolio_relevance(
            requirements=BRIEF["requirements"], expected_deliverables=BRIEF["expectedDeliverables"], portfolio=[_entry("Logo", "Brand identity")]
        )
        assert len(details) == 1
        assert details[0].missing, "an unrelated entry must report every term missing"
        assert details[0].ratio == 0.0


# ── what this module refuses to read ───────────────────────────────────


class TestDeliberateOmissions:
    def test_stopwords_exclude_pronouns_and_fillers_only(self):
        """The list is reviewed, not borrowed.

        An off-the-shelf stopword list removes words that describe deliverables,
        which would quietly delete most of the evidence.
        """
        for filler in ("the", "and", "with", "that"):
            assert filler in STOPWORDS
        for content in ("design", "build", "test", "api", "payment"):
            assert content not in STOPWORDS

    def test_image_url_is_not_evidence(self):
        """A filename is a label, not a claim about delivered work.

        Otherwise a well-named file stands in for evidence.
        """
        score_named, _ = portfolio_relevance(
            requirements=BRIEF["requirements"], expected_deliverables=BRIEF["expectedDeliverables"],
            portfolio=[
                {
                    "title": "Nothing",
                    "description": "Nothing relevant",
                    "imageUrl": "react-nodejs-postgresql-deployed-tests.png",
                }
            ],
        )
        assert score_named == 0.0

    def test_term_overlap_is_exact_after_normalisation(self):
        """No fuzzy matching.

        Substring or prefix matching would report "responsive" as satisfying
        "responsibility", which is a match that does not exist.
        """
        # Both of these are genuine matches, and both come from the reviewed
        # alias table rather than from string similarity. That is the distinction
        # worth asserting: the vocabulary knowledge lives in one reviewed file
        # instead of being spread through a similarity function.
        assert overlap("database", "databases").ratio == 1.0
        assert overlap("postgres", "postgresql").ratio == 1.0

        # ...and a term the alias table says nothing about stays unmatched even
        # when the two strings are similar.
        assert overlap("responsive", "responsibility").ratio == 0.0


class TestTermOverlapRecord:
    def test_is_frozen(self):
        result = TermOverlap(ratio=0.5, missing=("a",), matched={"b": "b"})
        with pytest.raises(Exception):
            result.ratio = 0.9  # type: ignore[misc]

    def test_covered_counts_matched_terms(self):
        assert TermOverlap(ratio=0.5, missing=("a",), matched={"b": "b"}).covered == 1
        assert TermOverlap(ratio=0.0, missing=(), matched={}).covered == 0


class TestOverlapHelperEdgeCases:
    """The bare ``overlap`` helper, which ``portfolio_relevance`` does not use.

    Its empty-brief branch reports ratio 1.0 -- "full coverage of nothing". That
    is correct for the helper and deliberately *not* what the factor reports: the
    factor checks for an empty brief before reaching here and returns None, so the
    two never have to be told apart by a caller.
    """

    def test_empty_brief_is_full_coverage_of_nothing(self):
        result = overlap("", "React and PostgreSQL everywhere")
        assert result.ratio == 1.0
        assert result.matched == {}

    def test_empty_evidence_reports_every_brief_term_missing(self):
        result = overlap("React PostgreSQL Docker", None)
        assert result.ratio == 0.0
        assert result.missing == ("docker", "postgresql", "react")

    def test_both_empty(self):
        result = overlap(None, None)
        assert result.ratio == 1.0
        assert result.missing == ()
