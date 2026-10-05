# WebOfWork AI Service

Agent stages for WebOfWork. Called by the .NET API; never by a browser.

```
React  ->  .NET  ->  this service  ->  .NET internal reads
                    |                        |
                    +---- writes artefacts --+
```

## Running it

```bash
cd ai
pip install -r requirements.txt
cp .env.example .env          # then set INTERNAL_API_KEY
uvicorn ai.main:app --port 8001 --reload
```

The key must match `AgenticAi:InternalApiKey` in the .NET `appsettings`. It is
checked in both directions: .NET sends it on `/agent/chat`, this service sends it
on every internal read and write.

Check it is up:

```bash
curl http://localhost:8001/health
```

With `LLM_PROVIDER_CHAIN=fixture` the whole pipeline runs with no API keys and no
network. That is the intended way to develop and to demo.

## The one design decision worth arguing about

**This service holds no checkpoints, and there is no LangGraph here.**

The plan this was built from specifies LangGraph with `interrupt_after=["validation"]`
and a `PostgresSaver`, so the graph owns "where is this run paused".

`WorkflowRun.Status` on the .NET side already *is* that state.
`PendingClientApproval` and `PendingManagerApproval` are distinct values, chosen by
the stage, and consumed by `ResumeWorkflowAsync`. Adding a second owner means two
components disagreeing about whether a run is waiting on a human — with no
principled tiebreak and no obvious symptom when it happens.

So a stage here is a **pure function of `(stage, context)`**:

- pausing is .NET's business, and .NET already does it;
- this service is stateless and horizontally scalable with no shared state;
- a resume is a second `/agent/chat` call with the recorded decision, not a
  checkpoint replay.

The cost is the ability to resume *mid-node*. The pipeline does not need it: the
only pause is between validation and outreach, which is a node boundary anyway.

## What the model is allowed to do

| Agent | Model may | Model may **not** |
|---|---|---|
| Draft | write the prose, suggest ids | invent a category or skill id — checked against the supplied list, drops and notes |
| Planning | choose weights within bounds | produce weights that do not sum to 1, name an agent outside the registry, set a threshold the admin did not set |
| Matching | write one reason per candidate | influence any score — the formula is in code |
| Screening | write one rationale per proposal | assign a band — bands are derived from the score |
| Validation | restate the summary | change `valid`, add or remove an issue, move a number |
| Outreach | nothing | anything — the action is one call into .NET |

Every rule that matters is Python. That is the design, not an accident of
implementation: a model that could talk its way past a rule would be the rule not
doing its job.

## The safety boundary

An agent is a component whose inputs are model output, so "the model asked" is
never treated as authority.

- **Tool allow-list** — a tool not named in `TOOL_ALLOWLIST` cannot be called.
- **Per-agent permissions** — each agent gets a narrow set. The outreach agent can
  only send invitations; it cannot read the freelancer pool. The screening agent
  cannot read the project brief.
- **Read budget** — 20 reads per workflow, checked *before* each request, and a
  failed read is not charged.
- **Re-checks on the .NET side** — `POST /internal/actions/send-invitations`
  re-derives approval, stage and shortlist membership from the database. A prompt
  injection cannot compose an arbitrary request; it can only ask for one of these
  verbs, and each verb checks its own preconditions.

## What is never persisted

Raw prompts and raw completions. `tools/registry.py::_summarise` reduces every
tool input and output, and agents pass structured summaries to
`WorkflowStepLog`. A step log says *what was decided and why*, not what the model
was shown. A client's typed brief is summarised by length, not quoted.

## Layout

```
ai/
├── main.py                 two routes; no logic
├── config.py               env-driven, one required setting
├── middleware/             shared-secret check
├── clients/dotnet_client   reads (budgeted) + writes
├── llm/                    provider protocol, chain, cache, budget, providers
├── prompts/                one file per agent, versioned separately
├── agents/                 the six agents
├── tools/registry.py       allow-list, per-agent permissions, summarisation
├── policies/               the ten validation rules, pure functions
├── graph/dispatcher.py     stage -> agents
└── tests/                  guarantees that hold with no model
```

## Tests

```bash
cd ai && python -m pytest -q
```

`tests/test_guarantees.py` deliberately runs with no model, no network and no
database. Grounding, the tool gate, the read budget, the ten rules and the stage
dispatch are properties of the Python, and they must hold with the model switched
off — otherwise they only hold when the model happens to cooperate.

To add a fixture-backed test, record the response:

```python
import hashlib, json
from ai.llm.providers import FixtureProvider
key = FixtureProvider.key_for(the_prompt_text)   # exactly as sent
# append {"prompt_sha256": key, "response": "..."} to tests/fixtures/llm/{agent}.jsonl
```

A missing fixture is a **failure**, not a generated answer. A fixture provider
that invents output would let the suite pass against responses nobody wrote.

## Cost

`llm_max_calls_per_workflow` (default 15) and the read budget (20) are guards
against runaway loops, not quotas. Real spend is controlled by the cache and by
`LLM_PROVIDER_CHAIN`.
