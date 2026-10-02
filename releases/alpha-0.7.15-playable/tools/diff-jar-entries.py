#!/usr/bin/env python3
"""Per-entry diff of two JAR (zip) files (AI-100 rework).

The 2026-09-30 12:00 acceptance review rejected AI-100 because the Windows
jar (7796b68e) did not equal the canonical Linux jar (2db3a12c) even after
CRLF normalization. A whole-jar hash cannot say WHERE the bytes differ, so
this tool compares entry by entry and prints, per entry:

  - name, uncompressed size, CRC-32, SHA-256 of the uncompressed content
  - the zip header fields that must be OS-independent

Report sections:
  * entries only in A / only in B            -> entry-set difference
  * content differences                       -> real content difference
  - header differences with identical content -> zip metadata difference
    (e.g. create_system 0 vs 3, the AI-100 residue)

Exit 0 when the jars are entry-identical (same entry set, same content,
same pinned header fields); exit 1 otherwise. Stdlib only.

Usage: diff-jar-entries.py <a.jar> <b.jar>
"""
import hashlib
import sys
import zipfile

# Header fields the reproducible packer pins; any difference here with
# identical content is a metadata (not content) difference.
HEADER_FIELDS = ("create_system", "create_version", "extract_version",
                 "flag_bits", "compress_type", "external_attr",
                 "internal_attr")


def entry_report(zf, info):
    data = zf.read(info.filename)
    return {
        "name": info.filename,
        "size": info.file_size,
        "crc32": "%08x" % (info.CRC & 0xFFFFFFFF),
        "sha256": hashlib.sha256(data).hexdigest(),
        "date_time": info.date_time,
        "header": {f: getattr(info, f) for f in HEADER_FIELDS},
    }


def load(path):
    with zipfile.ZipFile(path) as zf:
        return {i.filename: entry_report(zf, i) for i in zf.infolist()}


def diff(a_path, b_path):
    a = load(a_path)
    b = load(b_path)
    only_a = sorted(set(a) - set(b))
    only_b = sorted(set(b) - set(a))
    content_diffs = []
    header_diffs = []
    for name in sorted(set(a) & set(b)):
        ra, rb = a[name], b[name]
        if (ra["size"], ra["crc32"], ra["sha256"]) != \
           (rb["size"], rb["crc32"], rb["sha256"]):
            content_diffs.append((name, ra, rb))
        else:
            hd = {f: (ra["header"][f], rb["header"][f])
                  for f in HEADER_FIELDS if ra["header"][f] != rb["header"][f]}
            if ra["date_time"] != rb["date_time"]:
                hd["date_time"] = (ra["date_time"], rb["date_time"])
            if hd:
                header_diffs.append((name, hd))
    return only_a, only_b, content_diffs, header_diffs


def main():
    if len(sys.argv) != 3:
        sys.exit("usage: diff-jar-entries.py <a.jar> <b.jar>")
    a_path, b_path = sys.argv[1], sys.argv[2]
    only_a, only_b, content_diffs, header_diffs = diff(a_path, b_path)

    if only_a:
        print("entries only in %s (%d):" % (a_path, len(only_a)))
        for n in only_a:
            print("  - %s" % n)
    if only_b:
        print("entries only in %s (%d):" % (b_path, len(only_b)))
        for n in only_b:
            print("  - %s" % n)
    for name, ra, rb in content_diffs:
        print("CONTENT DIFFERS: %s" % name)
        print("  a: size=%d crc32=%s sha256=%s" %
              (ra["size"], ra["crc32"], ra["sha256"]))
        print("  b: size=%d crc32=%s sha256=%s" %
              (rb["size"], rb["crc32"], rb["sha256"]))
    for name, hd in header_diffs:
        print("HEADER DIFFERS (content identical): %s" % name)
        for f, (va, vb) in hd.items():
            print("  %s: a=%r b=%r" % (f, va, vb))

    n = len(only_a) + len(only_b) + len(content_diffs) + len(header_diffs)
    if n == 0:
        print("IDENTICAL: same entry set, same content, same header fields")
        return 0
    print("DIFFERING ENTRIES: %d "
          "(only_a=%d only_b=%d content=%d header=%d)" %
          (n, len(only_a), len(only_b), len(content_diffs), len(header_diffs)))
    return 1


if __name__ == "__main__":
    sys.exit(main())
