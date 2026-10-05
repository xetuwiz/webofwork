"""Shared-secret authentication for the agent service.

The service exposes exactly two routes: an unauthenticated ``/health`` and an
authenticated ``/agent/chat``. Everything else is internal.

Why a shared secret rather than the JWT the rest of the platform uses: an agent
has no human behind it, so there is no identity to issue a token for. The key is
the authorisation, and the endpoints it guards re-derive every precondition from
the database rather than trusting anything the caller asserts.
"""

from __future__ import annotations

import hmac

from fastapi import Depends, Header, HTTPException, status

from ..config import Settings, get_settings


async def require_internal_key(
    x_internal_api_key: str | None = Header(default=None, alias="X-Internal-Api-Key"),
    settings: Settings = Depends(get_settings),
) -> None:
    """Reject a request that does not carry the shared secret.

    Raises:
        HTTPException: 401 when the header is absent or does not match.

    Notes:
        ``hmac.compare_digest`` rather than ``==``, because a byte-by-byte
        comparison short-circuits on the first mismatch and leaks, through
        response timing, how many leading characters were correct. The
        difference is small but it is free to avoid.

        The failure response is deliberately identical for "missing" and
        "wrong", so it does not confirm whether a key is configured at all.
    """
    if x_internal_api_key is None:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            # No echo of what was sent, and no hint about the expected value.
            detail="Unauthorized",
            headers={"WWW-Authenticate": "InternalApiKey"},
        )

    if not hmac.compare_digest(x_internal_api_key, settings.internal_api_key):
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Unauthorized",
            headers={"WWW-Authenticate": "InternalApiKey"},
        )
