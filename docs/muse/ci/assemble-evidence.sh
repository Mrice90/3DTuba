#!/usr/bin/env bash
# assemble-evidence.sh — fold qa-evidence/results.jsonl into qa-evidence.json
# with run metadata. Runs at $GITHUB_WORKSPACE via `bash ...` (see verify.yml).
# Always exits 0: a broken bundle must not mask the checks themselves.
set -u
ws="${GITHUB_WORKSPACE:?GITHUB_WORKSPACE not set}"
ws="${ws//\\//}"
ev="$ws/qa-evidence"
mkdir -p "$ev"
python3 - "$ev" <<'PYEOF'
import json, os, sys
ev = sys.argv[1]
checks = []
rp = os.path.join(ev, "results.jsonl")
if os.path.exists(rp):
    for line in open(rp):
        line = line.strip()
        if line:
            try:
                checks.append(json.loads(line))
            except json.JSONDecodeError:
                pass
doc = {
    "workflow": os.environ.get("GITHUB_WORKFLOW", "verify.yml"),
    "run_id": os.environ.get("GITHUB_RUN_ID"),
    "sha": os.environ.get("GITHUB_SHA"),
    "ref": os.environ.get("GITHUB_REF"),
    "os": os.environ.get("RUNNER_OS"),
    "checks": checks,
    "passed": bool(checks) and all(c.get("exit") == 0 for c in checks),
}
with open(os.path.join(ev, "qa-evidence.json"), "w") as f:
    json.dump(doc, f, indent=1)
print("evidence: %d checks recorded, passed=%s" % (len(checks), doc["passed"]))
PYEOF
exit 0
