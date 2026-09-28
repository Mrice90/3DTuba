#!/usr/bin/env python3
"""Build the Infinite Conquest asset prompt directory (AI-049).

Reads the pinned card manifest + alpha card JSONs (read-only inputs) and
generates:
  - asset-prompt-directory.json : machine-readable per-card asset specs
  - asset-prompt-directory.md   : human-readable directory + SFX group table

Scope v1: 119 ZEUS + POSEIDON alpha-json cards + 20 runtime tutors = 139 cards.
Excluded: 19 DEMO + 5 UNASSIGNED prototype cards (dev-only, not shippable).

SFX policy (per Product Owner 2026-09-27): sound effects grouped by faction +
archetype (fallback: faction + type); fully unique SFX only for rarity-4
cards (20 apex-file + 6 capitals + 6 other rarity-4).

Inputs (VM-local, read-only):
  manifest : /home/hatch/workspace/work-3dtuba/docs/muse/sprint-01/manifest.csv
  card JSON: /home/hatch/workspace/scratch/tuba-alpha/game-core/src/main/resources/cards/*.json
             (TubaExperiment @ 992bc95, read-only clone)

Usage: python3 build_asset_directory.py  (writes JSON + Markdown next to itself)
"""
import csv, json, glob, os, collections

HERE = os.path.dirname(os.path.abspath(__file__))
MANIFEST = "/home/hatch/workspace/work-3dtuba/docs/muse/sprint-01/manifest.csv"
CARD_JSON_GLOB = "/home/hatch/workspace/scratch/tuba-alpha/game-core/src/main/resources/cards/*.json"

FACTION_STYLE = {
    "ZEUS": ("Olympian storm aesthetic — dark marble and bronze, crackling gold-white "
             "lightning, storm-cloud drapery, celestial motifs; regal, severe, sky-wrathful."),
    "POSEIDON": ("Abyssal Atlantean aesthetic — bioluminescent teal glow, dark bronze and "
                 "coral growth, deep-sea pressure darkness, leviathan and tide motifs; "
                 "ancient, crushing, relentless."),
}

ARCHETYPE_FLAVOR = {
    "HUMAN": "mortal soldier of the gods, disciplined and armored",
    "STORM": "wreathed in living lightning, crackling with sky-wrath",
    "COAST": "shoreline guardian of salt-worn stone and wave-polished bronze",
    "MERFOLK": "sleek amphibious warrior, finned and swift",
    "FORTRESS": "living bastion of immovable stone-and-metal bulk",
    "BEAST": "muscular mythic beast, primal and powerful",
    "TIDAL": "embodiment of the tide itself, surging and relentless",
    "HIGHLAND": "mountain-born, wind-carved and lofty",
    "MEDICAL": "battlefield medic, calm amid chaos, soft restorative glow",
    "NYMPH": "ethereal nature spirit, graceful and luminous",
    "INDUSTRIAL": "forge-built construct, riveted plates and smoke",
    "AUTOMATON": "clockwork divine machine, precise and inexorable",
}

TYPE_FRAMING = {
    "CHARACTER": ("Single character miniature, rig-ready A-pose, clean limb separation for "
                  "animation. Imply the full animation set in the sculpt: idle stance, "
                  "attack windup, hit reaction, death collapse."),
    "STRUCTURE": ("Fortified building miniature with a strong, instantly readable silhouette. "
                  "Sculpt implies construction staging: foundation, raising, complete."),
    "LAND": ("Hexagonal terrain tile; top face fully detailed, edges designed to blend "
             "seamlessly with neighboring tiles (no hard borders). Subtle animated accents "
             "baked into the design: energy arcs / lapping water / drifting spores."),
    "SPELL": ("Pure VFX presentation, NO character: capture the cast-gesture moment and the "
              "impact frame as a two-beat effect (windup -> detonation). Design as a "
              "loopable games-asset effect with a clear focal point."),
    "CAPITAL": ("Monumental faction headquarters — the largest, most detailed silhouette on "
                "the board. Layered architecture around a glowing core; unmistakable from "
                "across the table. Implies build-up staging: ground placement -> rising "
                "structure -> completed citadel."),
}

