#!/usr/bin/env python3
"""Build a reproducible JAR (zip) from a classes directory (AI-078, AI-099, AI-100).

Fixed entry order (manifest first, then sorted paths), fixed timestamps
(2026-01-01 00:00:00 UTC), fixed Unix mode bits (0o644), an LF-normalized
manifest, and CRLF->LF-normalized text resources — so two builds from the
same sources produce byte-identical output on any OS, even when the source
checkout uses Windows CRLF line endings (git core.autocrlf). Shared by
build-release.sh and build-release.bat (Windows parity: the .bat used to call
`jar --create`, whose timestamps/ordering vary per run).

Usage: make-repro-jar.py <out.jar> <manifest.txt> <classes-dir>
Stdlib only.
"""
import os
import sys
import zipfile

FIXED_DT = (2026, 1, 1, 0, 0, 0)
# AI-099: fixed mode bits. The old code preserved os.stat() bits, which differ
# between Linux (0o644) and Windows (0o666) and broke cross-OS byte-identity.
FIXED_MODE = 0o644

# AI-100: text extensions whose line endings are normalized CRLF -> LF before
# being written into the jar. The alpha's card JSONs, LICENSE.txt and
# ATTRIBUTION.md are checked out with CRLF on Windows (core.autocrlf) and LF
# on Linux; copying them verbatim broke cross-OS byte-identity. Line endings
# are semantically neutral in these formats. Everything else (.class, images,
# audio, ...) is copied byte-for-byte and must never be touched.
TEXT_EXTS = frozenset({
    ".json", ".txt", ".md", ".properties", ".xml", ".csv", ".tsv",
    ".yaml", ".yml", ".ini", ".cfg", ".html", ".css", ".js",
})


def normalize_text(data):
    """CRLF -> LF, the canonical line ending for the reproducible build."""
    return data.replace(b"\r\n", b"\n")


def main():
    if len(sys.argv) != 4:
        sys.exit("usage: make-repro-jar.py <out.jar> <manifest.txt> <classes-dir>")
    jar_path, manifest_path, classes_dir = sys.argv[1], sys.argv[2], sys.argv[3]
    with zipfile.ZipFile(jar_path, "w", zipfile.ZIP_DEFLATED) as zf:
        # Manifest first, as `jar --create` does.
        zi = zipfile.ZipInfo("META-INF/MANIFEST.MF", date_time=FIXED_DT)
        zi.compress_type = zipfile.ZIP_DEFLATED
        zi.external_attr = FIXED_MODE << 16
        with open(manifest_path, "rb") as f:
            data = f.read()
        # AI-100: normalize *every* line ending, not just the trailing one.
        # The Windows .bat's echo-generated manifest.txt is CRLF throughout;
        # the old rstrip-only code left interior CRLFs in, so a Windows-built
        # jar hashed differently from a Linux-built one. The jar manifest
        # ends with exactly one newline.
        data = normalize_text(data).rstrip(b"\n") + b"\n"
        zf.writestr(zi, data)
        for root, dirs, files in os.walk(classes_dir):
            dirs.sort()
            for fn in sorted(files):
                full = os.path.join(root, fn)
                # Zip entry names always use forward slashes (os.sep is "\\"
                # on Windows).
                arc = os.path.relpath(full, classes_dir).replace(os.sep, "/")
                # The merged dependency jars bring their own META-INF/MANIFEST.MF;
                # skip it — ours is written first, above. (Without this the jar
                # carries a duplicate manifest entry.)
                if arc == "META-INF/MANIFEST.MF":
                    continue
                zi = zipfile.ZipInfo(arc, date_time=FIXED_DT)
                zi.compress_type = zipfile.ZIP_DEFLATED
                zi.external_attr = FIXED_MODE << 16
                with open(full, "rb") as f:
                    content = f.read()
                # AI-100: normalize text-resource line endings; binaries pass
                # through byte-for-byte.
                if os.path.splitext(arc)[1].lower() in TEXT_EXTS:
                    content = normalize_text(content)
                zf.writestr(zi, content)
    print("reproducible jar -> %s" % jar_path)


if __name__ == "__main__":
    main()
