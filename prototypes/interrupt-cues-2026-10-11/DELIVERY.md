# AI-080-REACTION-VISUAL-CUE + AI-082-TURN-SOUND-REVIEW delivery (2026-10-11, Claude Code)

**Package:** `playtest/unity-build-interrupt-2026-10-11/PLAY-INFINITE-CONQUEST.cmd`. Its README covers the behavior and checks, and `SHA256SUMS.txt` lists the hashes. Assembly-CSharp `d24e5992cec60f31a3b1150940e3d845f6405e564064fae502c1df21b396eda5`. `Bridge/` is copied unchanged from the delivered r3 package (RulesBridge.class `fde3dc31…`).

**Source:** `3DTuba-interrupt-cues/UnityProof`, a new isolated copy of `3DTuba-unified-r3/UnityProof`, taken at 10:37 EDT. At that time no r3 Assets file had changed since the 10:06 delivery. It has its own editor and build. `3DTuba-unified-r3/` (R3b owner) and all earlier packages were only read, except for one accidental overwrite (see below).

## Changes against the delivered r3 source

The patch is `interrupt-cues-vs-r3-delivered.patch`. The baseline files are in `baseline-r3-delivered/`; the PlaytestGame.cs (`b8a3f283…`) and LiveMatchValidation.cs (`d1ebb2d6…`) baselines match the hashes in the r3 DELIVERY.

| File | Change |
|---|---|
| `Scripts/ReactionCue.cs` (new, `9c0f4277…`) | Cue state for a reaction window: solo, online own seat, and online opponent seat. Draws the screen-edge glow, the REACT NOW banner (the opponent's last action, Cast/Pass guidance, a Pass button and a paused-time count) and outlines on castable spells. `CueLayout(W, H)` sizes these from the screen height. Also holds the calm turn-cue policy and the `TurnChime` setting (PlayerPrefs `ic.turnChime`) |
| `Scripts/PlaytestGame.cs` (`3d8f6f79…`) | Four lines: TURN_STARTED calls `PlayTurnCue(pl)`; TURN_ENDED plays no sound; `OverHud` also excludes `reactionRect`; OnGUI calls `DrawReactionCue(W, H)` before the pause/game-over overlays |
| `Scripts/SfxBank.cs` (`c7017eb2…`) | `PlayUiSoft` (fixed pitch, gain-scaled). The `turn`/`turn_end` tones are now one low chord with a `SoftEnv` fade, replacing the two-step beeps |
| `Scripts/PlaytestMenus.cs` (`12da0ad3…`) | Settings row "Turn chime: Calm / Off" |
| `Scripts/LiveMatchValidation.cs` (`35e35511…`) | In packaged live smoke, each decision inside a reaction window asserts that the cue and a Pass choice are up. Also adds a `05-reaction-window.png` shot and `reactionWindows`/`reactionCueChecks` counters |
| `Editor/InterruptCueChecks.cs` (new, `51c034c2…`) | `InterruptCueBuild.Build` runs 42 checks, then the unchanged `R3Build.Build` chain |

The command center, capital HP, Storm Relay, bridge, camera and coin code are unchanged.

## Checks, before and after

- **Before** (baseline, delivered `unity-build-2026-10-11`, seeds 42/713/7/3, both seats): smoke and 8/8 live matches PASS. Results are in `baseline-results.json` and `baseline/`.
- **After** (this build):
  - editor checks: InterruptCue 42, R3 27, camera 50, coin 16, restoration 669 (570 audio bindings), gameplay 25, all PASS;
  - SP1 audio: 230 cues PASS; player build succeeded;
  - packaged smoke PASS;
  - 24/24 live matches (12 seeds × 2 seats) PASS, and every R3 total equals the r3 delivery (`packaged-results.json`);
  - two reaction windows were checked with the cue up;
  - windowed screenshots at short heights (`shots-v2-*`);
  - the `.cmd` launch reached the menu (`launch-menu.png`).
- Two layout fixes came from the first screenshots (`shots-seed*`): the second text line was clipped and the edge glow was too faint. Both were fixed and rebuilt, and every check above was rerun on the final build.

## Mistake to report

While I was adapting `launch_smoke.ps1`, my first edit failed. The unmodified copy then launched the delivered **r3** package to its main menu and overwrote `reviews/restoration-r3/launch-menu.png` at about 11:16 EDT. The new file is a fresh capture of the same package's main menu. The original capture cannot be recovered. No other r3 file was touched.

## Still open

- Human look/feel and listening acceptance. Muse/Thalia's listening comparison was not done here.
- The bot only opens a reaction window when a castable spell exists. The automated matches hit 2, so most coverage comes from the editor checks and screenshots.
- On short screens, the opponent's played-card popup (about 1.8 s) can cover the hand when a window opens. The spell outline draws on top of it.
- Integration: these files should be merged into the R3b line (`3DTuba-unified-r3`) after R3b releases it. The PlaytestGame.cs hooks are four lines.
