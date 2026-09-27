#!/bin/bash
# fetch-source.sh — fetch the pinned alpha source READ-ONLY into the build area.
#
# Clones nothing from your working tree and pushes nothing anywhere: it
# creates <dest> (default ./build/alpha-src), fetches exactly one commit
# from GitHub, checks it out, and FAILS unless the checked-out commit equals
# the pin. The upstream repo is never modified.
#
# Usage:  ./fetch-source.sh [dest-dir]
# Env:    ALPHA_PIN  (default 992bc95c7164416ea0a25a4ce120f6ec0a0a167a)
#         ALPHA_REPO (default https://github.com/Mrice90/TubaExperiment.git)
set -euo pipefail

PIN="${ALPHA_PIN:-992bc95c7164416ea0a25a4ce120f6ec0a0a167a}"
REPO="${ALPHA_REPO:-https://github.com/Mrice90/TubaExperiment.git}"
DEST="${1:-./build/alpha-src}"

if [ -d "$DEST/.git" ]; then
    echo "== reusing existing checkout at $DEST =="
else
    echo "== fetching pinned source =="
    echo "repo: $REPO"
    echo "pin:  $PIN"
    rm -rf "$DEST"
    mkdir -p "$DEST"
    git init -q "$DEST"
    git -C "$DEST" remote add origin "$REPO"
    git -C "$DEST" fetch -q --depth 1 origin "$PIN"
    git -C "$DEST" checkout -q FETCH_HEAD
fi

HEAD="$(git -C "$DEST" rev-parse HEAD)"
echo "checked out: $HEAD"
if [ "$HEAD" != "$PIN" ]; then
    echo "ERROR: checkout ($HEAD) does not match pin ($PIN); refusing to continue." >&2
    exit 1
fi
echo "OK: source matches pin $PIN (read-only; upstream untouched)"
