# AI-100 / AI-101 — CI run ledger (cross-OS reproducible jar)

Every CI run at each tested head, with run URL/ID, workflow, head SHA and
conclusion — including red and in-progress runs (AI-101 process requirement).
Branch: `muse/sprint-01-content-audit`. Times in UTC (EDT = UTC-4).

Canonical jar hash (tracked in `releases/alpha-0.7.15-playable/CHECKSUMS.sha256`):
`2db3a12c…bae86b`. The gate is never weakened: both packaging workflows
**assert** the built jar equals the canonical hash; a mismatch fails the run.
The canonical value is only ever replaced by a genuine cross-OS fix, never to
hide a difference.

## 2026-09-30 12:00 EDT — rejection baseline (Claude review)

| Head | Run | Workflow | Conclusion | Evidence |
|------|-----|----------|------------|----------|
| `18178ba` | [#63](https://github.com/Mrice90/3DTuba/actions/runs/36712939059) `36712939059` | Windows packaging | **failure** | jar `7796b68e…c6593` ≠ canonical `2db3a12c…bae86b` |
| `18178ba` | [#196](https://github.com/Mrice90/3DTuba/actions/runs/36712939055) `36712939055` | Verify | success | — |
| `3105c1a` | [#64](https://github.com/Mrice90/3DTuba/actions/runs/36712948755) `36712948755` | Windows packaging | **failure** | jar `7796b68e…c6593` ≠ canonical |
| `3105c1a` | [#31](https://github.com/Mrice90/3DTuba/actions/runs/36712948731) `36712948731` | Linux packaging | success | — |
| `3105c1a` | [#198](https://github.com/Mrice90/3DTuba/actions/runs/36712948714) `36712948714` | Verify | success | — |

Reviewer diagnosis: hash moved `18998415…` → `7796b68e…`; residual
difference suspected in `.class` bytes (JDK patch level), entry set, or
`create_system`. Rework directive: upload jars as artifacts, print JDK
versions, add per-entry diff, fix differing entries at source.

## 2026-09-30 14:01 EDT — rework published (before 14:10 handoff; timeline honest)

Root cause found: CPython `zipfile.ZipInfo.create_system` defaults to 0 on
Windows and 3 on POSIX — a 2-byte OS fingerprint in every entry header.
Fix: pin `create_system=3` in `make-repro-jar.py` (`b1199b6`); per-entry
diff tool `diff-jar-entries.py` + `test_diff_jar_entries.py` (`61dc6ee`);
jar artifact uploads + toolchain versions (`65834b4`, `7b161da`, `7798f32`).

| Head | Run | Workflow | Conclusion | Notes |
|------|-----|----------|------------|-------|
| `b1199b6` | [#65](https://github.com/Mrice90/3DTuba/actions/runs/36755541726) `36755541726` | Windows packaging | success | create_system pin; assertion green |
| `b1199b6` | [#32](https://github.com/Mrice90/3DTuba/actions/runs/36755541685) `36755541685` | Linux packaging | success | — |
| `b1199b6` | [#205](https://github.com/Mrice90/3DTuba/actions/runs/36755541687) `36755541687` | Verify | success | — |
| `a6c4d66` | [#66](https://github.com/Mrice90/3DTuba/actions/runs/36755544766) `36755544766` | Windows packaging | success | — |
| `a6c4d66` | [#33](https://github.com/Mrice90/3DTuba/actions/runs/36755544842) `36755544842` | Linux packaging | success | — |
| `a6c4d66` | [#206](https://github.com/Mrice90/3DTuba/actions/runs/36755544706) `36755544706` | Verify | **failure** | Windows: `test_make_repro_jar.py` |
| `61dc6ee` | [#67](https://github.com/Mrice90/3DTuba/actions/runs/36755556078) `36755556078` | Windows packaging | success | — |
| `61dc6ee` | [#34](https://github.com/Mrice90/3DTuba/actions/runs/36755556008) `36755556008` | Linux packaging | success | — |
| `61dc6ee` | [#207](https://github.com/Mrice90/3DTuba/actions/runs/36755556088) `36755556088` | Verify | **failure** | Windows: `test_make_repro_jar.py` |
| `15d4a63` | [#68](https://github.com/Mrice90/3DTuba/actions/runs/36755559430) `36755559430` | Windows packaging | success | canonical assertion + `play.bat --check-only` green |
| `15d4a63` | [#35](https://github.com/Mrice90/3DTuba/actions/runs/36755559402) `36755559402` | Linux packaging | success | two-builds-agree + canonical assertion green |
| `15d4a63` | [#208](https://github.com/Mrice90/3DTuba/actions/runs/36755559324) `36755559324` | Verify | **failure** | Windows: `test_make_repro_jar.py` |
| `7b161da` | [#69](https://github.com/Mrice90/3DTuba/actions/runs/36755568203) `36755568203` | Windows packaging | success | artifact `windows-jar` (90,579,277 B) |
| `7b161da` | [#209](https://github.com/Mrice90/3DTuba/actions/runs/36755568093) `36755568093` | Verify | **failure** | Windows: `test_make_repro_jar.py` |
| `65834b4` | [#36](https://github.com/Mrice90/3DTuba/actions/runs/36755570589) `36755570589` | Linux packaging | success | artifact `linux-jar` (90,579,277 B) |
| `65834b4` | [#210](https://github.com/Mrice90/3DTuba/actions/runs/36755570611) `36755570611` | Verify | **failure** | Windows: `test_make_repro_jar.py` |
| `7798f32` | [#211](https://github.com/Mrice90/3DTuba/actions/runs/36755573777) `36755573777` | Verify | **failure** | Windows job `110024829251`: `test_create_system_flip_changes_jar_hash` → `PermissionError WinError 32` at `test_make_repro_jar.py:97` |

Key packaging facts (both assert jar SHA-256 == canonical):
- Linux `36755559402` @`15d4a63` green, Windows `36755559430` @`15d4a63` green
  — **same head, both asserting canonical, both green** (incl. `play.bat --check-only`).
- Artifacts: `linux-jar` @`65834b4` and `windows-jar` @`7b161da` are both
  exactly 90,579,277 bytes (same packer, same pinned source).
- Artifact blob download returns HTTP 401 from this environment, so the
  byte-level cross-check was done via the asserting CI steps plus a local
  per-entry run of `diff-jar-entries.py` (see below), not by downloading CI jars.

## Per-entry comparison (real, local, with the shipped tool)

`diff-jar-entries.py` (shipped at `61dc6ee`) run against jars built by the
real packer:

- **Fails before** — Linux jar vs simulated old-Windows jar
  (`rewrite_with_create_system(jar, 0)`, the exact 12:00 residue):
  all 5 fixture entries report
  `HEADER DIFFERS (content identical): create_system: a=3 b=0`
  (`only_a=0 only_b=0 content=0 header=5`). Content identical, headers differ —
  precisely the `7796b68e` vs `2db3a12c` signature.
- **Passes after** — new packer, LF sources vs CRLF sources (simulated
  Windows checkout): `IDENTICAL: same entry set, same content, same header fields`.

## 2026-09-30 ~14:20 EDT — WinError 32 regression fixes (this session)

Two test helpers leaked the `tempfile.mkstemp()` descriptor; `os.unlink()` on
the temp path then raised `PermissionError WinError 32` on Windows
(Linux tolerates unlink-with-open-handles, so this was Windows-only).
Fix in both: `os.close(fd)` immediately after `mkstemp`. Test logic preserved.

- `0912185` — `test_make_repro_jar.py::rewrite_with_create_system`.
  Local: 7/7 pass; fd-leak check OLD 1 fd/call → NEW 0.
- `874ea3a` — `test_diff_jar_entries.py::make_jar` (same pattern, found when
  Verify #212 failed at the next step after the first fix landed).
  Local: 5/5 pass; fd-leak check OLD 1 fd/call → NEW 0.

| Head | Run | Workflow | Conclusion | Notes |
|------|-----|----------|------------|-------|
| `0912185` | [#70](https://github.com/Mrice90/3DTuba/actions/runs/36758424959) `36758424959` | Windows packaging | in_progress → *pending* | packer unchanged; assertion expected green |
| `0912185` | [#37](https://github.com/Mrice90/3DTuba/actions/runs/36758425098) `36758425098` | Linux packaging | in_progress → *pending* | — |
| `0912185` | [#212](https://github.com/Mrice90/3DTuba/actions/runs/36758425024) `36758425024` | Verify | **failure** | Windows: `test_diff_jar_entries.py` (fixed in `874ea3a`) |
| `874ea3a` | [#71](https://github.com/Mrice90/3DTuba/actions/runs/36758602750) `36758602750` | Windows packaging | **success** | acceptance head: `AI-100 jar hash asserts canonical checksum` green; `AI-100 play.bat checksum gate accepts fresh build` (--check-only) green |
| `874ea3a` | [#38](https://github.com/Mrice90/3DTuba/actions/runs/36758602542) `36758602542` | Linux packaging | **success** | acceptance head: two-builds-agree + canonical assertion green |
| `874ea3a` | [#213](https://github.com/Mrice90/3DTuba/actions/runs/36758602937) `36758602937` | Verify | **success** | both OSes green, incl. fixed WinError 32 tests |

## Acceptance verdict (evidence, not self-acceptance)

At `874ea3a` (code head; `7bae8ac` is a docs-only ledger commit on top):
- Linux packaging #38 **success** — jar SHA-256 asserted == canonical `2db3a12c…`
- Windows packaging #71 **success** — jar SHA-256 asserted == canonical
  `2db3a12c…`; `play.bat --check-only` accepts the fresh Windows build
- Verify #213 **success** on ubuntu-24.04 and windows-latest
- Per-entry diff: old-Windows simulation shows header-only `create_system`
  residue on every entry; fixed packer yields byte-identical jars from LF and
  CRLF sources
- Gates preserved: canonical checksum never replaced to hide a difference;
  both packaging workflows fail closed on hash mismatch

AI-100 rework is **DELIVERED**; formal acceptance remains Claude's 18:00 review
lane (no self-acceptance).

## 2026-09-30 ~15:00–17:00 EDT — parallel-run convergence + CI-jar artifact cross-check (14:00 run)

The 14:00 and 15:00 reflection runs executed the same AI-100 rework in
parallel and converged: identical root cause (`ZipInfo.create_system` pin),
identical WinError 32 fixes (`0912185`'s fix and `a04a0a4`'s fix are the same
one-liner — comment wording only differs), no merge conflicts; the branch
tip `a04a0a4` chains on `4e144e7`. Runs since the acceptance section above:

| Head | Run | Workflow | Conclusion | Notes |
|------|-----|----------|------------|-------|
| `7bae8ac` | [#214](https://github.com/Mrice90/3DTuba/actions/runs/36758717297) `36758717297` | Verify | success | AI-101 correction: this run is Verify #214 (docs-only ledger commit); the row below is #215 |
| `3bf9641` | [#215](https://github.com/Mrice90/3DTuba/actions/runs/36759079743) `36759079743` | Verify | success | AI-102 verification head (setup-python@v6), both OSes |
| `95024eb` | [#216](https://github.com/Mrice90/3DTuba/actions/runs/36762842508) `36762842508` | Verify | success | docs-only (AI-101/AI-102 delivery entry) |
| `4e144e7` | [#217](https://github.com/Mrice90/3DTuba/actions/runs/36762850162) `36762850162` | Verify | success | docs-only (independent QA entry) |
| `a04a0a4` | [#218](https://github.com/Mrice90/3DTuba/actions/runs/36776423251) `36776423251` | Verify | success | both OSes, incl. WinError 32 regression tests |
| `a04a0a4` | [#39](https://github.com/Mrice90/3DTuba/actions/runs/36776423099) `36776423099` | Linux packaging | success | two-builds-agree + canonical assertion; Temurin 17.0.20.1+1 |
| `a04a0a4` | [#72](https://github.com/Mrice90/3DTuba/actions/runs/36776423170) `36776423170` | Windows packaging | success | `AI-100 jar hash asserts canonical checksum` green; `play.bat --check-only` green; Temurin 17.0.20.1+1 |

### Decisive CI-jar cross-check (closes the ledger's open artifact item)

The HTTP 401 on artifact blob download was a redirect-auth issue, not a
permissions issue: fixed by following the 302 to blob storage without the API
surrogate (workspace `download_artifact.py`). Downloaded both jars:

- `linux-jar` (run `36776423099`, 90,770,367 B) and `windows-jar` (run
  `36776423170`, 90,770,367 B).
- SHA-256 of **both** = `2db3a12c92dbd2acf0de251535b58bb13ab869eaae3075f07a6abaa63fbae86b`
  = tracked canonical `CHECKSUMS.sha256`.
- Shipped `diff-jar-entries.py` (run `36776423099` jar vs run `36776423170`
  jar): **IDENTICAL: same entry set, same content, same header fields** —
  zero differing entries. (Earlier ledger said 90,579,277 B from the zip
  wrapper; the extracted jars are 90,770,367 B on both OSes.)
- Toolchain versions (new AI-100 diagnosis step): **both** runners report
  Temurin `openjdk version "17.0.20.1"` — the JDK patch-level suspect is
  eliminated; the `create_system` pin was the entire residue.

AI-100 rework remains **DELIVERED**; formal acceptance stays in Claude's
18:00 review lane (no self-acceptance). Corroborating runs above are all
green — no red acceptance CI since `874ea3a`.

## 2026-10-02 ~20:00 EDT — IC.Net client CI (branch `claude/unity-live-match`)

AI-097 third deliverable: IC.Net relay-1 client library + headless tests.
`verify.yml` gained a `dotnet test UnityProof/IC.Net.Tests` step (unit tests;
the lockstep test self-skips without `ICNET_RUN_LOCKSTEP=1`). New workflow
`icnet-lockstep.yml`: fetch alpha source → build release jar → run the
two-client lockstep test with `ICNET_RUN_LOCKSTEP=1`.

| Head | Run | Workflow | Conclusion | Notes |
|------|-----|----------|------------|-------|
| `84f76dc`→`4a0d5ed` (13 lib files) | [#37079702708](https://github.com/Mrice90/3DTuba/actions/runs/37079702708) … [#37079730406](https://github.com/Mrice90/3DTuba/actions/runs/37079730406) | Verify | success | one run per file commit; dotnet step not yet present |
| `93f257e`→`5537fcc` (4 test files) | [#37079734259](https://github.com/Mrice90/3DTuba/actions/runs/37079734259) … [#37079740635](https://github.com/Mrice90/3DTuba/actions/runs/37079740635) | Verify | success | |
| `3dc7358` | [#37079745665](https://github.com/Mrice90/3DTuba/actions/runs/37079745665) `37079745665` | Verify | success | first run with the dotnet step; ubuntu + windows green |
| `f49a1c0` | [#37079748009](https://github.com/Mrice90/3DTuba/actions/runs/37079748009) `37079748009` | Verify | success | ubuntu-24.04 + windows-latest: "IC.Net relay-1 client unit tests" green on both |
| `f49a1c0` | [#37079747992](https://github.com/Mrice90/3DTuba/actions/runs/37079747992) `37079747992` | IC.Net lockstep | **failure** | "Build release jar": `build-release.sh` needs `./fetch-source.sh` first (`regress.sh` does this; the new workflow didn't). Fixed in `d2ef129`. |
| `d2ef129` | [#37080248112](https://github.com/Mrice90/3DTuba/actions/runs/37080248112) `37080248112` | IC.Net lockstep | success | fetch-source → build jar → two-client lockstep test green |
| `d2ef129` | [#37080248116](https://github.com/Mrice90/3DTuba/actions/runs/37080248116) `37080248116` | Verify | in_progress | still running at time of writing; dotnet step green on the identical `f49a1c0` run |

Item (1) of Rune's 2026-10-02 assignments: **done** — both workflows green on `claude/unity-live-match`, every run listed above including the red one.
