#!/usr/bin/env python3
"""Pick one take per cue, clean it up, and write the game-ready SFX set + manifest.

For each key in SFX_NEEDS.json with downloaded takes (assets/audio/takes/<key>/):
  - drop near-silent takes (max volume below -35 dB)
  - pick the take whose length is closest to the target seconds (first take wins ties)
  - trim leading silence, peak-normalize to -6 dBFS, add a 15 ms fade-out
  - write assets/audio/sfx/<key>.mp3 (the layout coverage.py resolves as sfx/<key>.mp3)
A pick listed in picks_override.json ({"<key>": <take number 1-4>}) wins over the heuristic.
Writes assets/audio/SFX_MANIFEST.json and prints coverage. Requires ffmpeg/ffprobe.
"""
import glob
import json
import os
import re
import subprocess

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
AUDIO = os.path.join(ROOT, "docs/production/audio")
TAKES = os.environ.get("SFX_TAKES_DIR", os.path.join(ROOT, "assets/audio/takes"))
OUT = os.path.join(ROOT, "assets/audio/sfx")
MANIFEST = os.path.join(ROOT, "assets/audio/SFX_MANIFEST.json")
PEAK_DB = -6.0


def probe(path):
    dur = float(subprocess.run(
        ["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", path],
        capture_output=True, text=True, check=True).stdout.strip())
    err = subprocess.run(["ffmpeg", "-hide_banner", "-i", path, "-af", "volumedetect", "-f", "null", "-"],
                         capture_output=True, text=True).stderr
    peak = float(re.search(r"max_volume: (-?[\d.]+) dB", err).group(1))
    return dur, peak


def ledger():
    entries = {}
    path = os.path.join(AUDIO, "sfx_ledger.jsonl")
    if os.path.exists(path):
        for line in open(path):
            if line.strip():
                e = json.loads(line)
                entries[e["key"]] = e  # last write wins (re-fetches)
    return entries


def main():
    needs = json.load(open(os.path.join(AUDIO, "SFX_NEEDS.json")))["needs"]
    override_path = os.path.join(AUDIO, "picks_override.json")
    override = json.load(open(override_path)) if os.path.exists(override_path) else {}
    led = ledger()
    os.makedirs(OUT, exist_ok=True)
    rows, missing = [], []
    for need in needs:
        key = need["key"]
        takes = sorted(glob.glob(os.path.join(TAKES, key, "*.mp3")))
        if not takes:
            missing.append(key)
            continue
        info = []
        for t in takes:
            n = int(os.path.basename(t).split("_", 1)[0])
            dur, peak = probe(t)
            info.append({"take": n, "file": t, "seconds": round(dur, 3), "peak_db": peak})
        usable = [i for i in info if i["peak_db"] > -35] or info
        if key in override:
            pick = next(i for i in info if i["take"] == override[key])
            why = "override"
        else:
            pick = min(usable, key=lambda i: (abs(i["seconds"] - need["seconds"]), i["take"]))
            why = "closest_length"
        gain = PEAK_DB - pick["peak_db"]
        dest = os.path.join(OUT, f"{key}.mp3")
        fade_start = max(pick["seconds"] - 0.015, 0)
        subprocess.run(
            ["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", pick["file"], "-af",
             f"silenceremove=start_periods=1:start_threshold=-50dB,volume={gain:.2f}dB,"
             f"afade=t=out:st={fade_start:.3f}:d=0.015",
             "-ar", "44100", "-b:a", "128k", dest], check=True)
        gen = os.path.basename(pick["file"]).split("_", 1)[1][:-4]
        e = led.get(key, {})
        row = {k: need[k] for k in ("key", "scope", "card_id", "cue", "prompt", "seconds") if k in need}
        row.update({
            "file": f"sfx/{key}.mp3", "pick": pick["take"], "pick_reason": why,
            "pick_generation_id": gen, "pick_seconds": pick["seconds"],
            "takes": len(info), "flow_id": e.get("flow_id"), "node_id": e.get("node_id"),
            "generation_ids": e.get("generation_ids"),
        })
        rows.append(row)
    json.dump({"count": len(rows), "needed": len(needs), "missing": missing,
               "credits": round(sum(e.get("credits", 0) for e in led.values()), 1),
               "picks": rows}, open(MANIFEST, "w"), indent=1)
    print(f"{len(rows)}/{len(needs)} cues picked; {len(missing)} missing -> {MANIFEST}")


if __name__ == "__main__":
    main()
