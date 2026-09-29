#!/bin/sh
# AI-072: reproducible win-split protocol over seeded AI matches.
#
# Runs 20 seeds (1..20) in each of the three EventDump modes:
#   base   — seat 0 = Zeus starter,   seat 1 = Poseidon starter
#   swap   — seat 0 = Poseidon starter, seat 1 = Zeus starter
#   mirror — seat 0 = Zeus starter,    seat 1 = Zeus starter (seat-bias control)
# Every dump is validated against the AI-062 board-events wire format.
# Analysis only — TubaExperiment stays read-only; engine/bots/rules are
# untouched, only the harness deck assignment varies by mode.
#
# Usage: ./run-balance.sh [out-dir] [seeds]
#   out-dir defaults to $SCRIPT_DIR/balance-dumps; seeds defaults to 20
#   (seeds are 1..N). Same seed+mode is byte-identical across runs.
#
# Prereq: infinite-conquest-alpha-*.jar built (./build-release.sh or regress.sh).
set -eu

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
RELEASE_DIR="$(CDPATH= cd -- "$SCRIPT_DIR/../.." && pwd)"
BOARD_EVENTS="$RELEASE_DIR/../../docs/muse/sprint-02/board-events"
OUT_DIR="${1:-$SCRIPT_DIR/balance-dumps}"
NSEEDS="${2:-20}"

JAR="$(ls "$RELEASE_DIR"/infinite-conquest-alpha-*.jar 2>/dev/null | head -1 || true)"
if [ -z "$JAR" ]; then
  echo "run-balance: no release JAR found in $RELEASE_DIR — run ./build-release.sh first" >&2
  exit 1
fi
echo "run-balance: using $JAR"

command -v javac >/dev/null || { echo "run-balance: javac not found (need JDK 17+)" >&2; exit 1; }
command -v python3 >/dev/null || { echo "run-balance: python3 not found" >&2; exit 1; }

case "$NSEEDS" in
  ''|*[!0-9]*) echo "run-balance: seeds must be a positive integer" >&2; exit 1;;
esac

mkdir -p "$OUT_DIR" "$SCRIPT_DIR/classes"
javac -encoding UTF-8 -nowarn -cp "$JAR" -d "$SCRIPT_DIR/classes" "$SCRIPT_DIR/EventDump.java"
echo "run-balance: compiled"

SUMMARY="$OUT_DIR/summary.tsv"
printf 'mode\tseed\twinner\tevents\tturns\n' > "$SUMMARY"

fail=0
for mode in base swap mirror; do
  seed=1
  while [ "$seed" -le "$NSEEDS" ]; do
    dump="$OUT_DIR/dump-$mode-$seed.jsonl"
    line="$(java -cp "$SCRIPT_DIR/classes:$JAR" EventDump "$seed" "$dump" "--mode=$mode" \
      || { echo "run-balance: FAILED mode=$mode seed=$seed" >&2; fail=1; continue; })"
    winner="$(printf '%s' "$line" | sed -n 's/.*winner: \([0-9]*\).*/\1/p')"
    events="$(wc -l < "$dump" | tr -d ' ')"
    # fold the JSONL into a JSON array for validate.py
    as_json="$OUT_DIR/dump-$mode-$seed.json"
    python3 -c "import json,sys; json.dump([json.loads(l) for l in open(sys.argv[1]) if l.strip()], open(sys.argv[2],'w'))" \
      "$dump" "$as_json"
    python3 "$BOARD_EVENTS/validate.py" "$BOARD_EVENTS/event-schema.json" "$as_json" >/dev/null \
      || { echo "run-balance: VALIDATION FAILED mode=$mode seed=$seed" >&2; fail=1; }
    # wire 'turn' is 1-based clamped; max turn in the dump
    turns="$(python3 -c "import json;print(max(e['turn'] for e in json.load(open('$as_json'))))")"
    rm -f "$as_json"
    printf '%s\t%s\t%s\t%s\t%s\n' "$mode" "$seed" "$winner" "$events" "$turns" >> "$SUMMARY"
    seed=$((seed + 1))
  done
done

if [ "$fail" -ne 0 ]; then
  echo "run-balance: FAILED" >&2
  exit 1
fi

echo
echo "mode   | zeus_wins | poseidon_wins | draws | n"
echo "-------+-----------+---------------+-------+---"
for mode in base swap mirror; do
  # In mirror mode both seats hold the Zeus starter; report seat wins instead.
  if [ "$mode" = "mirror" ]; then
    awk -F'\t' -v m="$mode" '$1==m { if ($3=="0") s0++; else if ($3=="1") s1++; else d++; } \
      END { printf "mirror | seat0=%-6d| seat1=%-9d| draws=%-3d| n=%d\n", s0, s1, d, s0+s1+d }' "$SUMMARY"
  else
    # base: Zeus = seat 0; swap: Zeus = seat 1
    seat="$([ "$mode" = "base" ] && echo 0 || echo 1)"
    awk -F'\t' -v m="$mode" -v z="$seat" '$1==m { if ($3==z) zw++; else if ($3=="draw") d++; else pw++; } \
      END { printf "%-6s | zeus=%-7d| poseidon=%-6d| draws=%-3d| n=%d\n", m, zw, pw, d, zw+pw+d }' "$SUMMARY"
  fi
done
echo
echo "run-balance: $((3 * NSEEDS)) dumps in $OUT_DIR, all VALID; detail in $SUMMARY"
