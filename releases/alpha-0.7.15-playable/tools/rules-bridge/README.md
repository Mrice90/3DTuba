# AI-079 — Rules Bridge (headless playable)

A small Java program against the pinned alpha JAR that runs a HEX match
and talks **line-delimited JSON over stdin/stdout**. The Claude Unity
thread spawns this process from UnityProof to make the board playable.
**The protocol below is stable** — v1.0.0. Breaking changes get a
minor-version bump and a changelog entry here.

- Board: 4×6 HEX, odd-row offset (`MatchRules.hex()` / `BoardGeometry.HEX`).
- Capitals: player 0 `(1,0)`, player 1 `(2,5)`.
- Decks: the alpha starter decks, unchanged.
- Bot: seeded RNG (deterministic per match seed); difficulty MORTAL, HERO
  or DEMIGOD.

## Protocol

One JSON object per line in each direction. **Stdout is JSONL only** —
diagnostics go to stderr. Every request carries `id` and `op`; every
response carries `id`, `ok` and `revision`, so requests and responses
correlate even when pipelined. Success responses include `events` (AI-062
wire format), `state` (redacted) and `legal`. Errors never mutate state or
revision.

### `new` — start a match

```json
{"id":"r1","op":"new","seed":42,"human_player":0,
 "human_faction":"ZEUS","bot_faction":"POSEIDON","difficulty":"HERO"}
```

- `seed`: integer match seed (bot RNGs derive from it; same seed replays).
- `human_player`: `0` or `1` — which seat the human controls.
- `human_faction` / `bot_faction`: `"ZEUS"` or `"POSEIDON"`.
- `difficulty`: `"MORTAL"`, `"HERO"` or `"DEMIGOD"`.

Reply: `{"id":"r1","ok":true,"revision":0,"events":[...],"state":{...},
"legal":[...]}`. If the opening player is a bot, its turns run
automatically and their events are included, so the reply always lands on
a human decision (or game over).

### `legal` — legal actions for the current human player

```json
{"id":"r2","op":"legal"}
```

Reply: `{"id":"r2","ok":true,"revision":3,"events":[],"state":{...},
"legal":[...]}`.

### `act` — perform an action

```json
{"id":"r3","op":"act","action_id":"r0-a5"}
```

Reply on success: `{"id":"r3","ok":true,"revision":4,"events":[...],
"state":{...},"legal":[...]}`. `events` are the AI-062 wire format
(`docs/muse/sprint-02/board-events/event-schema.json`, `seq` renumbered
0..N-1 per response): the human action's events, the bot opponent's
reaction spell (if any), then every automatic bot turn up to the next
human decision or game over.

Reply on a stale or fabricated id — **no state or revision mutation**:

```json
{"id":"r3","ok":false,"revision":3,"error_code":"INVALID_ACTION",
 "error":"unknown or stale action id: r0-a5 (revision 3)"}
```

Error codes: `INVALID_ACTION` (stale/fabricated id, illegal act),
`BAD_REQUEST` (malformed request, unknown op), `NO_MATCH` (no match
started), `INTERNAL` (never expected; report it).

### Action ids

Deterministic and **revision-scoped**: `r<revision>-a<index>`, e.g.
`r3-a12`. The index walks the engine's legal-action list in order, so the
same state always yields the same ids. Ids die with their revision —
re-query `legal` (or read `legal` off any success response) after every
`act`.

Action fields (common: `id`, `type`, `command` — the raw engine command):

| type       | extra fields |
|------------|--------------|
| `play`     | `hand_index`, `card_id`, `card_name`, `instance_id`, `to:{x,y}` |
| `burrow`   | `hand_index`, `card_id`, `card_name`, `instance_id`, `to:{x,y}` |
| `move`     | `from:{x,y}`, `to:{x,y}`, `instance_id`, `card_id` |
| `blink`    | `from:{x,y}`, `to:{x,y}`, `instance_id`, `card_id` |
| `attack`   | `from:{x,y}`, `to:{x,y}`, `instance_id`, `card_id`, `target_instance_id`, `target_card_id` |
| `activate` | `at:{x,y}`, `instance_id`, `card_id` |
| `cast`     | `hand_index`, `card_id`, `card_name`, `instance_id`, `target:{x,y}`, `target_instance_id`, `destination:{x,y}` (teleport only) |
| `end_turn` | — |

### State snapshot (redacted)

```json
{
  "seed": 42, "turn": 3, "phase": "PLAY",
  "active_player": 0, "winner": null, "you": 0, "revision": 3,
  "players": [
    {"seat":0,"faction":"ZEUS","controller":"human","gp":7,
     "hand":[{"card_id":"...","instance_id":"..."}],
     "hand_count":5,"deck_count":35,"discard_count":2},
    {"seat":1,"faction":"POSEIDON","controller":"bot","gp":5,
     "hand":[],"hand_count":5,"deck_count":33,"discard_count":4}
  ],
  "board": [
    {"x":1,"y":0,"stack":[
      {"card_id":"...","instance_id":"...","owner":0}
    ]}
  ]
}
```

- `phase`: engine phase name (`PLAY`, `GAME_OVER`, …).
- `winner`: `0`, `1`, or `null`.
- `turn`: 1-based (engine turn 0 clamped).
- `board`: occupied hexes only; `stack` is bottom-to-top (public).
- **Redaction: the opponent's `hand` is always empty — only
  `hand_count` is exposed. Deck identities are never exposed (counts
  only).** Your own hand is fully visible.

### Example session

```
→ {"id":"r1","op":"new","seed":42,"human_player":0,"human_faction":"ZEUS","bot_faction":"POSEIDON","difficulty":"HERO"}
← {"id":"r1","ok":true,"revision":0,"events":[...setup...],"state":{...turn 1...},"legal":[{...,"id":"r0-a0",...}]}
→ {"id":"r2","op":"act","action_id":"r0-a0"}
← {"id":"r2","ok":true,"revision":1,"events":[...CARD_PLAYED...],"state":{...},"legal":[{...,"id":"r1-a0",...}]}
→ {"id":"r3","op":"act","action_id":"r0-a0"}
← {"id":"r3","ok":false,"revision":1,"error_code":"INVALID_ACTION","error":"unknown or stale action id: r0-a0 (revision 1)"}
```

## v1 limitations

- A human opponent gets no reaction-spell prompt during bot turns; the bot
  loop plays through. Human-vs-bot is the supported playtesting setup.
- No mulligan UI; the mulligan window closes on the first action as in the
  CLI.

## Files

- `RulesBridge.java` — the bridge (stdlib + Jackson from the alpha JAR).
- `test_bridge.py` — protocol tests: determinism, valid act,
  stale/fabricated rejection, no mutation on rejection, bot auto-play,
  scripted GAME_OVER, AI-062 validation of every event, redaction.
- `fixtures/golden-seed-42.jsonl` — golden transcript (responses,
  `sort_keys` JSON, one per line).
- `run.sh` / `run.bat` — CI entry points.

## Changelog

- v1.0.0 (AI-079, 2026-09-29): stable protocol — `id`/`op` requests,
  `id`/`ok`/`revision` responses, revision-scoped action ids,
  `INVALID_ACTION` without mutation, redacted state, bot auto-play,
  JSONL-only stdout.
