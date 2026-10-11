# Combined playtest build (2026-10-11)

An overlay for the restoration/r3 Unity project in its R3b state (`3DTuba-unified-r3/UnityProof`, which is not in git). It combines:

- R3b (already in that project);
- the reaction cue and calmer turn sounds (`prototypes/interrupt-cues-2026-10-11`);
- Codex's AI-087 native C# rules handoff. Rules run in-process, so the game no longer needs Java.

`overlay/Assets/` holds every changed or new file, including `Plugins/NativeRules` (the pinned RulesCore/Native DLLs) and `StreamingAssets/NativeRules` (rules data). `combined-vs-r3b-sources.patch` is the exact script diff. Build with `-executeMethod CombinedBuild.Build`. `overlay/Tools/installer/build_installer.ps1` packages the native build into `InfiniteConquest-Setup.exe`, with no Java (see `INSTALLER-README.md`). `DELIVERY.md` lists the checks, including 24/24 native matches whose transcripts are identical to the Java build. Do not merge before human review.
