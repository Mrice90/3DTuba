# Independent QA: AI-104 / AI-105 / AI-106 (claude/unity-live-match @ c86e23b)

**QA owner:** Muse (Rune) — assigned P1 at the 2026-10-02 ~20:45 EDT IC-S03 replan ("Independent QA of `claude/unity-live-match` `c86e23b`: rebuild the bridge classes, run test_bridge.py + playtest smoke if possible, review AI-104/105/106 code. Acceptance: Written QA note with any defects as new rows.").
**Run:** lobby-lab-reflection-2000, 2026-10-02 ~20:00 EDT.
**Reviewed branch state:** `claude/unity-live-match` tip `f49a1c0` at review time; the 19 IC.Net client commits sit directly on top of `c86e23b` (the QA target), which is untouched. All code review below is against `c86e23b`; the bridge rebuild is against the v1.1.0 `RulesBridge.java` on `muse/sprint-01-content-audit` tip.
**Verdict: no defects found.** The three AI-104/105/106 code changes are reviewed clean; the bridge rebuild matches Claude's "rebuilt from v1.1.0 source" claim (test_bridge.py 11/11). No new backlog rows needed. Acceptance ownership stays where the board put it: AI-104/105 review is Claude's NEXT row; AI-106's live acceptance is Mathew's mouse match (AI-080).

## AI-104 — ghost token (PlaytestGame.cs)

Mechanism: a `HashSet<string> destroyed`; `CARD_DESTROYED` adds the instance id; events that act on an instance (`ActsOnInstance`: CHARACTER_MOVED, ATTACK_RESOLVED, OPPORTUNITY_ATTACK, DAMAGE_DEALT, CARD_ABILITY_TRIGGERED) are skipped when the id is in the set (`GhostEventsSkipped++`); `CARD_PLAYED` removes the id (replay/new-play safe); `ClearMatch()` clears the set.

Instance-acting case coverage in `Apply()` was reviewed case by case and is complete: CHARACTER_MOVED ✓ (the respawn path — the pre-fix code spawned a fresh piece at `from` when the piece was missing), ATTACK_RESOLVED/OPPORTUNITY_ATTACK ✓, DAMAGE_DEALT ✓, CARD_ABILITY_TRIGGERED ✓, CARD_DESTROYED ✓ (source), CAPITAL_HIT ✓ (targets by card id via `DetailCardId()`, capital pieces are not instance-tracked), and MATCH_STARTED/TURN_STARTED/PHASE_CHANGED/TURN_ENDED/CARD_DRAWN/GP_GENERATED/GP_SPENT/CARD_PLAYED/CAPITAL_REVEALED/GAME_OVER don't act on piece instances.

Smoke gains two checks: `EnemyShareViolations == 0` (every hex scanned after every event in smoke mode — consistent with `MovementRules.passability`: an enemy Character is BLOCKED, so no hex may hold Characters of both seats) and `GhostEventsSkipped > 0` (a tripwire: smoke fails if seed-42 stops producing post-death events, which would mean the engine's event ordering changed — intentional, not a bug).

Notes (non-blocking): if a future engine ever revives an instance mid-match, the set would suppress its events until `CARD_PLAYED` — by design for v1.0 (no revive in the pinned alpha). Skipped events don't add to the match log, so the log stays honest.

## AI-105 — card faces (CardFaces.cs, stage_card_faces.py, card-faces.json, CardArt/*.jpg)

`stage_card_faces.py` reads the pinned alpha jar read-only, extracts 13 fields + keywords per card, stages art resized to 384px, and writes the JSON with explicit `encoding="utf-8"` / `newline="\n"` (AI-069-safe). Independently verified at `c86e23b`: **143 faces, 119 with art — all 119 JPGs present and their paths match the JSON `art` fields exactly** (zero faces-with-art-but-no-file). `CardFaces.Get` never throws: unknown ids fall back to the catalog stand-in (and `PlaytestCatalog.Get` itself returns typed stand-ins, never null). The 20 engine-generated tutor cards correctly get name/type-only faces. Smoke check `CardFaces.Count >= 139 && arts >= 100` is wired and truthful against `PlaytestCatalog.All` (139).

Notes (non-blocking): the JSON carries 143 faces vs 139 catalog entries — the extras are DEMO/UNASSIGNED prototypes, harmless since nothing looks them up; staged art misses 24 cards (the 20 tutors + 4 others), shown as "art pending".

## AI-106 — live human-vs-bot by default (PlaytestGame.cs DiscoverBridge)

`DiscoverBridge` probes `<build>/Bridge` then `<build>/../Bridge` for a `*.jar` plus `classes/RulesBridge.class`, then finds Java via `JAVA_HOME` then `PATH`, and launches `"java" -cp "classes<SEP>jar" RulesBridge`. Missing bridge or Java falls back to the recorded playback with an on-screen reason (shown in the top bar via `bridgeHint`); `-playback` forces the recording; `-bridgeCmd`/`IC_BRIDGE_CMD` still override.

Notes (non-blocking): the lookup is Windows-only (`java.exe` hard-coded) — matches the Windows exe target, no defect; if several jars sit in `Bridge/`, the first alphabetically wins — deterministic, but the folder should ship exactly one jar; `Bridge/` itself is a build-time artifact (not committed), so the distribution must place jar + classes next to the exe or the game silently falls back to playback (with the reason on screen, which is the honest failure mode).

## Bridge rebuild + test_bridge.py (the "rebuild the bridge classes" step)

Rebuilt `RulesBridge.java` (v1.1.0 source from `muse/sprint-01-content-audit` tip) with Temurin JDK 17.0.20.1 against the canonical jar `2db3a12c92dbd2acf0de251535b58bb13ab869eaae3075f07a6abaa63fbae86b` → clean compile. `test_bridge.py`: **PASS 11/11** — deterministic seed-42 output, valid act, stale/fake → INVALID_ACTION with no mutation, bot auto-play, scripted GAME_OVER (66 turns, winner 0), 3490/3490 events AI-062 VALID, hidden-state redaction, plus the 5 hash properties (canonical 64-char hex, read-only, state-sensitive, seeded cross-process equality, divergent-intent divergence).

## Playtest smoke (`-playtestSmoke`)

**Not executable in this sandbox** — the Unity build needs a Unity/Windows environment. The commit message claims `PLAYTEST_SMOKE PASS (11 checks)` plus one live turn played by mouse (Arc Relay Scout to 1,1; bot attacked, destroyed it, hit the Zeus capital 20→19); those claims are recorded but not independently verified here. The smoke *code* itself (the two new AI-104 checks, the AI-105 face/art counts) was reviewed and is sound. AI-106's real acceptance remains Mathew's mouse-driven match (AI-080), which no automated check can substitute.
