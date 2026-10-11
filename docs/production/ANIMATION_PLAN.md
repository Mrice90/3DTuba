# Animation plan: summon, move, attack, hit, death (AI-060b)

Owner: Claude (animation thread), 2026-10-08. Code: `UnityProof/Assets/Playtest/Scripts/UnitAnimator.cs`.

Every board piece gets a `UnitAnimator`. It moves the piece's visual parts onto a `Pivot` child, so body motion never fights `BoardView` (which owns the root position and slot scale). Each rule event the match plays (recorded or live) calls one clip. Each clip uses the model's own skeletal clip when the staged model has one, and the procedural version below otherwise. Root travel (hex hops, lunges) is always procedural, so it lines up with the board.

## Animation list

| Event (rules) | CHARACTER | STRUCTURE | CAPITAL | LAND |
|---|---|---|---|---|
| `CARD_PLAYED` → summon | Teleport beam, then the unit materialises (thin and tall to full size with overshoot), then a landing squash and sparks | Rises out of the ground with shudder and dust, then a core flash and light camera shake | Same as structure, bigger and slower | Terraform: the slab grows up out of the tile with a ripple ring |
| idle | Hover bob and weight shift (procedural), or the model's idle clip (looped) | still | still | still |
| `CHARACTER_MOVED` → move | Turns, anticipates, then hops hex by hex along the path with forward lean, squash and a dust puff on each landing. The walk clip loops while moving when the model has one | n/a | n/a | n/a |
| `ATTACK_RESOLVED` / `OPPORTUNITY_ATTACK` → attack | Wind-up (turns, leans back, charge sparks). Range 1 units lunge with an energy slash; ranged units recoil and fire the bolt | Turret recoil plus bolt | Turret recoil plus bolt | n/a |
| `DAMAGE_DEALT` / `CAPITAL_HIT` → hit | White flash, flinch away from the attacker, scaled by damage. 3+ damage shakes the camera | Flash and rattle | Flash, rattle and shake | Flash and rattle |
| `CARD_DESTROYED` → death | Flash, stagger, topple away from the killer, dissolve into rising sparks and sink. Leaves a scorch mark | Shudder, then blows apart into faction-coloured debris with fire and smoke, then collapses into the ground. Heavy camera shake | Same at capital scale; also plays at `GAME_OVER` for the losing capital | Cracks and sinks |
| SPELL | No board piece. It keeps the existing cast burst (`Vfx.SpellBurst`) | | | |

## Clip sources

1. **Skeletal clips from Meshy.** `glb_to_playtest_token.py` now keeps the armature and every action of a rigged GLB (`--static` forces the old baked token). `PlaytestTokenImport` imports the clips and loops idle, walk and run. `UnitAnimator.CueForClipName` maps clip names to cues (death/dead/die, hit/hurt, attack/shoot/slash/cast, walk/run, summon/spawn/intro, idle). Clips play through a `PlayableGraph`; no Animator Controller asset is needed.
2. **Procedural clips.** These cover every type with no assets needed, so all 139 cards animate today.
3. **Effects.** Particles come from `Vfx`; scorch and debris are in `UnitAnimator`. Camera trauma is in `CameraRig.Trauma`.

The smoke run (`-playtestSmoke`) plays the full set for one piece of each type and fails on any exception. It also reports `riggedModels`, the number of staged models that carry skeletal clips.

## What still needs assets (asset library thread)

- **Rigged characters with clips.** Meshy auto-rig and animate for all 48 character models: idle, walk, attack, hit and death at least. Right now 3 characters are staged from `rigged/` GLBs (Olympian Storm Titan, Keraunos Prime, Poseidon's Trident Core). Until they are re-staged with the new converter, none carries clips in Unity.
- **One GLB per card with every action.** Meshy's animation endpoint exports one GLB per animation. Those must be merged into a single GLB, or the converter must be extended to read `*_animated` siblings, before staging.
- **33 structure models** still use stand-ins (Meshy credits ran out). Structures and capitals stay procedural by design (tweens, no rigs).
- **Signature presentations** for apex (rarity 4) cards: a bespoke summon per apex card. This is future work after the base set is reviewed.
- **Spell cast presentations** per spell (16 cards). Spells currently share the faction burst.
