#!/bin/bash
# smoke.sh — runnable smoke checks for the built alpha jar.
# Usage:  ./smoke.sh [jar-path]   (default: ./infinite-conquest-alpha-0.7.15.jar)
# Env:    JAR_PATH  (alternative way to point at the jar)
# Exits 0 only if every check passes. The headless engine check runs 36
# seeded bot-vs-bot matches (~10s) through the real game code.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
JAR="${1:-${JAR_PATH:-$SCRIPT_DIR/infinite-conquest-alpha-0.7.15.jar}}"
case "$JAR" in
    /*) ;;
    *) JAR="$PWD/$JAR" ;;  # the engine check runs from /tmp; keep the path valid
esac
PASS=0; FAIL=0

check() { # check <name> <command...>
    local name="$1"; shift
    if "$@" >/tmp/smoke-out.txt 2>&1; then
        echo "PASS: $name"; PASS=$((PASS+1));
    else
        echo "FAIL: $name"; sed 's/^/      /' /tmp/smoke-out.txt | head -5; FAIL=$((FAIL+1));
    fi
}

command -v java >/dev/null || { echo "FAIL: java not found on PATH"; exit 1; }

echo "== smoke: $JAR =="
check "jar exists and is non-empty" test -s "$JAR"
check "manifest names the GUI entry point" \
    sh -c "unzip -p \"$JAR\" META-INF/MANIFEST.MF | grep -q 'Main-Class: com.infiniteconquest.gui.GameShell'"
check "entry-point class is inside the jar" \
    sh -c "unzip -l \"$JAR\" | grep -q 'com/infiniteconquest/gui/GameShell.class'"
check "card data resources are inside the jar" \
    sh -c "unzip -l \"$JAR\" | grep -q 'cards.*\.json'"
check "headless engine: 36 seeded bot matches complete" \
    sh -c "cd /tmp && java -cp \"$JAR\" com.infiniteconquest.cli.InfiniteConquestCli simulate 1 42 /tmp/smoke-report.json | grep -q 'Simulated 36 matches'"

echo "== result: $PASS passed, $FAIL failed =="
[ "$FAIL" -eq 0 ]
