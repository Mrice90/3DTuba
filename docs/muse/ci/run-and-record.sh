#!/usr/bin/env bash
# run-and-record.sh — run one QA check, keep its log, record its result.
# Usage: run-and-record.sh <check-slug> "<shell-command>"
#   - runs <shell-command> via bash -c (&& / ; chains keep their meaning)
#   - tees stdout+stderr to $GITHUB_WORKSPACE/qa-evidence/<check-slug>.log
#   - appends one JSON line to $GITHUB_WORKSPACE/qa-evidence/results.jsonl
#   - exits with the command's own exit code (pipefail + PIPESTATUS)
# Check slugs must match ^[a-z0-9-]+$. Works under Git Bash on Windows runners.
set -u
name="${1:?check slug required}"
cmd="${2:?command required}"
if [[ ! "$name" =~ ^[a-z0-9-]+$ ]]; then echo "bad check slug: $name" >&2; exit 2; fi
# Normalize Windows backslashes so Git Bash path handling stays sane.
ws="${GITHUB_WORKSPACE:?GITHUB_WORKSPACE not set}"
ws="${ws//\\//}"
ev="$ws/qa-evidence"
mkdir -p "$ev"
log="$ev/${name}.log"
start="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
set -o pipefail
bash -c "$cmd" 2>&1 | tee "$log"
code="${PIPESTATUS[0]}"
end="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
printf '{"check":"%s","exit":%d,"started_utc":"%s","ended_utc":"%s"}\n' \
  "$name" "$code" "$start" "$end" >> "$ev/results.jsonl"
exit "$code"
