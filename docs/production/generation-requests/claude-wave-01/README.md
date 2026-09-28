# AI-052 — Claude generation wave 01

Brief version `AI-052-v1`. This wave holds three style-library requests, each with one Meshy job and one ElevenLabs job. They are ready for the coordinator to launch. **Nothing has been submitted to any provider and no credits have been spent.**

Source: Muse asset prompt directory v1, `docs/muse/sprint-01/asset-prompts/asset-prompt-directory.json` on `origin/muse/sprint-01-content-audit` @ `fb19be14c07972fc95a34b5762842a647544e5c2`. It was read with `git show` only and was not checked out, merged, or edited. The machine-readable requests are in `requests.json`.

## Selections

| Role | Card ID | Name | Silhouette | SFX group (cards) |
|---|---|---|---|---|
| Zeus character | `zeus_siege_thunder_ram` | Thunder Ram (CHARACTER, rarity 3, SIEGE) | low quadruped wedge, horns leading | ZEUS_CHARACTER (10) |
| Poseidon character | `poseidon_leviathan_wakeborn` | Leviathan Wakeborn (CHARACTER, rarity 3, VANGUARD) | tall limbless S-coil | POSEIDON_BEAST (3) |
| Board structure | `poseidon_abyss_gate` | Abyss Gate (STRUCTURE, rarity 3, FORTRESS) | wide squat ring-in-arch | POSEIDON_FORTRESS (4) |

Why these three:
- `zeus_ability_skyline_seer` is excluded.
- None of the three SFX groups overlaps the in-flight Seer work (AI-050/051/053, group ZEUS_HUMAN).
- The three outlines differ in both height and footprint.
- The Zeus spires were passed over because a tall needle would echo the Seer's upright staff.
- "Thunder Ram" is read as a war ram beast (the card is a SIEGE character), not a battering-ram object.

## Staging directories

Each directory holds a `tracking.json` with every field ASSET_PIPELINE.md requires. All fields are `null` until the job is submitted.

| Card | Meshy | ElevenLabs |
|---|---|---|
| `zeus_siege_thunder_ram` | `assets/staging/meshy/AI-052-zeus_siege_thunder_ram/` | `assets/staging/elevenlabs/AI-052-zeus_siege_thunder_ram/` |
| `poseidon_leviathan_wakeborn` | `assets/staging/meshy/AI-052-poseidon_leviathan_wakeborn/` | `assets/staging/elevenlabs/AI-052-poseidon_leviathan_wakeborn/` |
| `poseidon_abyss_gate` | `assets/staging/meshy/AI-052-poseidon_abyss_gate/` | `assets/staging/elevenlabs/AI-052-poseidon_abyss_gate/` |

## Provider settings

- **Meshy:** Text to 3D, Meshy 6, Pose None, 1 generation, Private license. Use the `meshy.prompt` in `requests.json` verbatim; each prompt is under 800 characters and self-contained. The target is a GLB with +Y up and +Z forward. The origin and height targets are listed per request. Estimated cost is **20 credits per request, 60 for the wave**, based on the AI-050 charge. Texture and remesh are extra and need separate approval.
- **ElevenLabs:** Sound Effects, with the model to be confirmed at submission (`eleven_text_to_sound_v2` is expected). Use the Muse SFX-group brief verbatim.
  - The outputs are **group assets** named `<group>_<cue>.wav`, shared across every card in the group.
  - No credit estimate is recorded yet. The coordinator records the flow's estimate and gets PO approval first (the AI-051 precedent is 158.4 credits).
  - ElevenLabs generates no shorter than 0.5 s. Shorter cues are generated at 0.5 s and trimmed at integration.

## Problems in the Muse briefs (flagged, not edited)

- **ZEUS_CHARACTER and POSEIDON_BEAST:** the brief's five designed effects are select/deploy/attack/hit/death, but its covered cues include MOVE and have no select cue. Generate MOVE as the fifth effect or confirm with Muse.
- **POSEIDON_FORTRESS:** the brief designs attack/hit/death, but the card's cues are only CLICK/DEPLOY/DESTROY. Generate only select, deploy, and death unless Muse says otherwise. This also saves credits.

## Review gates

| Gate | Stage | Owner | Check |
|---|---|---|---|
| G0-validated | before submission | Claude Code | `validate_requests.py` exits 0 and the tests pass |
| G1-cost-approved | before submission | PO | Approve the exact credit estimate for each job |
| G2-submitted-recorded | at submission | Coordinator | Record in `tracking.json`: job ID, time, submitter, and balance before/after. Check the provider history first and never resubmit an uncertain paid job |
| G3-preview-review | after generation | Astra + PO | Silhouette matches from the board camera, key features are present, and there is no existing-IP likeness. A failure means REJECT, not a silent regenerate |
| G4-technical | after download | Astra | GLB passes AI-054 `check_glb.py` and is remeshed to the board budget. Audio is unclipped, nonverbal, and correctly named |
| G5-in-game | integration | Astra | Unity import, in-game screenshot, and a dense-mix listen. Then the manifest row and commit |

## Validate

```
cd docs/production/generation-requests/claude-wave-01
python validate_requests.py
python -m unittest test_validate_requests -v
```

The validator reads the Muse directory with `git show fb19be14c07972fc95a34b5762842a647544e5c2:<path>`. It needs the `origin/muse/sprint-01-content-audit` objects to have been fetched.
