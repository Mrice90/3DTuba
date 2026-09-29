#!/usr/bin/env python3
"""Build the per-card presentation manifest (AI-064).

Reads the AI-049 asset prompt directory (139 cards) and generates
docs/muse/sprint-02/presentation/presentation-manifest.json:
  every card -> the AI-062 events it can emit, each with an animation clip
  key and a unique SFX key (<card_id>_<cue>), plus model/texture paths.

SFX cue set (ElevenLabs lane): summon, move, attack, hit, death, ability, idle.
Animation keys come from the card's AI-049 animation_events; events without a
matching clip get animation=null (coverage.py reports them as missing — that
is the point: nothing shows what each card still lacks).

Staging layout assumed by coverage.py (relative to the staging root):
  meshy/<card_id>.glb                  model
  meshy/<card_id>_textures/            textures dir
  animations/<card_id>_<slug>.fbx      one per animation key
  sfx/<card_id>_<cue>.wav              one per SFX key
"""
import json
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
ASSET_DIR = os.path.join(HERE, "..", "..", "sprint-01", "asset-prompts",
                         "asset-prompt-directory.json")

# AI-062 event -> (animation key or None, SFX cue), per card type.
EVENT_MAP = {
    "CHARACTER": [
        ("CARD_PLAYED", "deploy_flight(400ms)", "summon"),
        ("CHARACTER_MOVED", "move(320ms)", "move"),
        ("ATTACK_RESOLVED", "melee(360ms)/ranged_projectile", "attack"),
        ("OPPORTUNITY_ATTACK", "melee(360ms)/ranged_projectile", "attack"),
        ("DAMAGE_DEALT", "hit_flash(350ms)", "hit"),
        ("CARD_ABILITY_TRIGGERED", None, "ability"),
        ("CARD_DESTROYED", "destroy(320ms)", "death"),
    ],
    "LAND": [
        ("CARD_PLAYED", "land_pop(120ms)", "summon"),
        ("TERRAIN_TRIGGERED", "activated_ability", "ability"),
        ("CARD_DESTROYED", "destroy(320ms)", "death"),
    ],
    "STRUCTURE": [
        ("CARD_PLAYED", "deploy", "summon"),
        ("DEVELOPMENT_PASSIVE_TRIGGERED", "activated_ability", "ability"),
        ("CARD_ABILITY_TRIGGERED", "activated_ability", "ability"),
        ("CARD_DESTROYED", "destroy(320ms)", "death"),
    ],
    "CAPITAL": [
        ("CARD_PLAYED", "deploy", "summon"),
        ("CAPITAL_PASSIVE_TRIGGERED", None, "ability"),
        ("CAPITAL_HIT", "capital_hit_flash", "hit"),
        ("CARD_DESTROYED", "destroy(320ms)", "death"),
    ],
    "SPELL": [
        ("CARD_PLAYED", "cast", "summon"),
    ],
}

# Non-event SFX cues per type (idle is ambient, not tied to a board event).
IDLE_CUE_TYPES = {"CHARACTER", "STRUCTURE", "LAND", "CAPITAL"}


def slug(anim_key):
    """Sanitize an animation key for a filename."""
    s = re.sub(r"[^a-z0-9]+", "_", anim_key.lower()).strip("_")
    return re.sub(r"_+", "_", s)


def main():
    with open(ASSET_DIR, encoding="utf-8") as f:
        cards = json.load(f)["cards"]

    manifest = {}
    for c in cards:
        cid, ctype = c["id"], c["type"]
        events = {}
        anim_keys = set()
        sfx_keys = set()
        for event, anim, cue in EVENT_MAP[ctype]:
            sfx_key = f"{cid}_{cue}"
            events[event] = {"animation": anim, "sfx": sfx_key}
            if anim:
                anim_keys.add(anim)
            sfx_keys.add(sfx_key)
        if ctype in IDLE_CUE_TYPES:
            sfx_keys.add(f"{cid}_idle")

        manifest[cid] = {
            "name": c["name"],
            "faction": c["faction"],
            "type": ctype,
            "model_path": f"meshy/{cid}.glb",
            "textures_path": f"meshy/{cid}_textures/",
            "events": events,
            "animations": sorted(
                ({"key": a, "file": f"animations/{cid}_{slug(a)}.fbx"}
                 for a in anim_keys),
                key=lambda x: x["key"]),
            "sfx": sorted(
                ({"key": k, "file": f"sfx/{k}.wav"} for k in sfx_keys),
                key=lambda x: x["key"]),
        }

    out = {
        "generated": "2026-09-28",
        "cue_set": ["summon", "move", "attack", "hit", "death", "ability", "idle"],
        "staging_layout": {
            "model": "meshy/<card_id>.glb",
            "textures": "meshy/<card_id>_textures/",
            "animations": "animations/<card_id>_<slug>.fbx",
            "sfx": "sfx/<card_id>_<cue>.wav",
        },
        "cards": manifest,
    }
    path = os.path.join(HERE, "presentation-manifest.json")
    with open(path, "w", encoding="utf-8") as f:
        json.dump(out, f, indent=1)
    n_events = sum(len(m["events"]) for m in manifest.values())
    print(f"manifest: {len(manifest)} cards, {n_events} event mappings -> {path}")


if __name__ == "__main__":
    main()
