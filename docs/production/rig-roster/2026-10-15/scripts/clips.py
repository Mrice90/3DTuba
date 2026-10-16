# AI-060-RIG-COMBAT clip library. Every clip is ADDITIVE on the model's own bind pose: each bone gets
# rotations about axes of the character's facing frame (X = character's left, Y = backward, Z = up), so a
# posed Meshy statue keeps its card-art silhouette and gains motion on top of it.
# Sign conventions (right-hand rule, facing frame):
#   +X on Spine/Head  = pitch forward          -X on Arm/UpLeg = swing forward      +X on ForeArm/Leg = bend
#   +Z on Spine       = turn to own left       Arm raise: Left -Y, Right +Y
# Each clip returns {bone: [(axis, deg), ...], "_hips": (dx, dy, dz) in units of H}.
import math

FPS = 24
def ss(x):  # smoothstep 0..1
    x = min(1.0, max(0.0, x)); return x * x * (3 - 2 * x)
def seg(t, a, b): return ss((t - a) / (b - a)) if b > a else float(t >= b)
def bump(t, a, peak, b):  # 0 at a, 1 at peak, 0 at b
    return seg(t, a, peak) if t <= peak else 1 - seg(t, peak, b)
def env(t, keys):  # piecewise smooth through (frame, value) keys
    if t <= keys[0][0]: return keys[0][1]
    for (f0, v0), (f1, v1) in zip(keys, keys[1:]):
        if t <= f1: return v0 + (v1 - v0) * ss((t - f0) / (f1 - f0))
    return keys[-1][1]

def side(w): return ("Right", "Left") if w == "Right" else ("Left", "Right")
def raise_ax(s): return -1 if s == "Left" else 1  # sign on Y that raises the arm sideways
def add(P, bone, axis, deg):
    if abs(deg) > 1e-6: P.setdefault(bone, []).append((axis, deg))

# ---------------------------------------------------------------- loops
def idle(t, n, cfg):
    P = {}; w = 2 * math.pi * t / n; amp = cfg.get("idle_amp", 1.0)
    br = math.sin(w)  # one breath per cycle
    add(P, "Spine1", "X", -1.2 * br * amp); add(P, "Spine2", "X", -1.6 * br * amp)
    for s in ("Left", "Right"):
        add(P, s + "Shoulder", "Y", raise_ax(s) * 1.5 * br * amp)
        add(P, s + "Arm", "X", 2.0 * math.sin(w + (0.6 if s == "Left" else 0.0)) * amp)
        add(P, s + "ForeArm", "X", 1.5 * math.sin(w + 1.0) * amp)
    add(P, "Head", "Z", 4.0 * math.sin(w * 1 + 0.8) * amp); add(P, "Head", "X", 1.5 * math.sin(2 * w) * amp)
    add(P, "Spine", "Z", 1.5 * math.sin(w + 0.3) * amp)
    if cfg.get("loco") == "hover":
        return P, (0, 0, 0.012 * math.sin(w) * amp)
    for s in ("Left", "Right"):
        add(P, s + "UpLeg", "X", -1.5 * (1 - math.cos(w)) * amp); add(P, s + "Leg", "X", 3.0 * (1 - math.cos(w)) * amp)
    return P, (0, 0, 0)

