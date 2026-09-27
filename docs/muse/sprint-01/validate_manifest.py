#!/usr/bin/env python3
"""Rerunnable validator for the sprint-01 asset manifest (AI-027/AI-035).

Checks:
  1. 391 rows, 391 unique card_ids, zero duplicates
  2. CSV and JSON id-sets are equal
  3. Required fields present and non-blank on every row
  4. Counts by source: alpha-json=143, alpha-generated=20, original-json=228
  5. Exactly 20 generated tutor rows (source == 'alpha-generated')
  6. provenance non-empty on every row

Usage:
  python3 validate_manifest.py [--csv manifest.csv] [--json manifest.json]
Exit code 0 = all checks pass, 1 = any failure.
"""
import argparse
import csv
import json
import os
import sys
from collections import Counter

REQUIRED = ["card_id", "name", "faction", "type", "source",
            "needed_3d", "animation_events", "sound_cues", "provenance"]
EXPECTED_COUNTS = {"alpha-json": 143, "alpha-generated": 20, "original-json": 228}
EXPECTED_TOTAL = 391


def fail(msg, failures):
    failures.append(msg)
    print(f"FAIL: {msg}")


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    ap = argparse.ArgumentParser()
    ap.add_argument("--csv", default=os.path.join(here, "manifest.csv"))
    ap.add_argument("--json", default=os.path.join(here, "manifest.json"))
    a = ap.parse_args()
    failures = []

    with open(a.csv, encoding="utf-8") as f:
        rows = list(csv.DictReader(f))
    with open(a.json, encoding="utf-8") as f:
        j = json.load(f)
    jrows = j if isinstance(j, list) else j.get("cards", j)

    # 1. row count + uniqueness
    print(f"rows: {len(rows)} (expected {EXPECTED_TOTAL})")
    ids = [r["card_id"] for r in rows]
    print(f"unique ids: {len(set(ids))} (expected {EXPECTED_TOTAL})")
    if len(rows) != EXPECTED_TOTAL:
        fail(f"row count {len(rows)} != {EXPECTED_TOTAL}", failures)
    if len(set(ids)) != len(ids):
        dupes = [i for i, c in Counter(ids).items() if c > 1]
        fail(f"duplicate ids: {dupes}", failures)

    # 2. CSV/JSON id-set equality
    jids = {e["card_id"] for e in jrows}
    print(f"json entries: {len(jrows)}; csv/json id sets match: {jids == set(ids)}")
    if jids != set(ids):
        fail("csv/json id sets differ", failures)

    # 3. required fields non-blank
    blank_cols = [c for c in REQUIRED if any(not (r.get(c) or "").strip() for r in rows)]
    print(f"required columns with blanks: {blank_cols or 'none'}")
    if blank_cols:
        fail(f"blank required columns: {blank_cols}", failures)

    # 4. counts by source
    by_source = dict(Counter(r["source"] for r in rows))
    print(f"counts by source: {by_source} (expected {EXPECTED_COUNTS})")
    if by_source != EXPECTED_COUNTS:
        fail(f"source counts {by_source} != {EXPECTED_COUNTS}", failures)

    # 5. generated tutors
    tutors = [r for r in rows if r["source"] == "alpha-generated"]
    print(f"generated tutor rows: {len(tutors)} (expected 20)")
    if len(tutors) != 20:
        fail(f"tutor rows {len(tutors)} != 20", failures)

    # 6. provenance everywhere
    no_prov = [r["card_id"] for r in rows if not (r.get("provenance") or "").strip()]
    print(f"rows missing provenance: {len(no_prov)}")
    if no_prov:
        fail(f"missing provenance: {no_prov[:5]}", failures)

    if failures:
        print(f"\n{len(failures)} check(s) FAILED")
        return 1
    print("\nALL CHECKS PASSED")
    return 0


if __name__ == "__main__":
    sys.exit(main())
