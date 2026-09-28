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

## Get the built jar

The prebuilt `infinite-conquest-alpha-0.7.15.jar` (~91 MB) ships with the
release handoff — **not in this repository**: publishing the ~91 MB jar via the GitHub Contents API was refused with HTTP 409 (repository rule validation), so the repo carries the recipe, checksums, and launchers instead of
the binary. Verify any jar you receive before running it:

```
sha256sum -c CHECKSUMS.sha256        # Linux / macOS
certutil -hashfile infinite-conquest-alpha-0.7.15.jar SHA256   # Windows
```

Expected: `728c3fc1…5149029523` (full hash in `CHECKSUMS.sha256`;
provenance in `PROVENANCE.md`).

## Rebuild it yourself (fetch → build → smoke → play)

Prerequisites: **JDK 17+** and **git**. No Gradle, no npm install.

**Linux / macOS:**
```
cd releases/alpha-0.7.15-playable
./fetch-source.sh     # pinned read-only source -> ./build/alpha-src (verifies the pin)
./build-release.sh    # diagnostics -> javac build -> jar -> CHECKSUMS.sha256 -> smoke.sh
./smoke.sh            # re-runnable any time: manifest, classes, card data, 36 seeded bot matches
./play.sh
```

**Windows** (same flow; scripts mirror the Linux recipe):
```
cd releases\alpha-0.7.15-playable
fetch-source.bat
build-release.bat
smoke.bat
play.bat
```

`build-release` fails fast with a plain-English error if Java is missing/too
old, the source checkout is absent, or the checkout does not match the release
pin (`992bc95`). `play.*` refuses to run a missing or checksum-mismatched jar
and tells you exactly where to get one. Full runbook, exact commands, and what
was verified on which OS: `docs/muse/sprint-01/alpha-build-handoff.md`.

## Regression (AI-048)

`regress.sh` / `regress.bat` re-runs the whole pipeline (fetch → build →
smoke) inside an isolated temp copy of this directory and reports
`REGRESSION: PASS` or `REGRESSION: FAIL at <stage>`. After the build it
verifies the fresh jar against its **own** freshly generated
`CHECKSUMS.sha256` — rebuilds are content-equivalent, not byte-identical
(JAR timestamps/ordering vary), so an unrelated reference artifact is never
used for comparison.

```
./regress.sh                 # full clean regression, expect REGRESSION: PASS
./regress.sh --break=pin     # fault injection: off-pin source must fail the build
./regress.sh --break=dep     # fault injection: corrupted dependency jar must fail hash verification
./regress.sh --break=compile # fault injection: syntax error must fail javac
./regress.sh --break=checksum # fault injection: corrupted jar must fail verification
./regress.sh --break=smoke   # fault injection: missing card data must fail the build's smoke step
./regress.sh --break=depswap # fault injection (AI-055): self-consistent wrong jar + .sha1 must fail the pinned SHA-256 check
```

**Windows console note (AI-057):** `regress.bat` ends with `exit %EXITCODE%`
(not `exit /b`) so its exit code survives the `call :fail` / `call :pass`
subroutines — this is what CI asserts on. The side effect: double-clicking
`regress.bat` in Explorer closes the console window as soon as it finishes.
For manual runs, open a console first (`cmd`) and run it there, or launch it
as `cmd /k regress.bat` to keep the window open after the verdict.

In `--break` mode the harness expects the failure: `intentional break
correctly detected at stage '<stage>'` means the detection test passed. Set
`REGRESS_KEEP=1` to keep the temp dir for inspection. Upstream repos are only
fetched read-only; nothing is pushed anywhere.

