# Alpha build handoff — reproducible recipe (AI-046)

How to go from a **clean 3DTuba checkout** to a verified playable
`infinite-conquest-alpha-0.7.15.jar`. Follow-up to the AI-044 packaging; it
duplicates none of that work — it makes the handoff reproducible and honest
about its prerequisites.

All paths below are relative to the 3DTuba checkout root.

## Prerequisites

- **JDK 17 or newer**, full JDK (needs `javac` and the `jar` tool, not just a
  JRE). Temurin/Adoptium recommended: https://adoptium.net
- **git** (to fetch the pinned source read-only)
- ~2 GB free disk (source + build staging + ~91 MB jar)
- No Gradle, no npm, no other installs.

## The flow

### 1. Fetch the pinned source (read-only)

```
cd releases/alpha-0.7.15-playable
./fetch-source.sh        # Linux / macOS
fetch-source.bat         # Windows
```

Creates `build/alpha-src/` (git-ignored), fetches **exactly one commit**
(`992bc95c7164416ea0a25a4ce120f6ec0a0a167a`,
`strip/zeus-poseidon-desktop`), and **fails unless the checkout equals the
pin**. Nothing is pushed or modified upstream — the source repos stay
read-only.

### 2. Build

```
./build-release.sh       # Linux / macOS
build-release.bat        # Windows
```

Runs diagnostics first (Java present? 17+? `javac`/`jar` available? source
present and on-pin?) and fails fast with a plain-English error otherwise.
Then:

1. **Dependencies:** Jackson 2.18.2 (databind/core/annotations) is fetched
   from Maven Central into `build/deps/` — it is *not* tracked in the
   upstream repo, so a clean fetch would otherwise be missing it. Each jar's
   SHA-256 is verified against a pinned hash before use; a mismatch aborts
   the build.
2. `javac` the four modules, merge resources, merge Jackson, assemble the
   fat jar, write `CHECKSUMS.sha256`, copy jar + checksums next to the
   launchers, and run the smoke checks.

Override env (both OSes): `ALPHA` (source dir), `STAGE` (work dir),
`ALPHA_ALLOW_UNPINNED=1` (build a different checkout — **not** the release
recipe; the pin check is fail-closed by default).

### 3. Smoke-check (re-runnable any time)

```
./smoke.sh [jar]         # Linux / macOS
smoke.bat [jar]          # Windows
```

Checks, in order: jar exists and is non-empty → manifest `Main-Class` is
`com.infiniteconquest.gui.GameShell` → entry-point class is in the jar →
card JSON resources are in the jar → **headless engine**: 36 seeded
bot-vs-bot matches complete (`simulate 1 42`, ~10 s). Exits non-zero on any
failure.

### 4. Play

```
./play.sh                # Linux / macOS
play.bat                 # Windows  (or double-click it)
```

The launchers check Java and the jar. **If the jar is missing** they do not
fail cryptically — they explain the jar is not in the repo (90MB+ blobs are
rejected by a repository rule), where the handoff copy comes from, the exact
`sha256sum`/`certutil` command to verify it, and the expected hash. If
`CHECKSUMS.sha256` sits next to the jar, the checksum is verified
automatically before launch.

## What was verified where (honest ledger)

**Linux (this environment), 2026-09-27:**
- `fetch-source.sh` → pin verified (`992bc95…`).
- Full rebuild from the fetched source: Jackson 2.18.2 fetched from Maven
  Central and hash-verified, 118 sources compiled, jar assembled,
  `CHECKSUMS.sha256` written, `smoke.sh` 5/5 PASS.
- The fresh rebuild is **content-equivalent but not byte-identical** to the
  reference handoff jar (observed: `20696b45…f14af` vs reference
  `728c3fc1…5149029523` — zip timestamps/ordering differ per run). Each
  rebuild regenerates its own `CHECKSUMS.sha256`; verify the jar you hold
  against the checksums file next to it.
- `play.sh` verified under dash: missing-java and missing-jar paths print
  plain-English errors (the missing-jar message is honest about the jar not
  being in the repo); checksum-OK jar proceeds to launch; tampered jar is
  refused with a checksum-mismatch error.
- Negative paths verified: missing source dir, pin mismatch (fail-closed
  unless `ALPHA_ALLOW_UNPINNED=1`).

**Windows: scripts authored, not executed here.**
`fetch-source.bat`, `build-release.bat`, `smoke.bat`, `play.bat` (CRLF) mirror
the verified Linux logic line-for-line (`javac @argfile`, `jar`, `xcopy`,
`certutil -hashfile`), but this environment has no Windows host — treat the
first real Windows run as a shakedown and report back.

## Provenance & checksums

- `releases/alpha-0.7.15-playable/PROVENANCE.md` — source pin, toolchain,
  entry points, reference verification.
- `releases/alpha-0.7.15-playable/CHECKSUMS.sha256` — reference SHA-256 of
  the handoff jar. Regenerated on every rebuild.

## Limits (read before distributing)

- The jar is **not published** anywhere by this work: no binary in the repo,
  no formal GitHub Release created. Distribution remains the manual handoff.
- This is the **2D Java alpha**, not the 3D migration; local play only.
- The Windows scripts are untested-on-Windows until someone runs them there.
- Build timestamps inside the jar differ per run; the reference checksum is
  for the handoff copy, regenerated honestly by each rebuild.
