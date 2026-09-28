# 2026-09-28 10:45 EDT — Claude (covering Astra) independent review: AI-052-WIN / AI-046 / AI-048

Reviewer: Claude, standing in for Astra (out of tokens until ~2026-10-04), at the Product Owner's direction. The review is independent of Muse. It covers the CI evidence plus a code review of the head. It is not a local Windows execution.

## Evidence examined
- Windows packaging run **36419150586** at `ed78731` (latest release-lane code; later commits 1c01571/707d5cb are records only): all 12 steps success — fetch, build+smoke, smoke 5/5 re-run, clean regress, and break modes pin/dep/compile/checksum/smoke.
- Earlier greens 36386714758 (`f1ba391`) and 36387087779 (`39ef992`).
- Code review of `releases/alpha-0.7.15-playable/{build-release,regress,play}.{bat,sh}` and `.github/workflows/windows-packaging.yml` at `707d5cb`.

## Verdict
- **AI-052-WIN: ACCEPTED (Windows happy path + harness).** A clean fetch→build→smoke→regress on windows-latest/JDK 17 passes at the exact head. The `exit` (not `exit /b`) fix correctly ends regress.bat under `call`.
- **AI-046: stays REVIEW.** Blocked by AI-055 (security) below, not by Windows function. It moves to DONE when AI-055 lands and CI is green.
- **AI-048: PARTIAL.** Windows negative coverage exists. The Linux end-to-end run is still unproven (Muse's 07:00 note: sandbox network).

## Findings
| ID | Sev | Finding | Owner |
|---|---|---|---|
| AI-055 | P1 (AI-005) | Dependency trust regression: build scripts download `*.jar.sha1` from the same Maven Central URL as the jar and check against it. That catches corruption, not substitution (a compromised mirror or MITM serves both). Pin the full hashes of jackson-databind/core/annotations 2.18.2 in both scripts and fail closed; keep the fetched .sha1 as a secondary check. Add a break mode proving a self-consistent wrong jar+.sha1 pair is rejected. | Muse |
| AI-056 | P2 (AI-006) | Break-mode detection is stage-level only. `--break=pin/dep/compile/smoke` all pass if `build-release.bat` exits non-zero for *any* reason, so a broken pin or hash check could still be reported "correctly detected" if some other build failure happens. Assert the specific failure: distinct exit codes per check (build-release.bat already has 20–26), and have regress.bat match the expected code per mode. | Muse |
| AI-057 | P3 | `regress.bat` ends with `exit %EXITCODE%`, which closes an interactive console window when run by hand. Document it in the README, or use a wrapper so manual runs keep the window. | Muse |
| note | — | The `verify` stage compares the jar with the checksum generated in the same run, not with the recorded reference. That's acceptable given the documented non-byte-identical rebuilds, but the README/AI-048 wording "drift vs recorded reference" should be corrected to match. play.bat missing/tampered-jar refusal isn't exercised in CI. | Muse |

## Assignments (next checkpoint 12:00 EDT)
- **Muse:** AI-055 (P1), then AI-056 (P2), then AI-048 Linux end-to-end. Stay in the releases lane plus your docs. Record CI run IDs. Self-run CI is delivery evidence; Claude/Astra accept.
- **Claude (covering Astra):** 4 daily checkpoints, independent review of each Muse head, media queue.
- **Meshy:** Thunder Ram, Leviathan Wakeborn and Abyss Gate remeshed (9,936 / ~10K / 29,862 tris) and textured (2K PBR, 30 credits). Next: download and in-game import, which is waiting on the Unity lane. Skyline Seer (AI-050) is held for the PO likeness verdict.
- **ElevenLabs:** AI-053 melee cue, 4 candidates at 0.5 s (~7 credits). Next: palette review with AI-051.
- **Spend rule (PO, 2026-09-28 10:05):** credits may be used without per-job approval; no dollar spend without PO approval.
