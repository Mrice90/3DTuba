import json, glob, os, sys
from PIL import Image, ImageDraw, ImageFont
O = sys.argv[1]
font = ImageFont.load_default(size=16); small = ImageFont.load_default(size=13)
views = ["front34", "side", "detail"]; T = 300
os.makedirs(f"{O}/sheets", exist_ok=True)
rows = []
for f in sorted(glob.glob(f"{O}/models/*/*_report.json")):
    r = json.load(open(f)); n = r["card"]
    W = T * len(views) * 2; H = T + 60
    img = Image.new("RGBA", (W, H), (26, 30, 42, 255)); d = ImageDraw.Draw(img)
    d.text((8, 6), f"{n}   {r['tris_before']:,} -> {r['tris_after']:,} tris   min silhouette IoU {min(r['silhouette_iou'].values()):.3f}", font=font, fill=(255, 220, 120))
    for i, v in enumerate(views):
        for j, tag in enumerate(("before", "after")):
            x = (i * 2 + j) * T
            t = Image.open(f"{O}/renders/{n}_{tag}_{v}.png").convert("RGBA").resize((T, T))
            img.alpha_composite(t, (x, 50))
            d.text((x + 6, 32), f"{v} {tag}", font=small, fill=(200, 210, 230))
    p = f"{O}/sheets/{n}_before_after.png"; img.convert("RGB").save(p); rows.append(p)
# overview: front34 before/after for all 12, 4 per row
cells = []
for p in rows:
    n = os.path.basename(p).replace("_before_after.png", "")
    cells.append((n, f"{O}/renders/{n}_before_front34.png", f"{O}/renders/{n}_after_front34.png"))
C = 4; S = 220; ov = Image.new("RGBA", (C * S * 2, ((len(cells) + C - 1) // C) * (S + 24)), (26, 30, 42, 255)); d = ImageDraw.Draw(ov)
for k, (n, a, b) in enumerate(cells):
    x = (k % C) * S * 2; y = (k // C) * (S + 24)
    ov.alpha_composite(Image.open(a).convert("RGBA").resize((S, S)), (x, y + 22)); ov.alpha_composite(Image.open(b).convert("RGBA").resize((S, S)), (x + S, y + 22))
    d.text((x + 4, y + 4), n.replace("poseidon_", "") + "  (before | after)", font=small, fill=(255, 220, 120))
ov.convert("RGB").save(f"{O}/sheets/overview_before_after.png"); print("ok", len(rows))
