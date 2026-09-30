#!/usr/bin/env python3
"""AI-075: event-to-presentation timeline tool.

Reads an AI-066 JSONL event dump plus the presentation manifest and emits
a per-event cue schedule for the Unity presentation layer (AI-060b/c):
start time, duration, animation clip key, SFX key, and camera/impact hook.

Usage:
    timeline.py <events.jsonl> <presentation-manifest.json> [<out.json>]

Output is a JSON array of cues, one per input event, in order:
    {"seq": 0, "event": "CARD_PLAYED", "turn": 1, "player": 0,
     "card_id": "zeus_...", "instance_id": "...",
     "start_ms": 0, "duration_ms": 400,
     "anim_key": "deploy_flight", "sfx_key": "zeus_..._summon",
     "impact_hook": "none"}

Timing is sequential: each cue starts when the previous one ends.
Durations come from the manifest animation string (e.g. "deploy(400ms)");
events without a manifest entry fall back to DEFAULT_DURATIONS.
"""
import json
import re
import sys
from pathlib import Path

DUR_RE = re.compile(r"\((\d+)ms\)")

# Fallback durations (ms) when the manifest has no entry for an event.
DEFAULT_DURATIONS = {
    "CARD_PLAYED": 800,
    "CHARACTER_MOVED": 600,
    "ATTACK_RESOLVED": 1000,
    "OPPORTUNITY_ATTACK": 800,
    "DAMAGE_DEALT": 400,
    "CARD_DESTROYED": 800,
    "CARD_ABILITY_TRIGGERED": 600,
    "TERRAIN_TRIGGERED": 600,
    "CARD_DRAWN": 300,
    "GP_GAINED": 200,
    "GP_SPENT": 200,
    "TURN_ENDED": 300,
    "PHASE_CHANGED": 300,
    "GAME_OVER": 2000,
}

# Camera/impact hooks for AI-060c. Values are hook names the Unity side
# implements; "none" means no camera effect.
IMPACT_HOOKS = {
    "ATTACK_RESOLVED": "shake_small",
    "OPPORTUNITY_ATTACK": "shake_small",
    "CARD_DESTROYED": "shake_small",
    "DAMAGE_DEALT": "flash",
    "GAME_OVER": "shake_large",
}

DEFAULT_ANIM = {
    "CARD_PLAYED": "deploy",
    "CHARACTER_MOVED": "move",
    "ATTACK_RESOLVED": "attack",
    "OPPORTUNITY_ATTACK": "attack",
    "DAMAGE_DEALT": "hit",
    "CARD_DESTROYED": "destroy",
    "CARD_ABILITY_TRIGGERED": "ability",
    "TERRAIN_TRIGGERED": "ability",
    "CARD_DRAWN": "draw",
    "GAME_OVER": "game_over",
}


def parse_anim(anim_str):
    """Split 'deploy_flight(400ms)' -> ('deploy_flight', 400)."""
    if not anim_str:
        return None, None
    m = DUR_RE.search(anim_str)
    dur = int(m.group(1)) if m else None
    key = DUR_RE.sub("", anim_str).strip()
    # 'melee(360ms)/ranged_projectile' -> take the first variant
    key = key.split("/")[0].strip()
    return key or None, dur


def build_cues(events, manifest):
    cards = manifest.get("cards", {})
    # instance_id -> card_id, learned from CARD_PLAYED events
    inst_to_card = {}
    for e in events:
        if e.get("event") == "CARD_PLAYED" and e.get("instance_id"):
            inst_to_card[e["instance_id"]] = e.get("card_id")

    cues = []
    t = 0
    for e in events:
        ev = e.get("event")
        card_id = e.get("card_id") or inst_to_card.get(e.get("instance_id"))
        anim_key, sfx_key, duration = None, None, None

        if card_id and card_id in cards:
            entry = cards[card_id].get("events", {}).get(ev)
            if entry:
                anim_key, duration = parse_anim(entry.get("animation"))
                sfx_key = entry.get("sfx")

        if anim_key is None:
            anim_key = DEFAULT_ANIM.get(ev)
        if duration is None:
            duration = DEFAULT_DURATIONS.get(ev, 500)

        cues.append({
            "seq": e.get("seq"),
            "event": ev,
            "turn": e.get("turn"),
            "player": e.get("player"),
            "card_id": card_id,
            "instance_id": e.get("instance_id"),
            "start_ms": t,
            "duration_ms": duration,
            "anim_key": anim_key,
            "sfx_key": sfx_key,
            "impact_hook": IMPACT_HOOKS.get(ev, "none"),
        })
        t += duration
    return cues


def main():
    if len(sys.argv) < 3:
        print(__doc__.strip().splitlines()[-8], file=sys.stderr)
        print("usage: timeline.py <events.jsonl> <manifest.json> [<out.json>]",
              file=sys.stderr)
        sys.exit(2)
    events = [json.loads(l) for l in open(sys.argv[1], encoding="utf-8")
              if l.strip()]
    manifest = json.load(open(sys.argv[2], encoding="utf-8"))
    cues = build_cues(events, manifest)
    out = json.dumps(cues, indent=1)
    if len(sys.argv) > 3:
        Path(sys.argv[3]).write_text(out, encoding="utf-8")
    else:
        print(out)


if __name__ == "__main__":
    main()
