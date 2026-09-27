#!/usr/bin/env python3
"""
test_validate_manifest.py -- regression suite for validate_manifest.py (AI-037).

Stdlib only (unittest). Tests use temporary fixtures and never modify the
committed manifests, except PinnedAuditSnapshotTests, which read (never write)
the committed manifest.csv / manifest.json sitting next to this file.

Run:  cd docs/muse/sprint-01 && python3 -m unittest test_validate_manifest -v
"""

import csv
import json
import os
import sys
import tempfile
import unittest
from collections import Counter
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import validate_manifest as vm

HERE = os.path.dirname(os.path.abspath(__file__))


def prov_for(source):
    if source == "alpha-json":
        return "game-core/src/main/resources/cards/faction-cards.json @ " + vm.PINNED_ALPHA_COMMIT
    if source == "original-json":
        return ("Desolate-Tuba:game-core/src/main/resources/cards/faction-cards.json @ "
                + vm.PINNED_ORIGINAL_COMMIT)
    if source == "alpha-generated":
        return vm.TUTOR_PROVENANCE
    return "unverified provenance (fixture-only source)"


def make_row(card_id, source="alpha-json", **overrides):
    row = {
        "card_id": card_id,
        "name": "Name " + card_id,
        "faction": "ZEUS",
        "type": "CHARACTER",
        "source": source,
        "art_path": "",
        "art_status": "bespoke",
        "needed_3d": "stylized character model",
        "animation_events": "deploy",
        "sound_cues": "DEPLOY",
        "provenance": prov_for(source),
        "status": "PROTOTYPE",
        "expansion_gap": "false",
        "notes": "fixture",
    }
    row.update(overrides)
    return row


