#!/bin/bash
# regress.sh — bounded AI-004/AI-048 packaging regression for the alpha release recipe.
#
# Runs the full packaging pipeline (fetch-source.sh -> build-release.sh, which
# auto-runs smoke.sh) inside an isolated temp copy of this directory, then
# verifies the freshly built JAR against its OWN freshly generated
# CHECKSUMS.sha256. Rebuilds are content-equivalent, not byte-identical
# (JAR timestamps/ordering vary), so comparing against an unrelated reference
# artifact is wrong — each build is checked against itself.
#
# Stages and failure detection: every stage's exit status is checked and the
# stage is named on failure. Final verdict:
#   REGRESSION: PASS                     (exit 0)
#   REGRESSION: FAIL at <stage> (<why>)  (exit 1)
#
# Intentional-break modes (prove failure detection works):
#   --break=pin        move the temp source tree off the release pin; the build
#                      must fail closed at pin verification (ALPHA_ALLOW_UNPINNED
#                      is unset).
#   --break=dep        corrupt a dependency jar in the temp tree; the build's
#                      hash verification must fail.
#   --break=compile    inject a syntax error into one temp source file; javac
#                      must fail.
#   --break=checksum   corrupt the built JAR after smoke; the self-checksum
#                      verification must fail.
#   --break=smoke      remove card data resources from the temp tree; the
#                      build's smoke step must fail (underlying smoke.sh
#                      non-zero exit).
# In --break mode the harness EXPECTS the pipeline to fail at that stage:
#   REGRESSION: intentional break correctly detected at '<stage>'  (exit 0)
# If the pipeline passes despite the break:
#   REGRESSION: BREAK NOT DETECTED  (exit 1)
#
# Env: REGRESS_KEEP=1 keeps the temp dir for inspection.
# Upstream repos are only fetched (read-only); nothing is pushed anywhere.
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BREAK_MODE=""
for arg in "$@"; do
  case "$arg" in
    --break=*) BREAK_MODE="${arg#--break=}" ;;
    -h|--help) sed -n '2,26p' "$0"; exit 0 ;;
    *) echo "ERROR: unknown argument: $arg" >&2; exit 2 ;;
  esac
done
case "$BREAK_MODE" in ""|pin|dep|compile|checksum|smoke) ;; *) echo "ERROR: --break must be pin, dep, compile, checksum, or smoke" >&2; exit 2 ;; esac

# A known non-pin commit on the same branch, used only for the --break=pin
# fault injection (checked out inside the temp dir; upstream untouched).
OFF_PIN="a833daa039b9e02f5375c2dfe545623d763c132a"

WORK="$(mktemp -d "${TMPDIR:-/tmp}/alpha-regress-XXXXXX")"
echo "regress: work dir $WORK"
cleanup() {
  if [ "${REGRESS_KEEP:-0}" = "1" ]; then echo "regress: kept $WORK"; return; fi
  rm -rf "$WORK"
}
trap cleanup EXIT

# Fresh copy of the packaging dir, with any previous build output removed so
# the regression always starts clean.
cp -a "$SCRIPT_DIR"/. "$WORK"/
rm -rf "$WORK/build" "$WORK"/*.jar
# A pristine clone may not carry the exec bit (e.g. archives); ensure the
# recipe scripts are runnable in the isolated copy.
chmod +x "$WORK"/*.sh 2>/dev/null || true
cd "$WORK"

case "$BREAK_MODE" in
  pin)      EXPECT_FAIL_AT="build" ;;
  dep)      EXPECT_FAIL_AT="build" ;;
  compile)  EXPECT_FAIL_AT="build" ;;
  checksum) EXPECT_FAIL_AT="verify" ;;
  smoke)    EXPECT_FAIL_AT="build" ;;
  *)        EXPECT_FAIL_AT="" ;;
esac

fail() { # fail <stage> <detail>
  if [ -n "$BREAK_MODE" ]; then
    if [ "$1" = "$EXPECT_FAIL_AT" ]; then
      echo "REGRESSION: intentional break correctly detected at stage '$1' ($2)"
      exit 0
    fi
    echo "REGRESSION: FAIL at stage '$1' ($2) — break '$BREAK_MODE' expected failure at '$EXPECT_FAIL_AT'"
    exit 1
  fi
  echo "REGRESSION: FAIL at stage '$1' ($2)"
  exit 1
}

pass() {
  if [ -n "$BREAK_MODE" ]; then
    echo "REGRESSION: BREAK NOT DETECTED — pipeline passed despite --break=$BREAK_MODE"
    exit 1
  fi
  echo "REGRESSION: PASS"
  exit 0
}

echo "== stage: fetch =="
if ! ./fetch-source.sh; then fail fetch "fetch-source.sh exited non-zero"; fi
echo "stage fetch: OK"

if [ "$BREAK_MODE" = "pin" ]; then
  git -C build/alpha-src fetch -q --depth 1 origin "$OFF_PIN" \
    || fail build "could not fetch off-pin commit for fault injection"
  git -C build/alpha-src checkout -q FETCH_HEAD
  echo "regress: intentional break — source now at $(git -C build/alpha-src rev-parse HEAD) (not the release pin)"
fi

if [ "$BREAK_MODE" = "dep" ]; then
  mkdir -p build/deps
  printf 'corrupted-dependency' > build/deps/jackson-core-2.18.2.jar
  echo "regress: intentional break — corrupted build/deps/jackson-core-2.18.2.jar"
fi

if [ "$BREAK_MODE" = "compile" ]; then
  target="$(find build/alpha-src/game-cli/src/main/java -name '*.java' | head -1)"
  [ -n "$target" ] || fail build "no java source found for fault injection"
  printf '\n@@@INVALID-JAVA-SYNTAX@@@\n' >> "$target"
  echo "regress: intentional break — injected syntax error into $target"
fi

if [ "$BREAK_MODE" = "smoke" ]; then
  count="$(find build/alpha-src -path '*src/main/resources*' -iname '*card*.json' -delete -print | wc -l)"
  [ "$count" -gt 0 ] || fail build "no card JSON resources found for fault injection"
  echo "regress: intentional break — removed $count card JSON resources from temp tree"
fi

echo "== stage: build =="
if ! ./build-release.sh; then fail build "build-release.sh exited non-zero (for --break=smoke this is the expected underlying smoke.sh failure)"; fi
echo "stage build: OK"

echo "== stage: verify =="
JAR="$(ls infinite-conquest-alpha-*.jar 2>/dev/null | head -1)"
[ -n "$JAR" ] || fail verify "no built jar found"
[ -f CHECKSUMS.sha256 ] || fail verify "CHECKSUMS.sha256 missing after build"

if [ "$BREAK_MODE" = "checksum" ]; then
  printf 'x' >> "$JAR"
  echo "regress: intentional break — corrupted $JAR"
fi

if ! sha256sum -c CHECKSUMS.sha256 >/dev/null 2>&1; then
  fail verify "sha256sum -c CHECKSUMS.sha256 failed against this build's own generated checksum"
fi
echo "stage verify: OK ($JAR matches its own generated checksum)"

pass
