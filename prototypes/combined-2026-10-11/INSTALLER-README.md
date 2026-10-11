# Infinite Conquest 3D Playtest installer: combined build, native rules (2026-10-11)

Double-click `InfiniteConquest-Setup.exe`. It installs the game for this Windows user, with no admin rights, at `%LOCALAPPDATA%\Programs\Infinite Conquest 3D`. It also adds Start menu and desktop shortcuts and an entry under Settings > Apps so you can uninstall it.

**This installer needs no Java.** It installs `playtest/unity-build-combined-2026-10-11/`: R3/R3b, the reaction cue and calmer turn sounds, and the native C# rules. The rules run inside the game, so there is no `Bridge/` folder, no Java runtime and no JAR. The installer build script now refuses any build that has Java bridge files in it.

The older installer in `../installer/` (2026-10-02) is the Java-bridge build. I left it unchanged.

## Checks

| Check | Result |
|---|---|
| Building the installer from the Java fallback build | Refused (no native rules) |
| Double-click install | Exit 0. Files, Start menu and desktop shortcuts, and the Apps entry `2026.10.11-native` all created |
| Installed files | 0 `.jar`/`.class`/`java*.exe`/`jvm.dll` files and no `Bridge/`. The RulesCore `1a985fdc…`, Native `d20045d7…` and Assembly-CSharp `470d77cc…` DLLs equal the package's |
| Launched from the Start menu shortcut, with Java off PATH and JAVA_HOME unset | Main menu (1296×688, responding); no Java process started (`reviews/combined-2026-10-11/launch-menu-installed.png`) |
| Installed copy: smoke + live matches for seeds 42 and 713, both seats (Zeus and Poseidon), Java off PATH | 5/5 PASS. All four matches reached game over, with engine responses identical to both the Java build and the folder build |

Not checked: uninstall, and online play between two PCs.

SHA256 of `InfiniteConquest-Setup.exe`: `7379171c14b08d55935279a55ced45afcbdb14c536d273b9c4a23f21ee2f20d1`
