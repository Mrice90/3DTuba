# AI-079 — Rules Bridge (headless playable)

A small Java program against the pinned alpha JAR that runs a HEX match
and talks **line-delimited JSON over stdin/stdout**. The Claude Unity
thread spawns this process from UnityProof to make the board playable.
**The protocol below is stable** — v1.3.0. Breaking changes get a
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
- `rules` (optional): `"alpha"` (default, the pinned rules) or `"ic3d"`
  (summon slots + covered Structures, see `overlay/README.md`).
- `reactions` (optional, default `false`): open reaction windows for the
  human during bot turns (see below). Off, the bot plays straight through
  as in v1.2.0.

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

### Reaction windows (AI-080-REACTION-WINDOW)

With `"reactions": true` on `new`, the bot turn pauses after each bot
action (other than ending its turn) for which the human holds a reaction
spell it can afford and legally aim — the same moments the bot itself
gets to react to the human. The response then has
`state.reaction_window: true`, `active_player` still the bot, and `legal`
holding only `react` actions, one `pass` and one `pass_turn`:

```json
{"type":"react","hand_index":11,"card_id":"zeus_chain_lightning",
 "card_name":"Chain Lightning","instance_id":"…","target":{"x":3,"y":4},
 "target_instance_id":"…","target_card_id":"poseidon_kraken_tendril_drone",
 "covered":false,"id":"r8-a0","command":"react 0 11 3 4"}
{"type":"pass","id":"r8-a2","command":"pass"}
{"type":"pass_turn","id":"r8-a3","command":"pass turn"}
```

`act` on any of them answers the window: the reaction resolves (or
nothing happens on a pass), the revision advances, and the bot turn resumes once,
up to the next human decision — another window, the human's turn, or
game over. One reaction per window. `pass_turn` also skips every further
window until the bot ends its current turn; windows can open again on its
next turn. A human holding a castable spell can otherwise see a window
after almost every bot action (hundreds over a long match), so the client
should offer `pass_turn` prominently or auto-pass. A stale, fabricated or engine-rejected
id gets `INVALID_ACTION` and leaves the window open with state and
revision unchanged. `pass` changes no game state: a match in which the
human passes every window reaches the same states and hashes as the same
match with `reactions` off (checked over 60 human actions on seeds 1, 2
and 42, passing 322 to 555 windows).

### `hash` — canonical state hash (AI-097 lockstep)

```json
{"id":"r4","op":"hash"}
```

Reply: `{"id":"r4","ok":true,"revision":3,"turn":3,
"state_hash":"9f2c…64 hex chars"}`.

`state_hash` is SHA-256 over the **canonical** JSON of the full
*unredacted* game state: seed, turn, phase, active player, winner, both
players' GP, and every card in both hands, both decks, both discard
piles and on the board — identities (`card_id`, `instance_id`, owner,
zone) plus all mutable per-card state (damage, combat damage, tapped,
movement spent, attacked/blink/ability flags, bonuses). Canonical form:
object keys sorted, arrays in encounter order (board cells sorted by
`x`,`y`, stacks bottom-to-top), compact separators, standard escaping —
so the same logical state always serializes to the same bytes.

Read-only: `hash` never mutates state or revision. The `state` snapshot
stays redacted; `hash` is the deliberate exception — the relay's
lockstep hash exchange (`prototypes/lobby-lab/docs/relay-design.md`)
compares it across two clients that both legitimately hold the full
state. Seeded setup: both clients start with `new` using the same
revealed seed (AI-097 seed commit-reveal) and apply the same relay
intents in the same order — engine determinism (seed-derived shuffle,
`nameUUIDFromBytes` instance ids, seeded bot RNGs) makes their hashes
match. `NO_MATCH` before `new`, as with `legal`/`act`.

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
| `react`    | same as `cast` (reaction window only) |
| `pass`, `pass_turn` | — (reaction window only) |
| `end_turn` | — |

### State snapshot (redacted)

```json
{
  "seed": 42, "turn": 3, "phase": "PLAY",
  "active_player": 0, "winner": null, "you": 0, "revision": 3,
  "rules": "alpha", "reaction_window": false,
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

- Reaction prompts are opt-in (`reactions` on `new`). Without them a
  human opponent gets no reaction-spell prompt during bot turns and the
  bot loop plays through. Human-vs-bot is the supported playtesting setup.
- No mulligan UI; the mulligan window closes on the first action as in the
  CLI.

## Files

- `RulesBridge.java` — the bridge (stdlib + Jackson from the alpha JAR).
- `overlay/` — 3DTuba rules overlay (engine classes compiled with the
  bridge; `rules:"ic3d"` turns its changes on). `build_classes.py` builds
  the classes for a playtest `Bridge/` folder.
- `audit/` — rules audit (`audit/run.sh <jar>`).
- `test_bridge.py` — protocol tests: determinism, valid act,
  stale/fabricated rejection, no mutation on rejection, bot auto-play,
  scripted GAME_OVER, AI-062 validation of every event, redaction,
  AI-097 `hash` op (canonical hash, read-only, seeded-setup and
  lockstep-determinism proofs), AI-080 reaction windows (react/pass
  only, bad ids change nothing, resume exactly once, off by default).
- `fixtures/golden-seed-42.jsonl` — golden transcript (responses,
  `sort_keys` JSON, one per line).
- `run.sh` / `run.bat` — CI entry points.

## Changelog

- v1.3.0 (2026-10-10): additive. `new` takes `reactions` (default
  `false`). With it, bot turns pause in reaction windows: `legal` offers
  `react` actions, `pass` and `pass_turn`, `state.reaction_window` is
  true, and the bot turn resumes once after the answer. State always carries
  `reaction_window`. With `reactions` off every response, event and hash
  matches v1.2.0 apart from that field (checked over 80-step matches,
  seeds 42/2/7, both rule sets).
- v1.2.0 (2026-10-08): additive. `new` takes `rules` (`alpha` default,
  `ic3d`). Actions: `cast` carries `target_card_id`; `cast`/`activate`
  carry `covered` (true when aimed at, or fired from, a card under the top
  of its stack; the command then ends in that card's instance id). State:
  `rules`, and per stack card `damage`, plus `slots`/`slots_used` on
  Structures and Capitals under `ic3d`. `hash` includes the summon-slot
  ledger under `ic3d` only, so `alpha` hashes are unchanged. A rejected
  human action no longer lets the bot react before the error returns.

- v1.1.0 (AI-097, 2026-10-02): additive `hash` op — canonical SHA-256 of
  the full unredacted state for the relay lockstep hash exchange;
  seeded-setup contract documented (same revealed seed + same intents
  ⇒ identical hashes). Backward compatible with v1.0.0 clients.
- v1.0.0 (AI-079, 2026-09-29): stable protocol — `id`/`op` requests,
  `id`/`ok`/`revision` responses, revision-scoped action ids,
  `INVALID_ACTION` without mutation, redacted state, bot auto-play,
  JSONL-only stdout.
