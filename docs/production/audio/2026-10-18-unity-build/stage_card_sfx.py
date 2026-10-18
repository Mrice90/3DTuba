"""Stage PR #24 SFX into the card-sfx Unity copy: copy mp3s into Resources/MuseSfx, write card-sfx.json, add cues to cards.json."""
import hashlib, json, pathlib, shutil, sys
SRC = pathlib.Path(sys.argv[1])          # pr24-audio/assets/audio
DST = pathlib.Path(sys.argv[2])          # UnityProof/Assets/Playtest/Resources
CUES = ["deploy","move","attack","hit","destroy","ability","idle","signature"]
man = json.loads((SRC/"SFX_MANIFEST.json").read_text(encoding="utf-8"))
assert man["count"] == 740 and not man["missing"], man["count"]
cards_path = DST/"Playtest"/"cards.json"
cards = json.loads(cards_path.read_text(encoding="utf-8"))
byid = {c["id"]: c for c in cards["cards"]}
entries, replaced, added, cue_adds = [], 0, 0, 0
for p in man["picks"]:
    key = p["key"]; f = SRC/p["file"]
    tgt = DST/"MuseSfx"/(key + ".mp3")
    if tgt.exists(): replaced += 1
    else: added += 1
    shutil.copyfile(f, tgt)
    card_id, cue = "", ""
    if p["scope"] != "core":
        cue = next(q for q in CUES if key.endswith("_" + q)); card_id = key[:-len(cue)-1]
        c = byid[card_id]
        if cue not in (c["sfx"] or []): c["sfx"] = list(c["sfx"] or []) + [cue]; cue_adds += 1
    entries.append(dict(key=key, card_id=card_id, cue=cue, scope="core" if p["scope"]=="core" else "card",
                        sha256=hashlib.sha256(f.read_bytes()).hexdigest(),
                        lufs=(p.get("loudness") or {}).get("target_lufs", 0.0)))
cards_path.write_text(json.dumps(cards, indent=1, ensure_ascii=False), encoding="utf-8")
(DST/"Playtest"/"card-sfx.json").write_text(json.dumps(dict(count=len(entries), entries=entries), indent=1), encoding="utf-8")
print(json.dumps(dict(entries=len(entries), replaced=replaced, added=added, catalog_cues_added=cue_adds)))
