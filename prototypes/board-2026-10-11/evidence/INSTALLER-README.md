# Infinite Conquest 3D Playtest installer: board build (2026-10-11)

Double-click `InfiniteConquest-Setup.exe`. Same per-user install as the combined installer (no admin, `%LOCALAPPDATA%\Programs\Infinite Conquest 3D`, replaces the combined install, Apps entry version `2026.10.11-board`), built from `playtest/unity-build-board-2026-10-11/`. No Java.

Start menu folder "Infinite Conquest 3D Playtest" now has:
- Infinite Conquest 3D Playtest (also on the desktop): the engine's 4 × 6 board.
- Infinite Conquest (6x8 board preview): `-board 6x8`.
- Infinite Conquest (hexagon board preview): `-board hex`.
- Uninstall Infinite Conquest 3D Playtest.

Install-script changes: the two preview shortcuts; a running game is only closed if it runs from the install folder; `IC_TEST_ROOT=<dir>` installs everything (files, shortcuts, an Apps entry named "… (test install)") under that folder for testing, and its uninstaller removes only that.

## Checks (reviews/board-2026-10-11/test_installer.ps1, output installer-test.txt)

| Check | Result |
|---|---|
| Install into an isolated test root | exit 0; exe, 4 Start menu shortcuts, desktop shortcut, test Apps entry created |
| Installed payload | 0 Java files; Assembly-CSharp `6766c75b…`, RulesCore `1a985fdc…`, Native `d20045d7…` equal the package |
| Each shortcut, Java off PATH and JAVA_HOME unset | normal, 6x8 and hexagon all reached the main menu (1296×688, responding), right `-board` argument, 0 Java processes |
| Uninstall of the test install | files, Start menu folder, desktop shortcut and Apps entry all removed |
| Matt's real install | untouched (Apps version 2026.10.11-native, same exe hash before/after) |

Not checked: installing over the real install (by design), IExpress quiet `/Q` mode.

SHA256 of `InfiniteConquest-Setup.exe`: `864bbc839b9468be7b0dd040b365443a6aa305f0832361514d4b259769badfba`
