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

echo "== diagnostics =="
command -v git >/dev/null || die "git not found on PATH (needed for the pin check)"
command -v java >/dev/null || die "java not found on PATH — install JDK 17+ (Temurin/Adoptium)"
command -v javac >/dev/null || die "javac not found on PATH — install a full JDK 17+ (not just a JRE)"
command -v jar >/dev/null || die "'jar' tool not found on PATH — install a full JDK 17+"
JAVA_MAJOR="$(java -version 2>&1 | head -1 | sed -E 's/.*version "([0-9]+).*/\1/')"
[ "${JAVA_MAJOR:-0}" -ge 17 ] 2>/dev/null || die "java $JAVA_MAJOR is too old — JDK 17+ required"
echo "java: $(java -version 2>&1 | head -1)"

[ -d "$ALPHA/.git" ] || die "alpha source not found at $ALPHA — run ./fetch-source.sh first"
HEAD="$(git -C "$ALPHA" rev-parse HEAD)"
echo "alpha: $HEAD"
if [ "$HEAD" != "$PIN" ] && [ "${ALPHA_ALLOW_UNPINNED:-0}" != "1" ]; then
    die "alpha checkout ($HEAD) does not match release pin ($PIN). Set ALPHA_ALLOW_UNPINNED=1 to build it anyway (not the release recipe)."
fi

echo "== dependencies (pinned, hash-verified) =="
# The Jackson jars are NOT tracked in the upstream repo; fetch the exact
# artifacts from Maven Central and verify SHA-256 before use.
DEPS="$SCRIPT_DIR/build/deps"
MVN="https://repo1.maven.org/maven2/com/fasterxml/jackson/core"
JACKSON_VER="2.18.2"
# AI-052-WIN: no hardcoded hashes; the published .sha256 is fetched from
# Maven Central (cached in $DEPS) and the jar is verified against it.
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
        fetch "$url" "$DEPS/$jar" || die "download failed for $jar"
    fi
    if [ ! -f "$DEPS/$jar.sha256" ]; then
        echo "fetching $jar.sha256 ..."
        fetch "$url.sha256" "$DEPS/$jar.sha256" || die "could not fetch $jar.sha256"
    fi
    (cd "$DEPS" && sha256sum -c "$jar.sha256") || die "checksum mismatch for $jar (see $jar.sha256)"
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
  -d "$STAGE/classes" @"$STAGE/sources.txt"

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
jar --create --file "$STAGE/release/$JAR" \
    --manifest "$STAGE/manifest.txt" -C "$STAGE/classes" .

echo "== checksum =="
(cd "$STAGE/release" && sha256sum "$JAR" | tee CHECKSUMS.sha256)
cp "$STAGE/release/$JAR" "$SCRIPT_DIR/$JAR"
cp "$STAGE/release/CHECKSUMS.sha256" "$SCRIPT_DIR/CHECKSUMS.sha256"
echo "copied $JAR + CHECKSUMS.sha256 next to the launchers"

echo "== smoke =="
JAR_PATH="$SCRIPT_DIR/$JAR" bash "$SCRIPT_DIR/smoke.sh"

echo "== done =="
ls -la "$SCRIPT_DIR/$JAR" "$SCRIPT_DIR/CHECKSUMS.sha256"
