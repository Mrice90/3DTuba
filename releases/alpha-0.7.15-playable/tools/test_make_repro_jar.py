#!/usr/bin/env python3
"""Regression tests for tools/make-repro-jar.py (AI-100).

Covers the cross-OS reproducibility fixes: CRLF manifests and CRLF text
resources must normalize to the same bytes an LF build produces, binary
entries must pass through untouched, and repeated builds must be
byte-identical. Stdlib only.
"""
import hashlib
import os
import shutil
import subprocess
import sys
import tempfile
import unittest
import zipfile

HERE = os.path.dirname(os.path.abspath(__file__))
SCRIPT = os.path.join(HERE, "make-repro-jar.py")


def sha(data):
    return hashlib.sha256(data).hexdigest()


def build_jar(manifest_bytes, files):
    """Run make-repro-jar.py on an in-memory fixture; return the jar bytes."""
    tmp = tempfile.mkdtemp(prefix="reprojar-")
    try:
        classes = os.path.join(tmp, "classes")
        os.makedirs(classes)
        man = os.path.join(tmp, "manifest.txt")
        with open(man, "wb") as f:
            f.write(manifest_bytes)
        for rel, content in files.items():
            full = os.path.join(classes, rel)
            os.makedirs(os.path.dirname(full), exist_ok=True)
            with open(full, "wb") as f:
                f.write(content)
        out = os.path.join(tmp, "out.jar")
        r = subprocess.run([sys.executable, SCRIPT, out, man, classes],
                           capture_output=True, text=True)
        assert r.returncode == 0, r.stderr
        with open(out, "rb") as f:
            return f.read()
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def read_entry(jar_bytes, name):
    fd, path = tempfile.mkstemp(suffix=".jar", prefix="reprojar-")
    try:
        with os.fdopen(fd, "wb") as f:
            f.write(jar_bytes)
        with zipfile.ZipFile(path) as zf:
            return zf.read(name)
    finally:
        os.unlink(path)


CLASS_BLOB = b"\xca\xfe\xba\xbe" + bytes(range(256)) * 4  # has 0x0D 0x0A runs


class TestReproJar(unittest.TestCase):
    def test_crlf_manifest_normalizes_to_lf(self):
        jar = build_jar(b"Manifest-Version: 1.0\r\nMain-Class: Foo\r\n\r\n",
                        {"a/A.class": CLASS_BLOB})
        data = read_entry(jar, "META-INF/MANIFEST.MF")
        self.assertNotIn(b"\r", data)
        self.assertTrue(data.endswith(b"\n"))
        self.assertFalse(data.endswith(b"\n\n"))

    def test_lf_and_crlf_manifests_give_same_jar(self):
        files = {"a/A.class": CLASS_BLOB}
        lf = build_jar(b"Manifest-Version: 1.0\nMain-Class: Foo\n", files)
        crlf = build_jar(b"Manifest-Version: 1.0\r\nMain-Class: Foo\r\n", files)
        self.assertEqual(sha(lf), sha(crlf))

    def test_crlf_text_resources_normalize(self):
        json_crlf = b'{\r\n  "a": 1\r\n}\r\n'
        json_lf = b'{\n  "a": 1\n}\n'
        files_crlf = {"cards/data.json": json_crlf,
                      "audio/LICENSE.txt": b"x\r\ny\r\n"}
        files_lf = {"cards/data.json": json_lf,
                    "audio/LICENSE.txt": b"x\ny\n"}
        a = build_jar(b"Manifest-Version: 1.0\n", files_crlf)
        b = build_jar(b"Manifest-Version: 1.0\n", files_lf)
        self.assertEqual(sha(a), sha(b))
        self.assertEqual(read_entry(a, "cards/data.json"), json_lf)

    def test_binary_entries_untouched(self):
        jar = build_jar(b"Manifest-Version: 1.0\n",
                        {"art/img.png": CLASS_BLOB,
                         "a/A.class": CLASS_BLOB})
        self.assertEqual(read_entry(jar, "art/img.png"), CLASS_BLOB)
        self.assertEqual(read_entry(jar, "a/A.class"), CLASS_BLOB)

    def test_builds_are_byte_identical(self):
        files = {"b/B.class": CLASS_BLOB,
                 "cards/d.json": b'{"k": "v"}\r\n'}
        a = build_jar(b"Manifest-Version: 1.0\r\n", files)
        b = build_jar(b"Manifest-Version: 1.0\r\n", files)
        self.assertEqual(a, b)


if __name__ == "__main__":
    unittest.main()
