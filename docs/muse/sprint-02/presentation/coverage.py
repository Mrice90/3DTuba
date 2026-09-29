#!/usr/bin/env python3
"""Per-card presentation coverage report (AI-064, AI-077).

Reads presentation-manifest.json and a staging root, then writes a Markdown
table per card showing whether the model, textures, each animation and each
SFX file are present or missing.

AI-077 resolution rules (recursive under the staging root):
  - Models: `meshy/<batch>/<card_id>.glb` found by recursive search;
    `meshy/<card_id>.glb` also accepted.
  - SFX: `picks/<card_id>_<cue>.wav` preferred; `sfx/<card_id>_<cue>.wav`
    or `.mp3` accepted as fallback.

Usage: python3 coverage.py <manifest.json> <staging_root> [output.md]
  Writes Markdown to output.md, or stdout if omitted. Exit 0 always; the
  report itself is the artifact (missing assets are expected pre-launch).

Stdlib only.
"""
import json
import os
import sys


def find_model(staging, card_id):
    """Recursive search for <card_id>.glb under staging/meshy/."""
    meshy = os.path.join(staging, "meshy")
    if not os.path.isdir(meshy):
        return None
    target = f"{card_id}.glb"
    for root, _dirs, files in os.walk(meshy):
        if target in files:
            return os.path.relpath(os.path.join(root, target), staging)
    return None


def find_sfx(staging, sfx_key):
    """Prefer picks/<key>.wav; accept sfx/<key>.wav or .mp3."""
    picks = os.path.join(staging, "picks", f"{sfx_key}.wav")
    if os.path.isfile(picks):
        return os.path.relpath(picks, staging)
    for ext in (".wav", ".mp3"):
        cand = os.path.join(staging, "sfx", f"{sfx_key}{ext}")
        if os.path.isfile(cand):
            return os.path.relpath(cand, staging)
    return None


def check(staging, rel):
    return os.path.isfile(os.path.join(staging, rel))


def check_textures(staging, rel):
    d = os.path.join(staging, rel)
    return os.path.isdir(d) and any(os.scandir(d))


def main():
    if len(sys.argv) < 3:
        sys.exit("usage: coverage.py <manifest.json> <staging_root> [output.md]")
    manifest_path, staging = sys.argv[1], sys.argv[2]
    out_path = sys.argv[3] if len(sys.argv) > 3 else None

    with open(manifest_path, encoding="utf-8") as f:
        manifest = json.load(f)
    cards = manifest["cards"]

    total = {"model": 0, "textures": 0, "animations": 0, "sfx": 0}
    have = {"model": 0, "textures": 0, "animations": 0, "sfx": 0}

    L = []
    L.append("# Presentation coverage report (AI-064)")
    L.append("")
    L.append(f"Staging root: `{staging}` — {len(cards)} cards.")
    L.append("")
    for cid in sorted(cards):
        c = cards[cid]
        L.append(f"## {c['name']} (`{cid}`) — {c['faction']} {c['type']}")
        L.append("")
        L.append("| Asset | Expected | Status |")
        L.append("|---|---|---|")
        rows = []
        model_rel = find_model(staging, cid)
        ok = model_rel is not None
        rows.append((f"Model", f"`{model_rel or c['model_path']}`", ok))
        total["model"] += 1
        have["model"] += ok
        ok = check_textures(staging, c["textures_path"])
        rows.append((f"Textures", f"`{c['textures_path']}`", ok))
        total["textures"] += 1
        have["textures"] += ok
        for a in c["animations"]:
            ok = check(staging, a["file"])
            rows.append((f"Anim `{a['key']}`", f"`{a['file']}`", ok))
            total["animations"] += 1
            have["animations"] += ok
        # Animations named in events but without a clip key are missing by definition.
        for event, e in sorted(c["events"].items()):
            if e["animation"] is None:
                rows.append((f"Anim for `{event}`", "— no clip key in manifest —", False))
                total["animations"] += 1
        for s in c["sfx"]:
            sfx_rel = find_sfx(staging, s["key"])
            ok = sfx_rel is not None
            rows.append((f"SFX `{s['key']}`", f"`{sfx_rel or s['file']}`", ok))
            total["sfx"] += 1
            have["sfx"] += ok
        for label, expected, ok in rows:
            L.append(f"| {label} | {expected} | {'✅' if ok else '❌'} |")
        L.append("")

    L.append("## Totals")
    L.append("")
    L.append("| Category | Present | Total |")
    L.append("|---|---|---|")
    for k in ("model", "textures", "animations", "sfx"):
        L.append(f"| {k} | {have[k]} | {total[k]} |")
    L.append("")

    text = "\n".join(L)
    if out_path:
        # AI-069: explicit UTF-8 + LF newlines — Windows defaults to cp1252,
        # which raised UnicodeEncodeError on the ✅/❌ status glyphs.
        with open(out_path, "w", encoding="utf-8", newline="\n") as f:
            f.write(text)
        print(f"coverage report -> {out_path}")
    else:
        print(text)


if __name__ == "__main__":
    main()
