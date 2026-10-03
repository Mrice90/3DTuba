#!/usr/bin/env python3
"""AI-080 / AI-052-ASSET: stage local media into UnityProof for the playtest build.

Reads (never writes) a staging folder laid out like 3DTuba/assets/staging:
  meshy/**/<card_id>.glb           (rigged/ preferred, then textured/, then newest batch dir)
  elevenlabs/**/picks/<card_id>_<cue>.wav   (or SFX_INDEX.json when the ElevenLabs thread wrote one)

Writes, inside UnityProof:
  Assets/Playtest/Resources/Playtest/cards.json   committed: 139-card catalog + which cards have media
  Assets/Playtest/Resources/Tokens/<card_id>/     ignored: Blender-normalised FBX + Token_*.png
  Assets/Playtest/Resources/Sfx/<card_id>_<cue>.wav  ignored: copies of the picks
  Build/staging-report.json                        check_glb.py results + selection decisions

Every chosen GLB goes through docs/production/tools/check_glb.py first; a rejected file
(exit 2: malformed/unsupported) is never imported. Height/feet failures are expected at this
stage and are normalised by glb_to_playtest_token.py + the Unity AI-063 budget.

usage: python stage_playtest_assets.py --staging <dir> [--blender exe] [--jobs 4] [--skip-models]
"""
import argparse, concurrent.futures as cf, datetime, json, os, re, shutil, subprocess, sys

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.dirname(HERE)
REPO = os.path.dirname(PROJECT)
MANIFEST = os.path.join(REPO, "docs", "muse", "sprint-02", "presentation", "presentation-manifest.json")
CHECK_GLB = os.path.join(REPO, "docs", "production", "tools", "check_glb.py")
RES = os.path.join(PROJECT, "Assets", "Playtest", "Resources")
CUES = ["deploy", "move", "attack", "hit", "destroy", "ability", "signature", "idle"]
# AI-077: the manifest still says summon/death; ElevenLabs picks use deploy/destroy.
CUE_ALIASES = {"summon": "deploy", "death": "destroy"}


def tier(path):
    p = path.replace("\\", "/").lower()
    if "/rigged/" in p:
        return 3
    if "/textured/" in p or "/hybrid/" in p:
        return 2  # hybrid = land textured onto the approved Blender hex base
    if "/meshy-raw/" in p:
        return 0  # raw Meshy output kept only for provenance
    return 1


def card_of(stem, ids):
    if stem in ids:
        return stem
    for suffix in ("_textured", "_rigged", "_animated"):
        if stem.endswith(suffix) and stem[: -len(suffix)] in ids:
            return stem[: -len(suffix)]
    return None


def check(glb):
    r = subprocess.run([sys.executable, CHECK_GLB, glb, "--no-manifest", "--json"],
                       capture_output=True, text=True, encoding="utf-8")
    try:
        data = json.loads(r.stdout)
        data = data[0] if isinstance(data, list) else data
    except Exception:
        data = {"status": "ERROR", "stderr": r.stderr[-400:]}
    data["exit"] = r.returncode
    return data


def convert(blender, glb, out_fbx):
    if os.path.isdir(os.path.dirname(out_fbx)):
        shutil.rmtree(os.path.dirname(out_fbx))
    r = subprocess.run([blender, "-b", "--factory-startup", "-P", os.path.join(HERE, "glb_to_playtest_token.py"),
                        "--", glb, out_fbx], capture_output=True, text=True, encoding="utf-8", errors="replace")
    ok = r.returncode == 0 and "PLAYTEST_TOKEN_OK" in r.stdout and os.path.exists(out_fbx)
    tail = [l for l in r.stdout.splitlines() if l.startswith(("TOKEN_SIZE", "DECIMATED", "TEXTURE"))]
    return ok, tail if ok else (tail + r.stdout.splitlines()[-5:] + r.stderr.splitlines()[-5:])


