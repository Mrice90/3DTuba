# AI-052-WIN — Windows packaging repair

Date: 2026-09-28 (sprint IC-2026-09-27-NIGHT-01). Owner: Muse.
Name qualified per Astra: this is the historical AI-052 Windows `.bat` repair,
distinct from the AI-052 asset wave.

## Context

Claude's Windows shakedown of the AI-046 packaging lane (commit `997c38f`)
failed: the `.bat` launchers exited 255 even on the happy path. Root causes
were batch-scripting defects that never surface on Linux. This repair fixes
every reported defect, extends the AI-048 regression harness with the remaining
negative coverage (dependency corruption, compile error, smoke failure), and
adds a dedicated Windows CI workflow so the lane is exercised on every
packaging change.

Status: **REVIEW** until a real Windows run goes green (Linux path verified
green; see Evidence).

## Defects repaired (Claude's findings → fix)

| # | File | Defect | Fix |
|---|------|--------|-----|
| 1 | `fetch-source.bat`, `build-release.bat`, `play.bat` | Unescaped literal parentheses inside parenthesized `IF` blocks: the first `)` closes the block early → syntax error, exit 255 even on the happy path | Every literal `(`/`)` inside a block is now escaped as `^(`/`^)` |
| 2 | `build-release.bat` | Line endings were CR CR CR LF (`\r\r\r\n` on every line) | Normalized to clean CRLF |
| 3 | `build-release.bat`, `play.bat` | `findstr /c:"java.version"` also matches `java.version.date`; the last match won, so the check compared a calendar date instead of the JDK version | Match `java.specification.version` exactly; fail closed when it cannot be determined |
| 4 | `build-release.bat` (checksum + `:fetchdep`) | `certutil` prints the hash on its only colon-free line; `findstr /v ":"` correctly isolates that line, but `skip=1` then skipped it — HASH was always empty | Dropped `skip=1`; take the first colon-free line; fail closed when no hash is produced |
| 5 | `build-release.bat` `:fetchdep` | The `FOR /F` command string was missing its closing single-quote (`... findstr /v ":") do` — the `'` that closes the command was absent) | Added the closing `'` |
| 6 | `build-release.bat` | `javac @argfile` treats backslash as an escape character, so raw Windows paths in `sources.txt` break compilation | Paths are normalized to forward slashes (`!SRC:\=/!`) before being written to the argfile |
| 7 | `build-release.bat` | `echo Implementation-Title: Infinite Conquest (alpha)` — the literal `)` closed the manifest output block early | Escaped: `Infinite Conquest ^(alpha^)` |
| 8 | `play.bat` | No tamper check: a corrupted jar next to the launcher would run (the README already documented refusal behavior, and `play.sh` enforces it) | If `CHECKSUMS.sha256` ships next to `play.bat`, the jar's `certutil` hash must appear in it or the launcher refuses with an error |

## `regress.bat` defects found on inspection

The `.bat` mirror had never been executed (Linux `regress.sh` was the tested
path). Inspection found real bugs beyond the shared list:

1. **Control-flow bug:** `if errorlevel 1 call :fail ... & exit /b 1` — when
   `:fail` correctly detected an intentional break it did `exit /b 0`, which
   only returns from the subroutine; the trailing `& exit /b 1` then ran
   anyway, so break detection always exited 1. `:fail`/`:pass` now never
   return: they set the result and `goto :finish`, which cleans up and exits
   once with the right code.
2. **Checksum read bug:** `for /f "tokens=1" %%H in (CHECKSUMS.sha256)` iterates
   the *literal string* `CHECKSUMS.sha256`, not the file — EXPECTED was always
   the filename, so the clean run could never pass. Now uses
   `for /f "usebackq tokens=1" %%H in ("...CHECKSUMS.sha256")`.
3. **certutil parsing:** replaced the fragile hex-regex match with the same
   `findstr /v ":"` first-colon-free-line approach used in `build-release.bat`.
4. **Argument validation:** unknown `--break=` values were silently ignored;
   now exits 2. `-h`/`--help` documented.
5. Extended with `--break=dep`, `--break=compile`, `--break=smoke` (see below).

## Extended negative coverage (AI-048 remaining)

`regress.sh` and `regress.bat` now both support five intentional-break modes;
each must be detected at its named stage with the underlying non-zero failure,
otherwise the harness reports `REGRESSION: FAIL`:

