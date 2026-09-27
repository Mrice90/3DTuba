# Alpha core foundation — verified runnable 2026-09-27

The TubaExperiment alpha game engine (pinned `992bc95`, branch
`strip/zeus-poseidon-desktop`) **builds cleanly and runs headless**.
This document is the reproducible recipe so the team has a working core
to build 3D functionality against before asset development starts.

The alpha repo itself was used read-only. Nothing was modified or pushed
there. All verification happened in a scratch copy.

## What was proven (2026-09-27, Rune)

- **Full compile:** all four modules compile with `javac` on JDK 17 —
  `game-core` (42 files), `game-cli` (21), `net-server` (6),
  `game-gui` (149 classes). Zero errors.
- **Headless match:** a complete bot-vs-bot match played through the real
  rules engine — ZEUS (Olympus Citadel) beat POSEIDON (Atlantis Nexus) in
  14 turns, seed 42, under 1 second wall time.
- **Unit tests:** 169/169 pass, including `MovementRulesTest` 9/9
  (the AI-036 parity fixture now has real executed results, not just
  source-derived expectations).

## Module map

| Module | Role | Entry point |
|---|---|---|
| `game-core` | Rules engine: board, cards, combat, GP economy, abilities | library (no main) |
| `game-cli` | Headless CLI: bot matches, balance simulator, deck tools | `com.infiniteconquest.cli.InfiniteConquestCli` |
| `net-server` | WebSocket bridge (multiplayer protocol) | library: `EmbeddedServer`, `WsBridge`, `Protocol` |
| `game-gui` | Desktop Swing client (needs a display) | `com.infiniteconquest.gui.InfiniteConquestGui` |

Card data is JSON under `game-core/src/main/resources/cards/`
(143 faction cards + capitals + starters); the engine loads it from the
classpath at runtime.

## Build & run (standard path)

Requires JDK 17 and Gradle 8+:

```bash
git clone --branch strip/zeus-poseidon-desktop \
  https://github.com/Mrice90/TubaExperiment.git
cd TubaExperiment
gradle :game-core:test          # 169 unit tests
gradle :game-cli:run --args="simulate 1 42 sim-report.json"
# ^ headless bot-vs-bot balance simulation, 1 match per capital pair
gradle :game-cli:run            # interactive human-vs-bot CLI match
gradle :game-gui:run            # desktop client (needs a display)
```

Dependencies are light: `jackson-databind:2.18.2` and JUnit 5
(`junit-bom:5.10.2`), both from Maven Central.

## Sandbox note (this machine only)

Gradle's daemon could not accept its loopback TCP connection in this
sandboxed VM, so the build was done with plain `javac`/`java` instead:
JDK 17 (Temurin) plus the four jars above on the classpath, resources
copied onto the classpath. On a normal dev machine the Gradle path
above is the one to use.

## What this unblocks

- **3D client work** can drive `game-core` directly (same JVM) or shell
  out to the CLI's `simulate` mode for scripted matches — no assets
  needed to exercise the full rules loop.
- **AI-036** movement parity now has executed evidence (9/9), ready for
  Astra's review against the Unity implementation (AI-030).
- **Multiplayer path**: `net-server`'s `WsBridge`/`Protocol` is the
  existing engine-to-network seam; the deployed lobby worker contract is
  fingerprinted separately in `deployed-worker-fingerprint.md`.

## Limitations

- The GUI was compile-verified only; no display was available to run it.
- Balance simulation defaults to 2 matches per capital pair (36 matches
  at 1 per pair); a single scripted match is the fastest smoke test.
- `POST /report`-style Elo writes remain a trust concern at the worker
  layer (see lobby-lab AI-041), independent of this engine verification.
