"""AI-105: stage card faces (stats, rules text, art) for the Unity playtest hand and card view.

Reads the pinned alpha jar (read-only) and writes:
  Assets/Playtest/Resources/Playtest/card-faces.json  - one entry per card id
  Assets/Playtest/Resources/CardArt/<card_id>.jpg     - art resized to fit 384x384

Usage: python stage_card_faces.py <path-to-infinite-conquest-alpha-0.7.15.jar>
"""
import io
import json
import os
import sys
import zipfile

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
RES = os.path.join(HERE, "..", "Assets", "Playtest", "Resources")
ART_SIZE = 384
FIELDS = ("id", "name", "type", "faction", "cost", "attack", "defense", "range", "movement", "hitPoints",
          "rulesText", "description", "rarity")


def main(jar_path):
    faces, art_by_id = {}, {}
    with zipfile.ZipFile(jar_path) as jar:
        for name in sorted(jar.namelist()):
            if name.startswith("cards/") and name.endswith(".json"):
                for card in json.loads(jar.read(name)).get("cards", []):
                    face = {k: card.get(k) for k in FIELDS}
                    face["keywords"] = card.get("keywords") or []
                    face["rulesText"] = face["rulesText"] or ""
                    face["description"] = face["description"] or ""
                    faces.setdefault(card["id"], face)
            elif name.startswith("art/") and name.lower().endswith((".jpg", ".png")):
                art_by_id[os.path.splitext(os.path.basename(name))[0]] = name
        art_dir = os.path.join(RES, "CardArt")
        os.makedirs(art_dir, exist_ok=True)
        with_art = 0
        for cid, face in faces.items():
            src = art_by_id.get(cid)
            face["art"] = ""
            if not src:
                continue
            img = Image.open(io.BytesIO(jar.read(src))).convert("RGB")
            img.thumbnail((ART_SIZE, ART_SIZE), Image.LANCZOS)
            img.save(os.path.join(art_dir, cid + ".jpg"), "JPEG", quality=85)
            face["art"] = "CardArt/" + cid
            with_art += 1
    out = {"source": os.path.basename(jar_path), "cards": sorted(faces.values(), key=lambda f: f["id"])}
    with open(os.path.join(RES, "Playtest", "card-faces.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(out, f, indent=1, ensure_ascii=False)
    print(f"card faces: {len(faces)} cards, {with_art} with art")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    main(sys.argv[1])
