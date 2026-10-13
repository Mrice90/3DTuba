# Infinite Conquest playtest: reaction cue + calmer turn sounds (2026-10-11)

Double-click `PLAY-INFINITE-CONQUEST.cmd`. Keep this whole folder together, including `Bridge/`.

This is the delivered r3 build (`playtest/unity-build-2026-10-11/`: capital HP, Storm Relay, ability command center) plus two changes from the 2026-10-10 restoration playtest plan. It does **not** include the R3b work (online HP, panel text wrap, line-of-sight card text), which another session is building into `unity-build-2026-10-11-r2/`.

## What changed

**Reaction window cue (AI-080-REACTION-VISUAL-CUE).** When the bot's turn pauses so you can cast a reaction spell:
- the screen edges glow amber and pulse;
- a **REACT NOW — enemy turn paused** banner appears under the top bar. It says what the opponent just did (for example "Just now: Zeus plays Stormgate Adept at 3,2") and what to do next ("Cast a glowing spell from your hand, or Pass"). It also has a **Pass** button and a timer showing how long the turn has been paused;
- each spell you can cast right now gets a pulsing outline in your hand;
- a soft chime plays once when the window opens, unless the turn chime is off. The cue never depends on sound alone.

The engine has no reaction time limit, so the timer counts up instead of down. The existing "Pass reaction" button and status line still work. Online, when your opponent is the one deciding, a quieter blue "Opponent may react — waiting" banner shows instead. The banner, its text and the glow are sized from the window height and stay inside the window from 1920×1080 down to 800×480.

**Calmer turn sounds (AI-082-TURN-SOUND-REVIEW).** The old cues were two quick beeps that stepped up in pitch at turn start and down at turn end, which testers said sounded like a charger plugging in and out. Now:
- your own turn starts with one low, soft chime (220/330/440 Hz) that fades in and out over 1.2 s, at about a third of the old loudness (peak 0.18 instead of 0.35, played at lower gain);
- the opponent's turn start makes no sound (the banner still shows);
- turn end makes no sound;
- the Demo match plays a quieter chime each turn;
- **Esc → Settings → Turn chime: Calm / Off** turns turn sounds off without touching Mute, combat or card sounds. The setting is saved.

No new audio files were added and nothing was generated: the turn cue is the game's existing built-in tone, retuned in code. A `Resources/Sfx/ui_turn.wav` override still replaces it if one is added later.

## Checks (all run on this build)

| Check | Result |
|---|---|
| New reaction-cue/turn-sound editor checks | 42/42 PASS (`checks/interrupt-cue-validation.json`) |
| R3 / camera / coin / restoration / gameplay editor checks | 27 / 50 / 16 / 669 / 25 PASS |
| SP1 audio build check | 230 cues / 88 cards PASS (`audio-build-validation.json`) |
| Packaged playback smoke | PASS |
| Packaged live matches, both seats, 12 seeds | 24/24 PASS to GAME_OVER. Totals match the r3 delivery exactly (2,226 actions, 420 command-center activations, 45 capital-damage, 33 heal checks). 2 reaction windows were hit, and the cue and a Pass choice were up both times |
| Windowed short-screen runs with screenshots | seed 8 Zeus at 1280×600 PASS; seed 12 Poseidon asked for 1024×540, but Windows opened it at 1280×649, PASS (`reviews/interrupt-cues-2026-10-11/shots-v2-*/05-reaction-window.png`) |
| Launch through `PLAY-INFINITE-CONQUEST.cmd` | Reached the main menu (1296×688 window, responding), then closed |

Hashes are in `SHA256SUMS.txt`. Human acceptance (look, feel and listening) is still open.
