#!/usr/bin/env python3
"""
test_validate_batch.py -- regression tests for validate_batch.py (AI-049).

Stdlib only. Reads (never writes) the committed manifest-entry.json, README.md
and docs/muse/sprint-01/manifest.csv; mutations happen on in-memory copies.

Run:  cd docs/production/batches/AI-049-skyline-seer && python -m unittest test_validate_batch -v
"""

import copy
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import validate_batch as vb

CSV_PATH = os.path.join(vb.REPO_ROOT, "docs", "muse", "sprint-01", "manifest.csv")


class BatchTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.entry = vb.load_entry(os.path.join(vb.HERE, "manifest-entry.json"))
        cls.rows = vb.load_source_rows(CSV_PATH, vb.EXPECTED_CARD_ID)
        with open(os.path.join(vb.HERE, "README.md"), "r", encoding="utf-8") as handle:
            cls.readme = handle.read()

    def errors_for(self, mutate):
        entry = copy.deepcopy(self.entry)
        mutate(entry)
        return vb.validate(entry, self.rows, self.readme)

    def assertError(self, errors, fragment):
        self.assertTrue(any(fragment in e for e in errors), errors)

    def test_committed_batch_is_valid(self):
        self.assertEqual([], vb.validate(self.entry, self.rows, self.readme))
        self.assertEqual(0, vb.main([]))

    def test_mismatched_card_id_rejected(self):
        def mutate(entry):
            entry["card_id"] = "zeus_ability_storm_seer"
        self.assertError(self.errors_for(mutate), "card_id")

    def test_mismatched_source_row_card_id_rejected(self):
        def mutate(entry):
            entry["source_row"]["card_id"] = "poseidon_ability_skyline_seer"
        self.assertError(self.errors_for(mutate), "source_row.card_id")

    def test_output_not_keyed_by_card_id_rejected(self):
        def mutate(entry):
            entry["outputs"]["meshy"]["model"] = (
                "assets/staging/meshy/AI-050-skyline-seer/skyline.glb")
        self.assertError(self.errors_for(mutate), "outputs.meshy.model")

    def test_missing_provider_tracking_field_rejected(self):
        for field in ("job_id", "credit_estimate", "reviewer", "result"):
            def mutate(entry, field=field):
                del entry["providers"][0][field]
            self.assertError(self.errors_for(mutate), "missing provider tracking fields")

    def test_missing_provider_record_rejected(self):
        def mutate(entry):
            entry["providers"] = entry["providers"][:1]
        self.assertError(self.errors_for(mutate), "missing provider record for ElevenLabs")

    def test_missing_technical_constraint_rejected(self):
        def mutate(entry):
            del entry["technical_constraints"]["meshy"]["forward_axis"]
            entry["technical_constraints"]["elevenlabs"]["loudness"] = ""
        errors = self.errors_for(mutate)
        self.assertError(errors, "technical_constraints.meshy missing ['forward_axis']")
        self.assertError(errors, "technical_constraints.elevenlabs missing ['loudness']")

    def test_missing_constraint_block_rejected(self):
        def mutate(entry):
            del entry["technical_constraints"]["elevenlabs"]
        self.assertError(self.errors_for(mutate), "technical_constraints.elevenlabs missing")

    def test_unrecognized_event_name_rejected(self):
        def mutate(entry):
            entry["events"][2]["event"] = "spell"
        self.assertError(self.errors_for(mutate), "unrecognized event 'spell'")

    def test_unrecognized_animation_event_rejected(self):
        def mutate(entry):
            entry["events"][0]["animation_events"] = ["teleport"]
        self.assertError(self.errors_for(mutate), "unrecognized animation event 'teleport'")

    def test_unrecognized_sound_cue_rejected(self):
        def mutate(entry):
            entry["events"][2]["sound_cue"] = "SPELL"  # real Cue, but not on this row
        self.assertError(self.errors_for(mutate), "unrecognized sound cue 'SPELL'")

    def test_readme_duration_drift_rejected(self):
        drifted = self.readme.replace("| MOVE | 0.5 s", "| MOVE | 0.35 s")
        self.assertNotEqual(drifted, self.readme)
        errors = vb.validate(copy.deepcopy(self.entry), self.rows, drifted)
        self.assertError(errors, "README.md move: length != manifest audio_duration_s 0.5 s")

    def test_parse_row_events(self):
        row = self.rows[0]
        self.assertEqual(
            {"deploy_flight": 400, "move": 320, "melee": 360, "ranged_projectile": None,
             "damage_float": 1200, "hit_flash": 350, "destroy": 320},
            vb.parse_animation_events(row["animation_events"]))
        self.assertEqual({"DEPLOY", "MOVE", "MELEE", "RANGED", "DAMAGE", "DESTROY"},
                         vb.parse_sound_cues(row["sound_cues"]))


if __name__ == "__main__":
    unittest.main()