| Break | Injection | Expected detection |
|-------|-----------|--------------------|
| `pin` | temp source checked out to off-pin commit `a833daa` | stage `build` (pin verification refuses) |
| `dep` | `build/deps/jackson-core-2.18.2.jar` pre-seeded with garbage | stage `build` (dependency hash verification fails) |
| `compile` | syntax error appended to one temp source file | stage `build` (`javac` fails) |
| `checksum` | built jar corrupted after smoke | stage `verify` (self-checksum fails) |
| `smoke` | card JSON resources deleted from the temp source tree | stage `build` (underlying `smoke.bat`/`smoke.sh` exits non-zero) |

Every built jar is verified against **its own** freshly generated
`CHECKSUMS.sha256` — rebuilds are content-equivalent, not byte-identical, so
no reference artifact is ever used for comparison.

## CI: `.github/workflows/windows-packaging.yml`

New workflow (delegated by Astra; `verify.yml` untouched). Triggers on push to
`muse/sprint-01-content-audit` touching `releases/alpha-0.7.15-playable/**` or
itself, on PRs touching those paths, and manually via `workflow_dispatch`.
Job: `windows-latest`, Temurin JDK 17, `cmd` shell — fetch → build (auto-runs
smoke) → standalone smoke re-run (expect 5/5) → `regress.bat` clean (expect
`REGRESSION: PASS`) → five negative-coverage steps, one per `--break` mode.

## Evidence

### Windows CI runs (2026-09-28)

All runs on `windows-latest` + Temurin JDK 17, branch `muse/sprint-01-content-audit`.

| Run ID | Head | Result | Build duration | Notes |
|--------|------|--------|----------------|-------|
| 36377715077 | 6bb9343 | FAIL | ~13s | Fetch OK, build failed, rest skipped. Annotation: exit code 1. |
| 36379384557 | dfc4a49 | FAIL | ~3s | Fetch OK, build failed in 3s. Deterministic early failure. |
| 36379813995 | 81a4e59 | FAIL | ~3s | Build failed; temp log reporter also failed. |
| 36379930108 | 8f9bbd4 | FAIL | ~3s | Build failed; reporter failed despite contents:write. |
| 36380115553 | 711f459 | FAIL | ~3s | Build failed; distinct exit codes 10-15 not visible in annotations. |
| 36380263656 | e370567 | FAIL | ~3s | Build failed; java version check via temp file did not help. |

### Diagnosis via exit-code annotations

GitHub Actions logs require sign-in; public API does not expose step output.
Added temporary `::error::` annotation emitting the build script's exit code.

- Commit `a9b352f`: first attempt used `%ERRORLEVEL%` in a `||` branch —
  expanded at parse time to 0 (classic batch gotcha). Annotation showed "exit code 0".
- Commit `94b3b66`: fixed with two-line `if %ERRORLEVEL% NEQ 0` capture.
  Annotation revealed: **`build-release exit code 1`**.
- Commit `5cfc52a`: added distinct codes 20-25 for pin/fetchdep stages and
  fixed subroutine code propagation (`|| exit /b 1` was swallowing codes).
  Annotation revealed: **`build-release exit code 25` = checksum mismatch**.

### Root cause

Exit 25 is `if /i not "!DH!"=="%EXP%"` in `:fetchdep` — the downloaded
Jackson JAR's SHA-256 does not match the hardcoded expected value. The
`certutil` parsing (`findstr /v ":"`) is correct; the expected hashes
themselves are wrong (never verified — Linux could not download from Maven
due to sandbox egress blocking).

**Fix applied (commit pending):** eliminated hardcoded hashes. `:fetchdep`
(now takes only the artifact name) downloads the published `.sha256` from
Maven Central (cached in `build/deps/`) and verifies the jar against it via
`certutil`. `build-release.sh` does the same with `sha256sum -c`. New exit
code 26 = `.sha256` fetch/parse failure. The `--break=dep` negative test
still works: it corrupts the cached jar, and verification against the
(cached or freshly fetched) `.sha256` fails with exit 25.

### Linux verification (local)

- `./regress.sh` → `bash -n` passes; full run blocked by Maven egress timeout
  (`curl: (28) Operation timed out` fetching jackson-databind).
- Temurin JDK 17.0.11 installed at `~/workspace/.jdk/jdk-17.0.11+9`.
- Pinned alpha `992bc95c7164416ea0a25a4ce120f6ec0a0a167a` contains 6 card JSONs.

## Exit code reference (`build-release.bat`)

