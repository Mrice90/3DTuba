# Board Event Contract v1 (AI-062)

**Status:** draft for review · **Source pin:** `Mrice90/TubaExperiment @ 992bc95`
(branch `strip/zeus-poseidon-desktop`) · **Scope:** read-only inspection of
`game-core`; no Java changes.

## Why this exists

The 3D board, animations and sounds must be driven by **authoritative rule
events**, but the Java core's event vocabulary was never written down for
presentation consumers. `GameEvent` (`game-core/.../core/GameEvent.java`)
already exists with 22 types and `GameState` emits them with a monotonic
`sequence`. This contract:

1. Adopts that vocabulary 1:1 (with exact `file:line` citations at the pin),
2. defines the **wire JSON** the presentation layer consumes,
3. names the **gaps** (damage, capital hits, per-step movement, capital
   placement) and specifies how the adapter derives them — no Java changes
   required to start presentation work.

## Wire envelope

Every event on the wire is a JSON object:

```json
{
  "event": "CARD_PLAYED",
  "seq": 12,
  "turn": 3,
  "player": 0,
  "card_id": "zeus_thunder_ram",
  "instance_id": "3f9a…",
  "from": {"x": 1, "y": 2},
  "to": {"x": 2, "y": 3},
  "stack_index": 1,
  "amount": 3,
  "detail": "free-text from the Java detail string",
  "animation": "deploy",
  "sfx": "DEPLOY"
}
```

`event`, `seq`, `turn`, `player` are always present. The rest are present only
when meaningful for that event (see table). `card_id` is the
`CardDefinition` id (e.g. `zeus_thunder_ram`); `instance_id` is the
`CardInstance` UUID. Coordinates are the 4×6 board (`BoardPosition`, x∈[0,3],
y∈[0,5]; player 0 home is y<3). `animation`/`sfx` use the AI-049 vocabulary
(see § Presentation hooks).

## Event table

`Src` = `game-core/src/main/java/com/infiniteconquest/core/` at `992bc95`.
`detail` quotes the Java `detail` string format.

| # | Wire event | Java type | Src (file:line) | Payload | Presentation hooks |
|---|------------|-----------|-----------------|---------|-------------------|
| 1 | `MATCH_STARTED` | same | `GameState.java:169` | — | board build-up; SFX `SHUFFLE` |
| 2 | `MULLIGAN_COMPLETED` | same | `GameState.java:144` | `amount`=redrawn count | hand fan update |
| 3 | `PHASE_CHANGED` | same | `GameState.java:187,275,289` | `detail`=`START`/`PLAY`/`END` | phase banner |
| 4 | `TURN_STARTED` | same | `GameState.java:287` | — | turn banner; SFX `CLICK` |
| 5 | `TURN_ENDED` | same | `GameState.java:188` | — | — |
| 6 | `CARD_DRAWN` | same | `GameState.java:231,316` | `card_id`, `instance_id` (owner-visible only) | `tutor_draw`; SFX `SHUFFLE` |
| 7 | `DRAW_FAILED` | same | `GameState.java:226,302` | — | empty-deck shake |
| 8 | `EXHAUSTION_DAMAGE` | same | `GameState.java:305` | `instance_id` (the permanent that took 1) | `hit_flash(350ms)`; SFX `DAMAGE` |
| 9 | `GP_GENERATED` | same | `GameState.java:339,346` | `amount` | GP counter tick |
| 10 | `GP_SPENT` | same | `GameState.java:204` | `amount` | GP counter tick |
| 11 | `CARDS_UNTAPPED` | same | `GameState.java:297` | `amount`=untapped count | ready glow |
| 12 | `CARD_PLAYED` | same | `GameState.java:196` | `card_id`, `instance_id`, `to`, `stack_index` | see §12 |
| 13 | `CHARACTER_MOVED` | same | `GameState.java:208` | `instance_id`, `from`, `to`, `amount`=distance | `move(320ms)` per step (§13) |
| 14 | `ATTACK_RESOLVED` | same | `GameState.java:212` | `instance_id` (attacker), `to` + target `instance_id` in `detail` | `melee(360ms)`/`ranged_projectile`; SFX `MELEE/RANGED` |
| 15 | `OPPORTUNITY_ATTACK` | same | `GameState.java:215` | `instance_id` (attacker), `to`=trigger hex | `melee(360ms)`; SFX `MELEE/RANGED` |
| 16 | `CARD_DESTROYED` | same | `GameState.java:246` | `card_id`, `instance_id` | `destroy(320ms)`; SFX `DESTROY` |
| 17 | `CAPITAL_PASSIVE_TRIGGERED` | same | `GameState.java:334` | `detail`=`<PASSIVE>: <text>` | `capital_shake(450ms)` |
| 18 | `DEVELOPMENT_PASSIVE_TRIGGERED` | same | `GameState.java:373` | `instance_id` | `particle_burst(Kenney)` |
| 19 | `CARD_ABILITY_TRIGGERED` | same | `GameState.java:377` | `instance_id` | `activated_ability`; SFX `CLICK` |
| 20 | `TERRAIN_TRIGGERED` | same | `GameState.java:386` | `instance_id` (source+target), `to`, `amount` | `particle_burst(Kenney)` |
| 21 | `GAME_OVER` | same | `GameState.java:392` | `player`=winner (`-1`=draw), `detail`=reason | victory/defeat cinematic |
| 22 | `DAMAGE_DEALT` | **synthetic** | derived (§22) | `instance_id` (target), `amount` | `damage_float(1200ms)` + `hit_flash(350ms)`; SFX `DAMAGE` |
| 23 | `CAPITAL_HIT` | **synthetic** | derived (§22) | `instance_id` (capital), `amount` | `capital_shake(450ms)` + `capital_hit_flash`; SFX `CAPITAL_HIT` |

