"""Cross-platform regression test for the AI-062 board-event validator (stdlib only)."""
import json
import os
import subprocess
import sys
import tempfile
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))


def run_validator(transcript):
    return subprocess.run(
        [sys.executable, "validate.py", "event-schema.json", transcript],
        capture_output=True, text=True, cwd=HERE)


class TestValidator(unittest.TestCase):
    def test_golden_transcript_passes(self):
        r = run_validator("golden-transcript.json")
        self.assertEqual(r.returncode, 0, r.stderr)

    def test_broken_transcript_fails(self):
        r = run_validator("broken-transcript.json")
        self.assertNotEqual(r.returncode, 0)
        out = r.stdout + r.stderr
        self.assertIn("INVALID", out)

    def _run_inline(self, events):
        with tempfile.NamedTemporaryFile("w", suffix=".json",
                                         delete=False) as f:
            json.dump(events, f)
            path = f.name
        try:
            return run_validator(path)
        finally:
            os.unlink(path)

    def _moved(self, fx, fy, tx, ty, amount):
        return [{"event": "CHARACTER_MOVED", "seq": 0, "turn": 1, "player": 0,
                 "instance_id": "test-instance", "from": {"x": fx, "y": fy},
                 "to": {"x": tx, "y": ty}, "amount": amount}]

    def test_hex_geometry_not_chebyshev(self):
        # (0,0)->(1,1) is Chebyshev distance 1 but hex distance 2 under
        # BoardGeometry.HEX (odd-row offset). amount=1 must be rejected.
        r = self._run_inline(self._moved(0, 0, 1, 1, 1))
        self.assertNotEqual(r.returncode, 0)
        self.assertIn("hex distance 2 != amount 1", r.stderr)

    def test_hex_move_correct_amount_passes(self):
        # Golden transcript's move: (1,1)->(2,3) is hex distance 2.
        r = self._run_inline(self._moved(1, 1, 2, 3, 2))
        self.assertEqual(r.returncode, 0, r.stderr)

    def test_hex_move_detour_amount_greater_than_distance_passes(self):
        # AI-072: the engine's cost is the step count along the shortest
        # LEGAL path (BFS detours around blockers), so amount may exceed the
        # geometric hex distance. (0,0)->(1,1) is hex distance 2; a 3-step
        # detour is legal engine output and must validate.
        r = self._run_inline(self._moved(0, 0, 1, 1, 3))
        self.assertEqual(r.returncode, 0, r.stderr)

    def test_teleport_amount_zero_skips_hex_distance(self):
        # amount=0 is a teleport/blink dissolve (§13); hex distance is
        # not enforced for it.
        r = self._run_inline(self._moved(0, 0, 3, 5, 0))
        self.assertEqual(r.returncode, 0, r.stderr)


if __name__ == "__main__":
    unittest.main()
