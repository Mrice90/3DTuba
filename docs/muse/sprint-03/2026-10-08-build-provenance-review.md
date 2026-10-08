# Build-Provenance Review — SP2 Unity Package vs Canonical 2D JAR Recipe

**Date:** 2026-10-08
**Lane:** Thalia (QA/docs/evidence review). Authorized: commit dedicated `docs/muse/sprint-03/` reports; no edits to shared backlog/log, gameplay, workflows, assets, or packages.
**Scope:** identify packaged-SP2-JAR vs older-canonical-JAR *recipe* differences from available source. **No mismatch or corruption is declared** — the packaged files are not accessible from this workspace, so file-level verification is impossible here.

## 1. Canonical 2D JAR recipe (independently verified from source)

Source: `~/workspace/dist-work/package_alpha.sh` (+ AGENTS.md lesson notes).

- Produces: `infinite-conquest-gui.jar` (Main-Class holder), `infinite-conquest-core.jar`, `infinite-conquest-cli.jar`, jackson jars — staged under `app/`.
- `MANIFEST.MF` of the gui jar carries a `Class-Path` entry naming `infinite-conquest-core.jar infinite-conquest-cli.jar` + jackson jars (`package_alpha.sh:37-41`). Reason (AGENTS.md): `java -jar` ignores sibling jars unless the manifest names them; without it the game dies on the loading screen with `NoClassDefFoundError`. Manifest lines wrap at 72 bytes with leading-space continuations.
- Launcher: `.vbs` → `javaw -jar app/infinite-conquest-gui.jar`.
- Gate: headless smoke test of `GameContext.load` under real `-jar` semantics; prints `SMOKE_OK cards=N` (`package_alpha.sh`).
- Deliverables: portable zip + NSIS installer; release assets `InfiniteConquest-Update-<ver>.zip` (jars-only), `InfiniteConquest-Alpha-<ver>-Setup.exe`, `InfiniteConquest-Alpha-<ver>-Windows-Portable.zip` (per `gh_release.py` contract in AGENTS.md).

## 2. SP2 Unity package recipe (relayed — Codex, 2026-10-07/08)

Package: `Infinite Conquest/playtest/unity-build-SP2-gameplay/`, launcher `PLAY-INFINITE-CONQUEST.cmd`.

- `InfiniteConquestPlaytest.exe` — Unity player executable (Unity 6000.6.3f1 per the Oct 2 baseline record).
- `Bridge/classes/RulesBridge.class` + rules JAR; Temurin 17.0.20.1 bundled.
- SHA256SUMS (relayed) covers: runtime `Assembly-CSharp.dll`, `resources.assets`, **three rules overlay classes**, the JAR, bridge, policy, and player executable.

## 3. Recipe differences identified

**D1. Different products, different recipes.** The 2D recipe builds a self-contained JVM game (jars + manifest classpath + VBS launcher). The SP2 recipe builds a Unity player with a Java rules *sidecar* consumed through the bridge. The "JAR" in the SP2 package is the rules engine as a library, not a game launcher — comparing it to the 2D gui jar is a category error.

**D2. Classpath mechanism differs by necessity.** 2D depends on `MANIFEST.MF Class-Path` (the 72-byte-wrap lesson). The SP2 Java side is launched by the `.cmd` with the bridge/classes on its classpath (relayed; `.cmd` contents not accessible here). No manifest Class-Path contract applies to the sidecar.

**D3. Unity-runtime artifacts have no 2D counterpart.** `Assembly-CSharp.dll` and `resources.assets` are Unity player build outputs. Their absence from the 2D recipe (and presence in SP2) is expected architecture, not drift.

**D4. Overlay classes and policy files are SP2-new concepts.** The three rules overlay classes (relayed as the slot-policy implementation surface) and the policy files (pooled slot configuration) have no counterpart in the 2D recipe — they are the new-mechanic surface, consistent with the slot system being NEW rather than a port.

**D5. Smoke gates differ in kind.** 2D gates on `GameContext.load` under real `-jar` semantics. SP2 gates on: 70 engine/bridge assertions, 11/11 baseline protocol with capacity disabled (golden preserved), 35 player-smoke checks, 235/235 replay events, and full GAME_OVER runs (all relayed).

## 4. Access blocker (stated plainly)

The SP2 package files (`Infinite Conquest/playtest/unity-build-SP2-gameplay/`) live on Mathew's local machine and are **not accessible from this workspace**. Therefore:
- No byte-level comparison of the packaged JAR against any recipe output was possible.
- The SHA256SUMS contents are relayed, not independently verified.
- §3 compares *recipe shapes* (source-verified 2D vs relay-described SP2), not artifacts.

## 5. Verdict

**No corruption or mismatch declared — and none verifiable from here.** The recipe differences (D1–D5) are architectural consequences of shipping a Unity player + Java sidecar instead of a JVM-only game. The one item that would close provenance honestly is Mathew-local verification: recompute the package SHA256SUMS on his machine and confirm the three overlay classes + policy match the reviewed slot design. That check belongs to hands with the package, not this lane.
