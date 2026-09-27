# Provenance — Infinite Conquest alpha 0.7.15 playable build

Reference build produced 2026-09-27 by Rune (AI-044), reproduced by recipe 2026-09-27 (AI-046).

- **Source:** `Mrice90/TubaExperiment`, branch `strip/zeus-poseidon-desktop`,
  commit `992bc95c7164416ea0a25a4ce120f6ec0a0a167a` — fetched and compiled
  **read-only**. The upstream repository was not modified, pushed to, or
  merged into.
- **Toolchain:** Temurin JDK 17 (`17.0.20.1`), `javac` direct (the Gradle
  daemon does not run in the build environment). Jackson 2.18.2
  (databind/core/annotations) merged from the source tree's `libs/`.
- **Entry point:** `com.infiniteconquest.gui.GameShell`
  (manifest `Main-Class`). CLI: `com.infiniteconquest.cli.InfiniteConquestCli`.
- **Contents:** 4 Java modules (game-core, game-cli, game-gui, net-server),
  card JSON/resources (~85 MB art/audio/VFX carried from the source tree).
- **Artifact:** `infinite-conquest-alpha-0.7.15.jar` (~91 MB)
- **SHA-256:** `728c3fc101ad686e8c73c7a9af979125d7052f943f7b89645edbdc5149029523`
  (see `CHECKSUMS.sha256`; `build-release.sh`/`.bat` regenerate it on rebuild)

## Verification (reference build)

- `GuiScreenshotHarness` under Xvfb: 130 screenshots, exit 0 (board,
  deck-builder, capital-placement, animation, bot-playback scenes).
- Headless seeded bot match (seed 42): Zeus/Olympus Citadel defeated
  Poseidon/Atlantis Nexus in 14 turns, `MATCH_COMPLETE`.
- `smoke.sh`: jar present, manifest + entry class + card JSONs present,
  `simulate 1 42` → "Simulated 36 matches" (35 completed, 1 draw).
- Linux build path re-verified end-to-end 2026-09-27 from a clean fetch
  (fetch → build → smoke 5/5). The fresh rebuild's SHA-256
  (`20696b45…f14af`) differs from the reference handoff hash below: the
  recipe is reproducible in *content*, not byte-identical (zip entry
  timestamps/ordering vary per run). `build-release.sh`/`.bat` regenerate
  `CHECKSUMS.sha256` on every build; the hash recorded here is for the
  reference handoff copy only — always verify against the checksums file
  that ships next to the jar you actually received.
- Windows scripts (`fetch-source.bat`, `build-release.bat`, `smoke.bat`,
  `play.bat`) mirror the verified Linux recipe but were **not executed on
  Windows** — see `docs/muse/sprint-01/alpha-build-handoff.md`.

## Distribution notes

- The jar is **not stored in this repository**: a repository rule rejects
  90MB+ blobs. It travels with the release handoff; this directory carries
  the reproducible recipe, checksums, launchers, and screenshots.
- No formal GitHub Release has been created for this build.
