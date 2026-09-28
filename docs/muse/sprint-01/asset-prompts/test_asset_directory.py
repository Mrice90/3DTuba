"""AI-063 regression test: board-scale contract on the asset prompt directory.

Every non-spell card must carry board_footprint and height_budget (sized for
one hex tile per docs/muse/sprint-02/board-scale.md); spells must have neither.
Land prompts must state the tile carries a unit/token on top. Stdlib only.
"""
import json
import os
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
DIRECTORY = os.path.join(HERE, "asset-prompt-directory.json")


def load_cards():
    with open(DIRECTORY) as f:
        return json.load(f)["cards"]


class TestBoardScale(unittest.TestCase):
    def test_non_spell_cards_have_scale_fields(self):
        missing = [c["id"] for c in load_cards()
                   if c["type"] != "SPELL"
                   and not (c.get("board_footprint") and c.get("height_budget"))]
        self.assertEqual(missing, [],
                         f"non-spell cards missing board_footprint/height_budget: {missing}")

    def test_spell_cards_have_no_scale_fields(self):
        bad = [c["id"] for c in load_cards()
               if c["type"] == "SPELL"
               and (c.get("board_footprint") or c.get("height_budget"))]
        self.assertEqual(bad, [],
                         f"spell cards must not have scale fields: {bad}")

    def test_land_prompts_carry_unit_on_top(self):
        bad = [c["id"] for c in load_cards()
               if c["type"] == "LAND"
               and "on top" not in c["meshy_prompt"].lower()]
        self.assertEqual(bad, [],
                         f"land prompts must say the tile carries a unit on top: {bad}")

    def test_hex_wording_kept(self):
        bad = [c["id"] for c in load_cards()
               if "hex" not in c["meshy_prompt"].lower()]
        self.assertEqual(bad, [],
                         f"prompts must keep the hex wording: {bad}")


if __name__ == "__main__":
    unittest.main()
