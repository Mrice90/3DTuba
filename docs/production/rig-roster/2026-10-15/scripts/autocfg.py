# Convert a fit.py A-pose joint fit into a rig_posed cfg (probe frame, units of H). Usage: autocfg.py <card> <fitjson> <probejson> <out cfg> [attack] [weapon_hand]
import json, sys
card, fj, pj, out = sys.argv[1:5]; attack = sys.argv[5] if len(sys.argv) > 5 else "slash"; hand = sys.argv[6] if len(sys.argv) > 6 else "Right"
f = json.load(open(fj)); p = json.load(open(pj)); H = f["H"]; xc = f["xc"]; off = p["off"]
def conv(v): return [round((v[0] + xc + off[0]) / H, 4), round((v[1] + off[1]) / H, 4), round(v[2] / H, 4)]
J = f["joints"]; m = {"Hips": "Hips", "Chest": "Spine2", "Neck": "Neck", "HeadTop": "HeadTop_End"}
for s in ("Left", "Right"):
    for n in ("Arm", "ForeArm", "Hand", "HandEnd", "UpLeg", "Leg", "Foot"): m[s + n] = s + n
    m[s + "Toe"] = s + "Toe_End"
cfg = {"attack": attack, "weapon_hand": hand, "facing": 0, "joints": {k: conv(J[v]) for k, v in m.items()}}
json.dump(cfg, open(out, "w"), indent=1); print(card, "H fit/probe", round(H, 4), round(p.get("H", 0), 4))
