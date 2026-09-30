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


def entry_infos(jar_bytes):
    fd, path = tempfile.mkstemp(suffix=".jar", prefix="reprojar-")
    try:
        with os.fdopen(fd, "wb") as f:
            f.write(jar_bytes)
        with zipfile.ZipFile(path) as zf:
            return {i.filename: i for i in zf.infolist()}
    finally:
        os.unlink(path)


def rewrite_with_create_system(jar_bytes, create_system):
    """Repack a jar changing ONLY the create_system field of each entry.

    Simulates what CPython's zipfile does on another OS (0 on Windows,
    3 on POSIX) while keeping every content byte identical.
    """
    fd, path = tempfile.mkstemp(suffix=".jar", prefix="reprojar-")
    try:
        with os.fdopen(fd, "wb") as f:
            f.write(jar_bytes)
        out_fd, out_path = tempfile.mkstemp(suffix=".jar", prefix="reprojar-")
        # Close the mkstemp descriptor immediately: ZipFile opens the path
        # itself, and on Windows os.unlink() fails with WinError 32 while any
        # handle is open (Linux allows unlink-with-open-handles, which is why
        # this only regressed on Windows CI).
        os.close(out_fd)
        try:
            with zipfile.ZipFile(path) as zin, \
                    zipfile.ZipFile(out_path, "w", zipfile.ZIP_DEFLATED) as zout:
                for info in zin.infolist():
                    data = zin.read(info.filename)
                    ni = zipfile.ZipInfo(info.filename, date_time=info.date_time)
                    ni.compress_type = info.compress_type
                    ni.external_attr = info.external_attr
                    ni.create_system = create_system
                    ni.flag_bits = info.flag_bits
                    zout.writestr(ni, data)
            with open(out_path, "rb") as f:
                return f.read()
        finally:
            os.unlink(out_path)
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

    def test_create_system_pinned_to_unix(self):
        # AI-100 rework: CPython defaults ZipInfo.create_system to 0 on
        # Windows and 3 on POSIX — an OS fingerprint in every entry header
        # that made the Windows jar hash differ from the Linux jar
        # (7796b68e vs 2db3a12c). The packer must pin it to 3 everywhere.
        jar = build_jar(b"Manifest-Version: 1.0\r\n",
                        {"a/A.class": CLASS_BLOB,
                         "cards/d.json": b'{"k": "v"}\r\n'})
        infos = entry_infos(jar)
        self.assertTrue(infos)
        for name, info in infos.items():
            self.assertEqual(info.create_system, 3, name)

    def test_create_system_flip_changes_jar_hash(self):
        # Mechanism guard: flipping ONLY create_system (the Windows default
        # of 0) on an otherwise identical jar changes the jar SHA-256.
        # This is the exact residue the 12:00 review found.
        jar = build_jar(b"Manifest-Version: 1.0\n",
                        {"a/A.class": CLASS_BLOB})
        windows_sim = rewrite_with_create_system(jar, 0)
        self.assertNotEqual(sha(jar), sha(windows_sim))
        # ...and content is untouched by the flip.
        self.assertEqual(read_entry(jar, "a/A.class"),
                         read_entry(windows_sim, "a/A.class"))


if __name__ == "__main__":
    unittest.main()
