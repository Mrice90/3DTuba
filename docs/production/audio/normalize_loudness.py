#!/usr/bin/env python3
"""Loudness-match the game SFX set so cues sit together in the mix.

pick_takes.py peak-normalizes every pick to -6 dBFS, which makes a soft UI tick as loud as a capital exploding.
This pass measures each file's integrated loudness (EBU R128) and applies one gain change toward a per-category
target, with the sample peak capped at -1 dBFS. Clips too short for an integrated reading (under ~0.4 s) fall back
to their short-term peak loudness.

Targets (LUFS):
  ui       -24  menus, hover/select ticks, card handling, legal-move shimmer, damage tick
  ambient  -26  card idle loops and board ambience
  event    -18  everything else (deploy, move, attack, hit, ability, destroy, core board events)
  hero     -16  signature cues, match start, victory, defeat, capital reveal

Rows in assets/audio/SFX_MANIFEST.json get `loudness`: {category, target_lufs, measured_lufs, gain_db}. A row that
already carries `loudness` is skipped, so the pass is safe to re-run; pick_takes.py writes fresh rows (without it)
whenever it re-picks from takes. Requires ffmpeg.
"""
import json
import os
import re
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
AUDIO_DIR = os.path.join(ROOT, "assets/audio")
MANIFEST = os.path.join(AUDIO_DIR, "SFX_MANIFEST.json")
TARGET = {"ui": -24.0, "ambient": -26.0, "event": -18.0, "hero": -16.0}
PEAK_CEILING = -1.0
TOLERANCE = 0.5

UI_CORE = ("core_ui_", "core_hex_hover", "core_hex_select", "core_card_hover", "core_card_pickup",
           "core_card_return_hand", "core_legal_move_show", "core_damage_number", "core_invalid_action",
           "core_command_center_open", "core_ability_ready")
HERO_CORE = ("core_match_start", "core_victory", "core_defeat", "core_capital_reveal")


def category(row):
    key, cue = row["key"], row.get("cue")
    if row.get("scope") == "core":
        if key.startswith("core_ambience_"):
            return "ambient"
        if key.startswith(UI_CORE):
            return "ui"
        if key.startswith(HERO_CORE):
            return "hero"
        return "event"
    if cue == "idle":
        return "ambient"
    if cue == "signature":
        return "hero"
    return "event"


def measure(path):
    err = subprocess.run(["ffmpeg", "-hide_banner", "-nostats", "-i", path, "-af", "ebur128=peak=sample",
                          "-f", "null", "-"], capture_output=True, text=True).stderr
    summary = err[err.rfind("Summary:"):]
    integrated = float(re.search(r"I:\s+(-?[\d.]+|-inf) LUFS", summary).group(1).replace("-inf", "-70"))
    peak = float(re.search(r"Peak:\s+(-?[\d.]+|-inf) dBFS", summary).group(1).replace("-inf", "-70"))
    if integrated <= -69:
        # Too short to gate: use the loudest momentary (400 ms) reading instead.
        moments = [float(m) for m in re.findall(r" M:\s*(-?[\d.]+)", err)]
        integrated = max(moments) if moments else peak - 10
    return integrated, peak


def main():
    manifest = json.load(open(MANIFEST))
    changed = 0
    for row in manifest["picks"]:
        if "loudness" in row:
            continue
        path = os.path.join(AUDIO_DIR, row["file"])
        cat = category(row)
        lufs, peak = measure(path)
        gain = TARGET[cat] - lufs
        gain = min(gain, PEAK_CEILING - peak)
        if abs(gain) >= TOLERANCE:
            tmp = path + ".tmp.mp3"
            subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", path, "-af",
                            f"volume={gain:.2f}dB", "-ar", "44100", "-b:a", "128k", tmp], check=True)
            os.replace(tmp, path)
            changed += 1
        else:
            gain = 0.0
        row["loudness"] = {"category": cat, "target_lufs": TARGET[cat], "measured_lufs": round(lufs, 1),
                           "gain_db": round(gain, 2)}
    json.dump(manifest, open(MANIFEST, "w"), indent=1)
    print(f"loudness pass: {changed} files re-gained, {len(manifest['picks'])} rows tagged", file=sys.stderr)


if __name__ == "__main__":
    main()