def walk(t, n, cfg):
    P = {}; w = 2 * math.pi * t / n
    if cfg.get("loco") == "hover":  # gliding drift for leaping / floating / diving poses
        add(P, "Spine", "X", 4 + 2 * math.sin(w)); add(P, "Spine2", "X", 2 * math.sin(w + 0.7))
        for s, ph in (("Left", 0), ("Right", math.pi)):
            add(P, s + "UpLeg", "X", 8 * math.sin(w + ph)); add(P, s + "Leg", "X", 6 * (1 + math.sin(w + ph + 1)))
            add(P, s + "Arm", "X", 6 * math.sin(w + ph + math.pi)); add(P, s + "Arm", "Y", raise_ax(s) * 5 * (1 + math.sin(w)))
        add(P, "Head", "X", -3 * math.sin(w))
        return P, (0, 0, 0.03 * math.sin(w))
    lean = cfg.get("walk_lean", 4)
    add(P, "Spine", "X", lean)
    for s, ph in (("Left", 0), ("Right", math.pi)):
        sw = math.sin(w + ph)
        add(P, s + "UpLeg", "X", -22 * sw)
        add(P, s + "Leg", "X", 6 + 34 * max(0.0, math.sin(w + ph + 1.9)) ** 1.5)
        add(P, s + "Foot", "X", -10 * sw)
        add(P, s + "Arm", "X", 14 * sw * cfg.get("arm_swing", 1.0))
        add(P, s + "ForeArm", "X", 8 + 8 * max(0.0, -sw))
    add(P, "Spine1", "Z", 5 * math.sin(w)); add(P, "Hips", "Z", -6 * math.sin(w))
    add(P, "Head", "Z", -3 * math.sin(w))
    return P, (0, 0, 0.012 * math.cos(2 * w))

# ---------------------------------------------------------------- attacks (impact frame recorded per style)
def attack(t, n, cfg):
    P, h = attack_raw(t, n, cfg); k = cfg.get("attack_arm", 1.0); g = cfg.get("lunge", 1.0)
    if k != 1.0:
        P = {b: ([(a, d * k) for a, d in v] if "Arm" in b else v) for b, v in P.items()}
    return P, tuple(x * g for x in h)

def attack_raw(t, n, cfg):
    st = cfg.get("attack", "thrust"); P = {}; W, O = side(cfg.get("weapon_hand", "Right"))
    sw = 1 if W == "Left" else -1  # +Z turns to own left; winding the right side back = turn left
    if st == "thrust":  # spear / trident / lance: wind back, lunge, recover. impact 14
        wind = bump(t, 0, 9, 14); hit = env(t, [(9, 0), (14, 1), (19, 1), (32, 0)])
        add(P, "Spine", "Z", sw * (-18 * wind + 14 * hit)); add(P, "Spine", "X", -6 * wind + 14 * hit)
        add(P, "Spine2", "X", 6 * hit)
        add(P, W + "Arm", "X", 35 * wind - 45 * hit); add(P, W + "ForeArm", "X", 50 * wind - 10 * hit)
        add(P, O + "Arm", "X", -15 * wind + 15 * hit)
        add(P, W + "UpLeg", "X", 10 * wind - 18 * hit); add(P, O + "UpLeg", "X", 14 * hit); add(P, O + "Leg", "X", 22 * hit)
        add(P, "Head", "X", -6 * hit)
        return P, (0, 0.03 * wind - 0.07 * hit, 0)
    if st == "slash":  # blade / fin / claw: raise, diagonal cut across. impact 13
        wind = bump(t, 0, 9, 13); hit = env(t, [(9, 0), (13, 1), (18, 1), (32, 0)])
        add(P, W + "Arm", "Y", raise_ax(W) * (70 * wind - 20 * hit)); add(P, W + "Arm", "X", 20 * wind - 55 * hit)
        add(P, W + "ForeArm", "X", 45 * wind)
        add(P, "Spine", "Z", sw * (-20 * wind + 28 * hit)); add(P, "Spine", "X", -8 * wind + 16 * hit)
        add(P, O + "Arm", "Y", raise_ax(O) * 20 * hit)
        add(P, O + "UpLeg", "X", 10 * hit); add(P, O + "Leg", "X", 18 * hit)
        return P, (0, 0.02 * wind - 0.05 * hit, 0)
    if st == "ranged":  # laser / harpoon / pulse weapon: shoulder, fire, recoil. impact (shot) 12
        aim = env(t, [(0, 0), (8, 1), (22, 1), (32, 0)]); kick = bump(t, 11, 13, 22)
        add(P, W + "Arm", "X", -40 * aim); add(P, W + "Arm", "Y", raise_ax(W) * 10 * aim); add(P, W + "ForeArm", "X", -15 * aim + 20 * kick)
        add(P, O + "Arm", "X", -25 * aim); add(P, O + "ForeArm", "X", 25 * aim)
        add(P, "Spine", "Z", sw * 10 * aim); add(P, "Spine2", "X", 4 * aim - 12 * kick)
        add(P, W + "Arm", "X", -18 * kick); add(P, "Head", "X", 5 * aim - 6 * kick)
        return P, (0, 0.03 * kick, 0)
    if st == "cast":  # oracle / mage / signal caster: gather, release with both hands. impact 15
        gather = bump(t, 0, 10, 15); rel = env(t, [(10, 0), (15, 1), (22, 1), (34, 0)])
        for s in ("Left", "Right"):
            add(P, s + "Arm", "X", 25 * gather - 60 * rel); add(P, s + "Arm", "Y", raise_ax(s) * (30 * gather - 5 * rel))
            add(P, s + "ForeArm", "X", 60 * gather - 10 * rel)
        add(P, "Spine2", "X", -10 * gather + 10 * rel); add(P, "Spine", "X", -4 * gather + 8 * rel); add(P, "Head", "X", -8 * gather + 4 * rel)
        return P, (0, 0.02 * gather - 0.04 * rel, 0.01 * gather)
    if st == "bash":  # shield wall: drive the shield arm and body forward. impact 12
        wind = bump(t, 0, 7, 12); hit = env(t, [(7, 0), (12, 1), (17, 1), (30, 0)])
        S_ = cfg.get("shield_hand", O)
        add(P, S_ + "Arm", "X", 15 * wind - 35 * hit); add(P, S_ + "ForeArm", "X", 20 * wind - 15 * hit)
        add(P, "Spine", "X", -8 * wind + 16 * hit); add(P, "Spine", "Z", (1 if S_ == "Right" else -1) * (12 * wind - 12 * hit))
        add(P, "LeftUpLeg", "X", -10 * hit); add(P, "RightUpLeg", "X", 10 * hit); add(P, "LeftLeg", "X", 15 * hit); add(P, "RightLeg", "X", 15 * hit)
        return P, (0, 0.03 * wind - 0.08 * hit, -0.01 * hit)
    raise ValueError(st)
