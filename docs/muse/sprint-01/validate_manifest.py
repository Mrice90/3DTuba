#!/usr/bin/env python3
"""
validate_manifest.py -- trustworthy stdlib-only validator for the sprint-01 asset manifest.

Checks (all must pass for exit 0):
  1. CSV and JSON each parse (malformed input -> exit 2 with a clear message).
  2. Both representations hold the pinned snapshot row count (default 391).
  3. card_id is unique within each representation; the id sets are equal across
     representations (no CSV-only / JSON-only ids).
  4. Every row carries exactly the 14 manifest columns; every column except
     art_path is non-blank (blank art_path is legitimate: 240 pinned rows ship
     no art file).
  5. Every field of every shared card_id compares equal after normalization.
  6. source is one of {alpha-json, alpha-generated, original-json}; per-source
     row counts match the snapshot.
  7. provenance is exact per source and pins the audited commits.
  8. The alpha-generated card_ids are exactly the 20 tutor IDs of the pinned audit.

Normalization (explicit):
  - Real booleans -> "true"/"false"; the strings "true"/"false" in any case
    (surrounding whitespace ignored) -> canonical lowercase. So JSON true
    compares equal to CSV "TRUE".
  - Everything else compares byte-for-byte: case is preserved, so "Abc" != "abc".

The 391-row expectation is a SNAPSHOT of the pinned audit
(TubaExperiment@992bc95c7164416ea0a25a4ce120f6ec0a0a167a,
Desolate-Tuba@dde98f8c71ec80ba9046271d1c84e160735c8fcb, 2026-09-27).
It is NOT a universal future card count: override with --expected-total and
--expected-source-counts when validating a different snapshot.

Exit codes: 0 = valid; 1 = validation failures; 2 = input error
(absent file, malformed CSV/JSON, unusable structure). Only the standard
library is used. See test_validate_manifest.py for the regression suite.
"""

import argparse
import csv
import json
import os
import sys
from collections import Counter

PINNED_ALPHA_COMMIT = "992bc95c7164416ea0a25a4ce120f6ec0a0a167a"
PINNED_ORIGINAL_COMMIT = "dde98f8c71ec80ba9046271d1c84e160735c8fcb"
ALLOWED_SOURCES = ("alpha-json", "alpha-generated", "original-json")
COLUMNS = ["card_id", "name", "faction", "type", "source", "art_path",
           "art_status", "needed_3d", "animation_events", "sound_cues",
           "provenance", "status", "expansion_gap", "notes"]
# art_path is the only legitimately blankable column (missing art is a real state).
NONBLANK_COLUMNS = [c for c in COLUMNS if c != "art_path"]
TUTOR_PROVENANCE = (
    "game-cli/src/main/java/com/infiniteconquest/cli/FactionTutorExpansion.java @ "
    + PINNED_ALPHA_COMMIT
    + " (runtime-generated ID scheme <faction>_tutor_<type>_<1-5>)")
EXPECTED_TUTORS = frozenset(
    "{0}_tutor_{1}_{2}".format(faction, kind, i)
    for faction in ("zeus", "poseidon")
    for kind in ("land", "structure")
    for i in range(1, 6))
SNAPSHOT_TOTAL = 391
SNAPSHOT_SOURCE_COUNTS = {"alpha-json": 143, "alpha-generated": 20, "original-json": 228}
MAX_EXAMPLES = 5


class ManifestError(Exception):
    """Input-level failure: absent file, malformed CSV/JSON, unusable structure."""


def norm_value(value):
    """Explicit normalization for cross-representation comparison.

    Booleans and the literals "true"/"false" (any case) canonicalize to
    "true"/"false"; everything else compares verbatim, case preserved.
    """
    if isinstance(value, bool):
        return "true" if value else "false"
    if value is None:
        return ""
    text = value if isinstance(value, str) else str(value)
    lowered = text.strip().lower()
    if lowered == "true":
        return "true"
    if lowered == "false":
        return "false"
    return text


def load_csv_rows(path):
    """Return (rows, column_names); raise ManifestError on absent/malformed input."""
    if not os.path.isfile(path):
        raise ManifestError("CSV not found: {0}".format(path))
    try:
        with open(path, "r", newline="", encoding="utf-8") as handle:
            reader = csv.DictReader(handle, strict=True)
            if reader.fieldnames is None:
                raise ManifestError("CSV has no header row: {0}".format(path))
            return list(reader), list(reader.fieldnames)
    except csv.Error as exc:
        raise ManifestError("malformed CSV {0}: {1}".format(path, exc))
    except UnicodeDecodeError as exc:
        raise ManifestError("CSV is not valid UTF-8 {0}: {1}".format(path, exc))


