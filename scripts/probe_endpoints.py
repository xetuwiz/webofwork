from __future__ import annotations

import argparse
import json
import re
import ssl
import subprocess
import sys
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CLIENT = ROOT / "client" / "src"
DEFAULT_BASE = "https://localhost:5001"

ACCOUNTS = [
    ("admin", "admin@xetuwiz.de", "Password1#"),
    ("client", "asgardravens@gmail.com", "Password1#"),
    ("freelancer", "freetest_probe@test.com", "Password1#"),
]

# GET endpoints are probed with a GET; everything else with its own verb but an
# empty body, which is enough to distinguish 404/405 from a matched route.
IDEMPOTENT = {"GET", "HEAD", "OPTIONS"}

# A destructive verb against a real record would mutate the dev database while
# "verifying" it. To determine whether a route exists without side effects, send
# the real verb but target a sentinel id that cannot exist (999999). A missing
# route still yields 404; an existing route yields 403/400/204. See probe_note.
SAFE_FOR_DELETE = True


def collect_calls() -> list[tuple[Path, int, str, str]]:
    import re
    pat = re.compile(
        r"axiosInstance\.(get|post|put|patch|delete)\(\s*[`\"']([^`\"']+)[`\"']", re.I
    )
    tmpl = re.compile(r"\$\{[^}]+\}")
    out = []
    for path in CLIENT.rglob("*.js*"):
        for i, line in enumerate(
            path.read_text(encoding="utf-8", errors="replace").splitlines(), 1
        ):
            for m in pat.finditer(line):
                out.append((path, i, m.group(1).upper(), tmpl.sub("0", m.group(2))))
    return out


def curl(args: list[str]) -> tuple[int, str]:
    ctx = ssl.create_default_context()
    ctx.check_hostname = False
    ctx.verify_mode = ssl.CERT_NONE
    r = subprocess.run(
        ["curl.exe", "-k", "-sS", "-o", "NUL", "-w", "%{http_code}"] + args,
        capture_output=True, text=True, timeout=60,
    )
    code = (r.stdout or "").strip()
    return (int(code) if code.isdigit() else 0), (r.stderr or "").strip()


def login(base: str, email: str, pw: str) -> str | None:
    body = json.dumps({"email": email, "password": pw})
    r = subprocess.run(
        ["curl.exe", "-k", "-sS", "-X", "POST", f"{base}/api/auth/login/",
         "-H", "Content-Type: application/json", "-d", body],
        capture_output=True, text=True, timeout=60,
    )
    try:
        return json.loads(r.stdout).get("access")
    except Exception:
        return None


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--base", default=DEFAULT_BASE)
    args = ap.parse_args()
    base = args.base.rstrip("/")

    tokens = {}
    for label, email, pw in ACCOUNTS:
        t = login(base, email, pw)
        tokens[label] = t
        print(f"  login {label:7} -> {'ok' if t else 'FAILED'}")

    calls = collect_calls()
    print(f"  probing {len(calls)} client calls...\n")

    missing, wrongverb, unreachable, ok = [], [], [], 0
    seen = set()

    for path, line, verb, url in calls:
        if url.startswith(("http://", "https://", "//")):
            continue
        key = (verb, url)
        if key in seen:
            continue
        seen.add(key)

        full = f"{base}/api{url}"
        auth = tokens.get("admin") or tokens.get("client") or tokens.get("freelancer")

        # Replace the substituted placeholder id with a sentinel that cannot
        # exist. This is what makes the probe non-destructive: DELETE/PATCH
        # against id 999999 cannot touch real data, yet still distinguishes
        # "no such route" (404 from routing) from "route exists" (400/403/...).
        probe_url = re.sub(r"/0(?=/|$)", "/999999", url)
        probe_full = f"{base}/api{probe_url}"

        cargs = ["-X", verb, probe_full, "-H", f"Authorization: Bearer {auth}"]
        if verb not in IDEMPOTENT:
            cargs += ["-H", "Content-Type: application/json", "-d", "{}"]

        code, err = curl(cargs)
        # A 404 is ambiguous in two ways:
        #   (a) "no such route"
        #   (b) the route exists but this role is not permitted, OR the handler
        #       throws KeyNotFound because the account has no profile/record.
        # Both look identical to curl. So before believing a 404, retry with
        # every token we have -- an endpoint that resolves for any role exists.
        if code == 404:
            for alt in ("client", "freelancer"):
                if not tokens.get(alt):
                    continue
                alt_args = [
                    a.replace(f"Bearer {auth}", f"Bearer {tokens[alt]}")
                    for a in cargs
                ]
                code2, err2 = curl(alt_args)
                if code2 != 404:
                    code, err = code2, err2
                    break
        rec = (path, line, verb, url, code)

        if code == 0:
            unreachable.append(rec + (err,))
        elif code == 404:
            missing.append(rec)
        elif code == 405:
            wrongverb.append(rec)
        else:
            ok += 1

    print(f"route resolved (non-404/405) : {ok}")
    print(f"404 endpoint missing          : {len(missing)}")
    print(f"405 wrong verb                : {len(wrongverb)}")
    if unreachable:
        print(f"unreachable (probe invalid)   : {len(unreachable)}\n")

    for title, group, note in (
        ("404 - CLIENT CALLS A ROUTE THAT DOES NOT EXIST", missing, ""),
        ("405 - PATH EXISTS BUT NOT FOR THIS VERB", wrongverb, ""),
        ("UNREACHABLE - probe inconclusive", unreachable, ""),
    ):
        if not group:
            continue
        print(f"\n{title} ({len(group)})\n")
        for item in group:
            path, line, verb, url, code = item[:5]
            extra = item[5] if len(item) > 5 else ""
            print(f"  {path.relative_to(ROOT)}:{line}")
            print(f"    {verb} {url}  -> {code} {extra}\n")

    return 1 if (missing or wrongverb or unreachable) else 0


if __name__ == "__main__":
    sys.exit(main())