IMPACT = {"thrust": 14, "slash": 13, "ranged": 12, "cast": 15, "bash": 12}
ATTACK_LEN = {"thrust": 33, "slash": 33, "ranged": 33, "cast": 35, "bash": 31}

def hit(t, n, cfg):
    P = {}; k = env(t, [(0, 0), (3, 1), (6, 0.8), (16, 0)]); sh = math.sin(t * 2.1) * bump(t, 0, 3, 10)
    add(P, "Spine", "X", -10 * k); add(P, "Spine2", "X", -8 * k); add(P, "Head", "X", -12 * k + 4 * sh)
    add(P, "Spine", "Z", 5 * sh)
    for s in ("Left", "Right"):
        add(P, s + "Arm", "Y", raise_ax(s) * 14 * k); add(P, s + "Arm", "X", 10 * k); add(P, s + "ForeArm", "X", 15 * k)
        add(P, s + "Leg", "X", 12 * k); add(P, s + "UpLeg", "X", -6 * k)
    return P, (0, 0.04 * k, 0)

def death(t, n, cfg):
    P = {}; rec = bump(t, 0, 4, 10); buck = env(t, [(6, 0), (18, 1)]); fall = env(t, [(16, 0), (32, 1)])
    bounce = bump(t, 32, 35, 40) * 0.06
    add(P, "Spine", "X", -10 * rec + 25 * buck - 15 * fall); add(P, "Spine2", "X", 12 * buck - 10 * fall)
    add(P, "Head", "X", -15 * rec + 20 * buck - 30 * fall)
    for s in ("Left", "Right"):
        k = cfg.get("crouch", 1.0)
        add(P, s + "UpLeg", "X", (-40 * buck + 20 * fall) * k); add(P, s + "Leg", "X", (70 * buck - 30 * fall) * k)
        add(P, s + "Arm", "Y", raise_ax(s) * (20 * rec - 10 * buck + 40 * fall)); add(P, s + "Arm", "X", 20 * buck)
        add(P, s + "ForeArm", "X", 30 * buck - 20 * fall)
    # whole-body topple backward with a twist; the rig pivots it about the heels and re-grounds every frame
    P["_root"] = [("X", -(78 + 6 * math.sin(bounce * 20)) * fall), ("Z", 18 * fall)]
    return P, (0, 0, 0)

