# Handoff to Codex: new event sounds from 3DTuba PR #24 (not wired)

From Claude, 2026-10-18 (AI-CARD-SFX-BUILD). Wiring these is your lane; I did not touch your folders, the release line or the rules engine.

## Where the files are

- Imported and shipping (inert) in `3DTuba-card-sfx-2026-10-18/UnityProof/Assets/Playtest/Resources/MuseSfx/core_<cue>.mp3`, so `SfxBank.PlayCore("<cue>")` already resolves them in that copy. Build: `playtest/unity-build-card-sfx-2026-10-18`.
- Originals: `reviews/card-sfx-2026-10-18/pr24-audio/assets/audio/sfx/` (PR #24 head `fb1a2b4`), with `SFX_MANIFEST.json`.
- Event map: `reviews/card-sfx-2026-10-18/pr24-audio/docs/production/audio/README.md` (table "Board event -> Core cue"). Coverage: `SFX_COVERAGE.md` next to it.

## Cues that need a hook

| Cue (`PlayCore` name) | Suggested event |
|---|---|
| `turn_start_player_calm`, `turn_start_opponent_calm` | Turn change. Today `ReactionCue.cs` plays the synthesised `PlayUiSoft("turn")` chime (approved Calm/Off); these are the calm replacements to audition against it, not a forced swap. |
| `turn_timer_warning_calm` (and the older `turn_timer_warning`) | Turn timer running low. |
| `capital_warning_calm` | Capital at low health. `SharedAudio.cs` plays `capital_warning` today. |
| `coin_flip_toss`, `coin_flip_land` | `CoinFlip3D` toss and landing (`MatchRestoration.cs` currently routes the coin to `PlayUi`). |
| `mulligan_swap` | Mulligan replacement confirmed. |
| `reaction_prompt`, `reaction_cast` | Interrupt window opens / a reaction is played (`ReactionCue.cs`). |
| `command_center_open`, `ability_ready` | Ability command center opens / a structure ability becomes usable. |

## Things to know

- All 740 files are loudness-matched (EBU R128: UI -24, idle/ambience -26, events -18, signature/victory -16 LUFS, peak -1 dBFS). In the card-sfx copy I turned off Unity's per-clip `normalize` for every MuseSfx import, because it re-peaks each clip and undoes the matching. If you take these files into your line, do the same or the levels drift back.
- The 326 sounds your 10-11 integration already shipped were replaced in place by their re-levelled versions (same names/GUIDs). `PlayCore` gains in `PlaytestGame.cs`/`SharedAudio.cs` were tuned for peak-normalised clips, so core cues may now sit a little quieter; worth a listen when you wire the rest.
- `MuseAudioBuild`'s 326-entry manifest is untouched and still passes. The 740-entry manifest is `Resources/Playtest/card-sfx.json`, validated by `Editor/CardSfxBuild.cs`.
- Card `idle` cues (123 cards) are imported but nothing plays them yet; that is a presentation hook, not a rules one.
- Signature cues: 32 cards have one, but `PlaytestGame` only plays `signature` for CHARACTER summons, so the 24 signature files for top-rarity spells, lands, structures and capitals are imported and never triggered. If you want them, route `signature` for those card types where the deploy/cast/place cue plays.
- In the copy only, I updated two checks in your `Editor/AudioFollowupBuild.cs` (lines 62-67): Maelstrom Verdict and Olympus Citadel now have their own PR #24 signature clips, so the checks accept "own library clip, else the old fallback". Nothing in your checkout changed.
