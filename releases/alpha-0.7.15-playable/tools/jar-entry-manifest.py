#!/usr/bin/env python3
"""Per-entry content manifest of a JAR, and a provenance comparison (AI-080-BUILD-PROVENANCE).

diff-jar-entries.py needs both jars side by side. The SP2 Unity package jar
lives on the laptop and the canonical jar is ~91 MB, so this tool reduces a
jar to a small TSV (one row per entry: name, size, SHA-256 of the
uncompressed content, class-file major version) that can travel instead.

  jar-entry-manifest.py dump <x.jar> [out.tsv]
  jar-entry-manifest.py compare <a.jar|a.tsv> <b.jar|b.tsv>

compare explains WHY two whole-jar hashes differ:
  ARCHIVE-ONLY      every entry identical; only zip packing differs
                    (timestamps, order, compression, header fields)
  JAVAC-VERSION     only .class files differ, and some differ in major
                    version (compiled by a different JDK, same source likely)
  CLASS-CODE        .class files differ at the same major version
                    (different source, or a different javac build)
  RESOURCES         non-class entries (card JSON, art, audio) differ
  ENTRY-SET         entries added or removed (new/overlay classes, assets)

Stdlib only. Exit 0 when content-identical, 1 otherwise.
"""
import hashlib
import sys
import zipfile

COLUMNS = ("name", "size", "sha256", "class_major")


def dump(jar_path):
    rows = {}
    with zipfile.ZipFile(jar_path) as zf:
        for info in zf.infolist():
            if info.is_dir():
                continue
            data = zf.read(info.filename)
            major = ""
            if info.filename.endswith(".class") and data[:4] == b"\xca\xfe\xba\xbe":
                major = str(int.from_bytes(data[6:8], "big"))
            rows[info.filename] = (str(info.file_size),
                                   hashlib.sha256(data).hexdigest(), major)
    return rows


def load(path):
    if not path.endswith(".tsv"):
        return dump(path)
    rows = {}
    with open(path, encoding="utf-8") as f:
        for line in f:
            if line.startswith("#") or not line.strip():
                continue
            name, size, sha, major = line.rstrip("\n").split("\t")
            if name == "name":
                continue
            rows[name] = (size, sha, major)
    return rows


def write_tsv(rows, out):
    out.write("\t".join(COLUMNS) + "\n")
    for name in sorted(rows):
        out.write("\t".join((name,) + rows[name]) + "\n")


def compare(a_path, b_path):
    a, b = load(a_path), load(b_path)
    only_a = sorted(set(a) - set(b))
    only_b = sorted(set(b) - set(a))
    changed = [n for n in sorted(set(a) & set(b)) if a[n][1] != b[n][1]]
    classes = [n for n in changed if n.endswith(".class")]
    resources = [n for n in changed if not n.endswith(".class")]
    version_shift = [n for n in classes if a[n][2] != b[n][2]]

    def majors(rows):
        return sorted({r[2] for r in rows.values() if r[2]})

    print("a: %s  entries=%d class majors=%s" % (a_path, len(a), majors(a)))
    print("b: %s  entries=%d class majors=%s" % (b_path, len(b), majors(b)))
    for label, names in (("only in a", only_a), ("only in b", only_b),
                         ("class content differs", classes),
                         ("resource content differs", resources)):
        if names:
            print("%s (%d):" % (label, len(names)))
            for n in names[:200]:
                print("  - %s" % n)
            if len(names) > 200:
                print("  ... %d more" % (len(names) - 200))

    verdicts = []
    if only_a or only_b:
        verdicts.append("ENTRY-SET")
    if resources:
        verdicts.append("RESOURCES")
    if classes:
        verdicts.append("JAVAC-VERSION" if version_shift else "CLASS-CODE")
    if not verdicts:
        print("VERDICT: ARCHIVE-ONLY (every entry content-identical; "
              "any whole-jar hash difference comes only from zip packing)")
        return 0
    print("VERDICT: %s" % " + ".join(verdicts))
    if version_shift:
        print("  %d of %d differing classes changed major version"
              % (len(version_shift), len(classes)))
    return 1


def main(argv):
    if len(argv) >= 2 and argv[0] == "dump":
        rows = dump(argv[1])
        if len(argv) == 3:
            with open(argv[2], "w", encoding="utf-8", newline="\n") as out:
                write_tsv(rows, out)
        else:
            write_tsv(rows, sys.stdout)
        return 0
    if len(argv) == 3 and argv[0] == "compare":
        return compare(argv[1], argv[2])
    sys.exit(__doc__)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
