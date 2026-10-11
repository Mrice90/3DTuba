# Reaction-window cue + calmer turn sounds (2026-10-11)

This is an overlay for the restoration/r3 Unity project (`3DTuba-unified-r3/UnityProof`, which is not in git). It covers AI-080-REACTION-VISUAL-CUE and AI-082-TURN-SOUND-REVIEW from the 2026-10-10 restoration playtest plan.

- `overlay/Assets/Playtest/...`: the changed and new files. Copy them over a delivered-r3 project. The `.patch` shows the exact diff against the delivered r3 sources.
- `DELIVERY.md`: the changes, the checks run before and after, and what is still open.
- `BUILD-README.md`: the README of the local build `playtest/unity-build-interrupt-2026-10-11/`.
- `evidence/`: packaged results, the editor check list, and short-screen screenshots of the cue.

No new audio files are added, and no ElevenLabs or Meshy jobs were run. Do not merge before human review. The R3b work (online HP, panel wrap, LOS text) is building separately, and these files need a merge into that line afterwards.
