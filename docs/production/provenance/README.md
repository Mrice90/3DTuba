# AI-content disclosure ledger (AI-219-AI-DISCLOSURE)

Steam asks every game to disclose AI-generated content on its store page and in its content survey. `AI_CONTENT_LEDGER.csv` lists, for each of the 139 Zeus and Poseidon cards that ship in 1.0, where its card art, 3D model and sound effects came from. `build_ledger.py` rebuilds it from files already in this repo.

## What the ledger shows (2026-10-16)

| Asset | Count | Made with | Source of truth |
|---|---|---|---|
| 3D models, characters, capitals and lands | 89 | Meshy (generative AI 3D), then cleaned, decimated and re-textured in Blender | `docs/reviews/2026-10-08-asset-library-gaps.csv`, Meshy batch manifests |
| 3D models, structures | 34 | Built by script in Blender 5.2.2; no generative AI | `assets/staging/structure-pilots-2026-10-09/README.md` |
| Spells | 16 | No model; effects only | none |
| Sound effects | 326 of 729 cues made (39 core, 287 card) | ElevenLabs (generative AI sound effects) | `assets/audio/SFX_MANIFEST.json`, `docs/production/audio/sfx_ledger.jsonl` |
| Card art | 134 from Desolate-Tuba, 5 from the TubaExperiment alpha | **Unknown: Matt to confirm** | `source_art` column of the gap list |

The sound thread is generating the remaining cues in ElevenLabs. Codex's SP1 audio set (190 cues, 2026-10-07) is outside this repo, and its tool should be confirmed and added once that folder is reachable.

## Still open

1. **Card art provenance.** Matt needs to say how the Desolate-Tuba and TubaExperiment card art was made (hand-drawn, commissioned, or an image generator, and which one). The card faces are the most visible asset, so this answer decides most of the disclosure.
2. **Meshy task IDs.** Several rows say "not recorded"; batch-07 holds only ID prefixes. Steam doesn't require task IDs, but they help prove which job made which file.
3. **Music.** None exists yet (AI-206-MUSIC). Add it here when it lands.

## Draft Steam disclosure (fill in the card-art sentence once Matt answers)

> **Pre-generated content.** Most 3D unit, capital and land models began as AI-generated meshes (Meshy) made from our own card art, then were cleaned up, decimated and re-textured by hand in Blender. Our sound effects were generated with ElevenLabs from written prompts and then selected, trimmed and loudness-matched by us. Structure models were built procedurally in Blender without generative AI. [Card art: to be confirmed.] All AI output was reviewed against our art direction before it went into the game.
>
> **Live-generated content.** None. The game generates no content with AI while it is running.
