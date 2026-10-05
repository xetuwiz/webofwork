"""FastAPI application for the WebOfWork AI service.

Two routes, both tiny:

* ``GET /health`` -- unauthenticated, for a container probe.
* ``POST /agent/chat`` -- authenticated with the shared key, and the only thing
  .NET can ask for.

The application does no work itself. It validates the request, hands it to
``StageDispatcher``, and returns the result. Keeping it this thin means the
interesting behaviour is testable without a web server.
"""

from __future__ import annotations

import logging
import os

from fastapi import Depends, FastAPI
from fastapi.responses import JSONResponse

from .config import get_settings
from .graph.dispatcher import AgentChatRequest, AgentChatResponse, StageDispatcher
from .llm.chain import describe_providers, log_configuration
from .middleware.internal_auth import require_internal_key

logging.basicConfig(
    level=os.getenv("LOG_LEVEL", "INFO").upper(),
    format="%(asctime)s %(levelname)-7s %(name)s: %(message)s",
)
log = logging.getLogger(__name__)

app = FastAPI(
    title="WebOfWork AI Service",
    version="0.1.0",
    description=(
        "Agent stages for WebOfWork. All business rules are enforced in Python; "
        "language models contribute wording and judgement only."
    ),
)


@app.get("/health")
async def health() -> dict[str, object]:
    """Liveness.

    Deliberately does not report whether the .NET API is reachable. A probe that
    fails when a dependency is down causes the orchestrator to restart a healthy
    process, which turns one bad dependency into an outage.

    Reports the LLM chain as configured, because "the service is up but no
    provider is usable" is the state that is otherwise invisible until a workflow
    fails. It is not a readiness signal -- a provider being down does not mean
    this process should be restarted.
    """
    return {
        "status": "ok",
        "service": "webofwork-ai",
        "providers": describe_providers(),
    }


@app.post(
    "/agent/chat",
    response_model=AgentChatResponse,
    dependencies=[Depends(require_internal_key)],
)
async def agent_chat(body: AgentChatRequest) -> AgentChatResponse:
    """Run one agent stage.

    Always returns 200 with a ``status`` field, including on failure. A
    non-2xx would make .NET treat a workflow that legitimately produced no result
    as a transport error, losing the reason -- and this endpoint's whole job is
    to report reasons.

    A malformed body still gets a 422 from FastAPI before it reaches here, which
    is correct: that is a caller bug and should be loud.
    """
    log.info(
        "agent/chat run=%s stage=%s project=%s",
        body.workflow_run_id, body.stage, body.project_id,
    )

    dispatcher = StageDispatcher(body)
    result = await dispatcher.run()

    log.info(
        "agent/chat run=%s stage=%s -> %s in %sms (%s/%s reads)",
        body.workflow_run_id, body.stage, result.status,
        result.duration_ms, result.reads_used, result.reads_budget,
    )
    return result


@app.exception_handler(ValueError)
async def value_error_handler(_request, exc: ValueError) -> JSONResponse:
    """A configuration or argument problem, reported as 400 rather than 500.

    Separated because a 500 reads as "the service is broken" and sends someone
    looking at the service, when the thing that is wrong is the value they sent.
    """
    log.warning("bad request value: %s", exc)
    return JSONResponse(status_code=400, content={"error": str(exc)})


@app.on_event("startup")
async def _startup() -> None:
    settings = get_settings()
    log.info(
        "webofwork-ai starting: env=%s fixture_fallback=%s read_budget=%s dotnet=%s",
        settings.app_env,
        settings.llm_allow_fixture_fallback,
        settings.max_internal_reads_per_workflow,
        # The base URL only, never the key.
        settings.dotnet_base_url,
    )
    # The chain as configured, including anything that will be skipped. Logged
    # here so a misconfiguration is visible at boot rather than at the first
    # workflow that happens to reach that provider.
    log_configuration(settings)
