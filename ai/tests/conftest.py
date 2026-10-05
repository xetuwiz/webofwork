"""Test-wide setup for the agent service.

## Why this exists

The service reads ``ai/.env`` for its settings, and that file contains a real
``INTERNAL_API_KEY``. ``get_settings`` is ``lru_cache``d, so the first call in a
process freezes whatever that file happens to contain. The route tests then send
their own test key and get a 401 -- not because the authentication is wrong, but
because they are being compared against a developer's local secrets.

That made four tests in ``test_guarantees.py`` pass or fail depending on whether
the machine running them had a populated ``.env``. On CI, with no ``.env``, they
failed. Locally they passed. A test whose result depends on untracked files is
not a test.

## The fix

Point the service at a known-good environment *before* anything imports the app,
and neutralise the ``.env`` file for the duration of the session. Both are needed:
the environment variable alone loses to ``.env`` on a developer's machine, and
``.env`` alone leaves a real key in the environment for anything that reads it.

Deliberately not done by deleting or editing ``ai/.env``. That file holds the
credentials needed to actually run the service against the live platform, and a
test run has no business destroying them.
"""

from __future__ import annotations

import os
from pathlib import Path

#: The key the route tests send in ``X-Internal-Api-Key``.
#:
#: It has to match ``KEY`` in ``test_guarantees.py``. Both are test values with no
#: access to anything: the app under test reaches only ``https://dotnet.invalid``
#: and whatever the tests hand it directly.
TEST_INTERNAL_API_KEY = "test-key-abc123"

#: Repository/package root, i.e. the directory holding ``config.py``.
_PACKAGE_ROOT = Path(__file__).resolve().parent.parent
_ENV_FILE = _PACKAGE_ROOT / ".env"


def pytest_configure(config: object) -> None:
    """Neutralise ambient configuration before any test module imports the app.

    Runs at configure time rather than as a fixture because the failure mode is
    import-order: ``ai.main`` builds the FastAPI app at import, and the
    dependencies resolve ``get_settings`` when a request is handled. A fixture
    that ran later would be too late for an app object already constructed
    against real settings.
    """
    os.environ["INTERNAL_API_KEY"] = TEST_INTERNAL_API_KEY

    # Point the dotnet client at an unroutable host so a test that genuinely tries
    # to reach the API fails loudly instead of succeeding against whatever
    # development database the machine happens to have configured.
    os.environ.setdefault("DOTNET_BASE_URL", "https://dotnet.invalid")

    # Neutralise the .env file for this session only.
    #
    # Renamed rather than emptied, and restored in pytest_unconfigure: a test run
    # that silently truncated a developer's credentials file would be a worse bug
    # than the one being fixed. If the file is absent -- as on CI -- there is
    # nothing to do.
    if _ENV_FILE.exists():
        parked = _PACKAGE_ROOT / ".env.parked-by-pytest"
        if not parked.exists():
            _ENV_FILE.rename(parked)
            globals()["_restore_env_file_on_exit"] = True
        else:
            # A previous run crashed before restoring. The parked copy is
            # authoritative, so put it back rather than compounding the mess.
            parked.rename(_ENV_FILE)
            globals()["_restore_env_file_on_exit"] = False
    else:
        # No .env to park -- on CI, or after a crashed run that already moved it.
        globals()["_restore_env_file_on_exit"] = _PACKED_PATH.exists()


#: Set in ``pytest_configure``. Module-level rather than a fixture so that
#: ``pytest_unconfigure`` can read it: pytest does not guarantee that unconfigure
#: hooks run when a session is interrupted, so the recovery below cannot depend on
#: this one having fired.
_restore_env_file_on_exit = False
_PACKED_PATH = _PACKAGE_ROOT / ".env.parked-by-pytest"


def _unpark() -> None:
    """Put the developer's ``.env`` back, whichever hook gets the chance first."""
    if not _restore_env_file_on_exit:
        return
    if _PACKED_PATH.exists() and not _ENV_FILE.exists():
        _PACKED_PATH.rename(_ENV_FILE)


def pytest_unconfigure(config: object) -> None:
    """Normal exit path."""
    _unpark()


def pytest_keyboard_interrupt(excinfo: object) -> None:
    """Ctrl-C: pytest skips ``pytest_unconfigure``, so recover here too."""
    _unpark()


def pytest_internalerror(excinfo: object) -> None:
    """An internal error: same reasoning as the interrupt case."""
    _unpark()


def pytest_report_header(config: object) -> list[str]:
    """Say which key the suite is running against.

    A 401 in these tests means "the test sent the wrong key", and this line makes
    that unambiguous instead of leaving it to be inferred.
    """
    return [
        f"agent tests: INTERNAL_API_KEY={TEST_INTERNAL_API_KEY!r} "
        f"(ai/.env {'parked' if _restore_env_file_on_exit else 'absent'})"
    ]