# AI-063 board-scale contract (docs/muse/sprint-02/board-scale.md). Budgets are
# sized for one hex tile on the 4x6 odd-row-offset HEX board
# (BoardGeometry.HEX at pin 992bc95). SPELL has no board presence.
BOARD_SCALE = {
    "LAND":      {"board_footprint": "1 hex tile", "height_budget": "≤0.25 units (top surface)"},
    "STRUCTURE": {"board_footprint": "≤0.9 hex",    "height_budget": "≤1.6 units"},
    "CHARACTER": {"board_footprint": "≤0.8 hex",    "height_budget": "1.8 units"},
    "CAPITAL":   {"board_footprint": "1 hex tile",  "height_budget": "≤2.2 units"},
    "SPELL":     {"board_footprint": None,          "height_budget": None},
}

# Stack-role + scale language appended to every Meshy prompt (AI-063). The hex
# wording elsewhere in the prompts is kept; this only adds the size contract
# and states what stacks on what.
PROMPT_SCALE = {
    "LAND": (" Scale: one full hex tile; top surface \u2264 0.25 units, flat and sturdy "
             "enough to carry a unit token standing on top of it."),
    "STRUCTURE": (" Scale: footprint \u2264 0.9 of a hex, height \u2264 1.6 units; sits on "
                  "top of a land tile without overhanging neighbouring hexes."),
    "CHARACTER": (" Scale: footprint \u2264 0.8 of a hex, 1.8 units tall; stands on top "
                  "of a land tile."),
    "CAPITAL": (" Scale: occupies one full hex, height \u2264 2.2 units; the headquarters "
                "other pieces gather around."),
    "SPELL": (" Scale: no board footprint \u2014 pure VFX."),
}

CUE_WORDS = {
    "DEPLOY": "placement thump", "DESTROY": "destruction crash", "CLICK": "selection click",
    "SHUFFLE": "card shuffle", "ATTACK": "attack whoosh-impact", "HIT": "hit thud",
    "DEATH": "death collapse", "MOVE": "movement whoosh", "CAST": "spell cast shimmer",
    "DRAW": "card draw snap", "HEAL": "restorative chime", "ACTIVATE": "ability trigger",
}

STYLE_ANCHOR = ("Stylized dark-mythology 3D game miniature for a hex-based tactics board game. "
                "Painterly detail, dramatic rim lighting, subtle glowing energy accents, "
                "clean readable silhouette at tabletop distance, centered composition. "
                "No background, no base scenery, no text, no watermark. Game-ready asset.")


def load_cards():
    by_id = {}
    for f in glob.glob(CARD_JSON_GLOB):
        d = json.load(open(f))
        items = d if isinstance(d, list) else d.get("cards", d)
        for c in items:
            by_id[c["id"]] = c
    return by_id