def load_json_rows(path):
    """Return the list of row dicts; raise ManifestError on absent/malformed input."""
    if not os.path.isfile(path):
        raise ManifestError("JSON not found: {0}".format(path))
    try:
        with open(path, "r", encoding="utf-8") as handle:
            data = json.load(handle)
    except json.JSONDecodeError as exc:
        raise ManifestError("malformed JSON {0}: {1}".format(path, exc))
    except UnicodeDecodeError as exc:
        raise ManifestError("JSON is not valid UTF-8 {0}: {1}".format(path, exc))
    if isinstance(data, dict):
        rows = data.get("cards")
    elif isinstance(data, list):
        rows = data
    else:
        rows = None
    if not isinstance(rows, list) or any(not isinstance(r, dict) for r in rows):
        raise ManifestError(
            "JSON must be a list of objects or {{\"cards\": [...]}}: {0}".format(path))
    return rows


def _examples(items):
    items = [str(i) for i in items]
    shown = items[:MAX_EXAMPLES]
    extra = len(items) - len(shown)
    text = ", ".join(shown)
    return text + (" (+{0} more)".format(extra) if extra else "")


def _provenance_error(source, provenance):
    prov = (provenance or "").strip()
    if source == "alpha-generated":
        if prov != TUTOR_PROVENANCE:
            return "must be exactly {0!r}".format(TUTOR_PROVENANCE)
        return None
    if source == "alpha-json":
        if prov.startswith("Desolate-Tuba:"):
            return "must not carry the Desolate-Tuba: prefix"
        if not prov.endswith("@ " + PINNED_ALPHA_COMMIT):
            return "must end with '@ {0}'".format(PINNED_ALPHA_COMMIT)
        return None
    if source == "original-json":
        if not prov.startswith("Desolate-Tuba:"):
            return "must start with 'Desolate-Tuba:'"
        if not prov.endswith("@ " + PINNED_ORIGINAL_COMMIT):
            return "must end with '@ {0}'".format(PINNED_ORIGINAL_COMMIT)
        return None
    return None  # unknown sources are reported by the source check


