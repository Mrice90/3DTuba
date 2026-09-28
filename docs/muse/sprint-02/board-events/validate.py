#!/usr/bin/env python3
"""Validate a board-event transcript against event-schema.json (AI-062).

Checks: envelope shape, event enum, seq monotonic from 0, per-event required
payload fields, hex bounds (4x6), and CHARACTER_MOVED hex distance
(BoardGeometry.HEX odd-row adjacency at pin 992bc95 -- not Chebyshev).
Stdlib only. Exit 0 = valid.
"""
import json
import sys

EVENTS = [
    "MATCH_STARTED", "MULLIGAN_COMPLETED", "PHASE_CHANGED", "TURN_STARTED",
    "TURN_ENDED", "CARD_DRAWN", "DRAW_FAILED", "EXHAUSTION_DAMAGE",
    "GP_GENERATED", "GP_SPENT", "CARDS_UNTAPPED", "CARD_PLAYED",
    "CHARACTER_MOVED", "ATTACK_RESOLVED", "OPPORTUNITY_ATTACK",
    "CARD_DESTROYED", "CAPITAL_PASSIVE_TRIGGERED",
    "DEVELOPMENT_PASSIVE_TRIGGERED", "CARD_ABILITY_TRIGGERED",
    "TERRAIN_TRIGGERED", "GAME_OVER", "DAMAGE_DEALT", "CAPITAL_HIT",
]

REQUIRED = {
    "CARD_PLAYED": ["card_id", "instance_id", "to"],
    "CHARACTER_MOVED": ["instance_id", "from", "to", "amount"],
    "ATTACK_RESOLVED": ["instance_id", "to"],
    "OPPORTUNITY_ATTACK": ["instance_id", "to"],
    "CARD_DESTROYED": ["card_id", "instance_id"],
    "DAMAGE_DEALT": ["instance_id", "amount"],
    "CAPITAL_HIT": ["instance_id", "amount"],
    "EXHAUSTION_DAMAGE": ["instance_id", "amount"],
    "GP_GENERATED": ["amount"],
    "GP_SPENT": ["amount"],
}


def check_hex(name, v, errors, i):
    if not isinstance(v, dict) or not isinstance(v.get("x"), int) \
            or not isinstance(v.get("y"), int):
        errors.append(f"[{i}] {name} must be {{x,y}} ints")
        return False
    if not (0 <= v["x"] <= 3 and 0 <= v["y"] <= 5):
        errors.append(f"[{i}] {name} out of 4x6 bounds: {v}")
        return False
    return True


def hex_distance(a, b):
    """BoardGeometry.HEX.distance at pin 992bc95.

    Odd-row offset -> axial: q = x - (y - (y & 1)) / 2, then the cube-coordinate
    max norm. Integer math matches the Java exactly (all values >= 0).
    """
    aq = a["x"] - (a["y"] - (a["y"] & 1)) // 2
    bq = b["x"] - (b["y"] - (b["y"] & 1)) // 2
    return max(abs(aq - bq), abs(a["y"] - b["y"]),
               abs(aq + a["y"] - bq - b["y"]))


def validate(transcript):
    errors = []
    if not isinstance(transcript, list):
        return ["top level must be a JSON array"]
    for i, e in enumerate(transcript):
        if not isinstance(e, dict):
            errors.append(f"[{i}] event must be an object")
            continue
        for f in ("event", "seq", "turn", "player"):
            if f not in e:
                errors.append(f"[{i}] missing required field '{f}'")
        if e.get("event") not in EVENTS:
            errors.append(f"[{i}] unknown event '{e.get('event')}'")
        if not isinstance(e.get("seq"), int) or e["seq"] != i:
            errors.append(f"[{i}] seq must be the 0-based index (got {e.get('seq')!r})")
        if not isinstance(e.get("turn"), int) or e["turn"] < 1:
            errors.append(f"[{i}] turn must be a positive int")
        if e.get("player") not in (0, 1, -1):
            errors.append(f"[{i}] player must be 0, 1 or -1")
        for f in REQUIRED.get(e.get("event"), []):
            if f not in e:
                errors.append(f"[{i}] {e.get('event')} missing required field '{f}'")
        from_ok = to_ok = False
        for h in ("from", "to"):
            if h in e:
                ok = check_hex(h, e[h], errors, i)
                if h == "from":
                    from_ok = ok
                else:
                    to_ok = ok
        if e.get("event") == "CHARACTER_MOVED" and from_ok and to_ok \
                and isinstance(e.get("amount"), int) and e["amount"] > 0:
            # amount == 0 is a teleport/blink (dissolve, board-events.md §13).
            # Otherwise the move must be exactly `amount` hex steps -- odd-row
            # offset adjacency, never Chebyshev.
            d = hex_distance(e["from"], e["to"])
            if d != e["amount"]:
                errors.append(
                    f"[{i}] CHARACTER_MOVED hex distance {d} != amount "
                    f"{e['amount']} (from {e['from']} to {e['to']})")
        if "amount" in e and (not isinstance(e["amount"], int) or e["amount"] < 0):
            errors.append(f"[{i}] amount must be a non-negative int")
    return errors


def main():
    if len(sys.argv) != 3:
        print("usage: validate.py event-schema.json transcript.json", file=sys.stderr)
        return 2
    schema_path, transcript_path = sys.argv[1], sys.argv[2]
    try:
        schema = json.load(open(schema_path))
    except Exception as ex:
        print(f"schema load failed: {ex}", file=sys.stderr)
        return 2
    # The schema file is the contract source of truth for the enum; the
    # validator's table must match it exactly.
    if set(schema["properties"]["event"]["enum"]) != set(EVENTS):
        print("validator EVENTS table does not match event-schema.json enum",
              file=sys.stderr)
        return 2
    try:
        transcript = json.load(open(transcript_path))
    except Exception as ex:
        print(f"transcript load failed: {ex}", file=sys.stderr)
        return 2
    errors = validate(transcript)
    for err in errors:
        print(err, file=sys.stderr)
    if errors:
        print(f"INVALID: {len(errors)} problem(s)", file=sys.stderr)
        return 1
    print(f"VALID: {len(transcript)} events, seq 0..{len(transcript)-1}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
