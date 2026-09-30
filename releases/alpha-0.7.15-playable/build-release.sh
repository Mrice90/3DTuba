#!/bin/bash
# build-release.sh — assemble the Infinite Conquest alpha playable release.
#
# Source: Mrice90/TubaExperiment @ 992bc95c7164416ea0a25a4ce120f6ec0a0a167a
#         (branch strip/zeus-poseidon-desktop), read-only. Nothing is modified there.
#         Run ./fetch-source.sh first to fetch the pinned source into ./build/alpha-src.
# Toolchain: JDK 17+ (Temurin/Adoptium recommended). Gradle is not used: the
# Gradle daemon does not run in the build environment, so this script builds
# with javac directly. Jackson 2.18.2 is fetched pinned+hash-verified from
# Maven Central (it is not tracked in the upstream repo).
#
# Env: ALPHA  (default ./build/alpha-src — must match the pin below)
#      STAGE  (default ./build/stage)
#      ALPHA_ALLOW_UNPINNED=1  to build a different checkout (not the release recipe)
set -euo pipefail

PIN="992bc95c7164416ea0a25a4ce120f6ec0a0a167a"
VERSION="0.7.15"
JAR="infinite-conquest-alpha-$VERSION.jar"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ALPHA="${ALPHA:-$SCRIPT_DIR/build/alpha-src}"
STAGE="${STAGE:-$SCRIPT_DIR/build/stage}"

die() { echo "ERROR: $*" >&2; exit 1; }
# AI-056: each verification check fails with its own exit code, so the
# regression harness can assert WHICH check fired — not just that the build
# failed. Codes:
#    1 diagnostics (missing tools, bad java, missing source)
#   20 source pin mismatch
#   21 dependency download failed
#   22 dependency .sha1 fetch failed
#   23 dependency .sha1 mismatch (transmission check)
#   24 dependency SHA-256 pin mismatch (supply-chain check, AI-055)
#   25 compilation/jarring failed
#   26 smoke test failed
die_code() { local code="$1"; shift; echo "ERROR: $*" >&2; exit "$code"; }

echo "== diagnostics =="
command -v git >/dev/null || die "git not found on PATH (needed for the pin check)"
command -v java >/dev/null || die "java not found on PATH — install JDK 17+ (Temurin/Adoptium)"
command -v javac >/dev/null || die "javac not found on PATH — install a full JDK 17+ (not just a JRE)"
command -v jar >/dev/null || die "'jar' tool not found on PATH — install a full JDK 17+"
# AI-058: read java.specification.version (not the first line of
# `java -version`), so JAVA_TOOL_OPTIONS noise ("Picked up ...") can't
# mislead the check — same approach as build-release.bat. The sed anchors
# to line start so java.vm.specification.version never matches.
JAVA_SPEC="$(java -XshowSettings:properties -version 2>&1 | sed -n 's/^[[:space:]]*java.specification.version = //p' | head -1 | tr -d '[:space:]')"
JAVA_MAJOR="$(printf '%s' "$JAVA_SPEC" | sed -E 's/^([0-9]+).*/\1/')"
[ "${JAVA_MAJOR:-0}" -ge 17 ] 2>/dev/null || die "java specification version ${JAVA_SPEC:-unknown} is too old — JDK 17+ required"
echo "java specification version: ${JAVA_SPEC:-unknown}"

[ -d "$ALPHA/.git" ] || die "alpha source not found at $ALPHA — run ./fetch-source.sh first"
HEAD="$(git -C "$ALPHA" rev-parse HEAD)"
echo "alpha: $HEAD"
if [ "$HEAD" != "$PIN" ] && [ "${ALPHA_ALLOW_UNPINNED:-0}" != "1" ]; then
    die_code 20 "alpha checkout ($HEAD) does not match release pin ($PIN). Set ALPHA_ALLOW_UNPINNED=1 to build it anyway (not the release recipe)."
fi

