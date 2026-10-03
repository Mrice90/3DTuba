# Independent QA: `claude/unity-live-match` `c86e23b` (AI-104/105/106)

Reviewer: Rune (Muse). Date: 2026-10-02 ~20:30 EDT.
Assignment: board `92576e0` QA row (ChatGPT out of tokens; review moved to Rune).
Scope: `c86e23b` "AI-104/105/106: live human-vs-bot by default, ghost-token fix, card faces"
(309 files, mostly first-push of the Unity work; review focused on the
Playtest scripts + the `RulesBridge.java` delta).

## Verdict: PASS with 2 minor observations (no defects)

### AI-104 — ghost-token fix (PASS)

`PlaytestGame.cs`: a `destroyed` `HashSet<string>` tracks instance_ids seen in
`CARD_DESTROYED`. In `Apply()`, events in `ActsOnInstance()` —
`CHARACTER_MOVED`, `ATTACK_RESOLVED`, `OPPORTUNITY_ATTACK`, `DAMAGE_DEALT`,
`CARD_ABILITY_TRIGGERED` — for a destroyed instance are skipped
(`GhostEventsSkipped++`); `CARD_PLAYED` clears the id (replay); `ClearMatch()`
clears the set. This matches the engine behavior in the comment: the engine
can emit `CARD_DESTROYED` before the rest of the same move (opportunity attack
kills the mover mid-move), so the later events must not respawn the token.

The `-playtestSmoke` self-test asserts `GhostEventsSkipped > 0` and
`EnemyShareViolations == 0` on the seed-42 playback. Logic reviewed: the guard
is null-safe (`e.instance_id != null` check), the set can't leak across matches
(`ClearMatch`), and replay is handled. No defect found.

### AI-105 — card faces (PASS)

`CardFaces.cs` (new): loads `Resources/Playtest/card-faces` (staged by
`UnityProof/Tools/stage_card_faces.py` from the pinned alpha jar); `Get(id)`
falls back to `PlaytestCatalog` for the engine's generated tutor cards;
`Art(id)` caches textures (null art cached as null — no repeated failed loads);
`Draw()` renders hand-compact and full-card faces with cost gem, art, stats,
rules text. `PlaytestCatalog.Get(null)` returns a safe stand-in, so
`CardFaces.Get(null)` cannot NRE. GUI.color is saved/restored around `Draw`.
No defect found.

### AI-106 — live human-vs-bot by default (PASS)

`PlaytestGame.Awake`: `-bridgeCmd` / `IC_BRIDGE_CMD` still honored;
otherwise `DiscoverBridge()` finds `<build>/Bridge` (jar + classes) and a Java
17 runtime, else falls back to the recorded playback with a hint. `StartLive`
→ `bridge.New(seed, humanSeat)` → `LiveLoop()` coroutine polls
`TryReceive()`, applies events, `SyncState()`, `ActLive()` on human pick.
Clean fallback chain; no defect found.

**Observation 1 (minor, non-blocking):** `DiscoverBridge()` only looks for
`java.exe` (Windows). On Linux/macOS the auto-discovery silently falls back to
playback. Fine for the Windows playtest builds, but the IC.Net README's
`RelayMatchDriver` sketch should note the platform assumption if a Linux
build is ever cut.

### RulesBridge.java delta (+8 lines) (PASS)

`c86e23b` adds a `to` (target `WireHex`) to `ATTACK_RESOLVED` /
`OPPORTUNITY_ATTACK` events, from the target's live board position with
`lastPos` fallback when the target already left the board. Assessment:
- **Additive only.** No existing field removed or renamed; `hash` op untouched
  (hash is over game state, not wire events); no game-logic change — the
  position is read from the real state, presentation-only.
- **Protocol-safe.** `to` is already a `WireHex` in the AI-062 schema.
- **test_bridge.py: 11/11 PASS** against the `c86e23b` bridge (run 2026-10-02,
  local; includes AI-062 validation of all emitted events, hash properties,
  determinism, GAME_OVER script).
- The committed `golden-seed-42.jsonl` was regenerated with the change; all 4
  attack events in it carry the new `to` field.

**Observation 2 (minor, process):** I could not byte-reproduce the committed
fixture locally — my local `build-release.sh` jar hashes `30943eee…`, not the
canonical `2db3a12c…` (known cross-OS residue; the AI-100 ledger tracks this).
The 11/11 protocol properties hold regardless of the engine jar, and the
fixture's new fields are structurally correct, but the byte-exact fixture
regeneration should be re-verified on a canonical-jar CI run
(`icnet-lockstep.yml` builds the jar but does not assert the canonical hash;
`linux-packaging.yml` does).

### Not covered

- **Playtest smoke in Unity**: cannot run Unity batch builds from this
  sandbox; the `-playtestSmoke` self-test (11 checks) was verified by code
  review only. The committed `docs/reviews/2026-09-29-unity-playtest/
  playtest-smoke.json` reports PASS.
- **Full `c86e23b` file review**: 309 files; review focused on the Playtest
  scripts, the bridge delta, and the smoke-test assertions per the assignment.
  Asset staging scripts (`stage_card_faces.py` etc.) and the 9 screenshots
  were not line-reviewed.

## Defects filed as rows

None. Two minor observations above; neither blocks AI-104/105/106 acceptance.
