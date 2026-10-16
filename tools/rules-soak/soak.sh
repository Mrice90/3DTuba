#!/usr/bin/env bash
# Parallel seeded soak + cross-process determinism check for the native rules core.
# Usage: ./soak.sh <engine-dir> <first-seed> <last-seed> [out-dir] [workers]
#   engine-dir holds bin/InfiniteConquest.{RulesCore,Native}.dll and data/ (cards/, faction-starters.json,
#   structure-slots.properties), e.g. engine/current.
# Extra args for every soak run can be passed with SOAK_ARGS, e.g. SOAK_ARGS='--extra {"board":"hexagon"}'.
set -euo pipefail
ENGINE=$(cd "$1" && pwd); A=$2; B=$3; OUT=${4:-soak-out}; W=${5:-$(nproc)}
HERE=$(cd "$(dirname "$0")" && pwd)
mkdir -p "$OUT"; OUT=$(cd "$OUT" && pwd)
dotnet build "$HERE/RulesSoak.csproj" -c Release -p:EngineDir="$ENGINE/bin" -o "$OUT/bin" >/dev/null
BIN="$OUT/bin/RulesSoak"
shard() { # mode first last outdir
  "$BIN" soak --data "$ENGINE/data" --mode "$1" --seeds "$2-$3" --out "$4" ${SOAK_ARGS:-} > "$4/log-$1-$2-$3.txt" 2>&1
}
run_pass() { # passdir
  mkdir -p "$1"
  local n=$(( (B - A + W) / W )) pids=()
  for ((s = A; s <= B; s += n)); do
    e=$(( s + n - 1 < B ? s + n - 1 : B ))
    shard mp "$s" "$e" "$1" & pids+=($!)
  done
  for ((s = A; s <= B; s += n)); do
    e=$(( s + n - 1 < B ? s + n - 1 : B ))
    shard bots "$s" "$e" "$1" & pids+=($!)
  done
  wait "${pids[@]}"
}
run_pass "$OUT/pass1"
# Second pass in fresh processes. .NET randomises string hash codes per process, so any rules
# outcome that depends on hash ordering shows up as a digest difference here.
run_pass "$OUT/pass2"
digest() { cat "$1"/summary-*.tsv | grep -v '^seed' | cut -f1-4,10,11 | sort; }
if diff <(digest "$OUT/pass1") <(digest "$OUT/pass2") > "$OUT/determinism.diff"; then
  echo "determinism: identical result, final hash and hash trail in both passes"
else
  echo "determinism: DIFFERENCES, see $OUT/determinism.diff"
fi
cat "$OUT"/pass1/summary-*.tsv | grep -v '^seed' | cut -f4 | sort | uniq -c
