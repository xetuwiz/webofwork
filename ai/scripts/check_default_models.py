"""Is every hardcoded model default actually offered by its provider?

`PROVIDER_DEFAULTS` in `llm/chain.py` names a model per provider, written from
memory. The comment on that dict already records the pattern:

    Cerebras went from "Llama 3.1 8B and Qwen3 235B" to two models

So the defaults drift, and nothing checks them. A wrong name is not a startup
error -- it is a 404 on the first inference call of the day, discovered live,
during a demo, on whichever provider the chain had to fall back to.

This script asks each provider what it serves and compares. Model listings are
free: no tokens, no inference quota.

Run from `ai/`. Exit code 1 if any configured model is not offered.
"""

from __future__ import annotations

import json
import ssl
import sys
import urllib.error
import urllib.request
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
sys.path.insert(0, str(Path(__file__).resolve().parent.parent.parent))

from ai.config import get_settings  # noqa: E402
from ai.llm.chain import PROVIDER_DEFAULTS  # noqa: E402

UA = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
    "(KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36"
)

CTX = ssl.create_default_context()
CTX.check_hostname = False
CTX.verify_mode = ssl.CERT_NONE


def _get(url: str, headers: dict[str, str], timeout: int = 25):
    req = urllib.request.Request(url, headers={"User-Agent": UA, **headers})
    try:
        with urllib.request.urlopen(req, timeout=timeout, context=CTX) as resp:
            return resp.status, json.loads(resp.read().decode("utf-8", "replace"))
    except urllib.error.HTTPError as exc:
        return exc.code, exc.read().decode("utf-8", "replace")[:200]
    except Exception as exc:  # noqa: BLE001 - the failure is the finding
        return -1, f"{type(exc).__name__}"


def _ids(payload) -> set[str]:
    """Normalise ids. Gemini returns `models/gemini-3.8-flash`, others bare."""
    found: set[str] = set()
    if isinstance(payload, dict):
        payload = payload.get("data") or payload.get("models") or []
    if isinstance(payload, list):
        for m in payload:
            if isinstance(m, dict):
                raw = m.get("id") or m.get("name")
                if raw:
                    found.add(str(raw).removeprefix("models/"))
            elif isinstance(m, str):
                found.add(m.removeprefix("models/"))
    return found


def main() -> int:
    settings = get_settings()
    key_vars = {
        "gemini": "llm_gemini_api_keys",
        "cerebras": "llm_cerebras_api_keys",
        "groq": "llm_groq_api_keys",
        "nvidia": "llm_nvidia_api_keys",
        "mistral": "llm_mistral_api_keys",
        "cohere": "llm_cohere_api_keys",
        "openrouter": "llm_openrouter_api_keys",
    }
    endpoints = {
        "gemini": "https://generativelanguage.googleapis.com/v1beta/models?key={k}",
        "cerebras": "https://api.cerebras.ai/v1/models",
        "groq": "https://api.groq.com/openai/v1/models",
        "nvidia": "https://integrate.api.nvidia.com/v1/models",
        "mistral": "https://api.mistral.ai/v1/models",
        "cohere": "https://api.cohere.com/v1/models",
        "openrouter": "https://openrouter.ai/api/v1/models",
    }

    print(f"{'provider':<11} {'key':>5} {'models':>7}  {'default model':<32} verdict")
    print("-" * 88)

    broken: list[tuple[str, str]] = []
    unreachable: list[str] = []

    for name in PROVIDER_DEFAULTS:
        raw = getattr(settings, key_vars.get(name, ""), "") or ""
        key = next((k.strip() for k in raw.split(",") if k.strip()), "")
        if not key:
            print(f"{name:<11} {'--':>5} {'--':>7}  {'(no key)':<32} skipped")
            continue

        configured = getattr(settings, f"llm_{name}_model", "") or PROVIDER_DEFAULTS[name][1]
        url = endpoints[name].format(k=key)
        headers = {} if name == "gemini" else {"Authorization": f"Bearer {key}"}
        status, payload = _get(url, headers)

        if status != 200:
            print(f"{name:<11} {'ok':>5} {'--':>7}  {configured:<32} LISTING HTTP {status}")
            unreachable.append(name)
            continue

        ids = _ids(payload)
        ok = configured in ids
        verdict = "offered" if ok else "NOT OFFERED"
        print(f"{name:<11} {'ok':>5} {len(ids):>7}  {configured:<32} {verdict}")
        if not ok:
            broken.append((name, configured))

    print()
    if broken:
        print("Configured but not served by the provider:")
        for name, model in broken:
            print(f"  {name:<11} {model}")
    if unreachable:
        print("Could not reach the listing endpoint (key validity unknown):")
        for name in unreachable:
            print(f"  {name:<11} -- see the HTTP status above")

    return 1 if broken else 0


if __name__ == "__main__":
    sys.exit(main())