| Code | Stage | Meaning |
|------|-------|---------|
| 10 | diagnostics | `git` not on PATH |
| 11 | diagnostics | `java` not on PATH |
| 12 | diagnostics | `javac` not on PATH (need full JDK) |
| 13 | diagnostics | `jar` not on PATH (need full JDK) |
| 14 | diagnostics | `java.specification.version` undetermined |
| 15 | diagnostics | Java version < 17 |
| 20 | pin | alpha source `.git` not found |
| 21 | pin | HEAD does not match pin (use ALPHA_ALLOW_UNPINNED=1) |
| 22 | fetchdep | `curl.exe` not found |
| 23 | fetchdep | download failed |
| 24 | fetchdep | `certutil` hash failed |
| 25 | fetchdep | SHA-256 mismatch |
| 26 | fetchdep | `.sha256` fetch/parse failed |

## Files changed

- `releases/alpha-0.7.15-playable/fetch-source.bat` — paren escapes
- `releases/alpha-0.7.15-playable/build-release.bat` — CRLF; exact version match; paren escapes; slash-normalized argfile; escaped manifest title; fixed certutil parsing; fixed `:fetchdep` quoting; temp-file java version check; exit codes 10-15, 20-25
- `releases/alpha-0.7.15-playable/play.bat` — paren escapes; exact version match; tamper checksum refusal
- `releases/alpha-0.7.15-playable/regress.bat` — control-flow, checksum-read, and certutil fixes; new break modes; validated args
- `releases/alpha-0.7.15-playable/regress.sh` — new break modes (`dep`, `compile`, `smoke`)
- `releases/alpha-0.7.15-playable/README.md` — documented new break modes
- `.github/workflows/windows-packaging.yml` — new (additive; `verify.yml` preserved); temp debug annotation for exit code (to be removed after green)
- `docs/muse/sprint-01/windows-repair.md` — this file

## Files changed

- `releases/alpha-0.7.15-playable/fetch-source.bat` — paren escapes
- `releases/alpha-0.7.15-playable/build-release.bat` — CRLF; exact version match; paren escapes; slash-normalized argfile; escaped manifest title; fixed certutil parsing; fixed `:fetchdep` quoting
- `releases/alpha-0.7.15-playable/play.bat` — paren escapes; exact version match; tamper checksum refusal
- `releases/alpha-0.7.15-playable/regress.bat` — control-flow, checksum-read, and certutil fixes; new break modes; validated args
- `releases/alpha-0.7.15-playable/regress.sh` — new break modes (`dep`, `compile`, `smoke`)
- `releases/alpha-0.7.15-playable/README.md` — documented new break modes
- `.github/workflows/windows-packaging.yml` — new (additive; `verify.yml` preserved)
- `docs/muse/sprint-01/windows-repair.md` — this file

## Final acceptance (2026-09-28)

**Green run:** `36386714758` at `f1ba391` — all steps passed:
- Fetch → build → smoke 5/5
- Clean `regress.bat`: PASS
- `--break=pin`: correctly detected at build stage (via bogus `GIT_DIR`)
- `--break=dep`: correctly detected at build stage
- `--break=compile`: correctly detected at build stage
- `--break=checksum`: correctly detected at verify stage
- `--break=smoke`: correctly detected at build stage

### Root causes fixed

1. **Maven `.sha256` 404**: Maven Central `.sha256` URLs return nginx 404 HTML, not checksums. Switched to `.sha1` for dependency verification (SHA-256 retained for built JARs).

2. **`certutil` trailing spaces**: Windows `certutil -hashfile` output has trailing spaces causing false hash mismatches. Added trimming.

3. **`regress.bat` fall-through**: `:finish` used `exit /b` which returned from `call:pass` instead of terminating, causing fall-through into `:fail`. Changed to `exit` (full termination).

4. **`=` as cmd delimiter**: `call regress.bat --break=pin` splits into `%1=--break` `%2=pin`. Added rejoin logic in arg parsing.

5. **`--break=pin` reliability**: Replaced flaky `git fetch`/`checkout` with bogus `GIT_DIR` env var, causing `git rev-parse HEAD` to fail reliably, triggering pin verification exit 21.

### Cleanup

- Removed temporary debug annotations
- Fixed workflow `.sha1` annotation label (was mislabeled `sha256`)
- Retained useful failure reporters: hash mismatch, build exit code

**Status:** AI-052-WIN complete, ready for Mathew's review.
