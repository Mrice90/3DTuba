# Rules-engine JAR provenance note: 163e2548 vs 2db3a12c

**Date:** 2026-10-09 · **Lane:** Claude · **Backlog:** AI-080-BUILD-PROVENANCE · **Reviewer:** Muse
**Closes:** the open evidence gate in `2026-10-08-build-provenance-review.md` §6.
No engine code changed, no new build published, `CHECKSUMS.sha256` untouched.

## Answer

The SP2 jar `163e2548…` is **not a corrupted or tampered canonical jar**. It is a
byte-for-byte copy of a jar built on the laptop on **2026-09-30 10:25 UTC** by
`Infinite Conquest/3DTuba-unity-playable/releases/alpha-0.7.15-playable/build-release.bat`
(same SHA-256, same 90,771,712 bytes). Two things make it differ from canonical:

1. **Locally patched engine source (behaviour difference).** That build compiled
   the 118 sources under `Infinite Conquest/playtest/build-kit/build/alpha-src/`
   (`build/stage/sources.txt`). That checkout's `HEAD` is the pin `992bc95`, but
   its working tree carries **uncommitted local edits**. Upstream
   `TubaExperiment` has no commit containing them (the only later commits on
   `strip/zeus-poseidon-desktop` are lore docs). 28 classes differ, all
   compiled by JDK 17 (class major 61, same as canonical):
   - `cli/BotPlayer` (+`$1`, `$Decision`): 3 extra methods, including
     `advanceScore(...)` (destination-aware move scoring, the AI-107 fix
     direction) and `attacksCharacter(...)`; capital-attack filtering now
     takes game state. **The bridge's bot auto-play uses this class, so SP2
     bot behaviour differs from the canonical engine.**
   - `gui/InfiniteConquestGui` and 24 inner classes: source shifted ~37
     lines (line-number tables) and the main class grew 1.5 KB. The Swing GUI
     is not used by the Unity bridge.
2. **Pre-AI-100 Windows packing (bytes only, no behaviour).** All 1,568 entries
   carry `create_system=0` (the AI-100 root cause, fixed later at `b1199b6`) and
   17 text resources plus `MANIFEST.MF` have CRLF line endings from a Windows
   checkout (the AI-099/100 CRLF delta). After CRLF normalisation those 17
   resources are identical to canonical. Entry order and timestamps match the
   reproducible packer.

On top of the jar, the SP2 package overlays `GameEngine`, `ActionHints`,
`SummonCapacityRules`, `RulesBridge` and `structure-slots.properties` from
`Bridge/classes/` (hashes in the package `SHA256SUMS.txt`); those were already known.

## Canonical half: reproduced

Fresh clone of `Mrice90/TubaExperiment` at pin `992bc95c7164416ea0a25a4ce120f6ec0a0a167a`
via `fetch-source.sh`, built with the repo's `build-release.sh` on Temurin
**17.0.20.1+1** (Linux x64, Python 3.13): two consecutive builds both gave
`2db3a12c92dbd2acf0de251535b58bb13ab869eaae3075f07a6abaa63fbae86b`
(90,770,367 bytes, 1,568 entries), smoke OK. Matches the CI-accepted hash (AI-100).

| Build | Hash | Entry-level verdict vs canonical |
|---|---|---|
| repo recipe, JDK 17 (×2) | `2db3a12c…` | identical |
| repo recipe, JDK 21 | `125d826b…0d54` | JAVAC-VERSION (278 classes 61→65) |
| SP2 package jar | `163e2548…7c89a` | CLASS-CODE (28) + RESOURCES (17, CRLF only) + zip headers |

## What is still needed to call SP2 reproducible

- **Pin the patch.** Export `git diff` from
  `playtest/build-kit/build/alpha-src/` (against `992bc95`) into this repo as a
  patch file, and record which backlog items it implements. The source files
  are too deeply nested for the cloud thread to read through the folder link,
  so this needs a local session (Codex, or Claude via Remote Control).
- **Rebuild with the current packer.** The jar was packed before the AI-100
  fix. Rebuilding pin + patch with today's `make-repro-jar.py` would give a
  new, reproducible hash for SP2. Doing that is a new build, so it is
  out of scope here.
- **Proposal:** `build-release.sh`/`.bat` call `javac` without `--release 17`,
  so the hash also depends on whichever JDK is on `PATH`. Pin `--release 17`
  or refuse a non-17 `javac`.

## Evidence

- `evidence/canonical-2db3a12c-entries.tsv`, `evidence/sp2-163e2548-entries.tsv`:
  per-entry manifests. Re-run the classification with
  `python releases/alpha-0.7.15-playable/tools/jar-entry-manifest.py compare <a> <b>`.
- Laptop files read 2026-10-09 (read-only): SP2 `Bridge/infinite-conquest-alpha-0.7.15.jar`,
  `SHA256SUMS.txt`, `README-PLAYTEST.txt`; `3DTuba-unity-playable/.../infinite-conquest-alpha-0.7.15.jar`
  (mtime 2026-09-30 10:25 UTC, SHA-256 `163e2548…`), its `build/stage/sources.txt`;
  `playtest/build-kit/build/alpha-src/.git/HEAD` = `992bc95…`.
  The build-kit folder also holds `upstream-0.7.15-unpatched.jar` and a different
  earlier patched jar `52a303a5…`. Neither is in the SP2 package.
- `javap -p -c -v` diff of `BotPlayer.class` and `InfiniteConquestGui$Intent.class`
  (canonical vs SP2), Temurin 17.0.20.1.
- Jackson 2.18.2 was fetched from the Google Maven Central mirror because
  `repo1.maven.org` returned HTTP 429. The script's pinned SHA-256 and SHA-1 checks passed.
