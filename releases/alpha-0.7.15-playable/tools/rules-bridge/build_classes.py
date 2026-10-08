#!/usr/bin/env python3
"""Compile the rules bridge for a playtest build's Bridge/ folder.

Compiles RulesBridge.java and the 3DTuba rules overlay (overlay/src: summon
slots, covered Structures, see overlay/README.md) against the pinned alpha
jar. The overlay classes go first on the classpath, so they shadow the jar's
copies of the same classes.

Usage:
  python build_classes.py --jar <infinite-conquest-alpha-0.7.15.jar> --out <build>/Bridge/classes

The Unity exe runs: java -cp "Bridge/classes;Bridge/<jar>" RulesBridge
"""
import argparse
import shutil
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--jar", required=True, help="pinned alpha jar")
    ap.add_argument("--out", required=True, help="output classes directory (replaced)")
    args = ap.parse_args()
    jar, out = Path(args.jar).resolve(), Path(args.out).resolve()
    if not jar.is_file():
        sys.exit(f"build_classes: jar not found: {jar}")
    if out.exists():
        shutil.rmtree(out)
    out.mkdir(parents=True)
    sources = sorted(str(p) for p in (HERE / "overlay" / "src").rglob("*.java"))
    sources.append(str(HERE / "RulesBridge.java"))
    cmd = ["javac", "-encoding", "UTF-8", "-nowarn", "--release", "17", "-cp", str(jar), "-d", str(out), *sources]
    result = subprocess.run(cmd, capture_output=True, text=True)
    if result.returncode != 0:
        sys.exit("build_classes: javac failed\n" + result.stderr)
    count = sum(1 for _ in out.rglob("*.class"))
    print(f"build_classes: {len(sources)} sources -> {count} classes in {out}")


if __name__ == "__main__":
    main()
