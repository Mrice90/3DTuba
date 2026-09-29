"""AI-064 regression test: coverage.py against a fixture staging tree.

Builds a small fake staging root (2 cards: one fully present, one partially
missing), runs coverage.py, and asserts the Markdown marks present/missing
correctly. Stdlib only.
"""
import json
import os
import subprocess
import sys
import tempfile
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
MANIFEST = os.path.join(HERE, "presentation-manifest.json")


def touch(path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w") as f:
        f.write("fixture")


class TestCoverage(unittest.TestCase):
    def test_fixture_tree(self):
        with open(MANIFEST) as f:
            cards = json.load(f)["cards"]
        cids = sorted(cards)[:2]
        full, partial = (cards[c] for c in cids)

        with tempfile.TemporaryDirectory() as staging:
            # Card 1: everything present.
            touch(os.path.join(staging, full["model_path"]))
            tex = os.path.join(staging, full["textures_path"])
            os.makedirs(tex, exist_ok=True)
            touch(os.path.join(tex, "albedo.png"))
            for a in full["animations"]:
                touch(os.path.join(staging, a["file"]))
            for s in full["sfx"]:
                touch(os.path.join(staging, s["file"]))
            # Card 2: model only; everything else missing.
            touch(os.path.join(staging, partial["model_path"]))

            out = os.path.join(staging, "coverage.md")
            r = subprocess.run(
                [sys.executable, os.path.join(HERE, "coverage.py"),
                 MANIFEST, staging, out],
                capture_output=True, text=True)
            self.assertEqual(r.returncode, 0, r.stderr)
            report = open(out).read()

            # Full card: its model row is present.
            self.assertIn(f"`{full['model_path']}` | ✅", report)
            # Partial card: model present, first sfx missing.
            self.assertIn(f"`{partial['model_path']}` | ✅", report)
            self.assertIn(f"`{partial['sfx'][0]['file']}` | ❌", report)
            # Totals line exists.
            self.assertIn("| sfx |", report)

    def test_manifest_keys_use_card_id_cue(self):
        with open(MANIFEST) as f:
            cards = json.load(f)["cards"]
        for cid, c in cards.items():
            for s in c["sfx"]:
                self.assertTrue(
                    s["key"].startswith(cid + "_"),
                    f"sfx key {s['key']} not <card_id>_<cue>")
                cue = s["key"][len(cid) + 1:]
                self.assertIn(cue, ["summon", "move", "attack", "hit",
                                    "death", "ability", "idle"])


if __name__ == "__main__":
    unittest.main()
