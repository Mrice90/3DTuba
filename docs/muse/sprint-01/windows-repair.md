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

TBD — filled in after the CI run. Linux verification commands run locally:

- `./regress.sh` → `REGRESSION: PASS`
- `./regress.sh --break=pin|dep|compile|checksum|smoke` → each `intentional break correctly detected at stage '<stage>'`

## Files changed

- `releases/alpha-0.7.15-playable/fetch-source.bat` — paren escapes
- `releases/alpha-0.7.15-playable/build-release.bat` — CRLF; exact version match; paren escapes; slash-normalized argfile; escaped manifest title; fixed certutil parsing; fixed `:fetchdep` quoting
- `releases/alpha-0.7.15-playable/play.bat` — paren escapes; exact version match; tamper checksum refusal
- `releases/alpha-0.7.15-playable/regress.bat` — control-flow, checksum-read, and certutil fixes; new break modes; validated args
- `releases/alpha-0.7.15-playable/regress.sh` — new break modes (`dep`, `compile`, `smoke`)
- `releases/alpha-0.7.15-playable/README.md` — documented new break modes
- `.github/workflows/windows-packaging.yml` — new (additive; `verify.yml` preserved)
- `docs/muse/sprint-01/windows-repair.md` — this file
