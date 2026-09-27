#!/bin/sh
# play.sh — launch Infinite Conquest (macOS / Linux). Requires Java 17+.
cd "$(dirname "$0")"
if ! command -v java >/dev/null 2>&1; then
  echo "Java 17+ is required but was not found. Install Temurin 17 from https://adoptium.net"
  exit 1
fi
exec java -jar infinite-conquest-alpha-0.7.15.jar "$@"
