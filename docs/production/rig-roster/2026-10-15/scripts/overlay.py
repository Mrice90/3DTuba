# Draw a config's joints and prop capsules on the clean probe renders (front xz, side yz). No Blender needed.
import sys, json
from PIL import Image, ImageDraw
probe, cfgp, out = sys.argv[1], sys.argv[2], sys.argv[3]
pj = json.load(open(probe + "_probe.json")); cfg = json.load(open(cfgp)); H = pj["H"]
S = 1.25 * max(1.0, pj["dims"][0] / H, pj["dims"][1] / H)
J = cfg["joints"]
links = [("Hips","Chest"),("Chest","Neck"),("Neck","HeadTop")]
for s in ("Left","Right"):
    links += [("Chest",s+"Arm"),(s+"Arm",s+"ForeArm"),(s+"ForeArm",s+"Hand"),(s+"Hand",s+"HandEnd"),("Hips",s+"UpLeg"),(s+"UpLeg",s+"Leg"),(s+"Leg",s+"Foot"),(s+"Foot",s+"Toe")]
tiles = []
for view, ax in (("front", 0), ("side", 1)):
    im = Image.open(f"{probe}_{view}_clean.png").convert("RGB"); R = im.size[0]; d = ImageDraw.Draw(im)
    def px(u, v): return (R/2 + u / S * R, R/2 - (v - 0.5) / S * R)
    def P(p): return px(p[ax] if len(p) == 3 else (p[0] if ax == 0 else None), p[2] if len(p) == 3 else p[1])
    for a, b in links:
        if a in J and b in J and (len(J[a]) == 3 or ax == 0) and (len(J[b]) == 3 or ax == 0):
            d.line([P(J[a]), P(J[b])], fill=(255, 60, 60) if "Right" in b else (60, 200, 255) if "Left" in b else (255, 255, 0), width=3)
    for k, p in J.items():
        if len(p) == 3 or ax == 0:
            x, y = P(p); d.ellipse([x-4, y-4, x+4, y+4], outline=(255, 255, 255), width=2)
    for pr in cfg.get("props", []):
        if "box" in pr or "disc_box" in pr:
            pr = dict(pr, box=pr.get("box", pr.get("disc_box")))
            b0, b1 = pr["box"]; x0, y0 = px(b0[ax], b1[2]); x1, y1 = px(b1[ax], b0[2]); d.rectangle([x0, y0, x1, y1], outline=(0, 255, 0), width=2)
        elif ax == 0 or len(pr["a"]) == 3:
            d.line([P(pr["a"]), P(pr["b"])], fill=(0, 255, 0), width=max(2, int(pr["r"] * 2 / S * R)))
    if "base_z" in cfg: y = px(0, cfg["base_z"])[1]; d.line([(0, y), (R, y)], fill=(255, 128, 0), width=2)
    tiles.append(im)
sh = Image.new("RGB", (tiles[0].size[0] * 2, tiles[0].size[1])); [sh.paste(t, (i * t.size[0], 0)) for i, t in enumerate(tiles)]; sh.save(out)
