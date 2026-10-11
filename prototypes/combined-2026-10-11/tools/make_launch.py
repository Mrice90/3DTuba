"""Writes launch_<name>.ps1 from the interrupt-cues launch smoke, pointed at one combined package."""
import pathlib, sys

HERE = pathlib.Path(__file__).resolve().parent
src = (HERE.parent / 'interrupt-cues-2026-10-11' / 'launch_smoke.ps1').read_text(encoding='utf-8')
for pkg, name in (('unity-build-combined-2026-10-11', 'native'), ('unity-build-combined-2026-10-11-java', 'java')):
    s = src.replace('playtest\\unity-build-interrupt-2026-10-11"', 'playtest\\' + pkg + '"')
    s = s.replace('reviews\\interrupt-cues-2026-10-11\\launch-menu.png', 'reviews\\combined-2026-10-11\\launch-menu-' + name + '.png')
    if pkg not in s or 'launch-menu-' + name not in s:
        sys.exit('replace failed for ' + name)
    (HERE / ('launch_' + name + '.ps1')).write_text(s, encoding='utf-8')
print('ok')
