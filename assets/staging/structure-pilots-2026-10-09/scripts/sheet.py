"""Review sheets: source art beside the four renders. Usage: python3 -I sheet.py OUT_DIR ART_DIR"""
import json, os, sys
from PIL import Image, ImageDraw
out, art = sys.argv[1], sys.argv[2]
stats = json.load(open(os.path.join(out, "stats.json")))
rows = []
for cid, st in stats.items():
    src = Image.open(os.path.join(art, f"{cid}.jpg")).convert("RGB")
    src = src.resize((int(src.width * 480 / src.height), 480))
    views = [Image.open(os.path.join(out, "renders", f"{cid}_{v}.png")).convert("RGB").resize((480, 480)) for v in ("front34", "front", "side", "top")]
    w = src.width + 480 * 4
    row = Image.new("RGB", (w, 520), (18, 20, 26))
    row.paste(src, (0, 40))
    for i, v in enumerate(views):
        row.paste(v, (src.width + 480 * i, 40))
    d = ImageDraw.Draw(row)
    d.text((10, 12), f"{st['title']} ({st['faction']})  |  DT source art  |  front 3/4, front, side, top on a 2.0-unit hex tile  |  "
                     f"{st['tris']} tris, {st['height']} tall, {max(st['size_x'], st['size_y'])} across", fill=(235, 235, 235))
    row.save(os.path.join(out, f"{cid}_sheet.jpg"), quality=90)
    rows.append(row)
W = max(r.width for r in rows)
sheet = Image.new("RGB", (W, sum(r.height for r in rows)), (18, 20, 26))
y = 0
for r in rows:
    sheet.paste(r, (0, y)); y += r.height
sheet.save(os.path.join(out, "structure_pilots_review_sheet.jpg"), quality=90)