### §12 — CARD_PLAYED subtypes
The Java event carries only the instance id (`GameState.java:196`); the
**card type** comes from the `CardDefinition` and selects the presentation:
- `CHARACTER` → character deploy: `deploy` or `deploy_flight(400ms)`; SFX `DEPLOY`
- `LAND` → land stacked: `land_pop(120ms)`; SFX `DEPLOY`
- `STRUCTURE` → structure raised: `deploy`; SFX `DEPLOY`
- `SPELL` → cast: `cast` → `play_flight(400ms)` → `resolve`; SFX `SPELL`
  (the spell instance moves to DISCARD; `CARD_PLAYED` fires from
  `GameEngine.castSpell` via `recordCardPlayed`)
- `CAPITAL` → only at match setup (no event; see § Gaps)

### §13 — CHARACTER_MOVED is one event per move action
`GameState.recordCharacterMoved` (`GameState.java:206`, emit at `:208`) emits **one**
event per move action: `"<instanceId> <from> -> <to> cost <distance>"`.
The per-step path is internal to `GameEngine.moveCharacter`. The adapter
**interpolates**: walk a hex-adjacent path from `from` to `to` in `amount`
steps — odd-row offset adjacency per `BoardGeometry.HEX`
(`HEX.distance(from, to) == amount`; never Chebyshev) — playing
`move(320ms)` per step. (Teleport/Blink also emit `CHARACTER_MOVED` with
distance 0 — present as a dissolve, not a walk.)

### §22 — Synthetic damage events
`addDamage`/`addCombatDamage` emit **no** Java event; only lethal damage
surfaces as `CARD_DESTROYED` (`GameState.java:246`). The adapter derives:
- after `ATTACK_RESOLVED`, `OPPORTUNITY_ATTACK`, `TERRAIN_TRIGGERED`, or
  spell `STRIKE_CHARACTER`/`DAMAGE_PERMANENT`, diff the target's
  `damage`/`combatDamage` between snapshots → emit `DAMAGE_DEALT`
  (`amount` = delta). If the target is a `CAPITAL`, emit `CAPITAL_HIT`
  instead (SFX `CAPITAL_HIT`, anim `capital_shake(450ms)`).
- `CARD_DESTROYED` always follows lethal damage; presenters should play
  `destroy(320ms)` once (suppress a duplicate damage float on the killing blow).

## Gaps (no Java event; contract position)

| Gap | Position |
|-----|----------|
| `CAPITALS_REVEALED` | Defined in `GameEvent.Type` but **never emitted** anywhere at the pin (dead). Excluded from the wire vocabulary; do not consume. |
| Capital placement | Happens in match setup with no event. Present the capital as part of `MATCH_STARTED` board build-up. |
| Non-lethal damage / capital hits | Derived synthetically (§22). If the Java side later adds events, they replace the derivation. |
| Per-step movement | Interpolated client-side (§13). |
| Spell targeting UI | `GameAction.CastSpell` carries `targetId`/`destination`; the *intent* is UI-side, the *resolution* is `CARD_PLAYED` + effect events. |

## Presentation hooks (AI-049 vocabulary)

Animation keys (`animation_events` in `asset-prompt-directory.json`):
`deploy`, `deploy_flight(400ms)`, `land_pop(120ms)`, `move(320ms)`,
`melee(360ms)/ranged_projectile`, `damage_float(1200ms)`,
`hit_flash(350ms)`, `destroy(320ms)`, `activated_ability`, `cast`,
`play_flight(400ms)`, `particle_burst(Kenney)`, `resolve`,
`capital_shake(450ms)`, `capital_hit_flash`, `tutor_draw`.

SFX cues (`sound_cues`): `DEPLOY`, `DESTROY`, `CLICK`, `DAMAGE`, `MOVE`,
`MELEE/RANGED`, `SHUFFLE`, `SPELL`, `CAPITAL_HIT`.

## Files

- `event-schema.json` — JSON Schema (draft 2020-12) for the envelope.
- `golden-transcript.json` — 16-event Zeus-vs-Poseidon transcript, schema-valid.
- `validate.py` — stdlib validator: schema shape, `seq` monotonic, per-event
  required fields. Exit 0 on the golden transcript; non-zero on broken input.
  Intended for `verify.yml`.