def sfx_from_index(staging, ids):
    """SFX_INDEX.json (ElevenLabs thread) wins when present; tolerate list or dict layouts."""
    for root, _, files in os.walk(staging):
        if "SFX_INDEX.json" in files:
            path = os.path.join(root, "SFX_INDEX.json")
            with open(path, encoding="utf-8") as f:
                data = json.load(f)
            entries = data.get("entries", data.get("picks", data)) if isinstance(data, dict) else data
            found = {}
            if isinstance(entries, dict):
                entries = [dict(v, card_id=v.get("card_id", k.rsplit("_", 1)[0])) if isinstance(v, dict)
                           else {"key": k, "file": v} for k, v in entries.items()]
            for e in entries:
                if not isinstance(e, dict):
                    continue
                f_ = e.get("file") or e.get("path") or e.get("pick")
                cid, cue = e.get("card_id"), e.get("cue")
                if f_ and (not cid or not cue):
                    m = re.match(r"(.+)_([a-z]+)\.(wav|mp3)$", os.path.basename(f_))
                    if m:
                        cid, cue = cid or m.group(1), cue or m.group(2)
                if not (f_ and cid in ids and cue):
                    continue
                full = f_ if os.path.isabs(f_) else os.path.join(os.path.dirname(path), f_)
                if not os.path.exists(full):
                    full = os.path.join(staging, f_)
                if os.path.exists(full) and full.lower().endswith(".wav"):
                    found[(cid, CUE_ALIASES.get(cue, cue))] = full
            return found, path
    return None, None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--staging", required=True)
    ap.add_argument("--blender", default=r"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe")
    ap.add_argument("--jobs", type=int, default=4)
    ap.add_argument("--skip-models", action="store_true", help="reuse already converted tokens")
    a = ap.parse_args()

    with open(MANIFEST, encoding="utf-8") as f:
        manifest = json.load(f)["cards"]
    ids = set(manifest)

    # --- models -------------------------------------------------------------------------------
    candidates = {}
    for root, _, files in os.walk(os.path.join(a.staging, "meshy")):
        for fn in files:
            if fn.lower().endswith(".glb"):
                cid = card_of(os.path.splitext(fn)[0], ids)
                if cid:
                    candidates.setdefault(cid, []).append(os.path.join(root, fn))
    report = {"generated": datetime.datetime.now().isoformat(timespec="seconds"), "staging": a.staging,
              "models": {}, "rejected": {}, "sfx": {}, "sfx_index": None}
    chosen = {}
    for cid, paths in sorted(candidates.items()):
        # rigged > textured > plain; then the newest batch/job directory name.
        paths.sort(key=lambda p: (tier(p), os.path.relpath(p, a.staging).replace("\\", "/")), reverse=True)
        for p in paths:
            c = check(p)
            rel = os.path.relpath(p, a.staging).replace("\\", "/")
            if c["exit"] == 2 or c.get("status") == "ERROR":
                report["rejected"][rel] = c.get("checks", c.get("stderr"))
                continue
            fails = [k["check"] for k in c.get("checks", []) if k.get("status") != "PASS"]
            topo = c.get("topology", {})
            chosen[cid] = p
            report["models"][cid] = {"source": rel, "alternatives": [os.path.relpath(q, a.staging).replace("\\", "/")
                                                                     for q in paths if q != p],
                                     "check_glb": c.get("status"), "check_fails": fails,
                                     "bounds_size": c.get("bounds", {}).get("size"),
                                     "triangles": topo.get("triangles"), "images": topo.get("images")}
            break

    tokens = os.path.join(RES, "Tokens")
    os.makedirs(tokens, exist_ok=True)
    if not a.skip_models:
        with cf.ThreadPoolExecutor(a.jobs) as ex:
            futs = {ex.submit(convert, a.blender, p, os.path.join(tokens, cid, cid + ".fbx")): cid
                    for cid, p in chosen.items()}
            for fu in cf.as_completed(futs):
                cid = futs[fu]
                ok, log = fu.result()
                report["models"][cid]["converted"] = ok
                report["models"][cid]["blender"] = log
                print(("OK   " if ok else "FAIL ") + cid, *log[:3])
    for cid in chosen:
        ok = os.path.exists(os.path.join(tokens, cid, cid + ".fbx"))
        report["models"][cid]["converted"] = ok

    # --- sound --------------------------------------------------------------------------------
    picks, index_path = sfx_from_index(a.staging, ids)
    report["sfx_index"] = index_path and os.path.relpath(index_path, a.staging)
    if picks is None:
        picks = {}
        for root, _, files in os.walk(os.path.join(a.staging, "elevenlabs")):
            if os.path.basename(root) != "picks":
                continue
            for fn in files:
                m = re.match(r"(.+)_([a-z]+)\.wav$", fn)
                if m and m.group(1) in ids:
                    picks[(m.group(1), CUE_ALIASES.get(m.group(2), m.group(2)))] = os.path.join(root, fn)
    sfx_dir = os.path.join(RES, "Sfx")
    if os.path.isdir(sfx_dir):
        shutil.rmtree(sfx_dir)
    os.makedirs(sfx_dir)
    for (cid, cue), src in sorted(picks.items()):
        shutil.copyfile(src, os.path.join(sfx_dir, f"{cid}_{cue}.wav"))
        report["sfx"].setdefault(cid, []).append(cue)

    # --- catalog ------------------------------------------------------------------------------
    cards = []
    for cid, c in manifest.items():
        has_model = report["models"].get(cid, {}).get("converted", False)
        cards.append({"id": cid, "name": c["name"], "faction": c["faction"], "type": c["type"],
                      "model": f"Tokens/{cid}/{cid}" if has_model else "",
                      "modelSource": report["models"].get(cid, {}).get("source", "") if has_model else "",
                      "sfx": sorted(report["sfx"].get(cid, []), key=lambda k: CUES.index(k) if k in CUES else 99)})
    os.makedirs(os.path.join(RES, "Playtest"), exist_ok=True)
    with open(os.path.join(RES, "Playtest", "cards.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump({"source": "docs/muse/sprint-02/presentation/presentation-manifest.json",
                   "cards": cards}, f, indent=1)
    os.makedirs(os.path.join(PROJECT, "Build"), exist_ok=True)
    with open(os.path.join(PROJECT, "Build", "staging-report.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(report, f, indent=1)
    n_models = sum(1 for c in cards if c["model"])
    print(f"STAGE_SUMMARY cards={len(cards)} models={n_models} rejected={len(report['rejected'])} "
          f"sfx_cards={len(report['sfx'])} sfx_files={len(picks)}")
    return 0 if len(cards) == 139 else 1


if __name__ == "__main__":
    sys.exit(main())
