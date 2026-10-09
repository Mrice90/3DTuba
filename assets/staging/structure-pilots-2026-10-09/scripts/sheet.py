"""Review sheets. Usage: python3 -I sheet.py OUT_DIR ART_ROOT
ART_ROOT holds desolate-tuba-acda76f/ and alpha-0.7.15/ (DT art is preferred when both exist).
Writes <card>_sheet.jpg (art + four views) and one contact sheet per faction (art above front 3/4)."""
import json, os, sys
from PIL import Image, ImageDraw
out, root = sys.argv[1], sys.argv[2]
stats = json.load(open(os.path.join(out, "stats.json")))

def art(cid):
    for sub in ("desolate-tuba-acda76f", "alpha-0.7.15"):
        p = os.path.join(root, sub, "structures", f"{cid}.jpg")
        if os.path.exists(p):
            return Image.open(p).convert("RGB"), sub
    raise FileNotFoundError(cid)

for cid, st in stats.items():
    src, sub = art(cid)
    src = src.resize((int(src.width * 480 / src.height), 480))
    views = [Image.open(os.path.join(out, "renders", f"{cid}_{v}.png")).convert("RGB").resize((480, 480)) for v in ("front34", "front", "side", "top")]
    row = Image.new("RGB", (src.width + 480 * 4, 520), (18, 20, 26))
    row.paste(src, (0, 40))
    for i, v in enumerate(views):
        row.paste(v, (src.width + 480 * i, 40))
    ImageDraw.Draw(row).text((10, 12), f"{st['title']} ({st['faction']})  |  {sub} art  |  front 3/4, front, side, top on a 2.0-unit hex tile  |  "
                                       f"{st['tris']} tris, {st['height']} tall, {max(st['size_x'], st['size_y'])} across", fill=(235, 235, 235))
    row.save(os.path.join(out, f"{cid}_sheet.jpg"), quality=90)

W, COLS = 360, 6
for faction in sorted({st["faction"] for st in stats.values()}):
    ids = [c for c, st in stats.items() if st["faction"] == faction]
    rows = (len(ids) + COLS - 1) // COLS
    cell_h = 24 + W * 2 // 3 + W
    sheet = Image.new("RGB", (W * COLS, cell_h * rows), (18, 20, 26))
    d = ImageDraw.Draw(sheet)
    for i, cid in enumerate(ids):
        x, y = (i % COLS) * W, (i // COLS) * cell_h
        a, _ = art(cid)
        sheet.paste(a.resize((W, W * 2 // 3)), (x, y + 24))
        sheet.paste(Image.open(os.path.join(out, "renders", f"{cid}_front34.png")).convert("RGB").resize((W, W)), (x, y + 24 + W * 2 // 3))
        d.text((x + 6, y + 6), stats[cid]["title"], fill=(235, 235, 235))
    sheet.save(os.path.join(out, f"structures_{faction.lower()}_sheet.jpg"), quality=86)
