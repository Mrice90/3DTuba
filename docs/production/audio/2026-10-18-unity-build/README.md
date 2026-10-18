# Infinite Conquest: card sounds playtest build (2026-10-18)

Owner: Claude (AI-CARD-SFX-BUILD, laptop thread, coordinator serial task 3).

## Run

`playtest/unity-build-card-sfx-2026-10-18/PLAY-INFINITE-CONQUEST.cmd` (portable, no Java). The rig-combat build `playtest/unity-build-rig-combat-2026-10-18/` and its source `3DTuba-rig-combat-2026-10-18/` were not modified.

## What changed

- Sounds: 3DTuba draft PR #24 head `fb1a2b4` (740/740 cues, ElevenLabs, EBU R128 level-matched), exported to `pr24-audio/`.
- Source: `3DTuba-rig-combat-2026-10-18/UnityProof` copied to the new `3DTuba-card-sfx-2026-10-18/UnityProof`.
- `stage_card_sfx.py` copied all 740 files into `Assets/Playtest/Resources/MuseSfx/` (326 replaced in place by their re-levelled versions, 414 new), wrote `Resources/Playtest/card-sfx.json`, and added 144 cues to card cue lists in `Resources/Playtest/cards.json` so they count as the card's own (this is what lets 8 top-rarity characters play their signature summon).
- `Editor/CardSfxBuild.cs` (entry `CardSfxBuild.Build`, then `RigCombatBuild.Build`): turns off Unity's per-clip normalize for MuseSfx (it would undo the level matching), then checks every cue imports with audible data and that each card's cue is what `SfxBank` plays on the matching board event.
- `Editor/AudioFollowupBuild.cs`: two signature checks updated, because two cards now have their own signature clip instead of a fallback.
- No runtime script changed. Rules engine, native DLLs, Codex and ChatGPT folders untouched.

## Verified

| Check | Result |
|---|---|
| Unity build (`logs/unity-build.log`) | Build Finished: Success |
| Card sounds (`editor-checks/card-sfx-validation.json`) | 4,468 checks PASS: 740 cues import, 690 card cues across all 139 cards each resolve to and play their own clip, no generic fallback |
| Inherited editor suites | 17 of 18 results byte-identical to the rig-combat build; restoration now binds 786 card sounds (was 642) and passes. Rig-combat keeps the same 5 known clip-offset notes |
| Packaged smoke | PASS; 446 card-specific sound plays (rig-combat build: 423) |
| Packaged bot matches, both seats, seeds 42/713/7/101/3/2026 (`packaged-results.json`) | 12/12 PASS; 614 actions, totals and winners identical to the rig-combat build |

## What plays and what doesn't

- Plays: every card's summon/place/cast, move, attack, hit, ability (and spell impact) and death/destruction, using its own sound. 8 top-rarity characters play a signature summon.
- Not played yet (imported, inert): new turn change, turn timer, capital warning (calm), coin flip, mulligan, interrupt window and ability panel sounds (handed to Codex, `CODEX_HANDOFF.md`); 123 card idle sounds; 24 signature sounds for top-rarity spells, lands, structures and capitals.
- Not verified: nobody has listened in game. Picks were automatic (closest length). With normalize off, sounds are level-matched but overall quieter than the peak-normalised set; core cue gains were tuned for the old files.
