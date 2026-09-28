# AI-049 — Skyline Seer production batch

Card: `zeus_ability_skyline_seer` / Skyline Seer. Brief version `AI-049-v1`, derived from the AI-050 and AI-051 v1 briefs in `docs/production/ASSET_QUEUE.md`. The v1 creative intent is unchanged. The only additions are details taken from repository evidence. Nothing has been submitted to Meshy or ElevenLabs, and no credits have been spent.

## Authoritative source row

`docs/muse/sprint-01/manifest.csv` line 23 (the matching `manifest.json` entry has the same values):

| Field | Value |
|---|---|
| card_id | `zeus_ability_skyline_seer` |
| name / faction / type | Skyline Seer / ZEUS / CHARACTER |
| source / status | `alpha-json` / PROTOTYPE (`expansion_gap=false`, runtime via PrototypeCardPool) |
| provenance | `game-core/src/main/resources/cards/faction-ability-cards.json @ 992bc95c7164416ea0a25a4ce120f6ec0a0a167a` |
| art_path / art_status | `game-gui/src/main/resources/art/characters/zeus_ability_skyline_seer.jpg` / bespoke |
| needed_3d | 3D character model — stylized, faction-themed; idle/attack/hit/death states |
| animation_events | `deploy_flight(400ms);move(320ms);melee(360ms)/ranged_projectile;damage_float(1200ms);hit_flash(350ms);destroy(320ms)` |
| sound_cues | `DEPLOY;MOVE;MELEE/RANGED;DAMAGE;DESTROY` |

Evidence limits:
- The row's events and cues are type-derived from `Fx.java` timings and the `SoundEffects.Cue` enum (`docs/muse/sprint-01/audit-report.md` §3, §5). They are not per-card design.
- The bespoke reference art sits in the reference repository at the pinned commit. It is not in this repository and was not inspected.
- **Verified 2026-09-27 (Claude Code, pinned source @ `992bc95`):** the card has `range: 2`, `movement: 3`, attack 4 and the `SHARP_SHOT` keyword. The alpha GUI plays RANGED when the target is more than one tile away and MELEE when adjacent (`game-gui/.../InfiniteConquestGui.java` line 2330). Ranged is the primary attack, so this batch keeps the v1 staff-bolt cue. Adjacent attacks will play MELEE, which this batch does not cover. A proposed v2 addition is tracked as AI-053 in `ASSET_QUEUE.md`.
- Generation lengths: `manifest-entry.json` requests 0.5 s for move and hit, the shortest length ElevenLabs generates. The design targets (0.35 s and 0.3 s) are reached by trimming during integration.

## Event mapping

| Event | Animation event(s) from row | Row timing | Sound cue | Generated audio length | Staging file(s) |
|---|---|---|---|---|---|
| deploy | `deploy_flight` | 400 ms | DEPLOY | 0.7 s | `zeus_ability_skyline_seer_deploy_c{01-04}.wav` |
| move | `move` | 320 ms | MOVE | 0.5 s (trim to 0.35 s) | `zeus_ability_skyline_seer_move_c{01-04}.wav` |
| attack | `ranged_projectile` | not given in row | RANGED | 0.8 s | `zeus_ability_skyline_seer_attack_c{01-04}.wav` |
| hit | `hit_flash` (+ `damage_float` 1200 ms) | 350 ms | DAMAGE | 0.5 s (trim to 0.3 s) | `zeus_ability_skyline_seer_hit_c{01-04}.wav` |
| destroy | `destroy` | 320 ms | DESTROY | 1.1 s | `zeus_ability_skyline_seer_destroy_c{01-04}.wav` |

## AI-050 Meshy production brief (AI-049-v1)

Output target: `assets/staging/meshy/AI-050-skyline-seer/`. Model: `zeus_ability_skyline_seer.glb`. Preview: `zeus_ability_skyline_seer_preview.png`.

> Create a stylized 3D battlefield character named Skyline Seer for a mythic Greek lightning-and-sky faction (Zeus) in a turn-based tactical board game. The figure is an original adult oracle-warrior, not a depiction of an existing copyrighted character.
>
> **Silhouette and style:** Give the figure a strong, readable silhouette from an elevated three-quarter board camera. It wears layered white and deep-blue robes, restrained bronze armor, and a wind-swept mantle with subtle storm motifs, and it carries a forked lightning staff. The staff is its attack focus. The character should feel wise, mobile, and dangerous rather than bulky.
>
> **Pose:** Use a symmetrical neutral A-pose or a relaxed combat-ready stance suitable for later rigging. The model will later need idle, attack, hit, and death states, so keep the limbs and the staff clear of the torso.
>
> **Geometry and materials:** Use clean game-ready geometry and closed, watertight surfaces where practical. Avoid tiny floating pieces and extreme cloth wisps. Separate the materials logically: robe, armor, staff, and skin/hair. Keep materials game-safe, with no photoreal human skin.
>
> **Technical:** Deliver a Unity-friendly GLB with Y as the up axis, facing +Z, feet at the origin, and a height of about 1.8 Unity units after import. Do not fuse a base to the feet.
>
> **Prohibited:** text, logos, gore, photoreal human skin, fused bases, and the likeness of any existing character.
>
> Provide a preview and the generated model. Do not invent gameplay mechanics or ask follow-up questions.

Acceptance (queue AI-050): the preview is approved; scale, orientation, materials, and topology are inspected; the optimized variant is produced during cleanup; and the Unity import succeeds.

## AI-051 ElevenLabs sound brief (AI-049-v1)

Output target: `assets/staging/elevenlabs/AI-051-skyline-seer/`. One file per cue candidate, named `zeus_ability_skyline_seer_{event}_c{01-04}.wav`.

> Create short, nonverbal game sound effects for Skyline Seer, a mythic Greek storm oracle in a turn-based tactical board game. The shared palette is airy electricity, bronze resonance, controlled wind, and faint mystical chimes. Every cue is a one-shot, not a loop.
>
> - **deploy** (0.7 s): a descending air rush that ends in a precise electric seal. The deploy flight animation runs 400 ms, and the seal should land near its end.
> - **move** (0.5 s, with all energy in the first 0.35 s): a light cloth/wind step with a tiny static lift, matching the 320 ms move.
> - **attack** (ranged, 0.8 s): a focused staff charge and a sharp sky-bolt release.
> - **hit** (0.5 s, with all energy in the first 0.3 s): a brief electrical crack with a restrained bronze impact, matching the 350 ms hit flash.
> - **destroy** (1.1 s): collapsing storm energy, a cloth fall, and a fading chime.
>
> **Prohibited:** speech, chanting, melody, thunderclap clichés, modern machinery, gun-like transients, excessive bass, and long reverb tails.
>
> Produce discrete files, not a montage. Format: 48 kHz WAV. Output must be unclipped. Final loudness and peak normalization happen during game integration.

Variation count: four candidates per cue, but only after cost approval. Acceptance (queue AI-051): the cues are distinct, unclipped, and readable in the mix, and each is mapped to its exact event and imported.

## Tracking and validation

`manifest-entry.json` records:
- the exact source row and provenance
- the event mapping
- the output paths
- the technical constraints
- a provider record for each job, with every field ASSET_PIPELINE.md requires: provider, job ID, brief version, credit estimate and charge, output location, reviewer, and result. These are `null` until submission.

Validate the batch:

```
cd docs/production/batches/AI-049-skyline-seer
python validate_batch.py
python -m unittest test_validate_batch -v
```
