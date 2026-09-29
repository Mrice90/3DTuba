# AI-075 — Event-to-presentation timeline

`timeline.py` turns an AI-066 JSONL event dump plus the presentation
manifest into a per-event cue schedule for the Unity presentation layer
(AI-060b/c).

## Usage

```
timeline.py <events.jsonl> <presentation-manifest.json> [<out.json>]
```

## Output

A JSON array of cues, one per input event, in order. Each cue:

| field | meaning |
|---|---|
| `seq`, `event`, `turn`, `player` | from the input event |
| `card_id`, `instance_id` | resolved via CARD_PLAYED (may be null) |
| `start_ms` | cue start; sequential — each cue starts when the previous ends |
| `duration_ms` | from the manifest animation string (`deploy(400ms)`), else the default table |
| `anim_key` | animation clip key (manifest, else event default) |
| `sfx_key` | SFX key from the manifest (may be null) |
| `impact_hook` | camera/impact hook for AI-060c: `shake_small`, `flash`, `shake_large`, or `none` |

## Default durations (ms)

CARD_PLAYED 800, CHARACTER_MOVED 600, ATTACK_RESOLVED 1000,
OPPORTUNITY_ATTACK 800, DAMAGE_DEALT 400, CARD_DESTROYED 800,
CARD_ABILITY_TRIGGERED 600, TERRAIN_TRIGGERED 600, CARD_DRAWN 300,
GP_GAINED/SPENT 200, TURN_ENDED/PHASE_CHANGED 300, GAME_OVER 2000,
anything else 500.

## Files

- `timeline.py` — the tool (stdlib only).
- `test_timeline.py` — determinism, golden match, schedule sanity.
- `fixtures/dump-seed-42.jsonl` — AI-066 dump, seed 42 (235 events).
- `fixtures/golden-seed-42-timeline.json` — golden cue schedule (235 cues, 87.9 s).
