# AI-080-COMBINED-2026-10-11 delivery (Claude Code)

**Main package:** `playtest/unity-build-combined-2026-10-11/PLAY-INFINITE-CONQUEST.cmd` runs the native C# rules and needs no Java.
**Fallback:** `playtest/unity-build-combined-2026-10-11-java/` is the same client on the R3b Java bridge.

Both packages have a README and a `SHA256SUMS.txt`. Assembly-CSharp hashes:
- native: `470d77cc…`
- Java: `d656fdbe…`

## Source

The source is `3DTuba-combined-2026-10-11/UnityProof`, a new isolated copy of the released `3DTuba-unified-r3/UnityProof` (the R3b delivered state), with its own editor and build. I applied the following in order:

1. **Reaction cue / calmer turn sounds**: `reviews/interrupt-cues-2026-10-11/interrupt-cues-vs-r3-delivered.patch`. It applied cleanly to the R3b sources once line endings were normalized.
2. **Native C# rules** (Codex AI-087 handoff):
   - `handoff/patches/BridgeClient.cs.patch` and `PlaytestGame.cs.patch`, which applied cleanly;
   - `Assets/Plugins/NativeRules/` and `Assets/StreamingAssets/NativeRules/`;
   - `Editor/NativeIntegrationBuild.cs`.
   - The packaged DLL hashes equal the handoff pins: RulesCore `1a985fdc…`, Native `d20045d7…`.
3. A new `Editor/CombinedBuild.cs`. `CombinedBuild.Build` runs `InterruptCueBuild.Validate`, then `NativeIntegrationBuild.Build` (native, R3, camera, then the coin, restoration, gameplay, audio and player build chain).

`combined-vs-r3b-sources.patch` holds the complete source diff against the R3b sources. No other files differ, apart from Unity `.meta` files. Stage A (Java) is the same source before step 2, built with `InterruptCueBuild.Build`.

## Checks

| | Stage A (Java) | Stage B (native) |
|---|---|---|
| Editor checks: cue / native / R3 / camera / coin / restoration / gameplay | 42 / – / 35 / 50 / 16 / 669 / 25 PASS | 42 / 12 / 35 / 50 / 16 / 669 / 25 PASS |
| SP1 audio | 230 cues PASS | 230 cues PASS |
| Packaged smoke | PASS | PASS |
| Packaged live, 12 seeds × 2 seats | 24/24 PASS (`stageA-java-results.json`) | 24/24 PASS with Java removed from PATH and JAVA_HOME unset (`stageB-native-results.json`) |
| Native compared with Java | – | All 24 parsed request/response transcripts identical (2,274 engine responses). Totals: 2,226 actions, 420 command-center activations, 45 capital-damage, 12 Storm Relay and 33 heal HUD checks, 2 reaction windows with the cue up |
| Java files in the payload | Bridge present (intended) | 0 jar/class/java exe/jvm |
| `.cmd` launch to the main menu | PASS (`launch-menu-java.png`) | PASS (`launch-menu-native.png`) |
| Short-window screenshot | (covered by the interrupt-cue build) | 1280×600 seed 8 PASS (`shots-native-seed8-seat0-1280x600/`) |

## Not run / open

- I did not run a packaged two-PC or relay online match: Node.js and the relay aren't set up on this machine. R3b's two-human bridge suites and Codex's native two-human protocol comparisons were run in their own deliveries, not again here.
- Human hands-on play of the combined native build.
- The installer/launch kit still describes the Java bridge. Per Codex's handoff, the recipe should drop the Bridge, Java and JAVA_HOME steps for the native build. I didn't change the installer.
- On short screens, the opponent's card popup can briefly cover the hand as a reaction window opens.
