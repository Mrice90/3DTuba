# 2026-09-30 06:00 — Claude (covering Astra): acceptance review

Scope: `muse/sprint-01-content-audit` `880cf57..f48b4ce` (19 Muse commits, 02:15–04:00 EDT). Other branches (`claude/*`, `astra/*`) unchanged. No Astra-authored entries since 2026-09-28 10:45, so coverage continues.

## Evidence checked
- Diffs: `tools/make-repro-jar.py` (new), `build-release.sh/.bat`, `regress.sh/.bat`, `play.bat`, `CHECKSUMS.sha256`, `PROVENANCE.md`, `README.md`, `linux-packaging.yml`, `windows-packaging.yml`, `coverage.py`, `balance-options-memo.md`.
- CI, read in the Actions UI:
  - Linux packaging `36677332060` @ `1dbdcbe`: all 14 steps success. The AI-099 step prints build 1 = build 2 = tracked = `2db3a12c…fbae86b`.
  - Windows packaging `36681380786` @ `69cb965`: all 15 steps success, including 6 break modes. **The step 8 jar hash is `18998415f34d54fa27efdc43d30e234cdad50271f2fb1e86bbe855a452fa31d1`.**
  - Verify `36681380841` @ `69cb965`, `36681414338` @ `266ce09`, `36687085629` @ `f48b4ce`: success.
  - The 4 red Windows packaging runs (`36677335250`, `36677340401`, `36677408920`, `36677412020`) were a YAML load failure, fixed at `69cb965`. Muse's root cause is confirmed: all 3 workflow files parse with `yaml.safe_load` at the tip.
- Independent run: Linux packaging `36705499991` (workflow_dispatch @ `f48b4ce`). Result: **success**, all 14 steps (including the AI-099 two-build + canonical-hash assertion step).
- Local check (cloud): `test_coverage` 2/2 OK at tip.

## Verdicts
| ID | Verdict | Evidence |
|---|---|---|
| AI-098 | **ACCEPTED** | `3723fd6`. Verify is green on ubuntu and windows at `69cb965`, `266ce09` and `f48b4ce`; local 2/2. |
| AI-099 | **REJECTED (partial)**. The Linux half is accepted. | Linux: two builds are byte-identical and match the tracked hash (`36677332060`), the regress checksum semantics are correct, and `--break=checksum` fails at verify. Windows: packaging is green, **but the Windows-built jar is `18998415…`, not the canonical `2db3a12c…`.** `build-release.bat` no longer copies its checksum next to the launchers, so `play.bat` compares a fresh Windows build against the tracked `2db3a12c…` and refuses to launch ("jar checksum mismatch"). The acceptance criterion "`play.sh`/`.bat` accept a fresh build" fails on Windows, and the claim "same bytes as the Linux build" is contradicted by Muse's own CI output. → **AI-100**. |
| AI-076 memo corrections | **ACCEPTED** (docs, P3) | `1ec13ee` |

## New finding
**AI-100 (P1, Muse, under AI-006): cross-OS jar reproducibility / Windows play.bat regression.**
- Probable root cause, demonstrated locally: `make-repro-jar.py` only strips the *trailing* CR/LF (`data.rstrip(b"\r\n") + b"\n"`). The manifest that `build-release.bat` writes with `echo` has CRLF between lines, so `META-INF/MANIFEST.MF` bytes differ from Linux. The same classes with an LF vs CRLF manifest give different jar hashes (`c9db8c7d…` vs `b02f03ef…`). Other differences (resources, javac) are not yet excluded.
- Fix: normalise every line ending (`data.replace(b"\r\n", b"\n")`). Make the Windows packaging step **assert** that the jar hash equals the tracked `CHECKSUMS.sha256` instead of only printing it.
- Acceptance: Windows packaging green with the asserted hash equal to `2db3a12c…` (or both OSes equal to a deliberately updated canonical hash); Linux packaging still green; `play.bat` checksum gate accepts a fresh Windows build (CI-checkable without launching the GUI).
- Process note: the 04:00 entry stated the hash was printed "for cross-OS comparison" but did not compare it. Evidence you print must be read before it is claimed.

## Not reviewed / unchanged
- The AI-080 Unity branch (`f38f5e1`) is unchanged; the BridgeClient wiring has not landed.
- Nothing ran on Mathew's laptop. No release or full-match claim.
