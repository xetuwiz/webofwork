# WebOfWork AI service.
#
# Multi-stage so the runtime image carries no compiler and no test files: an
# image with `gcc` in it is an image with a shell in it, and this one is reached
# over HTTP with a shared key.

# ── build ──────────────────────────────────────────────────────────────
FROM python:3.12-slim AS build

# Compiled wheels for the numeric stack, built here so the runtime stage needs
# no toolchain. `libpq` is a build-time need only -- the runtime reaches
# PostgreSQL through the .NET API and never opens a connection itself.
RUN apt-get update \
 && apt-get install -y --no-install-recommends build-essential libpq-dev \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /build
COPY requirements.txt .
RUN pip wheel --no-cache-dir --wheel-dir /wheels -r requirements.txt

# ── runtime ────────────────────────────────────────────────────────────
FROM python:3.12-slim AS runtime

# Non-root, because the service reads no files it did not ship and writes none.
RUN groupadd --system ai && useradd --system --gid ai --create-home ai

# `curl` for the healthcheck below, and nothing else.
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

COPY --from=build /wheels /wheels
COPY requirements.txt .
RUN pip install --no-cache-dir --no-index --find-links=/wheels -r requirements.txt \
 && rm -rf /wheels

WORKDIR /app
COPY --chown=ai:ai . .

# Writable only by the owner, so a compromised prompt cannot edit the prompts.
RUN chown -R ai:ai /app \
 && chmod -R go-w /app \
 && chmod -R u+w /app/ai/prompts

USER ai

EXPOSE 8001

ENV PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1 \
    # The fixture chain is the default on purpose: a container that starts
    # without configuration runs on recorded responses and says so, rather than
    # crashing on a missing key or silently burning a provider's quota.
    LLM_PROVIDER_CHAIN=fixture

# Reports the chain as configured. NOT a readiness gate for a restart policy:
# this process is fine when a provider is down, and restarting it would turn a
# provider outage into an application outage.
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
  CMD curl -fsS http://localhost:8001/health || exit 1

# One worker on purpose. The service holds an in-process LLM cache and a
# per-workflow budget, and two workers would mean two caches and two budgets --
# so a request could be served from a different cache than the one that answered
# its neighbour. Scale by running more containers, not more workers, which keeps
# each cache coherent.
CMD ["uvicorn", "ai.main:app", "--host", "0.0.0.0", "--port", "8001", "--workers", "1"]
