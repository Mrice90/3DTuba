# First-match tutorial script (AI-203-TUTORIAL, draft 1)

Owner: Claude (design and copy). Wiring: Codex. Rules text check: ChatGPT. Status: DRAFT for review, 2026-10-16.

**Goal:** a new player finishes one guided match as Zeus against a scripted Poseidon bot in about 10 minutes. By the end they know how to place lands, raise structures, summon into structure slots, move and attack, cast a spell, use an ability and destroy the enemy capital. Gate for milestone M1: a first-time player finishes it without help.

**Rules this script relies on.** ChatGPT should confirm each one against the 0.4.5 engine before Codex wires it. Anything marked `[confirm]` is my reading of the repo docs, not checked against the engine.
- Each turn you gain GP (the resource) and spend it to play cards. `[confirm: GP per turn and cap]`
- Lands go on empty hexes in your home rows; structures go on your lands; characters go on your structures or lands. A structure provides summon slots, and an ordinary character uses 1 slot. `[confirm: current slot numbers from AI-108]`
- Abilities of lands and structures still work when something stands on top of them (Matt's ruling, 2026-10-08).
- You win by bringing the enemy capital to 0 HP.

## Setup (fixed, not random)

- Seed and both decks are fixed, so every player sees the same match. Zeus tutorial deck: lands first, then one structure, two cheap characters, one damage spell (Skybreaker Bolt or the current equivalent) and one ability structure.
- The bot plays a scripted list of actions. It never uses reactions, and it only attacks when the script says so.
- No coin flip: the player goes first. No mulligan in the tutorial, and the mulligan is explained in a later tip instead.
- Turn timer off. Concede is hidden. Skip tutorial is in the pause menu.

## Beats

Each beat has one goal, one short line of copy (shown in a panel docked away from the board), a highlight, and the input the game waits for. Every other input is blocked during a beat, except camera controls.

| # | Beat | Copy (max ~18 words) | Highlight | Waits for |
|---|---|---|---|---|
| 1 | Welcome | "Zeus versus Poseidon. Destroy their capital before they destroy yours." | Both capitals, camera pans from yours to theirs | Click Continue |
| 2 | Camera | "Right-drag to turn the view. Scroll to zoom. Press Home to return." | None | Any camera move, or 5 seconds |
| 3 | Your hand and GP | "These are your cards. Playing one costs GP, shown here." | Hand, GP counter | Click Continue |
| 4 | Place a land | "Drag this land onto a glowing hex in your home rows." | One land card; legal hexes glow | Land placed |
| 5 | The board grows | "Every land you play builds the battlefield." | Newly built tile | Auto, 2 seconds |
| 6 | End turn | "Out of moves? End your turn." | End Turn button | End Turn |
| 7 | Enemy turn | "Poseidon places a land. Watch the enemy's side of the board." | Bot's new tile | Auto |
| 8 | Raise a structure | "Structures stand on your lands. Build one here." | Structure card, one legal land | Structure placed |
| 9 | Slots | "This structure has room for characters. Each slot holds one." | Slot pips on the structure | Click Continue |
| 10 | Summon | "Summon a character into the open slot." | Character card, the structure | Character summoned |
| 11 | Move | "Select your character, then click a glowing hex to move." | Character, legal move hexes | Move done |
| 12 | Enemy summons | "Poseidon summons a defender in your path." | Bot's character | Auto |
| 13 | Attack | "Select your character and click the enemy to attack." | Attack target | Attack resolved |
| 14 | Spells | "Spells act at once. Cast Skybreaker Bolt on the damaged enemy." | Spell card, the legal target | Spell resolved |
| 15 | Abilities | "Some lands and structures have abilities. Right-click this one to use it." | Ability structure, ability panel | Ability used |
| 16 | Covered abilities | "Abilities still work with a character standing on top." | Stack under a character | Click Continue |
| 17 | March on the capital | "Their capital is open. Attack it with everything you have." | Enemy capital, capital HP bar | Capital at 0 HP (the script ensures this in 2 to 3 turns) |
| 18 | Victory | "Victory! You're ready for a real match." | Victory screen | Buttons: Play vs Bot, Play Online, Main Menu |

## After the tutorial

- Tips shown once, the first time each comes up in a real match: mulligan, coin flip, reactions during the enemy's turn, slot limits, capital damage, structure destruction freeing slots.
- Achievement hook: "Finish the tutorial" (AI-095).
- Telemetry, if Matt approves: which beat players quit on.

## Production notes

- Copy is short on purpose. Players can reopen the full rules from the pause menu.
- Each beat's highlight uses the existing target-glow material. The panel uses the release UI style (AI-205), not the developer HUD.
- Audio: the sound thread's new coin-flip, mulligan, interrupt and ability-panel cues. A soft UI chime on each new beat.
- Voice-over is out of scope for 1.0.
- Localisation: all copy lives in one string table keyed by beat id (`tut.01.copy` and so on).