def main():
    cards_json = load_cards()
    rows = list(csv.DictReader(open(MANIFEST)))
    scope = [r for r in rows if r["faction"] in ("ZEUS", "POSEIDON")]
    print(f"scope: {len(scope)} cards "
          f"({sum(1 for r in scope if r['source']=='alpha-json')} json + "
          f"{sum(1 for r in scope if r['source']=='alpha-generated')} tutors)")

    entries = []
    for r in scope:
        cid = r["card_id"]
        j = cards_json.get(cid, {})
        archetypes = j.get("archetypes") or []
        keywords = j.get("keywords") or []
        desc = (j.get("description") or r["notes"] or "").strip()
        try:
            rarity = int(j.get("rarity", 0))
        except (TypeError, ValueError):
            rarity = 0
        is_r4 = (rarity == 4)
        faction, ctype, name = r["faction"], r["type"], r["name"]

        # ---- Meshy prompt ----
        flavor_bits = []
        if archetypes:
            flavor_bits.append("; ".join(ARCHETYPE_FLAVOR.get(a, a.lower()) for a in archetypes))
        if keywords:
            flavor_bits.append("keywords: " + ", ".join(keywords))
        if desc:
            flavor_bits.append(desc[:220])
        if r["source"] == "alpha-generated" and r["notes"]:
            flavor_bits.append(r["notes"][:160])
        flavor = " ".join(flavor_bits)

        prompt = (f"{STYLE_ANCHOR} Subject: \"{name}\", a {ctype.lower()} of the {faction.title()} faction. "
                  f"{FACTION_STYLE[faction]} {TYPE_FRAMING[ctype]}")
        if flavor:
            prompt += f" {ctype.title()} notes: {flavor}."
        if is_r4:
            prompt += (" Signature Apex-tier asset: push detail, presence and material richness "
                       "beyond standard cards — this is a centerpiece miniature.")
        # AI-063: board-scale contract + stack role (hex wording kept).
        prompt += PROMPT_SCALE[ctype]
        scale = BOARD_SCALE[ctype]

        # ---- SFX grouping ----
        if is_r4:
            sfx_mode, group_id = "unique", None
        else:
            basis = archetypes[0] if archetypes else ctype
            group_id = f"{faction}_{basis}"
            sfx_mode = "group"

        anim = [a for a in (r["animation_events"] or "").split(";") if a]
        cues = [c for c in (r["sound_cues"] or "").split(";") if c]

        entries.append({
            "id": cid, "name": name, "faction": faction, "type": ctype,
            "rarity": rarity, "apex_tier": is_r4, "archetypes": archetypes,
            "keywords": keywords, "description": desc[:300],
            "source": r["source"],
            "board_footprint": scale["board_footprint"],
            "height_budget": scale["height_budget"],
            "meshy_prompt": prompt, "animation_events": anim, "sound_cues": cues,
            "sfx_mode": sfx_mode, "sfx_group": group_id,
        })

    # ---- SFX groups ----
    groups = collections.OrderedDict()
    for e in entries:
        if e["sfx_mode"] != "group":
            continue
        g = groups.setdefault(e["sfx_group"], {"cards": [], "cues": collections.Counter()})
        g["cards"].append(e["id"])
        for c in e["sound_cues"]:
            g["cues"][c] += 1

    group_specs = {}
    for gid, g in sorted(groups.items()):
        faction, basis = gid.split("_", 1)
        cues = sorted(g["cues"], key=lambda c: (-g["cues"][c], c))
        cue_line = ", ".join(f"{c} ({CUE_WORDS.get(c, 'effect')})" for c in cues) or "standard cues"
        is_char = basis not in ("LAND", "STRUCTURE", "SPELL", "CAPITAL")
        palette = ("crackling electricity, bronze impacts, thunderous lows" if faction == "ZEUS"
                   else "deep water pressure, teal shimmer, crushing wave impacts")
        brief = (
            f"SFX GROUP {gid} — shared sound family for {len(g['cards'])} {faction.title()} "
            f"{basis.title()} cards. Palette: {palette}. "
            f"Design 5 short game-ready effects sharing one reverb space and material language: "
            f"select (0.2s), deploy/place (0.8s), "
            f"{'attack (0.7s), hit (0.4s), death (1.2s)' if is_char else 'activation (0.7s), destruction (1.3s)'}. "
            f"Cues covered: {cue_line}. WAV 48kHz/24-bit, peaks -6dB, no music, no voice. "
            f"File naming: {gid.lower()}_<cue>.wav"
        )
        group_specs[gid] = {"faction": faction, "basis": basis,
                            "card_count": len(g["cards"]), "cards": sorted(g["cards"]),
                            "brief": brief}

    # ---- Unique SFX briefs (rarity 4) ----
    for e in entries:
        if e["sfx_mode"] != "unique":
            continue
        palette = ("sovereign thunder, pealing bronze bells, sky-splitting crack"
                   if e["faction"] == "ZEUS"
                   else "abyssal pressure wave, leviathan call, tidal detonation")
        e["sfx_brief"] = (
            f"UNIQUE SFX — \"{e['name']}\" ({e['faction'].title()} {e['type'].title()}, Apex tier). "
            f"Signature sound, unmistakable in a busy mix. {e['description'][:160]} "
            f"Palette: {palette}. Design a 1.5–2.0s cinematic game effect: "
            f"anticipation swell → signature impact → decaying tail with faction reverb. "
            f"WAV 48kHz/24-bit, peaks -6dB, no music, no voice. "
            f"File naming: {e['id']}_sfx.wav"
        )
    for e in entries:
        if e["sfx_mode"] == "group":
            e["sfx_brief"] = group_specs[e["sfx_group"]]["brief"]

    # ---- Write JSON ----
    n_json = sum(1 for e in entries if e["source"] == "alpha-json")
    n_tut = sum(1 for e in entries if e["source"] == "alpha-generated")
    n_unique = sum(1 for e in entries if e["sfx_mode"] == "unique")
    out = {
        "generated": "2026-09-27",
        "scope": {
            "cards": len(entries),
            "alpha_json": n_json,
            "tutors": n_tut,
            "note": (f"{len(entries)} cards: {n_json} ZEUS/POSEIDON alpha-json + {n_tut} runtime tutors. "
                     "19 DEMO + 5 UNASSIGNED prototypes excluded (dev-only)."),
            "unique_sfx_count": n_unique,
            "unique_sfx_breakdown": ("20 apex-file cards + 6 capitals + 6 other rarity-4 "
                                     "(rarity 4 == apex tier in card data)"),
            "sfx_group_count": len(group_specs),
        },
        "sfx_groups": group_specs,
        "cards": entries,
    }
    with open(os.path.join(HERE, "asset-prompt-directory.json"), "w") as f:
        json.dump(out, f, indent=1)
    print(f"JSON: {len(entries)} cards, {len(group_specs)} sfx groups, "
          f"{out['scope']['unique_sfx_count']} unique-sfx cards")

    # ---- Write Markdown ----
    L = []
    L.append("# Asset Prompt Directory — v1 (AI-049)")
    L.append("")
    L.append(f"Generated 2026-09-27 from the pinned manifest (391 rows) + alpha card JSONs "
             f"(TubaExperiment @ 992bc95, read-only). Scope: **{len(entries)} cards** "
             f"({n_json} Zeus/Poseidon alpha-json + {n_tut} runtime tutors). "
             f"19 DEMO + 5 UNASSIGNED prototypes excluded as dev-only.")
    L.append("")
    L.append("## SFX policy")
    L.append("")
    L.append("- Sounds grouped by **faction + archetype** (fallback: faction + type).")
    L.append(f"- **{len(group_specs)} shared SFX groups**, one ElevenLabs brief each.")
    L.append(f"- **{n_unique} unique SFX** — rarity-4 only (20 apex-file cards + 6 capitals + "
             "6 other rarity-4; rarity 4 == apex tier in the card data).")
    L.append("")
    L.append("## SFX groups")
    L.append("")
    for gid, g in sorted(group_specs.items()):
        L.append(f"### {gid} — {g['card_count']} cards")
        L.append("")
        L.append(g["brief"])
        L.append("")
    L.append("## Unique SFX (Apex tier, rarity 4)")
    L.append("")
    for e in entries:
        if e["sfx_mode"] == "unique":
            L.append(f"### {e['name']} (`{e['id']}`)")
            L.append("")
            L.append(e["sfx_brief"])
            L.append("")
    L.append("## Per-card asset prompts")
    L.append("")
    for e in entries:
        L.append(f"### {e['name']} (`{e['id']}`) — {e['faction']} {e['type']} · rarity {e['rarity']}"
                 + (" · APEX" if e["apex_tier"] else ""))
        if e["board_footprint"] and e["height_budget"]:
            L.append(f"Board scale: footprint {e['board_footprint']}, height {e['height_budget']}")
        if e["archetypes"]:
            L.append(f"Archetypes: {', '.join(e['archetypes'])}")
        L.append("")
        L.append(f"**Meshy prompt:** {e['meshy_prompt']}")
        L.append("")
        if e["animation_events"]:
            L.append(f"**Animation events:** {', '.join(e['animation_events'])}")
            L.append("")
        L.append(f"**SFX:** {'UNIQUE — ' if e['sfx_mode']=='unique' else ''}"
                 f"{e['sfx_group'] or e['id'] + '_sfx'}")
        L.append("")
    with open(os.path.join(HERE, "asset-prompt-directory.md"), "w") as f:
        f.write("\n".join(L))
    print("Markdown written")


if __name__ == "__main__":
    main()
