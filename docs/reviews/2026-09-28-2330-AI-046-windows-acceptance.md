# AI-046-WIN-ACCEPT — independent Windows acceptance (Claude, covering Astra) — 2026-09-28 23:30 EDT

Machine: Mathew's PC (Windows 11 Home 10.0.26200), Temurin JDK 17.0.20.101 (Adoptium), cmd.exe.
Source: fresh `git clone --depth 1` of `muse/sprint-01-content-audit` at **1fc5789** (includes AI-055 pins `1c53026`, AI-056 `f663f24`, AI-048 Linux CI `de2b8e5`).

## Results
| Step | Command | Result |
|---|---|---|
| fetch | `fetch-source.bat` | exit 0, pin 992bc95 verified |
| build | `build-release.bat` | exit 0; the Jackson 2.18.2 jars pass the pinned SHA-256 check (AI-055) |
| smoke | `smoke.bat` | **5 passed, 0 failed** (jar, manifest entry point, entry class, card data, headless 36 seeded matches) |
| regress clean | `regress.bat` | **REGRESSION: PASS** (fetch OK, build OK, verify OK) |
| regress `--break=pin/dep/compile/checksum/smoke/depswap` | `regress.bat --break=<mode>` | **FAIL (harness error, not a detection result)**, see AI-065 |

Built artifact: `infinite-conquest-alpha-0.7.15.jar`, 83,249,781 bytes, SHA-256 `f7d887e370673169ef957a57a318de311706463337c6de5d490e0b1071ec7489`. The size differs from Thalia's Linux jar (90,812,575 B); rebuilds are documented as content-equivalent, not byte-identical.

## Verdict
- **Happy path ACCEPTED on a real Windows PC:** fetch → build (pinned deps) → smoke 5/5 → clean regress PASS.
- **Negative coverage NOT ACCEPTED locally.** Every `--break` run stopped before reaching its stage:
  1. `xcopy "%HERE%." "%WORK%\" /E /I /Q` → `File creation error - The system cannot find the path specified.` → `ERROR: copy failed` (regress.bat:85). This reproduces in isolation (`--break=pin`, after deleting all `alpha-regress-*` dirs). It is most likely the MAX_PATH limit: the release folder already holds `build\alpha-src\...` from the build step, and the clone lives under a deep `%TEMP%` path. Fix options: copy with `robocopy /E` (long-path aware), exclude `build\` from the copy (it is rebuilt anyway), or build into `%WORK%` directly.
  2. `set "WORK=%TEMP%\alpha-regress-%RANDOM%%RANDOM%"` (regress.bat:82): back-to-back `cmd /c` runs in the same second get the same `%RANDOM%` seed, so the next run hits `ERROR: cannot create ... already exists`. The early `exit /b 1` after a failed copy also skips the cleanup at line 272, which leaves the directory behind. Fix: add a uniqueness loop (`if exist` → retry) or use a PowerShell GUID, and clean up on every exit path.
- The CI (`windows-packaging.yml`) doesn't see these problems because each mode runs on a fresh runner with a short workspace path.
- **AI-046 stays REVIEW** until AI-065 lands and all 6 break modes report "intentional break correctly detected" on this PC.

New item: **AI-065** (P1, Muse, releases lane), repair both regress.bat defects above; acceptance is a local Windows run of all 6 `--break` modes back-to-back from a deep path.
