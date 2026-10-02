#!/usr/bin/env python3
"""AI-107: Tidepool Surveyor pacing quirk — characterization test.

Reads the committed seed-42 event dump and detects A->B->A movement
oscillations by the same instance (the bot pacing back and forth between two
adjacent hexes). This is a CHARACTERIZATION test: it documents the quirk
precisely as observed in the pinned-alpha bot. When the bot is rewritten, invert
the oscillation assertion to verify the fix.

Run:  python test_pacing.py   (or: python -m unittest test_pacing)
"""
import json
import re
import unittest
from pathlib import Path

DIR = Path(__file__).resolve().parent
DUMP = DIR.parent / "timeline" / "fixtures" / "dump-seed-42.jsonl"

MOVE_RE = re.compile(r"BoardPosition\[x=(\d+), y=(\d+)\] -> BoardPosition\[x=(\d+), y=(\d+)\]")

# The exact pacing bursts observed in the seed-42 dump (seq ranges), from the
# AI-107 analysis. Each burst is one instance oscillating 3,5 <-> 3,4.
EXPECTED_BURSTS = [
    (23, 25, "04e583b3"),
    (44, 46, "04e583b3"),
    (47, 49, "2a5eec6d"),
    (76, 79, None),   # mixed instances, same hex pair
    (103, 108, None), # mixed instances, same hex pair
    (134, 138, None), # mixed instances, same hex pair
]


def load_moves():
    """Return [(seq, turn, instance_prefix, (fx, fy), (tx, ty))] for CHARACTER_MOVED."""
    moves = []
    with open(DUMP) as f:
        for line in f:
            e = json.loads(line)
            if e.get("event") != "CHARACTER_MOVED":
                continue
            m = MOVE_RE.search(e.get("detail", ""))
            assert m, f"unparseable move detail at seq {e['seq']}: {e.get('detail')}"
            fx, fy, tx, ty = (int(m.group(i)) for i in range(1, 5))
            moves.append((e["seq"], e["turn"], e["instance_id"][:8], (fx, fy), (tx, ty)))
    return moves


def find_oscillations(moves):
    """Find A->B->A triples by the same instance. Returns [(seq_first, instance)]."""
    by_inst = {}
    for seq, turn, inst, frm, to in moves:
        by_inst.setdefault(inst, []).append((seq, frm, to))
    oscillations = []
    for inst, ms in by_inst.items():
        for (s1, f1, t1), (s2, f2, t2) in zip(ms, ms[1:]):
            if t1 == f2 and f1 == t2 and f1 != t1:
                oscillations.append((s1, inst))
    return oscillations


class TestPacingQuirk(unittest.TestCase):
    def test_dump_has_character_moves(self):
        moves = load_moves()
        self.assertGreater(len(moves), 20, "expected dozens of CHARACTER_MOVED events")

    def test_oscillations_detected(self):
        """The quirk: same instance moves A->B then immediately B->A."""
        oscillations = find_oscillations(load_moves())
        self.assertGreater(len(oscillations), 0, "no A->B->A oscillations found")
        # The quirk is general bot behavior, not Surveyor-specific: both
        # Tidepool Surveyors pace (22 of 25 oscillations), and two Zeus units
        # oscillate once or twice each.
        insts = {inst for _, inst in oscillations}
        self.assertEqual(
            insts, {"2a5eec6d", "04e583b3", "08c686ca", "6f143f78"},
            f"unexpected oscillating instances: {insts}")

    def test_oscillation_hex_pairs(self):
        """Every oscillation is one of the three observed adjacent pairs."""
        moves = {m[0]: (m[3], m[4]) for m in load_moves()}
        allowed = [{(3, 5), (3, 4)}, {(3, 3), (3, 4)}, {(3, 2), (3, 3)}]
        for seq, inst in find_oscillations(load_moves()):
            frm, to = moves[seq]
            self.assertIn({frm, to}, allowed,
                          f"seq {seq}: unexpected oscillation pair {frm} <-> {to}")

    def test_expected_bursts_present(self):
        """The documented pacing bursts are all present in the dump."""
        osc_seqs = [s for s, _ in find_oscillations(load_moves())]
        for start, end, inst_prefix in EXPECTED_BURSTS:
            hits = [s for s in osc_seqs if start <= s <= end]
            if inst_prefix:
                hits = [s for s, i in find_oscillations(load_moves())
                        if start <= s <= end and i == inst_prefix]
            self.assertGreater(len(hits), 0,
                               f"expected pacing burst seq {start}-{end} not found")


if __name__ == "__main__":
    unittest.main()