def summon(t, n, cfg):
    P = {}; c = env(t, [(0, 1), (4, 1), (15, 0)]); flare = bump(t, 9, 15, 30); k = cfg.get("crouch", 1.0)
    add(P, "Spine", "X", 30 * c - 8 * flare); add(P, "Spine2", "X", 15 * c - 8 * flare); add(P, "Head", "X", 20 * c - 10 * flare)
    for s in ("Left", "Right"):
        add(P, s + "UpLeg", "X", -55 * c * k); add(P, s + "Leg", "X", 85 * c * k); add(P, s + "Foot", "X", -30 * c * k)
        add(P, s + "Arm", "Y", raise_ax(s) * (-10 * c + 45 * flare)); add(P, s + "Arm", "X", -20 * c - 10 * flare)
        add(P, s + "ForeArm", "X", 50 * c)
    if cfg.get("loco") == "hover":
        return P, (0, 0, -0.12 * c + 0.03 * flare)
    return P, (0, 0, 0)

def clip_set(cfg):
    a = cfg.get("attack", "thrust")
    return [  # name, func, frames, loop, ground-every-frame, events
        ("Idle", idle, 48, True, cfg.get("loco") != "hover", {}),
        ("Walk", walk, 24, True, cfg.get("loco") != "hover", {}),
        ("Attack", attack, ATTACK_LEN[a], False, cfg.get("loco") != "hover", {"impact": IMPACT[a]}),
        ("Hit", hit, 18, False, cfg.get("loco") != "hover", {}),
        ("Death", death, 44, False, True, {"down": 32}),
        ("Summon", summon, 31, False, cfg.get("loco") != "hover", {"land": 15}),
    ]

# ---------------------------------------------------------------- creatures (kind = "creature")
# Bones: Hips (body), Spine1..n toward the head (last one is the head), Tail1..n, and limbs "<Name>1".."<Name>k"
# with cfg["limbs"][i] = {"name", "role": "leg"|"claw"|"fin", "phase": 0..1, "side": "L"|"R"}.
def _chain(cfg, key): return [f"{key}{i+1}" for i in range(max(0, len(cfg.get(key.lower(), [])) - 1))]
def _limbs(cfg): return cfg.get("limbs", [])
def c_idle(t, n, cfg):
    P = {}; w = 2 * math.pi * t / n; sp = _chain(cfg, "Spine"); tl = _chain(cfg, "Tail")
    for i, b in enumerate(sp): add(P, b, "X", -1.5 * math.sin(w) / max(1, len(sp))); add(P, b, "Z", 3.0 * math.sin(w + 0.6 * i) / max(1, len(sp)))
    for i, b in enumerate(tl): add(P, b, "Z", 6.0 * math.sin(w - 0.8 * i)); add(P, b, "X", 2.0 * math.sin(w - 0.8 * i + 1))
    for L in _limbs(cfg):
        add(P, L["name"] + "1", "X", 2.0 * math.sin(w + 6.28 * L.get("phase", 0)))
        if L.get("role") == "claw": add(P, L["name"] + "2", "X", 4.0 * math.sin(2 * w))
    return P, (0, 0, 0.006 * math.sin(w) * cfg.get("bob", 1.0))
def c_walk(t, n, cfg):
    P = {}; w = 2 * math.pi * t / n; sp = _chain(cfg, "Spine"); tl = _chain(cfg, "Tail")
    for i, b in enumerate(sp): add(P, b, "Z", 4.0 * math.sin(w + 0.5 * i) / max(1, len(sp)))
    for i, b in enumerate(tl): add(P, b, "Z", 10.0 * math.sin(w - 0.9 * i))
    for L in _limbs(cfg):
        ph = w + 2 * math.pi * L.get("phase", 0); k = cfg.get("stride", 1.0)
        if L.get("role", "leg") == "leg":
            add(P, L["name"] + "1", "X", -16 * k * math.sin(ph)); add(P, L["name"] + "2", "X", 18 * k * max(0.0, math.sin(ph + 1.6)))
        else: add(P, L["name"] + "1", "X", 6 * math.sin(ph))
    hov = cfg.get("loco") == "hover"
    return P, (0, 0, (0.03 * math.sin(w) if hov else 0.012 * abs(math.sin(w))) * cfg.get("bob", 1.0))
