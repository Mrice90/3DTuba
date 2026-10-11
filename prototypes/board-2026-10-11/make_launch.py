"""Writes launch_main.ps1 / launch_6x8.ps1 / launch_hex.ps1 from the interrupt-cues launch smoke, pointed at the board package."""
import pathlib, sys

HERE = pathlib.Path(__file__).resolve().parent
BS = chr(92)
src = (HERE.parent / 'interrupt-cues-2026-10-11' / 'launch_smoke.ps1').read_text(encoding='utf-8-sig')
for cmd, shot, name in (('PLAY-INFINITE-CONQUEST.cmd', 'launch-menu', 'main'), ('PLAY-BOARD-6x8-PREVIEW.cmd', 'launch-menu-6x8', '6x8'), ('PLAY-BOARD-HEX-PREVIEW.cmd', 'launch-menu-hex', 'hex')):
    s = src.replace('playtest' + BS + 'unity-build-interrupt-2026-10-11"', 'playtest' + BS + 'unity-build-board-2026-10-11"')
    s = s.replace('reviews' + BS + 'interrupt-cues-2026-10-11' + BS + 'launch-menu.png', 'reviews' + BS + 'board-2026-10-11' + BS + shot + '.png')
    s = s.replace('PLAY-INFINITE-CONQUEST.cmd', cmd)
    if 'unity-build-board-2026-10-11' not in s or shot + '.png' not in s or cmd not in s:
        sys.exit('replace failed for ' + name)
    (HERE / ('launch_' + name + '.ps1')).write_text(s, encoding='utf-8')
print('ok')
