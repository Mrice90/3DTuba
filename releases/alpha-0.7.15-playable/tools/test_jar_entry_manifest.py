#!/usr/bin/env python3
"""Tests for tools/jar-entry-manifest.py (AI-080-BUILD-PROVENANCE). Stdlib only."""
import os
import subprocess
import sys
import tempfile
import unittest
import zipfile

HERE = os.path.dirname(os.path.abspath(__file__))
SCRIPT = os.path.join(HERE, "jar-entry-manifest.py")


def klass(major, body=b"code"):
    return b"\xca\xfe\xba\xbe\x00\x00" + major.to_bytes(2, "big") + body


class JarEntryManifestTest(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()

    def tearDown(self):
        self.tmp.cleanup()

    def jar(self, name, entries, level=6):
        path = os.path.join(self.tmp.name, name)
        with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED,
                             compresslevel=level) as zf:
            for n, data in entries.items():
                zf.writestr(n, data)
        return path

    def run_tool(self, *args):
        p = subprocess.run([sys.executable, SCRIPT] + list(args),
                           capture_output=True, text=True)
        return p.returncode, p.stdout

    def base(self):
        return {"a/A.class": klass(61), "cards/x.json": b"{}"}

    def test_archive_only(self):
        a = self.jar("a.jar", self.base())
        b = self.jar("b.jar", dict(reversed(list(self.base().items()))), level=1)
        code, out = self.run_tool("compare", a, b)
        self.assertEqual(code, 0)
        self.assertIn("VERDICT: ARCHIVE-ONLY", out)

    def test_javac_version(self):
        a = self.jar("a.jar", self.base())
        b = self.jar("b.jar", dict(self.base(), **{"a/A.class": klass(65)}))
        code, out = self.run_tool("compare", a, b)
        self.assertEqual(code, 1)
        self.assertIn("VERDICT: JAVAC-VERSION", out)

    def test_class_code_and_entry_set(self):
        a = self.jar("a.jar", self.base())
        b = self.jar("b.jar", dict(self.base(), **{
            "a/A.class": klass(61, b"other"), "a/Overlay.class": klass(61)}))
        code, out = self.run_tool("compare", a, b)
        self.assertEqual(code, 1)
        self.assertIn("ENTRY-SET", out)
        self.assertIn("CLASS-CODE", out)
        self.assertIn("a/Overlay.class", out)

    def test_tsv_round_trip(self):
        a = self.jar("a.jar", self.base())
        tsv = os.path.join(self.tmp.name, "a.tsv")
        self.assertEqual(self.run_tool("dump", a, tsv)[0], 0)
        b = self.jar("b.jar", dict(self.base(), **{"cards/x.json": b"[]"}))
        code, out = self.run_tool("compare", tsv, b)
        self.assertEqual(code, 1)
        self.assertIn("VERDICT: RESOURCES", out)
        self.assertEqual(self.run_tool("compare", tsv, a)[0], 0)


if __name__ == "__main__":
    unittest.main()
