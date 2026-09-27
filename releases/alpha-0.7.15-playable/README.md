# Infinite Conquest — Minimally Playable Alpha Release (0.7.15)

A ready-to-run desktop build of the Infinite Conquest alpha: mythological
ranked-tactics on a 24-hex battlefield (Zeus vs Poseidon), with the full card
economy, deck builder, and bot opponents. No build step, no assets to install —
everything (code, card art, audio, VFX) is inside the jar.

**Requires: Java 17 or newer** (Temurin/Adoptium recommended). No other setup.

## Run it

- **Windows:** double-click `play.bat` (or double-click the `.jar` itself)
- **macOS / Linux:** `sh play.sh` (or `java -jar infinite-conquest-alpha-0.7.15.jar`)

A title screen opens: start a match, open the Deck Builder, or watch bots play.

## What you can play

- **Human vs Bot** — pick both factions and a Capital per side, then play the
  full game: deploy lands/structures/characters, move on the hex grid, melee
  and ranged attacks, spells, reactions, initiative coin, mulligans.
- **Bot vs Bot (watch)** — choose "Bot (watch match)" for Player 1 during setup.
- **Deck Builder** — faction → optional ally → Capital → cards (40-card minimum,
  60-card starters included). Share/import deck codes.
- **Command-line mode** — `java -cp infinite-conquest-alpha-0.7.15.jar com.infiniteconquest.cli.InfiniteConquestCli`
  for the text prototype (human vs bot, deterministic seeds, deck tools).

## Controls (graphical client)

- Drag a hand card or battlefield unit onto a gold-highlighted legal destination;
  click-select + **Legal Moves** tab works as a keyboard-friendly fallback.
- Hover the tucked hand to reveal cards, or click **Hand** to pin it open.
- Right-click a card for the full card view; right-click an occupied hex to
  inspect its stack.
- **F2** legal actions, **F3** history, **F4** board view, **F11** fullscreen,
  **Esc** close dialogs, **Ctrl+D** deck builder.

## What this is NOT (yet)

- This is the **2D Java alpha**, not the 3D migration — there is no Unity build
  yet. It exists so the team has something playable *today* while 3D work proceeds.
- Local play only. Online lobby/multiplayer is a separate slice
  (`prototypes/lobby-lab/` in this branch) and is not wired into this build.

## Provenance & verification

- Built read-only from `Mrice90/TubaExperiment` @ `992bc95`
  (branch `strip/zeus-poseidon-desktop`). That repo was not modified.
- Build recipe: `build-release.sh` (javac + jar; the Gradle daemon does not run
  in the build environment, so the script uses `javac` directly).
- Verified 2026-09-27: CLI bot-vs-bot completes headlessly; the Swing GUI was
  rendered under Xvfb by the alpha's own `GuiScreenshotHarness` — see
  `screenshots/` for the captured boards, deck builder, and animations.
- Upstream docs: the alpha's full manual lives in its README; the 3DTuba
  integration runbook is `docs/muse/sprint-01/alpha-core-foundation.md`.
