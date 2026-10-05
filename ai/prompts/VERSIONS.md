# Prompt versioning

## Why this file is a table and not a changelog

Because the fixtures are keyed by `sha256` of the prompt **exactly as the chain
sends it**. Editing `ai/prompts/draft.txt` invalidates `draft.jsonl`, and the
first test to touch the draft agent fails with `FixtureMissingError` naming the
prompt hash.

That is the mechanism working. This file is what makes the resulting failure
answerable: it says what changed, why, and what has to be re-recorded.

## The rule

**Any edit to a prompt in this directory requires re-running `record_all`, and
the re-recorded fixtures belong in the same commit as the prompt.**

A prompt change with stale fixtures does not fail in an interesting way. The
tests that replay fixtures fail loudly — good — but a developer under time
pressure will "fix" it by deleting the fixture, which turns a loud failure into
a permanently-uncovered agent.

## Prompts

| File | Agent | Answers | Depends on |
|---|---|---|---|
| `draft.txt` | Draft | A project draft from free text | The category and skill lists, supplied in the request so grounding costs no read budget |
| `planning.txt` | Planning | The five weights, the hard filters, the shortlist size, the delegated agents | The agent thresholds |
| `matching.txt` | Matching | A one-sentence reason per candidate | Scored candidates, **never** the score itself |
| `screening.txt` | Screening | A rationale per proposal | The fit score, never the band |
| `validation.txt` | Validation | `human_summary` and nothing else | The ten rule outcomes |
| `outreach.txt` | Outreach | One sentence describing what happened | Recorded for completeness; this agent makes no model call |

The last row is the one that looks like an oversight. It is not: outreach is
deterministic, and recording the fixture documents that the absence is
deliberate rather than something a later contributor "fixes" by adding a call to
the only agent that acts on the world.

## What must never go in a prompt

### An id that reaches the database

`category_id` and `skill_ids` are grounded to real rows *after* the model
answers. The prompt may name categories and skills in prose; what comes back is
checked regardless of what it says.

This is why the grounding exists rather than a more careful prompt. A prompt
cannot guarantee a valid id. A check can.

### A score, a price, or a verdict

The model writes prose and classifies into closed sets. It does not write:

- `final_score` — computed in code, `100 × Σ(weight_i × normalised_i)`;
- `fit_score` → band — the band is derived, so a model returning `strong_match`
  for 0.4 is corrected rather than obeyed;
- `valid`, `issues`, `cost_breakdown` — ten deterministic rules;
- `pricing_type`, `fixed_price_usd` on a fixed project — bounds-checked in
  `_validate`, because a model will eventually return a negative price.

### Anything that would need to be true tomorrow

The Validation Agent's prompt asks for a sentence *about* a computed verdict. If
it were asked to produce the verdict, the ten rules would become a
cross-check on a model rather than the gate itself.

## When a prompt change is a design change

Changing what a prompt asks *for* — adding a field to the output, changing a
classification into a judgement — is a change to the contract in
`agent-contracts.md`, and needs the same review as a schema change. The test to
watch is the one that enforces the model-authority table: if a prompt starts
asking for something the table says the model may not decide, a test fails.

## Re-recording

```bash
# from the repository root, not from ai/ -- the package is `ai.*`
python -m ai.scripts.record_all
```

Cost: one call per agent, six in total, against the provider chain. It prints the
provider that answered each one and then verifies every fixture replays offline
before exiting — so a recording that cannot be replayed fails here rather than in
CI.

Record against a provider that is *not* the one you develop against if you can.
The recorded text becomes the contract, and a fixture from a weaker model is a
more honest test of whether the prompt carries its own instructions.

## A prompt change that improved output, and what it cost

The matching prompt was changed so the reason had to name a specific project
requirement rather than restating the score. Before, reasons were generic
("experienced React developer with good ratings") and the `reason` column carried
no information the numbers did not. After, they name the matched skill.

The score was unchanged, because the score never depended on the prompt. That
separation is the point: prose improved on its own, and nothing downstream moved.