def write_pair(tmpdir, csv_rows, json_rows):
    """Write a temp CSV/JSON pair; return (csv_path, json_path)."""
    csv_path = os.path.join(tmpdir, "manifest.csv")
    json_path = os.path.join(tmpdir, "manifest.json")
    with open(csv_path, "w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=vm.COLUMNS)
        writer.writeheader()
        writer.writerows(csv_rows)
    with open(json_path, "w", encoding="utf-8") as handle:
        json.dump({"cards": json_rows}, handle)
    return csv_path, json_path


def check(csv_rows, json_rows, **kwargs):
    """validate() with snapshot expectations derived from the fixture itself."""
    params = dict(
        expected_total=len(csv_rows),
        expected_source_counts=dict(Counter(r["source"] for r in csv_rows)),
        expected_tutors={r["card_id"] for r in csv_rows
                         if r["source"] == "alpha-generated"},
    )
    params.update(kwargs)
    return vm.validate(csv_rows, vm.COLUMNS, json_rows, **params)


class ValidatorUnitTests(unittest.TestCase):
    def test_valid_minimal_passes(self):
        csv_rows = [make_row("c1"),
                    make_row("c2", source="original-json"),
                    make_row("zeus_tutor_land_1", source="alpha-generated")]
        json_rows = [dict(r) for r in csv_rows]
        self.assertEqual([], check(csv_rows, json_rows))

    def test_bool_normalization_json_true_vs_csv_TRUE(self):
        # JSON true (real boolean) must compare equal to CSV "TRUE".
        csv_rows = [make_row("c1", expansion_gap="TRUE")]
        json_rows = [make_row("c1", expansion_gap=True)]
        self.assertEqual([], check(csv_rows, json_rows))

    def test_bool_true_vs_false_mismatch_fails(self):
        csv_rows = [make_row("c1", expansion_gap="true")]
        json_rows = [make_row("c1", expansion_gap="false")]
        errors = check(csv_rows, json_rows)
        self.assertTrue(any("expansion_gap" in e for e in errors), errors)

    def test_case_preserved_in_ids(self):
        # "Abc" vs "abc" must NOT compare equal.
        csv_rows = [make_row("Abc")]
        json_rows = [make_row("abc")]
        errors = check(csv_rows, json_rows)
        self.assertTrue(any("only in CSV" in e for e in errors), errors)

    def test_duplicate_ids_in_csv_fail(self):
        csv_rows = [make_row("c1"), make_row("c1")]
        json_rows = [make_row("c1")]
        errors = check(csv_rows, json_rows)
        self.assertTrue(any("duplicate" in e and "CSV" in e for e in errors), errors)

    def test_duplicate_ids_in_json_fail(self):
        csv_rows = [make_row("c1")]
        json_rows = [make_row("c1"), make_row("c1")]
        errors = check(csv_rows, json_rows)
        self.assertTrue(any("duplicate" in e and "JSON" in e for e in errors), errors)

    def test_id_in_csv_only_fails(self):
        csv_rows = [make_row("c1"), make_row("c2")]
        json_rows = [make_row("c1")]
        errors = check(csv_rows, json_rows)
        self.assertTrue(any("only in CSV" in e for e in errors), errors)

    def test_field_value_mismatch_fails(self):
        csv_rows = [make_row("c1", name="Alpha")]
        json_rows = [make_row("c1", name="Beta")]
        errors = check(csv_rows, json_rows)
        self.assertTrue(any("c1.name" in e for e in errors), errors)

    def test_missing_column_fails(self):
        csv_rows = [make_row("c1")]
        del csv_rows[0]["notes"]
        cols = [c for c in vm.COLUMNS if c != "notes"]
        errors = vm.validate(csv_rows, cols, [make_row("c1")],
                             expected_total=1,
                             expected_source_counts={"alpha-json": 1},
                             expected_tutors=set())
        self.assertTrue(any("columns" in e for e in errors), errors)

    def test_blank_required_field_fails(self):
        csv_rows = [make_row("c1", name="  ")]
        json_rows = [make_row("c1", name="  ")]
        errors = check(csv_rows, json_rows)
        self.assertTrue(any("blank required field 'name'" in e for e in errors), errors)

    def test_blank_art_path_is_allowed(self):
        # art_path is the documented exception: missing art is a real state.
        csv_rows = [make_row("c1", art_path="")]
        json_rows = [make_row("c1", art_path="")]
        self.assertEqual([], check(csv_rows, json_rows))

    def test_missing_provenance_fails(self):
        csv_rows = [make_row("c1", provenance="")]
        json_rows = [make_row("c1", provenance="")]
        errors = check(csv_rows, json_rows)
        self.assertTrue(any("provenance" in e for e in errors), errors)

    def test_wrong_provenance_commit_fails(self):
        csv_rows = [make_row("c1", provenance="x @ " + vm.PINNED_ORIGINAL_COMMIT)]
        json_rows = [dict(r) for r in csv_rows]
        errors = check(csv_rows, json_rows)
        self.assertTrue(any("provenance" in e for e in errors), errors)

    def test_original_json_missing_prefix_fails(self):
        csv_rows = [make_row("c1", source="original-json",
                             provenance="game-core/x.json @ " + vm.PINNED_ORIGINAL_COMMIT)]
        json_rows = [dict(r) for r in csv_rows]
        errors = check(csv_rows, json_rows)
        self.assertTrue(any("Desolate-Tuba" in e for e in errors), errors)

    def test_alpha_json_with_original_prefix_fails(self):
        csv_rows = [make_row("c1", provenance="Desolate-Tuba:x @ " + vm.PINNED_ALPHA_COMMIT)]
        json_rows = [dict(r) for r in csv_rows]
        errors = check(csv_rows, json_rows)
        self.assertTrue(any("provenance" in e for e in errors), errors)

    def test_unknown_source_fails(self):
        csv_rows = [make_row("c1", source="mystery",
                             provenance="whatever")]
        json_rows = [dict(r) for r in csv_rows]
        errors = vm.validate(csv_rows, vm.COLUMNS, json_rows,
                             expected_total=1,
                             expected_source_counts={"mystery": 1},
                             expected_tutors=set())
        self.assertTrue(any("unknown source" in e for e in errors), errors)

    def test_tutor_id_outside_expected_set_fails(self):
        csv_rows = [make_row("hades_tutor_land_1", source="alpha-generated")]
        json_rows = [dict(r) for r in csv_rows]
        errors = check(csv_rows, json_rows,
                       expected_tutors={"zeus_tutor_land_1"})
        self.assertTrue(any("tutor set" in e for e in errors), errors)

    def test_snapshot_total_enforced_by_default(self):
        rows = [make_row("c1")]
        errors = vm.validate(rows, vm.COLUMNS, [dict(r) for r in rows])
        self.assertTrue(any("391" in e for e in errors), errors)

    def test_default_tutor_set_is_exactly_20(self):
        expected = {"{0}_tutor_{1}_{2}".format(f, k, i)
                    for f in ("zeus", "poseidon")
                    for k in ("land", "structure")
                    for i in range(1, 6)}
        self.assertEqual(20, len(vm.EXPECTED_TUTORS))
        self.assertEqual(expected, set(vm.EXPECTED_TUTORS))

    def test_norm_value_explicit(self):
        self.assertEqual("true", vm.norm_value(True))
        self.assertEqual("false", vm.norm_value(False))
        self.assertEqual("true", vm.norm_value("TRUE"))
        self.assertEqual("false", vm.norm_value(" False "))
        self.assertEqual("Abc", vm.norm_value("Abc"))  # case preserved
        self.assertEqual("", vm.norm_value(None))


class ValidatorFileTests(unittest.TestCase):
    def test_malformed_json_raises(self):
        with tempfile.TemporaryDirectory() as tmpdir:
            path = os.path.join(tmpdir, "m.json")
            with open(path, "w", encoding="utf-8") as handle:
                handle.write("{not valid json")
            with self.assertRaises(vm.ManifestError):
                vm.load_json_rows(path)

    def test_malformed_csv_raises(self):
        with tempfile.TemporaryDirectory() as tmpdir:
            path = os.path.join(tmpdir, "m.csv")
            with open(path, "w", newline="", encoding="utf-8") as handle:
                handle.write("a,b\n\"unclosed,c\n")  # strict mode: unexpected end of data
            with self.assertRaises(vm.ManifestError):
                vm.load_csv_rows(path)

    def test_absent_files_raise(self):
        with self.assertRaises(vm.ManifestError):
            vm.load_csv_rows(os.path.join("nope", "manifest.csv"))
        with self.assertRaises(vm.ManifestError):
            vm.load_json_rows(os.path.join("nope", "manifest.json"))

    def test_valid_pair_roundtrip_passes(self):
        with tempfile.TemporaryDirectory() as tmpdir:
            csv_rows = [make_row("c1"),
                        make_row("zeus_tutor_land_1", source="alpha-generated")]
            csv_path, json_path = write_pair(tmpdir, csv_rows, [dict(r) for r in csv_rows])
            loaded_csv, cols = vm.load_csv_rows(csv_path)
            loaded_json = vm.load_json_rows(json_path)
            self.assertEqual([], check(loaded_csv, loaded_json))

    def test_main_exit_zero_on_valid(self):
        # Default argv targets the real committed manifests next to this file.
        with mock.patch.object(sys, "argv", ["validate_manifest.py"]):
            self.assertEqual(0, vm.main())

    def test_main_exit_two_on_absent_file(self):
        argv = ["validate_manifest.py", "--csv", "/nonexistent/a.csv",
                "--json", "/nonexistent/b.json"]
        with mock.patch.object(sys, "argv", argv):
            self.assertEqual(2, vm.main())

    def test_main_exit_one_on_invalid(self):
        with tempfile.TemporaryDirectory() as tmpdir:
            csv_rows = [make_row("c1"), make_row("c1")]  # duplicate id
            csv_path, json_path = write_pair(tmpdir, csv_rows, [dict(r) for r in csv_rows])
            argv = ["validate_manifest.py", "--csv", csv_path, "--json", json_path,
                    "--expected-total", "2",
                    "--expected-source-counts", "alpha-json=2"]
            with mock.patch.object(sys, "argv", argv):
                self.assertEqual(1, vm.main())


class PinnedAuditSnapshotTests(unittest.TestCase):
    """Read-only checks against the committed manifests (never modified)."""

    def test_real_manifest_passes_with_snapshot_defaults(self):
        csv_rows, cols = vm.load_csv_rows(os.path.join(HERE, "manifest.csv"))
        json_rows = vm.load_json_rows(os.path.join(HERE, "manifest.json"))
        self.assertEqual([], vm.validate(csv_rows, cols, json_rows))

    def test_real_tutor_ids_match_expected_set(self):
        csv_rows, _ = vm.load_csv_rows(os.path.join(HERE, "manifest.csv"))
        tutor_ids = {r["card_id"] for r in csv_rows if r["source"] == "alpha-generated"}
        self.assertEqual(set(vm.EXPECTED_TUTORS), tutor_ids)


if __name__ == "__main__":
    unittest.main()