def validate(csv_rows, csv_columns, json_rows, expected_total=SNAPSHOT_TOTAL,
             expected_source_counts=None, expected_tutors=EXPECTED_TUTORS):
    """Validate loaded rows; return a list of error strings (empty == valid)."""
    errors = []
    if expected_source_counts is None:
        expected_source_counts = dict(SNAPSHOT_SOURCE_COUNTS)

    # 1. row counts against the pinned snapshot
    for label, rows in (("CSV", csv_rows), ("JSON", json_rows)):
        if len(rows) != expected_total:
            errors.append(
                "{0} rows {1} != expected {2} (pinned-audit snapshot; "
                "override with --expected-total)".format(label, len(rows), expected_total))

    # 2. column / key sets
    if set(csv_columns) != set(COLUMNS):
        errors.append("CSV columns differ from the 14 expected: {0}".format(
            _examples(sorted(set(csv_columns) ^ set(COLUMNS)))))
    bad_json_keys = [i for i, r in enumerate(json_rows) if set(r.keys()) != set(COLUMNS)]
    if bad_json_keys:
        errors.append("JSON row(s) with unexpected keys, first at index {0}".format(bad_json_keys[0]))

    csv_ids = [r.get("card_id") for r in csv_rows]
    json_ids = [r.get("card_id") for r in json_rows]

    # 3. required non-blank fields
    for label, rows in (("CSV", csv_rows), ("JSON", json_rows)):
        for col in NONBLANK_COLUMNS:
            bad = [r.get("card_id") for r in rows if not str(r.get(col) or "").strip()]
            if bad:
                errors.append("{0}: blank required field '{1}' on: {2}".format(
                    label, col, _examples(bad)))

    # 4. uniqueness within each representation
    for label, id_list in (("CSV", csv_ids), ("JSON", json_ids)):
        dupes = sorted({i for i, c in Counter(id_list).items() if c > 1 and i})
        if dupes:
            errors.append("{0} duplicate card_ids: {1}".format(label, _examples(dupes)))

    # 5. id-set equality across representations
    csv_set, json_set = set(csv_ids), set(json_ids)
    only_csv = sorted(csv_set - json_set)
    only_json = sorted(json_set - csv_set)
    if only_csv:
        errors.append("card_ids only in CSV: {0}".format(_examples(only_csv)))
    if only_json:
        errors.append("card_ids only in JSON: {0}".format(_examples(only_json)))

    by_id_csv = {r.get("card_id"): r for r in csv_rows}
    by_id_json = {r.get("card_id"): r for r in json_rows}

    # 6. field-by-field normalized comparison
    mismatch_notes = []
    for card_id in sorted(csv_set & json_set):
        crow, jrow = by_id_csv[card_id], by_id_json[card_id]
        for col in COLUMNS:
            if norm_value(crow.get(col)) != norm_value(jrow.get(col)):
                mismatch_notes.append(
                    "{0}.{1}: CSV={2!r} JSON={3!r}".format(
                        card_id, col, crow.get(col), jrow.get(col)))
    if mismatch_notes:
        errors.append("field mismatches ({0}): {1}".format(
            len(mismatch_notes), _examples(mismatch_notes)))

    # 7. allowed sources and per-source counts
    sources = [r.get("source") for r in csv_rows]
    bad_sources = sorted({s for s in sources if s not in ALLOWED_SOURCES})
    if bad_sources:
        errors.append("unknown source values: {0} (allowed: {1})".format(
            _examples(bad_sources), ", ".join(ALLOWED_SOURCES)))
    counts = Counter(sources)
    for src, want in expected_source_counts.items():
        got = counts.get(src, 0)
        if got != want:
            errors.append("source '{0}' rows {1} != expected {2}".format(src, got, want))

    # 8. exact tutor ID set
    tutor_ids = {i for i, r in by_id_csv.items() if r.get("source") == "alpha-generated"}
    want_tutors = set(expected_tutors)
    if tutor_ids != want_tutors:
        errors.append("alpha-generated IDs != expected tutor set "
                      "(missing: {0}; unexpected: {1})".format(
                          _examples(sorted(want_tutors - tutor_ids)) or "none",
                          _examples(sorted(tutor_ids - want_tutors)) or "none"))

    # 9. provenance per row (CSV is canonical; JSON equality already checked above)
    prov_errors = []
    for card_id in sorted(csv_set):
        row = by_id_csv[card_id]
        if row.get("source") not in ALLOWED_SOURCES:
            continue
        err = _provenance_error(row.get("source"), row.get("provenance"))
        if err:
            prov_errors.append("{0}: provenance {1}".format(card_id, err))
    if prov_errors:
        errors.append("provenance failures ({0}): {1}".format(
            len(prov_errors), _examples(prov_errors)))

    return errors


def parse_args(argv=None):
    here = os.path.dirname(os.path.abspath(__file__))
    parser = argparse.ArgumentParser(
        description="Validate the sprint-01 asset manifest (CSV vs JSON).")
    parser.add_argument("--csv", default=os.path.join(here, "manifest.csv"),
                        help="path to manifest.csv")
    parser.add_argument("--json", default=os.path.join(here, "manifest.json"),
                        help="path to manifest.json")
    parser.add_argument("--expected-total", type=int, default=SNAPSHOT_TOTAL,
                        help="pinned-audit snapshot row count (default %(default)s); "
                             "NOT a universal future card count")
    parser.add_argument("--expected-source-counts",
                        default=",".join("{0}={1}".format(k, v)
                                         for k, v in SNAPSHOT_SOURCE_COUNTS.items()),
                        help="comma-separated source=count pairs for the snapshot")
    return parser.parse_args(argv)


def main(argv=None):
    args = parse_args(argv)
    try:
        expected_source_counts = {}
        for pair in args.expected_source_counts.split(","):
            key, value = pair.split("=", 1)
            expected_source_counts[key.strip()] = int(value.strip())
    except ValueError:
        print("error: --expected-source-counts must look like "
              "'alpha-json=143,alpha-generated=20,original-json=228'", file=sys.stderr)
        return 2
    try:
        csv_rows, csv_columns = load_csv_rows(args.csv)
        json_rows = load_json_rows(args.json)
    except ManifestError as exc:
        print("INPUT ERROR: {0}".format(exc), file=sys.stderr)
        return 2
    errors = validate(csv_rows, csv_columns, json_rows,
                      expected_total=args.expected_total,
                      expected_source_counts=expected_source_counts)
    print("checked {0} CSV rows / {1} JSON rows (snapshot expectation: {2})".format(
        len(csv_rows), len(json_rows), args.expected_total))
    if errors:
        print("{0} problem(s) found:".format(len(errors)))
        for err in errors:
            print("  - {0}".format(err))
        return 1
    print("MANIFEST VALID: all checks passed")
    return 0


if __name__ == "__main__":
    sys.exit(main())
