#!/usr/bin/env bash
# Rules-core regression suite: replays every recorded case against the pinned engine.
#   clean/          full games whose state hash after every intent must reproduce exactly
#   known-findings/ shrunk repros of open engine findings; they PASS while the finding still
#                   reproduces. When ChatGPT fixes one, its case starts failing on purpose:
#                   move it to clean/ (re-record with `RulesSoak replay`) once the fix is accepted.
# Usage: ./test.sh [engine-dir]   (default engine/0.4.5)
set -euo pipefail
HERE=$(cd "$(dirname "$0")" && pwd)
ENGINE=$(cd "${1:-$HERE/engine/0.4.5}" && pwd)
VER=$(basename "$ENGINE")
(cd "$ENGINE" && sha256sum -c --quiet SHA256SUMS.txt)
OUT=$(mktemp -d)
dotnet build "$HERE/RulesSoak.csproj" -c Release -p:EngineDir="$ENGINE/bin" -o "$OUT/bin" >/dev/null
"$OUT/bin/RulesSoak" suite --data "$ENGINE/data" --dir "$HERE/cases/$VER"
