#!/usr/bin/env python3
"""
test_validate_requests.py -- regression tests for validate_requests.py (AI-052).

Stdlib only. Reads (never writes) the committed requests.json, README.md,
the six staging tracking.json files, and the Muse directory via `git show`.
Mutations happen on in-memory copies.

Run:  cd docs/production/generation-requests/claude-wave-01 && python -m unittest test_validate_requests -v
"""

import copy
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import validate_requests as vr


class WaveTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.requests = vr.load_json(os.path.join(vr.HERE, "requests.json"), "requests.json")
        cls.muse = vr.load_muse_directory()
        cls.tracking = vr.load_tracking(cls.requests)
        with open(os.path.join(vr.HERE, "README.md"), "r", encoding="utf-8") as handle:
            cls.readme = handle.read()

    def errors_for(self, mutate=None, tracking_mutate=None, readme=None):
        requests = copy.deepcopy(self.requests)
        tracking = copy.deepcopy(self.tracking)
        if mutate:
            mutate(requests)
        if tracking_mutate:
            tracking_mutate(tracking)
        return vr.validate(requests, self.muse, tracking,
                           self.readme if readme is None else readme)

    def assertError(self, errors, fragment):
        self.assertTrue(any(fragment in e for e in errors), errors)

    def test_committed_wave_is_valid(self):
        self.assertEqual([], self.errors_for())
        self.assertEqual(0, vr.main([]))

    def test_expected_selection(self):
        self.assertEqual(
            {"zeus_character": "zeus_siege_thunder_ram",
             "poseidon_character": "poseidon_leviathan_wakeborn",
             "board_structure": "poseidon_abyss_gate"},
            {r["role"]: r["card_id"] for r in self.requests["requests"]})

    def test_all_six_tracking_files_exist(self):
        self.assertEqual(6, len(self.tracking))
        self.assertTrue(all(v is not None for v in self.tracking.values()), self.tracking)
        for rec in self.tracking.values():
            self.assertEqual("REVIEW", rec["state"])
            self.assertTrue(rec["job_id"])
            self.assertTrue(rec["outputs"])

    def test_prompts_under_limit(self):
        for req in self.requests["requests"]:
            self.assertLess(len(req["meshy"]["prompt"]), vr.MAX_PROMPT_CHARS, req["card_id"])

    def test_excluded_card_rejected(self):
        def mutate(r):
            r["requests"][0]["card_id"] = "zeus_ability_skyline_seer"
        self.assertError(self.errors_for(mutate), "card is excluded")

    def test_wrong_wave_size_rejected(self):
        def mutate(r):
            r["requests"] = r["requests"][:2]
        self.assertError(self.errors_for(mutate), "exactly 3 requests")

    def test_role_faction_mismatch_rejected(self):
        def mutate(r):
            r["requests"][0]["role"], r["requests"][1]["role"] = (
                "poseidon_character", "zeus_character")
        self.assertError(self.errors_for(mutate), "needs faction")

    def test_source_field_drift_rejected(self):
        def mutate(r):
            r["requests"][1]["source"]["description"] = "A small crab."
        self.assertError(self.errors_for(mutate), "source.description")

    def test_unknown_card_rejected(self):
        def mutate(r):
            r["requests"][2]["card_id"] = "poseidon_abyss_gate_v2"
        self.assertError(self.errors_for(mutate), "holds 0 cards")

    def test_wrong_muse_commit_rejected(self):
        def mutate(r):
            r["requests"][0]["source"]["commit"] = "a6be686"
        self.assertError(self.errors_for(mutate), "source must pin")

    def test_long_prompt_rejected(self):
        def mutate(r):
            r["requests"][0]["meshy"]["prompt"] += " x" * 400
        self.assertError(self.errors_for(mutate), "must be < 800")

    def test_prompt_missing_own_name_rejected(self):
        def mutate(r):
            r["requests"][2]["meshy"]["prompt"] = r["requests"][2]["meshy"]["prompt"].replace(
                "Abyss Gate", "The Gate")
        self.assertError(self.errors_for(mutate), "does not name 'Abyss Gate'")

    def test_prompt_cross_contamination_rejected(self):
        def mutate(r):
            r["requests"][0]["meshy"]["prompt"] += " Pairs with Leviathan Wakeborn."
        self.assertError(self.errors_for(mutate), "names another wave card")

    def test_prompt_not_self_contained_rejected(self):
        def mutate(r):
            r["requests"][1]["meshy"]["prompt"] += " Style as above."
        self.assertError(self.errors_for(mutate), "not self-contained")

    def test_prompt_missing_prohibition_rejected(self):
        def mutate(r):
            r["requests"][1]["meshy"]["prompt"] = r["requests"][1]["meshy"]["prompt"].replace(
                "No text, logos,", "No")
        self.assertError(self.errors_for(mutate), "lacks prohibition")

    def test_sfx_brief_drift_rejected(self):
        def mutate(r):
            r["requests"][0]["elevenlabs"]["sfx_brief"] += " Add a choir."
        self.assertError(self.errors_for(mutate), "not verbatim Muse group ZEUS_CHARACTER")

    def test_sfx_group_mismatch_rejected(self):
        def mutate(r):
            r["requests"][0]["elevenlabs"]["sfx_group"] = "ZEUS_HUMAN"
        self.assertError(self.errors_for(mutate), "elevenlabs.sfx_group")

    def test_missing_provider_model_rejected(self):
        def mutate(r):
            r["requests"][1]["meshy"]["model"] = ""
            del r["requests"][2]["elevenlabs"]["type"]
        errors = self.errors_for(mutate)
        self.assertError(errors, "meshy.model missing")
        self.assertError(errors, "elevenlabs.type missing")

    def test_duplicate_silhouette_rejected(self):
        def mutate(r):
            r["requests"][1]["silhouette_class"] = r["requests"][0]["silhouette_class"]
        self.assertError(self.errors_for(mutate), "silhouette_class must be present and distinct")

    def test_missing_tracking_file_rejected(self):
        def tmutate(t):
            t[("ElevenLabs", "poseidon_abyss_gate")] = None
        self.assertError(self.errors_for(tracking_mutate=tmutate),
                         "missing or malformed at assets/staging/elevenlabs/AI-052-poseidon_abyss_gate/")

    def test_tracking_field_missing_rejected(self):
        def tmutate(t):
            del t[("Meshy", "zeus_siege_thunder_ram")]["credit_estimate"]
        self.assertError(self.errors_for(tracking_mutate=tmutate), "missing tracking fields")

    def test_tracking_wrong_location_rejected(self):
        def tmutate(t):
            t[("Meshy", "poseidon_leviathan_wakeborn")]["output_location"] = (
                "assets/staging/meshy/AI-050-skyline-seer/")
        self.assertError(self.errors_for(tracking_mutate=tmutate), "output_location")

    def test_submitted_without_job_id_rejected(self):
        def tmutate(t):
            t[("Meshy", "poseidon_abyss_gate")]["state"] = "SUBMITTED"
            t[("Meshy", "poseidon_abyss_gate")]["job_id"] = None
        self.assertError(self.errors_for(tracking_mutate=tmutate), "job_id required")

    def test_charge_before_submission_rejected(self):
        def tmutate(t):
            rec = t[("ElevenLabs", "zeus_siege_thunder_ram")]
            rec["state"] = "BRIEF"
            rec["job_id"] = None
            rec["credit_charge"] = 40
        self.assertError(self.errors_for(tracking_mutate=tmutate), "credit_charge recorded")

    def test_missing_review_gate_rejected(self):
        def mutate(r):
            r["review_gates"] = [g for g in r["review_gates"] if g["gate"] != "G1-cost-approved"]
        self.assertError(self.errors_for(mutate), "review_gates missing ['G1-cost-approved']")

    def test_readme_missing_staging_dir_rejected(self):
        readme = self.readme.replace("assets/staging/meshy/AI-052-poseidon_abyss_gate/", "")
        self.assertNotEqual(readme, self.readme)
        self.assertError(self.errors_for(readme=readme),
                         "README.md does not mention 'assets/staging/meshy/AI-052-poseidon_abyss_gate/'")

    def test_bad_muse_commit_is_input_error(self):
        with self.assertRaises(vr.RequestError):
            vr.load_muse_directory(commit="0000000000000000000000000000000000000000")


if __name__ == "__main__":
    unittest.main()
