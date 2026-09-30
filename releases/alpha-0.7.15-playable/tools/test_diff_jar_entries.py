#!/usr/bin/env python3
"""Tests for tools/diff-jar-entries.py (AI-100 rework). Stdlib only."""
import io
import os
import subprocess
import sys
import tempfile
import unittest
import zipfile

HERE = os.path.dirname(os.path.abspath(__file__))
SCRIPT = os.path.join(HERE, "diff-jar-entries.py")


def make_jar(entries, create_system=3):
    """entries: {name: (bytes, extra_header_overrides)}."""
    fd, path = tempfile.mkstemp(suffix=".jar", prefix="diffjar-")
    try:
        with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED) as zf:
            for name in sorted(entries):
                data, overrides = entries[name]
                zi = zipfile.ZipInfo(name, date_time=(2026, 1, 1, 0, 0, 0))
                zi.compress_type = zipfile.ZIP_DEFLATED
                zi.external_attr = 0o644 << 16
                zi.create_system = create_system
                for k, v in overrides.items():
                    setattr(zi, k, v)
                zf.writestr(zi, data)
        with open(path, "rb") as f:
            return f.read()
    finally:
        os.unlink(path)


def write_temp(data):
    fd, path = tempfile.mkstemp(suffix=".jar", prefix="diffjar-")
    with os.fdopen(fd, "wb") as f:
        f.write(data)
    return path


def run_diff(a_bytes, b_bytes):
    pa, pb = write_temp(a_bytes), write_temp(b_bytes)
    try:
        r = subprocess.run([sys.executable, SCRIPT, pa, pb],
                           capture_output=True, text=True)
        return r.returncode, r.stdout
    finally:
        os.unlink(pa)
        os.unlink(pb)


class TestDiffJarEntries(unittest.TestCase):
    def test_identical_jars(self):
        entries = {"a/X.class": (b"\xca\xfe" + b"\x00" * 50, {}),
                   "res/d.json": (b'{"k": 1}\n', {})}
        a = make_jar(entries)
        b = make_jar(entries)
        code, out = run_diff(a, b)
        self.assertEqual(code, 0)
        self.assertIn("IDENTICAL", out)

    def test_content_difference_reported(self):
        a = make_jar({"a/X.class": (b"AAAA", {})})
        b = make_jar({"a/X.class": (b"BBBB", {})})
        code, out = run_diff(a, b)
        self.assertEqual(code, 1)
        self.assertIn("CONTENT DIFFERS: a/X.class", out)
        self.assertIn("crc32=", out)
        self.assertIn("sha256=", out)

    def test_entry_set_difference_reported(self):
        a = make_jar({"a/X.class": (b"AAAA", {}),
                      "extra/Y.txt": (b"hi\n", {})})
        b = make_jar({"a/X.class": (b"AAAA", {})})
        code, out = run_diff(a, b)
        self.assertEqual(code, 1)
        self.assertIn("extra/Y.txt", out)
        self.assertIn("only in", out)

    def test_header_only_difference_reported(self):
        # The AI-100 residue: identical content, create_system 3 vs 0.
        entries = {"a/X.class": (b"\xca\xfe" + b"\x00" * 50, {})}
        a = make_jar(entries, create_system=3)
        b = make_jar(entries, create_system=0)
        code, out = run_diff(a, b)
        self.assertEqual(code, 1)
        self.assertIn("HEADER DIFFERS (content identical): a/X.class", out)
        self.assertIn("create_system", out)
        self.assertNotIn("CONTENT DIFFERS", out)

    def test_usage_error(self):
        r = subprocess.run([sys.executable, SCRIPT], capture_output=True,
                           text=True)
        self.assertNotEqual(r.returncode, 0)


if __name__ == "__main__":
    unittest.main()