echo "== dependencies (pinned, hash-verified) =="
# The Jackson jars are NOT tracked in the upstream repo; fetch the exact
# artifacts from Maven Central and verify them before use.
#
# AI-055: supply-chain pin. The full SHA-256 of each jar is hardcoded below
# (verified 2026-09-28 against Maven Central; the jars also match their
# published .sha1). The fetched .sha1 is kept as a secondary transmission
# check, but the PIN is the trust anchor: a malicious mirror can serve a
# self-consistent jar+.sha1 pair, and only the pin catches that
# (see regress.sh --break=depswap). When Jackson is bumped, update these
# pins with review — never delete the check.
DEPS="$SCRIPT_DIR/build/deps"
MVN="https://repo1.maven.org/maven2/com/fasterxml/jackson/core"
JACKSON_VER="2.18.2"
pin_for() { # AI-055: SHA-256 pin per artifact
    case "$1" in
        jackson-databind)    echo "4b364e6850dc89172fcf1d4dd26b8ff5488eda44ff4657e22dd265203dd5ab3c" ;;
        jackson-core)        echo "d8054ae7c0d1c2d2f55d28e46026ebe5892881f3fab5f439233184381c3b4a1f" ;;
        jackson-annotations) echo "581bd61000ef7648943f781ca05689e56d03f6052748365a8e2b3a9b5d3fa32f" ;;
    esac
}
mkdir -p "$DEPS"
fetch() {
    if command -v curl >/dev/null; then curl -sSL --max-time 180 -o "$2" "$1";
    elif command -v wget >/dev/null; then wget -q -O "$2" "$1";
    else die "neither curl nor wget found — cannot fetch dependencies"; fi
}
for art in jackson-databind jackson-core jackson-annotations; do
    jar="$art-$JACKSON_VER.jar"
    url="$MVN/$art/$JACKSON_VER/$jar"
    if [ ! -f "$DEPS/$jar" ]; then
        echo "fetching $jar ..."
        fetch "$url" "$DEPS/$jar" || die_code 21 "download failed for $jar"
    fi
    if [ ! -f "$DEPS/$jar.sha1" ]; then
        echo "fetching $jar.sha1 ..."
        fetch "$url.sha1" "$DEPS/$jar.sha1" || die_code 22 "could not fetch $jar.sha1"
    fi
    # Secondary check: Maven .sha1 files contain just the hash; compare manually.
    exp=$(awk '{print $1}' "$DEPS/$jar.sha1")
    got=$(sha1sum "$DEPS/$jar" | awk '{print $1}')
    [ -n "$exp" ] && [ "$exp" = "$got" ] || die_code 23 "checksum mismatch for $jar (expected $exp, got $got)"
    # AI-055: primary trust anchor — pinned SHA-256, fail closed.
    pin="$(pin_for "$art")"
    got256=$(sha256sum "$DEPS/$jar" | awk '{print $1}')
    [ -n "$pin" ] && [ "$pin" = "$got256" ] || die_code 24 "SHA-256 pin mismatch for $jar (supply-chain check failed; expected $pin, got $got256)"
    echo "verified $jar (sha1 + pinned sha256)"
done
echo "dependencies verified"
CP_JARS="$DEPS/jackson-databind-$JACKSON_VER.jar:$DEPS/jackson-core-$JACKSON_VER.jar:$DEPS/jackson-annotations-$JACKSON_VER.jar"

export JAVA_HOME="${JAVA_HOME:-$(dirname "$(dirname "$(command -v javac)")")}"
export PATH="$JAVA_HOME/bin:$PATH"

rm -rf "$STAGE"
mkdir -p "$STAGE/classes" "$STAGE/release"

echo "== compiling =="
find "$ALPHA/game-core/src/main/java" "$ALPHA/game-cli/src/main/java" \
     "$ALPHA/game-gui/src/main/java" "$ALPHA/net-server/src/main/java" \
     -name '*.java' > "$STAGE/sources.txt"
echo "sources: $(wc -l < "$STAGE/sources.txt")"
javac -encoding UTF-8 -nowarn \
  -cp "$CP_JARS" \
  -d "$STAGE/classes" @"$STAGE/sources.txt" || die_code 25 "compilation failed (javac)"

echo "== resources =="
for m in game-core game-cli game-gui net-server; do
  if [ -d "$ALPHA/$m/src/main/resources" ]; then
    cp -r "$ALPHA/$m/src/main/resources/." "$STAGE/classes/"
  fi
done

echo "== merging jackson =="
cd "$STAGE/classes"
for j in "$DEPS"/jackson-*.jar; do
  unzip -o -q "$j" -x 'META-INF/*.SF' 'META-INF/*.DSA' 'META-INF/*.RSA'
done
cd - > /dev/null

echo "== jarring =="
cat > "$STAGE/manifest.txt" <<EOF
Manifest-Version: 1.0
Main-Class: com.infiniteconquest.gui.GameShell
Implementation-Title: Infinite Conquest (alpha)
Implementation-Version: $VERSION
EOF
# AI-078/AI-099: reproducible JAR via tools/make-repro-jar.py — fixed timestamps
# (2026-01-01 00:00:00 UTC), sorted entries, fixed mode bits and an
# LF-normalized manifest, so two builds give the same SHA-256 on any OS.
python3 "$SCRIPT_DIR/tools/make-repro-jar.py" "$STAGE/release/$JAR" "$STAGE/manifest.txt" "$STAGE/classes" \
    || die_code 25 "reproducible jarring failed"

echo "== checksum =="
(cd "$STAGE/release" && sha256sum "$JAR" | tee CHECKSUMS.sha256)
cp "$STAGE/release/$JAR" "$SCRIPT_DIR/$JAR"
# AI-078/AI-099: the build no longer rewrites the tracked CHECKSUMS.sha256 —
# it is the canonical release checksum, updated deliberately (see
# PROVENANCE.md), never per build. The checksum of THIS build lives in the
# staging area ($STAGE/release/CHECKSUMS.sha256) for verification; regress.sh
# verifies the final jar against it.
echo "copied $JAR next to the launchers (this build's CHECKSUMS.sha256 stays in $STAGE/release/)"

echo "== smoke =="
JAR_PATH="$SCRIPT_DIR/$JAR" bash "$SCRIPT_DIR/smoke.sh" || die_code 26 "smoke test failed (smoke.sh)"

echo "== done =="
ls -la "$SCRIPT_DIR/$JAR"
