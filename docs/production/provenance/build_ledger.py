"""AI-219-AI-DISCLOSURE: build the shipped-asset provenance ledger.

Reads only files already in this repo and writes AI_CONTENT_LEDGER.csv next to
this script. Re-run after new assets land:

    python3 docs/production/provenance/build_ledger.py
"""
import csv
import json
import pathlib

ROOT = pathlib.Path(__file__).resolve().parents[3]
OUT = pathlib.Path(__file__).resolve().parent / "AI_CONTENT_LEDGER.csv"

GAPS = ROOT / "docs/reviews/2026-10-08-asset-library-gaps.csv"
STRUCTURES = ROOT / "assets/staging/structure-pilots-2026-10-09/README.md"
SFX_NEEDS = ROOT / "docs/production/audio/SFX_NEEDS.json"
SFX_MADE = ROOT / "assets/audio/SFX_MANIFEST.json"


def model_tool(row, blender_ids):
    if row["id"] in blender_ids:
        return "Blender (built by script, no generative AI)", "assets/staging/structure-pilots-2026-10-09"
    if row["type"] == "Spell":
        return "none (effect only)", ""
    src = row["source"]
    if src.startswith("staging/meshy/"):
        return "Meshy (generative AI 3D)", src
    if src:
        return "unknown", src
    return "none yet (procedural stand-in)", ""


def card_art_note(source_art):
    if source_art.startswith("desolate-tuba/"):
        return "Desolate-Tuba card art", "UNKNOWN - Matt to confirm"
    if source_art.startswith("alpha-0.7.15/"):
        return "TubaExperiment alpha 0.7.15 card art", "UNKNOWN - Matt to confirm"
    return source_art or "none", "UNKNOWN - Matt to confirm"


def main():
    blender_ids = set()
    for line in STRUCTURES.read_text(encoding="utf-8").splitlines():
        if line.startswith("| `"):
            blender_ids.add(line.split("`")[1])

    needs = json.loads(SFX_NEEDS.read_text(encoding="utf-8"))["needs"]
    made = {p["key"] for p in json.loads(SFX_MADE.read_text(encoding="utf-8"))["picks"]}
    needed_by_card, made_by_card = {}, {}
    for n in needs:
        if n["scope"] != "card":
            continue
        cid = n["card_id"]
        needed_by_card[cid] = needed_by_card.get(cid, 0) + 1
        if n["key"] in made:
            made_by_card[cid] = made_by_card.get(cid, 0) + 1

    rows = [r for r in csv.DictReader(GAPS.open(encoding="utf-8")) if r["priority"] != "P4"]
    fields = ["id", "name", "faction", "type", "card_art", "card_art_tool",
              "model_tool", "model_source", "meshy_task_ids",
              "sfx_cues_made", "sfx_cues_needed", "sfx_tool"]
    with OUT.open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=fields)
        w.writeheader()
        for r in sorted(rows, key=lambda r: (r["faction"], r["type"], r["id"])):
            tool, src = model_tool(r, blender_ids)
            art, art_tool = card_art_note(r["source_art"])
            w.writerow({
                "id": r["id"], "name": r["name"], "faction": r["faction"], "type": r["type"],
                "card_art": art, "card_art_tool": art_tool,
                "model_tool": tool, "model_source": src,
                "meshy_task_ids": r["meshy_task_ids"] if tool.startswith("Meshy") else "",
                "sfx_cues_made": made_by_card.get(r["id"], 0),
                "sfx_cues_needed": needed_by_card.get(r["id"], 0),
                "sfx_tool": "ElevenLabs (generative AI)" if needed_by_card.get(r["id"]) else "",
            })
    core_needed = sum(1 for n in needs if n["scope"] == "core")
    core_made = sum(1 for n in needs if n["scope"] == "core" and n["key"] in made)
    print(f"{len(rows)} cards -> {OUT.relative_to(ROOT)}; core cues {core_made}/{core_needed}")


if __name__ == "__main__":
    main()
