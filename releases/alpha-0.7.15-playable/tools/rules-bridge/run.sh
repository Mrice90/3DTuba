#!/bin/sh
# AI-079: build the rules bridge against the pinned alpha JAR and run the
# protocol tests (determinism, valid act, stale/fake rejection, no mutation
# on rejection, bot auto-play, scripted GAME_OVER, AI-062 validation of
# every event, hidden-state redaction). Writes the golden transcript
# fixture (fixtures/golden-seed-42.jsonl).
#
# Usage: ./run.sh
# Prereq: infinite-conquest-alpha-*.jar built (./build-release.sh or regress.sh).
set -eu

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
exec python3 "$SCRIPT_DIR/test_bridge.py"
