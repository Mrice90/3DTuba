# Game SFX set (ElevenLabs)

The complete sound effect set for Infinite Conquest 3D: every card cue in the presentation manifest plus the board, turn and UI sounds that no card owns.

## Files

| Path | What it is |
| --- | --- |
| `card_voices.json` | One sound identity per card (materials and mechanisms in the neo-futuristic style). Edit this to change how a card sounds. |
| `build_sfx_needs.py` | Builds `SFX_NEEDS.json` from the presentation manifest and `card_voices.json`. |
| `SFX_NEEDS.json` | The needed-SFX list: 729 cues (39 core + 690 card), each with its prompt and target length. |
| `fetch_takes.py` | Downloads one cue's ElevenLabs takes and appends a line to `sfx_ledger.jsonl`. |
| `sfx_ledger.jsonl` | Provider record per cue: flow ID, node ID, all four generation IDs, credits. |
| `pick_takes.py` | Picks one take per cue, trims leading silence, peak-normalizes to -6 dBFS, adds a 15 ms fade, writes `assets/audio/sfx/<key>.mp3` and `assets/audio/SFX_MANIFEST.json`. |
| `picks_override.json` | Optional `{ "<key>": <take 1-4> }` to replace an auto-pick after listening. |

## Naming and layout

Card cues use the manifest key `<card_id>_<cue>` with the AI-077 cue set: `deploy`, `move`, `attack`, `hit`, `destroy`, `ability`, `idle`, and `signature` for rarity-4 cards. Files live at `assets/audio/sfx/<key>.mp3`, which `coverage.py` already resolves as `sfx/<key>.mp3` when run against `assets/audio`.

Core cues use the `core_` prefix and map to board events like this:

| Board event | Core cue |
| --- | --- |
| `MATCH_STARTED` | `core_match_start`, `core_board_build`, then `core_ambience_<faction>_board` (loop) |
| `TURN_STARTED` | `core_turn_start_player` / `core_turn_start_opponent` |
| `CARD_DRAWN` | `core_card_draw` (tutor draws: `core_tutor_draw`) |
| `CARD_PLAYED` (LAND) | `core_hex_tile_place` layered under the card's `deploy` |
| `DAMAGE_DEALT` | `core_damage_number` layered with the target's `hit` |
| `EXHAUSTION_DAMAGE` | `core_exhaustion_damage` |
| `OPPORTUNITY_ATTACK` | `core_opportunity_attack` layered with the attacker's `attack` |
| `CAPITAL_HIT` | the capital's `hit`, plus `core_capital_warning` at low health |
| Match end | `core_victory` / `core_defeat` |
| Hex hover / select / legal moves / invalid | `core_hex_hover`, `core_hex_select`, `core_legal_move_show`, `core_invalid_action` |
| Hand and menus | `core_card_*`, `core_ui_*`, `core_end_turn_button`, `core_turn_timer_warning` |

## Regenerating

Each cue is one ElevenLabs `eleven_text_to_sound_v2` generation with 4 takes (about 67 credits per cue). Never re-run a generation to retry a download: re-poll the run for fresh URLs and run `fetch_takes.py` again. The raw takes download to `assets/audio/takes/`, which is gitignored; all takes stay on the ElevenLabs flows listed in the ledger.

```
python3 docs/production/audio/build_sfx_needs.py
python3 docs/production/audio/pick_takes.py
```

## Review

Picks are automatic (closest length to the target), not listened to. The in-game review should swap weak picks through `picks_override.json` and re-run `pick_takes.py`.
