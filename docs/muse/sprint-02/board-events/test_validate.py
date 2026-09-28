"""Cross-platform regression test for the AI-062 board-event validator (stdlib only)."""
import subprocess
import sys
import unittest

HERE = __file__.rsplit("/", 1)[0] or "."


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


if __name__ == "__main__":
    unittest.main()
