#!/bin/sh
# AI-108-OPENING: run the structures-first opening probe and print the tables
# used in docs/muse/sprint-03/2026-10-09-ai-108-opening-probe.md.
# Prereq: ../../infinite-conquest-alpha-0.7.15.jar (./build-release.sh).
# Usage: ./run.sh [seeds]   (default 1000; ~8 min on 4 cores)
set -eu
DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
JAR="$DIR/../../infinite-conquest-alpha-0.7.15.jar"
SEEDS="${1:-1000}"
OUT="$DIR/build"
rm -rf "$OUT"; mkdir -p "$OUT/classes" "$OUT/out" "$OUT/lever"
javac -cp "$JAR" -d "$OUT/classes" "$DIR/OpeningProbe.java"
CP="$JAR:$OUT/classes"
{
for s in on off; do for d in HERO MORTAL; do for f in "ZEUS POSEIDON" "POSEIDON ZEUS"; do
  n="$s-$d-$(echo $f | tr ' ' -)"
  echo "java -cp $CP OpeningProbe $s $d $f 1 $SEEDS > $OUT/out/$n.csv"
done; done; done
for v in mull deck16 deck16mull; do for f in "ZEUS POSEIDON" "POSEIDON ZEUS"; do
  echo "java -cp $CP OpeningProbe on HERO $f 1 $SEEDS $v > $OUT/lever/on-HERO-$(echo $f | tr ' ' -)-$v.csv"
done; done
} | xargs -P 4 -I{} sh -c '{} 2>/dev/null'
cp "$OUT"/out/on-HERO-*.csv "$OUT/lever/"
python3 "$DIR/analyze.py" "$OUT/out"
python3 "$DIR/levers.py" "$OUT/lever" "$OUT/out"
