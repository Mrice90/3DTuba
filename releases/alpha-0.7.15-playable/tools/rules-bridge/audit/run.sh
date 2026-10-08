#!/bin/sh
# Rules audit: seeded bot-vs-bot matches under both rule sets. At every sampled
# decision the legal-action list and the engine must agree both ways (every
# listed action accepted, every other well-formed action rejected), and board
# invariants must hold after every action. See RulesAudit.java.
#
# Usage: ./run.sh <alpha jar> [matches] [sampleEvery]
set -eu
HERE="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
JAR="$1"; MATCHES="${2:-20}"; EVERY="${3:-3}"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
python3 "$HERE/../build_classes.py" --jar "$JAR" --out "$WORK/classes"
javac -encoding UTF-8 -nowarn -cp "$WORK/classes:$JAR" -d "$WORK/classes" "$HERE/RulesAudit.java"
for rules in alpha ic3d; do
  java -cp "$WORK/classes:$JAR" RulesAudit "$rules" "$MATCHES" "$EVERY"
done