def c_attack(t, n, cfg):
    P = {}; sp = _chain(cfg, "Spine"); tl = _chain(cfg, "Tail"); ns = max(1, len(sp))
    wind = bump(t, 0, 8, 12); hit = env(t, [(8, 0), (12, 1), (17, 1), (30, 0)])
    for b in sp: add(P, b, "X", (-14 * wind + 20 * hit) / ns)
    for b in tl: add(P, b, "X", 12 * wind - 8 * hit)
    for L in _limbs(cfg):
        if L.get("role") == "claw":
            add(P, L["name"] + "1", "X", 25 * wind - 45 * hit); add(P, L["name"] + "2", "X", -30 * wind + 25 * hit)
        elif L.get("role", "leg") == "leg": add(P, L["name"] + "2", "X", 10 * wind)
    return P, (0, 0.05 * wind - 0.12 * hit * cfg.get("lunge", 1.0), 0.02 * wind)
def c_hit(t, n, cfg):
    P = {}; sp = _chain(cfg, "Spine"); tl = _chain(cfg, "Tail"); k = env(t, [(0, 0), (3, 1), (6, 0.8), (16, 0)])
    sh = math.sin(t * 2.1) * bump(t, 0, 3, 10)
    for b in sp: add(P, b, "X", -10 * k / max(1, len(sp))); add(P, b, "Z", 6 * sh / max(1, len(sp)))
    for b in tl: add(P, b, "Z", -8 * sh)
    for L in _limbs(cfg): add(P, L["name"] + "2", "X", 12 * k)
    return P, (0, 0.05 * k, 0)
def c_death(t, n, cfg):
    P = {}; sp = _chain(cfg, "Spine"); tl = _chain(cfg, "Tail"); buck = env(t, [(2, 0), (14, 1)]); fall = env(t, [(12, 0), (30, 1)])
    for b in sp: add(P, b, "X", 25 * buck / max(1, len(sp)))
    for b in tl: add(P, b, "Z", 15 * fall); add(P, b, "X", 8 * buck)
    for L in _limbs(cfg):
        sgn = 1 if L.get("side", "L") == "L" else -1
        add(P, L["name"] + "1", "Y", -sgn * 35 * buck); add(P, L["name"] + "2", "X", 40 * buck)
    roll = cfg.get("death_roll", 70)
    P["_root"] = [("Y", -roll * fall)]
    return P, (0, 0, 0)
def c_summon(t, n, cfg):
    P = {}; sp = _chain(cfg, "Spine"); tl = _chain(cfg, "Tail"); c = env(t, [(0, 1), (4, 1), (16, 0)]); flare = bump(t, 10, 16, 30)
    for b in sp: add(P, b, "X", (25 * c - 15 * flare) / max(1, len(sp)))
    for b in tl: add(P, b, "X", 20 * c); add(P, b, "Z", 10 * flare)
    for L in _limbs(cfg):
        if L.get("role") == "claw": add(P, L["name"] + "1", "X", 20 * c - 35 * flare)
        else: add(P, L["name"] + "2", "X", 35 * c)
    return P, (0, 0, -0.06 * c * cfg.get("bob", 1.0))
def creature_clip_set(cfg):
    g = cfg.get("loco") != "hover"
    return [("Idle", c_idle, 48, True, g, {}), ("Walk", c_walk, 24, True, g, {}),
            ("Attack", c_attack, 31, False, g, {"impact": 12}), ("Hit", c_hit, 18, False, g, {}),
            ("Death", c_death, 40, False, True, {"down": 30}), ("Summon", c_summon, 31, False, g, {"land": 16})]
