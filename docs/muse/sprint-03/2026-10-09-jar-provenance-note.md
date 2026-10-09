# Rules-engine JAR provenance note: 163e2548 vs 2db3a12c

**Date:** 2026-10-09 · **Lane:** Claude (cloud) · **Backlog:** AI-080-BUILD-PROVENANCE · **Reviewer:** Muse
**Closes in part:** the open evidence gate in `2026-10-08-build-provenance-review.md` §6.
No engine code changed, no new build published, `CHECKSUMS.sha256` untouched.

## Result

1. **`2db3a12c…bae86b` is reproducible from clean source.** Fresh clone of
   `Mrice90/TubaExperiment` at pin `992bc95c7164416ea0a25a4ce120f6ec0a0a167a`
   via `fetch-source.sh`, built with the repo's own `build-release.sh` on
   Temurin **17.0.20.1+1** (Linux x64, Python 3.13): two consecutive builds both produced
   `2db3a12c92dbd2acf0de251535b58bb13ab869eaae3075f07a6abaa63fbae86b`
   (90,770,367 bytes, 1,568 entries), smoke passed. This matches the CI-accepted hash (AI-100).
2. **`163e2548…7c89a` cannot come from that recipe with those inputs.** The
   recipe is deterministic (fixed timestamps, sorted entries, pinned header
   fields, pinned Jackson SHA-256s), so a different hash means at least one
   input differed. Three inputs are proven to move the hash:
   | Input changed | Hash produced | Entry-level effect |
   |---|---|---|
   | none (recipe as written, JDK 17) | `2db3a12c…` | identical |
   | **JDK 21** javac instead of 17 | `125d826b…0d54` | all 278 engine classes change class-file major version 61→65; resources identical |
   | zip repacked by another tool (same content) | `c0085712…` (example) | every entry identical; only zip packing differs |
   | different engine source / added classes | (not tested; would show) | changed or added `.class` entries |
3. **Which of these produced `163e2548` is not yet known.** The SP2 jar lives
   only on the laptop (`Infinite Conquest/playtest/unity-build-SP2-gameplay/`).
   The deciding check is one command on that machine (below). Nothing here
   suggests tampering; every observed difference so far has a mundane cause.

## Finding worth fixing later (not done here)

`build-release.sh`/`.bat` call `javac` **without `--release 17`**, so the
hash silently depends on whichever JDK is on `PATH` (JDK 21 gave `125d826b`).
Either pin `--release 17` (changes nothing for JDK 17 builds) or have the
script refuse a non-17 `javac`. Left as a proposal because this lane is told
not to change the build.

## Laptop check that closes the gate

`tools/jar-entry-manifest.py` (new, stdlib only, tested in `verify.yml`)
compares the SP2 jar against the canonical entry manifest committed at
`docs/muse/sprint-03/evidence/canonical-2db3a12c-entries.tsv`, so the 91 MB
canonical jar does not need to be on the laptop:

```
python releases/alpha-0.7.15-playable/tools/jar-entry-manifest.py compare ^
  docs/muse/sprint-03/evidence/canonical-2db3a12c-entries.tsv ^
  "<Infinite Conquest>\playtest\unity-build-SP2-gameplay\<rules jar>"
```

Verdict meanings:
- `ARCHIVE-ONLY`: same engine and resources as canonical; repacked by a
  non-reproducible jar step. Benign; record the packer.
- `JAVAC-VERSION`: same source compiled by a different JDK. Benign; record the JDK.
- `CLASS-CODE` / `ENTRY-SET`: engine source differs from pin `992bc95`
  (for example SP2 slot/capacity changes compiled into the jar instead of the
  separate overlay classes). Then the SP2 source commit must be pinned and the
  SP2 recipe written down before the jar can be called reproducible.
- `RESOURCES`: card JSON/art/audio inside the jar differ; list them.

## Evidence

- Build logs: local runs 2026-10-09 01:24 UTC, both `rc=0`, `smoke OK`.
- Entry manifest of the reproduced canonical jar: `evidence/canonical-2db3a12c-entries.tsv`.
- Jackson 2.18.2 jars were fetched from the Google Maven Central mirror because
  `repo1.maven.org` returned HTTP 429; the script's pinned SHA-256 and SHA-1
  checks passed, so the bytes are identical to Central's.
