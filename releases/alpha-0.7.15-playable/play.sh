#!/bin/sh
# play.sh — launch the Infinite Conquest alpha (macOS / Linux).
# Expects infinite-conquest-alpha-0.7.15.jar next to this script.
set -eu
DIR="$(cd "$(dirname "$0")" && pwd)"
JAR="$DIR/infinite-conquest-alpha-0.7.15.jar"

if ! command -v java >/dev/null 2>&1; then
    echo "ERROR: java not found — install JDK 17+ (Temurin/Adoptium), then re-run." >&2
    exit 1
fi
if [ ! -s "$JAR" ]; then
    echo "ERROR: $JAR not found." >&2
    echo "The jar is NOT stored in this repository (a repo rule rejects 90MB+ blobs)." >&2
    echo "Get it from the release handoff, place it next to play.sh, and verify it:" >&2
    echo "  sha256sum -c CHECKSUMS.sha256" >&2
    echo "Or rebuild it from the pinned source: ./fetch-source.sh && ./build-release.sh" >&2
    exit 1
fi
if [ -f "$DIR/CHECKSUMS.sha256" ]; then
    if ! (cd "$DIR" && sha256sum -c CHECKSUMS.sha256); then
        echo "ERROR: jar checksum mismatch — do not run it; re-fetch or rebuild." >&2
        exit 1
    fi
fi
exec java -jar "$JAR" "$@"
