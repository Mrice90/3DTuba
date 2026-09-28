# Repo and UnityProof review against the product vision

Reviewer: Claude (repo review thread), 2026-09-28 19:30 EDT. Branch `muse/sprint-01-content-audit` at `1fc5789`. Read-only review; no game code or config was changed.

Vision being measured against: a full 3D board that builds as tokens are played, animated tokens with their own sounds, Hearthstone-class polish (see PRODUCT_BACKLOG.md, "Product vision").

## Where things stand
- **Packaging and CI are in good shape.** Muse delivered AI-048/055/056/057/058: pinned Jackson SHA-256, a `depswap` break mode, distinct exit codes per check, and Windows plus Linux packaging workflows. `verify.yml` covers the manifest validator, lobby-lab and the worker smoke on both OSes.
- **Media is moving.** 6 capitals and 8 apex units are generated, textured and staged; `TokenPreview` renders them on the proof board.
- **The playable 3D game is still a two-case movement proof.** This is the largest gap to the vision.

## Findings

### Architecture (blocks the vision)
1. **No rules in Unity.** `UnityProof/Assets/Proof/MovementState.cs` implements only the AI-036 fixture: a 1-point budget and one hard-coded enemy structure at (1,0). There are no cards, stacks, turns, combat or capitals. The real rules are in TubaExperiment `game-core` (Java, 169 tests reported). AI-003 has not decided how Unity gets authoritative rules (C# port, Java sidecar or server). Until it does, AI-060 cannot start.
   *Action:* AI-062 (board event contract with golden transcripts) lets presentation work begin against recorded events whichever way AI-003 goes.
2. **No presentation layer yet.** No Animator, Timeline, AudioSource, VFX, camera rig or data layer (ScriptableObjects/prefabs) exists. The board and pieces are primitives built in code, and the UI is legacy `OnGUI`. That is fine for a proof, but it is not a foundation for AI-060a–c.
3. **The asset prompts describe the wrong board.** All 139 `meshy_prompt` strings say "hex-based", and every LAND asks for a hexagonal tile. The rules board is a 4×6 square grid with stacking (`BoardPosition` WIDTH 4/HEIGHT 6, Chebyshev distance). *Action:* AI-063, before any land is generated.
4. **No size contract except characters.** `check_glb.py` defaults to the 1.8-unit character contract. Keraunos Spire came out about 2.5 tiles tall and Abyssal Court overhangs its tile. *Action:* AI-063 adds a per-type footprint and height budget.
5. **The sound vocabulary is split.** The prompt directory uses alpha cue names (DEPLOY, DESTROY, CLICK, MELEE/RANGED…) and 107 of 139 cards share group SFX. The ElevenLabs lane now produces per-unit summon/move/attack/hit/death/ability/idle. *Action:* AI-064 gives one per-card event → animation → SFX mapping and a coverage report.

### Code quality (UnityProof)
6. **Duplicated board constants.** The 4×6 size and 1.3 spacing are repeated in `MovementProof.Position`, the `tiles = new Renderer[4,6]` array, `TokenPreview.Cell` and its loops, instead of using `MovementState.Width/Height` or one shared `BoardLayout`. They will drift once the board changes.
7. **The build overwrites the scene.** `ProofBuild.Build` creates a new empty scene, saves it over `Assets/Scenes/MovementProof.unity` and resets `EditorBuildSettings.scenes` on every build. Any hand-authored scene work would be lost. A real board scene needs a build method that builds the existing scene.
8. **One material for every renderer.** `TokenPreview.BuildMaterial` rewrites `Token.mat` and assigns it to every renderer of the token. That works for single-mesh Meshy output but will break multi-material models.
9. **Template leftovers.** `Assets/TutorialInfo`, `Readme.asset` and `SampleScene.unity` are Unity template files and can go.
10. **Unity is not in CI.** Unity builds and the `-proofSmoke` run are only verified by hand on Mathew's PC. A licensed Unity CI job needs a Unity license secret (a Product Owner decision). A cheaper step is a `dotnet test` job that compiles the engine-free C# (`MovementState` and future rules/event code) and runs its assertions.

### Process
11. **The shared backlog was overwritten.** Commit `e5bcb77` (Muse, 19:02) rewrote PRODUCT_BACKLOG.md from a copy taken before `2ac2be7`, dropping the product vision, AI-060 and AI-061. They are restored in this commit, and a fetch-before-write rule is added under "How to use this file".
12. **The local clone has drifted.** On Mathew's PC, `3DTuba` is checked out on `astra/unity-movement-proof` with modified PRODUCT_BACKLOG.md/SPRINT_LOG.md and untracked `assets/` and `docs/production/`. The shared branch is `muse/sprint-01-content-audit`. Nothing was changed here; Claude (meetings thread) should reconcile it at the next checkpoint.

## Work assigned
| ID | Owner | Summary |
|---|---|---|
| AI-062 | Muse (Rune) | Board event contract v1: vocabulary with Java source citations, JSON schema, golden Zeus-vs-Poseidon transcript, validator in CI |
| AI-063 | Muse (Rune) | Asset prompts corrected for the square stacked board, plus a per-type footprint and height budget |
| AI-064 | Muse (Rune) | Per-card presentation manifest and coverage report (after AI-062) |

Findings 2, 6, 7 and 8 are Unity code changes in the Claude/Astra lane. They are recorded here and need Mathew's approval before anyone edits game code.
