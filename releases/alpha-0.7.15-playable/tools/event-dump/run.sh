#!/bin/sh
# AI-066: build the event-dump tool against the pinned alpha JAR, run seeded
# Zeus-vs-Poseidon AI matches, and validate the JSONL dumps against the
# AI-062 board-events wire format (validate.py must pass on all 3 seeds).
#
# Usage: ./run.sh [out-dir]
# Prereq: infinite-conquest-alpha-*.jar built (./build-release.sh or regress.sh).
set -eu

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
RELEASE_DIR="$(CDPATH= cd -- "$SCRIPT_DIR/../.." && pwd)"
BOARD_EVENTS="$RELEASE_DIR/../../docs/muse/sprint-02/board-events"
OUT_DIR="${1:-$SCRIPT_DIR/dumps}"

JAR="$(ls "$RELEASE_DIR"/infinite-conquest-alpha-*.jar 2>/dev/null | head -1 || true)"
if [ -z "$JAR" ]; then
  echo "event-dump: no release JAR found in $RELEASE_DIR — run ./build-release.sh first" >&2
  exit 1
fi
echo "event-dump: using $JAR"

command -v javac >/dev/null || { echo "event-dump: javac not found (need JDK 17+)" >&2; exit 1; }
command -v python3 >/dev/null || { echo "event-dump: python3 not found" >&2; exit 1; }

mkdir -p "$OUT_DIR" "$SCRIPT_DIR/classes"
javac -encoding UTF-8 -nowarn -cp "$JAR" -d "$SCRIPT_DIR/classes" "$SCRIPT_DIR/EventDump.java"
echo "event-dump: compiled"

SEEDS="42 1234 98765"
fail=0
for seed in $SEEDS; do
  dump="$OUT_DIR/dump-seed-$seed.jsonl"
  echo "event-dump: seed $seed -> $dump"
  java -cp "$SCRIPT_DIR/classes:$JAR" EventDump "$seed" "$dump" || { echo "event-dump: FAILED seed $seed" >&2; fail=1; continue; }
  # validate.py takes a JSON array; fold the JSONL into one for the check.
  as_json="$OUT_DIR/dump-seed-$seed.json"
  python3 -c "import json,sys; json.dump([json.loads(l) for l in open(sys.argv[1]) if l.strip()], open(sys.argv[2],'w'))" \
    "$dump" "$as_json"
  python3 "$BOARD_EVENTS/validate.py" "$BOARD_EVENTS/event-schema.json" "$as_json" \
    || { echo "event-dump: VALIDATION FAILED seed $seed" >&2; fail=1; }
  rm -f "$as_json"
done

if [ "$fail" -ne 0 ]; then
  echo "event-dump: FAILED" >&2
  exit 1
fi
echo "event-dump: all 3 seeds dumped and VALID"
