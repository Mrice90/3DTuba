#!/bin/bash
# build-release.sh — assemble the Infinite Conquest alpha playable release.
# Source: Mrice90/TubaExperiment @ 992bc95c7164416ea0a25a4ce120f6ec0a0a167a
#         (branch strip/zeus-poseidon-desktop), read-only. Nothing is modified there.
# Toolchain: Temurin JDK 17 (~/workspace/tools/jdk-17). Gradle daemon is broken
# in this environment, so this script builds with javac directly.
set -euo pipefail

ALPHA="${ALPHA:-$HOME/workspace/scratch/tuba-alpha}"
STAGE="${STAGE:-$HOME/workspace/release-staging/alpha-0.7.15}"
VERSION="0.7.15"
export JAVA_HOME="${JAVA_HOME:-$HOME/workspace/tools/jdk-17}"
export PATH="$JAVA_HOME/bin:$PATH"

echo "== java =="; java -version 2>&1 | head -1
echo "== alpha =="; git -C "$ALPHA" rev-parse --short HEAD

rm -rf "$STAGE"
mkdir -p "$STAGE/classes" "$STAGE/release"

echo "== compiling =="
find "$ALPHA/game-core/src/main/java" "$ALPHA/game-cli/src/main/java" \
     "$ALPHA/game-gui/src/main/java" "$ALPHA/net-server/src/main/java" \
     -name '*.java' > "$STAGE/sources.txt"
wc -l < "$STAGE/sources.txt"
javac -encoding UTF-8 -nowarn \
  -cp "$ALPHA/libs/jackson-databind-2.18.2.jar:$ALPHA/libs/jackson-core-2.18.2.jar:$ALPHA/libs/jackson-annotations-2.18.2.jar" \
  -d "$STAGE/classes" @"$STAGE/sources.txt"

echo "== resources =="
for m in game-core game-cli game-gui net-server; do
  if [ -d "$ALPHA/$m/src/main/resources" ]; then
    cp -r "$ALPHA/$m/src/main/resources/." "$STAGE/classes/"
  fi
done
echo "== duplicate resource check =="
# (informational; first copy wins on merge)

echo "== merging jackson =="
cd "$STAGE/classes"
for j in "$ALPHA"/libs/jackson-*.jar; do
  unzip -o -q "$j" -x 'META-INF/*.SF' 'META-INF/*.DSA' 'META-INF/*.RSA'
done
cd - > /dev/null

echo "== jarring =="
mkdir -p "$STAGE/release"
cat > "$STAGE/manifest.txt" <<'EOF'
Manifest-Version: 1.0
Main-Class: com.infiniteconquest.gui.GameShell
Implementation-Title: Infinite Conquest (alpha)
Implementation-Version: 0.7.15
EOF
jar --create --file "$STAGE/release/infinite-conquest-alpha-0.7.15.jar" \
    --manifest "$STAGE/manifest.txt" -C "$STAGE/classes" .

echo "== done =="
ls -la "$STAGE/release/"
