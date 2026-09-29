#!/usr/bin/env python3
"""AI-075: timeline.py tests.

- Deterministic: two runs on the seed-42 dump give identical output.
- Golden: output matches fixtures/golden-seed-42-timeline.json.
- Schedule sanity: cues are sequential (start_ms == cumulative durations),
  every cue has the required fields, durations are positive.
"""
import json
import subprocess
import sys
from pathlib import Path

DIR = Path(__file__).resolve().parent
DUMP = DIR / "fixtures" / "dump-seed-42.jsonl"
MANIFEST = DIR.parent / "presentation" / "presentation-manifest.json"
GOLDEN = DIR / "fixtures" / "golden-seed-42-timeline.json"

REQUIRED = {"seq", "event", "turn", "player", "card_id", "instance_id",
            "start_ms", "duration_ms", "anim_key", "sfx_key", "impact_hook"}


def run():
    p = subprocess.run(
        [sys.executable, str(DIR / "timeline.py"), str(DUMP), str(MANIFEST)],
        capture_output=True, text=True)
    assert p.returncode == 0, f"timeline.py failed: {p.stderr[-500:]}"
    return json.loads(p.stdout)


def main():
    for f in (DUMP, MANIFEST, GOLDEN):
        assert f.exists(), f"missing {f}"
    a = run()
    b = run()
    assert a == b, "timeline output not deterministic"
    print("ok: deterministic")

    golden = json.loads(GOLDEN.read_text(encoding="utf-8"))
    assert a == golden, "timeline output differs from golden"
    print(f"ok: golden match ({len(golden)} cues)")

    t = 0
    for c in a:
        assert REQUIRED <= set(c), f"cue {c.get('seq')} missing fields"
        assert c["start_ms"] == t, f"cue {c['seq']}: not sequential"
        assert c["duration_ms"] > 0, f"cue {c['seq']}: bad duration"
        t += c["duration_ms"]
    print(f"ok: sequential schedule, total {t} ms")
    print("test_timeline: PASS (3/3)")


if __name__ == "__main__":
    main()
