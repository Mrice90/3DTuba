#!/usr/bin/env python3
"""Build the complete needed-SFX list for Infinite Conquest 3D.

Inputs (pinned, in-repo):
  docs/muse/sprint-02/presentation/presentation-manifest.json  -> card cue keys
  docs/production/audio/card_voices.json                       -> per-card sound identity
Output:
  docs/production/audio/SFX_NEEDS.json  (one entry per cue: key, scope, prompt, target seconds)

Card cues follow the manifest key format <card_id>_<cue> (AI-077 cue set).
Core cues cover board, turn flow and UI events from board-events.md that no card owns.
"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
MANIFEST = os.path.join(ROOT, "docs/muse/sprint-02/presentation/presentation-manifest.json")
VOICES = os.path.join(ROOT, "docs/production/audio/card_voices.json")
OUT = os.path.join(ROOT, "docs/production/audio/SFX_NEEDS.json")

TAIL = "sci-fi game sound effect, no music, no voice"

FACTION = {
    "ZEUS": "high-voltage electric crackle",
    "POSEIDON": "deep water pressure",
}

# (prefix, seconds) per cue and card type. {v} = card voice, {f} = faction texture.
CUE = {
    "CHARACTER": {
        "deploy": ("unit materializing onto the battlefield with a landing impact; {v}", 1.0),
        "move": ("unit moving a short distance; {v}", 0.8),
        "attack_melee": ("melee energy weapon strike with impact; {v}", 0.8),
        "attack_ranged": ("firing an energy weapon shot, laser blast; {v}", 0.8),
        "hit": ("unit taking a hit, armor impact and damage sparks; {v}", 0.5),
        "destroy": ("unit destroyed, breaking apart and powering down; {v}", 1.2),
        "ability": ("unit activating a special power, rising charge; {v}", 1.0),
        "idle": ("unit idle, quiet subtle ambient hum, soft; {v}", 1.5),
        "signature": ("legendary hero entrance, epic powerful surge and huge impact; {v}, {f}", 2.5),
    },
    "LAND": {
        "deploy": ("hex terrain tile rising into place, {v}", 1.0),
        "ability": ("terrain power activating, {v}, energy pulse", 1.0),
        "destroy": ("terrain tile collapsing and crumbling, {v}", 1.2),
        "idle": ("{v}, quiet ambient texture, soft and short", 1.5),
        "signature": ("legendary terrain awakening, {v}, epic deep swell", 2.5),
    },
    "STRUCTURE": {
        "deploy": ("mechanical structure unfolding and locking into place, {v}", 1.2),
        "ability": ("structure activating, {v}, power surge", 1.0),
        "destroy": ("structure exploding and collapsing, metal debris, {v}", 1.5),
        "idle": ("{v}, quiet machine ambience, soft and short", 1.5),
        "signature": ("legendary structure powering up, {v}, epic resonance", 2.5),
    },
    "SPELL": {
        "deploy": ("spell cast, {v}, quick magical energy release", 1.2),
        "signature": ("legendary spell resolving, {v}, massive epic impact", 2.5),
    },
    "CAPITAL": {
        "deploy": ("capital fortress rising from the ground, {v}, massive", 2.0),
        "ability": ("capital fortress activating its power, {v}", 1.2),
        "hit": ("capital fortress struck, heavy impact on armored walls, {v}", 0.8),
        "destroy": ("capital fortress collapsing in a huge explosion, {v}", 2.5),
        "idle": ("{v}, low ambient fortress hum, soft and short", 2.0),
        "signature": ("capital fortress full power reveal, {v}, epic triumphant surge", 3.0),
    },
}

CORE = [
    ("core_match_start", "Futuristic match start, power-up surge and deep booming energy hit", 2.0),
    ("core_board_build", "Holographic hex game board assembling, cascading tile clicks and energy shimmer", 2.0),
    ("core_hex_tile_place", "Holographic hex tile materializing on a metal game board, rising shimmer, soft magnetic clunk", 0.8),
    ("core_hex_hover", "Tiny soft holographic UI tick, subtle and clean", 0.2),
    ("core_hex_select", "Crisp sci-fi UI selection click with short bright tone", 0.3),
    ("core_legal_move_show", "Soft energy grid lighting up, gentle rising digital shimmer", 0.6),
    ("core_invalid_action", "Short low digital error buzz, sci-fi UI denied", 0.4),
    ("core_turn_start_player", "Your turn begins, bright futuristic chime with rising energy swell", 1.2),
    ("core_turn_start_opponent", "Opponent turn begins, darker low futuristic chime, ominous", 1.2),
    ("core_end_turn_button", "Heavy sci-fi button press, mechanical clunk with energy release", 0.6),
    ("core_turn_timer_warning", "Futuristic warning pulse ticking, urgent short beeps", 1.5),
    ("core_card_draw", "Holographic card drawn from a deck, quick digital swipe and shimmer", 0.6),
    ("core_card_hover", "Soft holographic card lift, gentle whoosh", 0.3),
    ("core_card_pickup", "Holographic card picked up, light energy whoosh", 0.4),
    ("core_card_play", "Holographic card played onto the board, energy whoosh into impact", 0.8),
    ("core_card_return_hand", "Holographic card sliding back into a hand, soft reverse whoosh", 0.5),
    ("core_deck_shuffle", "Futuristic holographic deck shuffle, rapid digital flicks", 1.2),
    ("core_tutor_draw", "Special card summoned from the deck, magical digital chime and shimmer", 1.0),
    ("core_resource_gain", "Energy resource gained, bright crystal charge-up ping", 0.6),
    ("core_resource_spend", "Energy resource spent, quick power drain whoosh", 0.5),
    ("core_damage_number", "Short punchy impact tick for a damage number popping up", 0.3),
    ("core_heal", "Healing energy, soft sparkling rising chime", 0.8),
    ("core_buff", "Stat boost power-up, quick rising energy zing", 0.6),
    ("core_debuff", "Weakening effect, falling distorted energy drain", 0.6),
    ("core_shield_block", "Energy shield blocking an attack, deflection zap", 0.5),
    ("core_exhaustion_damage", "Fatigue damage, dull draining thud with static", 0.6),
    ("core_opportunity_attack", "Quick reactive strike, sharp energy slash whoosh", 0.5),
    ("core_capital_reveal", "Hidden fortress revealed, deep dramatic energy boom", 1.5),
    ("core_capital_warning", "Capital under threat alarm, pulsing sci-fi klaxon", 1.5),
    ("core_victory", "Victory sting, triumphant futuristic energy swell and thunder, no music", 3.0),
    ("core_defeat", "Defeat sting, power failing, descending drone and shutdown", 3.0),
    ("core_ui_click", "Clean sci-fi menu button click", 0.2),
    ("core_ui_back", "Sci-fi menu back button, soft lower click", 0.2),
    ("core_ui_open_panel", "Holographic panel opening, quick digital slide", 0.4),
    ("core_ui_close_panel", "Holographic panel closing, quick digital slide down", 0.4),
    ("core_ui_notification", "Futuristic notification ping, two soft tones", 0.5),
    ("core_matchmaking_found", "Match found, energetic sci-fi alert chime", 1.0),
    ("core_ambience_zeus_board", "Ambient sky city above the clouds, wind, distant thunder, electric hum", 6.0),
    ("core_ambience_poseidon_board", "Ambient deep sea city, muffled water, distant whale calls, sonar pings", 6.0),
]


def main():
    cards = json.load(open(MANIFEST))["cards"]
    voices = json.load(open(VOICES))
    needs = []
    for key, prompt, secs in CORE:
        needs.append({"key": key, "scope": "core", "prompt": f"{prompt}, {TAIL}", "seconds": secs})
    for cid, card in cards.items():
        voice = voices[cid]
        table = CUE[card["type"]]
        for sfx in sorted(card["sfx"], key=lambda s: s["key"]):
            cue = sfx["key"][len(cid) + 1:]
            spec = cue
            if card["type"] == "CHARACTER" and cue == "attack":
                spec = "attack_" + voice.get("attack", "melee")
            text, secs = table[spec]
            prompt = text.format(v=voice["voice"], f=FACTION[card["faction"]])
            needs.append({
                "key": sfx["key"], "scope": "card", "card_id": cid, "card": card["name"],
                "faction": card["faction"], "type": card["type"], "cue": cue,
                "prompt": f"{prompt}, {TAIL}", "seconds": secs,
            })
    json.dump({"generated_from": ["presentation-manifest.json", "card_voices.json"],
               "count": len(needs), "needs": needs}, open(OUT, "w"), indent=1)
    print(f"{len(needs)} cues ({len(CORE)} core, {len(needs) - len(CORE)} card) -> {OUT}")


if __name__ == "__main__":
    main()
