#!/usr/bin/env python3
"""
validate_batch.py -- stdlib-only validator for the AI-049 Skyline Seer batch.

Checks manifest-entry.json against the authoritative sprint-01 manifest row
(docs/muse/sprint-01/manifest.csv) and the asset pipeline contract
(docs/production/ASSET_PIPELINE.md):
  1. card_id is exactly zeus_ability_skyline_seer, exists once in the CSV, and
     every output path/file name is keyed by that id.
  2. source_row equals the CSV row field-for-field; provenance matches the row
     and pins the audited alpha commit.
  3. Events are exactly deploy/move/attack/hit/destroy; every animation event
     and sound cue is one the source row declares (and every cue is a
     SoundEffects.Cue name); animation timings match the row.
  4. Each provider record carries every tracking field required by
     ASSET_PIPELINE.md (provider, job ID, brief version, credit estimate and
     charge, output location, reviewer, result) plus work_id and state.
  5. Meshy and ElevenLabs technical constraints are present and non-blank.
  6. README.md names the card id and both staging directories.

Exit codes: 0 = valid; 1 = validation failures; 2 = input error.
Run:  python validate_batch.py   (defaults resolve relative to this file)
"""

import argparse
import csv
import json
import os
import re
import sys

EXPECTED_CARD_ID = "zeus_ability_skyline_seer"
PINNED_ALPHA_COMMIT = "992bc95c7164416ea0a25a4ce120f6ec0a0a167a"
MANIFEST_COLUMNS = ["card_id", "name", "faction", "type", "source", "art_path",
                    "art_status", "needed_3d", "animation_events", "sound_cues",
                    "provenance", "status", "expansion_gap", "notes"]
# Queue AI-051: deploy/move/attack/hit/destroy.
EXPECTED_EVENTS = ("deploy", "move", "attack", "hit", "destroy")
# SoundEffects.Cue enum per docs/muse/sprint-01/audit-report.md section 3.
SOUND_EFFECT_CUES = frozenset((
    "MOVE", "DEPLOY", "MELEE", "RANGED", "SPELL", "DAMAGE", "PENALTY", "DESTROY",
    "VICTORY", "DEFEAT", "CLICK", "HOVER", "YOUR_TURN", "ENEMY_TURN", "KEEP",
    "SHUFFLE", "COIN_GAIN", "COIN_SPEND", "REACTION", "NOTIFY", "CAPITAL_HIT"))
PROVIDER_FIELDS = ("work_id", "provider", "state", "job_id", "brief_version",
                   "credit_estimate", "credit_charge", "output_location",
                   "reviewer", "result")
PROVIDER_NONBLANK = ("work_id", "provider", "state", "brief_version", "output_location")
QUEUE_STATES = ("BRIEF", "ESTIMATED", "SUBMITTED", "GENERATED", "REVIEW",
                "ACCEPTED", "INTEGRATED", "BLOCKED")
PROVIDER_TARGETS = {
    "Meshy": ("AI-050", "assets/staging/meshy/AI-050-skyline-seer/"),
    "ElevenLabs": ("AI-051", "assets/staging/elevenlabs/AI-051-skyline-seer/"),
}
MESHY_CONSTRAINTS = ("format", "up_axis", "forward_axis", "origin", "height_units",
                     "pose", "materials", "topology", "prohibited")
ELEVENLABS_CONSTRAINTS = ("format", "sample_rate_hz", "looping", "speech",
                          "variations_per_cue", "variations_gate", "loudness",
                          "prohibited")

HERE = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))


class BatchError(Exception):
    """Input-level failure: absent or malformed file."""


def load_entry(path):
    if not os.path.isfile(path):
        raise BatchError("manifest entry not found: {0}".format(path))
    try:
        with open(path, "r", encoding="utf-8") as handle:
            data = json.load(handle)
    except (json.JSONDecodeError, UnicodeDecodeError) as exc:
        raise BatchError("malformed manifest entry {0}: {1}".format(path, exc))
    if not isinstance(data, dict):
        raise BatchError("manifest entry must be a JSON object: {0}".format(path))
    return data


def load_source_rows(path, card_id):
    """Return every CSV row whose card_id equals card_id."""
    if not os.path.isfile(path):
        raise BatchError("source manifest not found: {0}".format(path))
    try:
        with open(path, "r", newline="", encoding="utf-8") as handle:
            return [r for r in csv.DictReader(handle, strict=True)
                    if r.get("card_id") == card_id]
    except (csv.Error, UnicodeDecodeError) as exc:
        raise BatchError("malformed source manifest {0}: {1}".format(path, exc))


def parse_animation_events(text):
    """'a(400ms);b(1ms)/c' -> {'a': 400, 'b': 1, 'c': None}."""
    events = {}
    for group in text.split(";"):
        for token in group.split("/"):
            match = re.fullmatch(r"\s*([a-z_]+)\s*(?:\((\d+)ms\))?\s*", token)
            if match:
                events[match.group(1)] = int(match.group(2)) if match.group(2) else None
    return events


def parse_sound_cues(text):
    return {t.strip() for g in text.split(";") for t in g.split("/") if t.strip()}


def _blank(value):
    return value is None or (isinstance(value, (str, list, dict)) and not value)


def validate(entry, source_rows, readme_text=None):
    """Return a list of error strings (empty == valid)."""
    errors = []
    card_id = entry.get("card_id")

    # 1. card identity
    if card_id != EXPECTED_CARD_ID:
        errors.append("card_id {0!r} != expected {1!r}".format(card_id, EXPECTED_CARD_ID))
    if len(source_rows) != 1:
        errors.append("source manifest holds {0} rows for {1!r} (need exactly 1)".format(
            len(source_rows), EXPECTED_CARD_ID))
        return errors
    row = source_rows[0]

    # 2. source row + provenance
    entry_row = entry.get("source_row")
    if not isinstance(entry_row, dict):
        errors.append("source_row missing or not an object")
        entry_row = {}
    for col in MANIFEST_COLUMNS:
        if entry_row.get(col) != row.get(col):
            errors.append("source_row.{0} {1!r} != manifest {2!r}".format(
                col, entry_row.get(col), row.get(col)))
    if set(entry_row) - set(MANIFEST_COLUMNS):
        errors.append("source_row has unknown columns: {0}".format(
            sorted(set(entry_row) - set(MANIFEST_COLUMNS))))
    if not row.get("provenance", "").endswith("@ " + PINNED_ALPHA_COMMIT):
        errors.append("manifest provenance does not pin {0}".format(PINNED_ALPHA_COMMIT))
    prov = entry.get("provenance")
    if not isinstance(prov, dict):
        errors.append("provenance missing or not an object")
    else:
        if prov.get("card_definition") != row.get("provenance"):
            errors.append("provenance.card_definition != manifest provenance")
        if prov.get("pinned_commit") != PINNED_ALPHA_COMMIT:
            errors.append("provenance.pinned_commit != {0}".format(PINNED_ALPHA_COMMIT))

    # 3. events
    row_anim = parse_animation_events(row.get("animation_events", ""))
    row_cues = parse_sound_cues(row.get("sound_cues", ""))
    unknown_row_cues = sorted(row_cues - SOUND_EFFECT_CUES)
    if unknown_row_cues:
        errors.append("manifest sound cues not in SoundEffects.Cue: {0}".format(unknown_row_cues))
    events = entry.get("events")
    if not isinstance(events, list):
        errors.append("events missing or not a list")
        events = []
    names = [e.get("event") if isinstance(e, dict) else None for e in events]
    if sorted(n for n in names if n) != sorted(EXPECTED_EVENTS) or len(names) != len(EXPECTED_EVENTS):
        errors.append("events {0} != expected {1}".format(names, list(EXPECTED_EVENTS)))
    for event in events:
        if not isinstance(event, dict):
            errors.append("event entry is not an object: {0!r}".format(event))
            continue
        name = event.get("event")
        if name not in EXPECTED_EVENTS:
            errors.append("unrecognized event {0!r}".format(name))
        anims = event.get("animation_events")
        if not isinstance(anims, list) or not anims:
            errors.append("{0}: animation_events missing".format(name))
            anims = []
        for anim in anims:
            if anim not in row_anim:
                errors.append("{0}: unrecognized animation event {1!r} (row declares {2})".format(
                    name, anim, sorted(row_anim)))
        if anims and anims[0] in row_anim and event.get("animation_ms") != row_anim[anims[0]]:
            errors.append("{0}: animation_ms {1!r} != row timing {2!r} for {3}".format(
                name, event.get("animation_ms"), row_anim[anims[0]], anims[0]))
        cue = event.get("sound_cue")
        if cue not in row_cues or cue not in SOUND_EFFECT_CUES:
            errors.append("{0}: unrecognized sound cue {1!r} (row declares {2})".format(
                name, cue, sorted(row_cues)))
        duration = event.get("audio_duration_s")
        if not isinstance(duration, (int, float)) or isinstance(duration, bool) or duration <= 0:
            errors.append("{0}: audio_duration_s must be a positive number".format(name))
        expected_audio = "{0}{1}_{2}_c{{01-04}}.wav".format(
            PROVIDER_TARGETS["ElevenLabs"][1], EXPECTED_CARD_ID, name)
        if event.get("audio_output") != expected_audio:
            errors.append("{0}: audio_output {1!r} != {2!r}".format(
                name, event.get("audio_output"), expected_audio))

    # outputs keyed by card id
    outputs = entry.get("outputs") or {}
    meshy_out = outputs.get("meshy") or {}
    meshy_dir = PROVIDER_TARGETS["Meshy"][1]
    for key, suffix in (("model", ".glb"), ("preview", "_preview.png")):
        want = meshy_dir + EXPECTED_CARD_ID + suffix
        if meshy_out.get(key) != want:
            errors.append("outputs.meshy.{0} {1!r} != {2!r}".format(key, meshy_out.get(key), want))
    audio_out = outputs.get("elevenlabs") or {}
    if audio_out.get("directory") != PROVIDER_TARGETS["ElevenLabs"][1]:
        errors.append("outputs.elevenlabs.directory != queue target")
    if not str(audio_out.get("file_pattern", "")).startswith(EXPECTED_CARD_ID + "_"):
        errors.append("outputs.elevenlabs.file_pattern must start with the card id")

    # 4. provider tracking
    providers = entry.get("providers")
    if not isinstance(providers, list):
        errors.append("providers missing or not a list")
        providers = []
    seen = set()
    for rec in providers:
        if not isinstance(rec, dict):
            errors.append("provider record is not an object")
            continue
        label = rec.get("provider", "?")
        seen.add(label)
        missing = [f for f in PROVIDER_FIELDS if f not in rec]
        if missing:
            errors.append("{0}: missing provider tracking fields {1}".format(label, missing))
        blank = [f for f in PROVIDER_NONBLANK if f in rec and _blank(rec[f])]
        if blank:
            errors.append("{0}: blank provider tracking fields {1}".format(label, blank))
        if rec.get("state") not in QUEUE_STATES:
            errors.append("{0}: state {1!r} not a queue state".format(label, rec.get("state")))
        if label in PROVIDER_TARGETS:
            work_id, target = PROVIDER_TARGETS[label]
            if rec.get("work_id") != work_id or rec.get("output_location") != target:
                errors.append("{0}: work_id/output_location must be {1} / {2}".format(
                    label, work_id, target))
        if rec.get("state") != "BRIEF" and _blank(rec.get("job_id")):
            errors.append("{0}: job_id required once past BRIEF".format(label))
    for label in PROVIDER_TARGETS:
        if label not in seen:
            errors.append("missing provider record for {0}".format(label))

    # 5. technical constraints
    constraints = entry.get("technical_constraints")
    if not isinstance(constraints, dict):
        errors.append("technical_constraints missing or not an object")
        constraints = {}
    for provider, keys in (("meshy", MESHY_CONSTRAINTS), ("elevenlabs", ELEVENLABS_CONSTRAINTS)):
        block = constraints.get(provider)
        if not isinstance(block, dict):
            errors.append("technical_constraints.{0} missing".format(provider))
            continue
        missing = [k for k in keys if k not in block or _blank(block[k])]
        if missing:
            errors.append("technical_constraints.{0} missing {1}".format(provider, missing))
    meshy_c = constraints.get("meshy") or {}
    if meshy_c.get("format") not in (None, "glb"):
        errors.append("technical_constraints.meshy.format must be glb")
    audio_c = constraints.get("elevenlabs") or {}
    if audio_c.get("format") not in (None, "wav") or audio_c.get("sample_rate_hz") not in (None, 48000):
        errors.append("technical_constraints.elevenlabs must be 48000 Hz wav")
    if audio_c.get("looping") is True or audio_c.get("speech") is True:
        errors.append("technical_constraints.elevenlabs must be non-looping and nonverbal")

    # 6. README ties the briefs to the row
    if readme_text is not None:
        for needle in (EXPECTED_CARD_ID, meshy_dir, PROVIDER_TARGETS["ElevenLabs"][1]):
            if needle not in readme_text:
                errors.append("README.md does not mention {0!r}".format(needle))
        # The event table must quote the generation length the entry requests.
        table = {line.split("|")[1].strip(): line for line in readme_text.splitlines()
                 if line.startswith("| ") and line.count("|") > 2}
        for event in events:
            if not isinstance(event, dict) or event.get("event") not in EXPECTED_EVENTS:
                continue
            name, duration = event["event"], event.get("audio_duration_s")
            row_line = table.get(name)
            if row_line is None:
                errors.append("README.md event table has no row for {0!r}".format(name))
            elif "| {0} s".format(duration) not in row_line:
                errors.append("README.md {0}: length != manifest audio_duration_s {1} s".format(
                    name, duration))

    return errors


def main(argv=None):
    parser = argparse.ArgumentParser(description="Validate the AI-049 Skyline Seer batch.")
    parser.add_argument("--entry", default=os.path.join(HERE, "manifest-entry.json"))
    parser.add_argument("--csv", default=os.path.join(
        REPO_ROOT, "docs", "muse", "sprint-01", "manifest.csv"))
    parser.add_argument("--readme", default=os.path.join(HERE, "README.md"))
    args = parser.parse_args(argv)
    try:
        entry = load_entry(args.entry)
        rows = load_source_rows(args.csv, EXPECTED_CARD_ID)
        if not os.path.isfile(args.readme):
            raise BatchError("README not found: {0}".format(args.readme))
        with open(args.readme, "r", encoding="utf-8") as handle:
            readme_text = handle.read()
    except BatchError as exc:
        print("INPUT ERROR: {0}".format(exc), file=sys.stderr)
        return 2
    errors = validate(entry, rows, readme_text)
    if errors:
        print("{0} problem(s) found:".format(len(errors)))
        for err in errors:
            print("  - {0}".format(err))
        return 1
    print("BATCH VALID: AI-049 {0} matches source row and pipeline contract".format(
        EXPECTED_CARD_ID))
    return 0


if __name__ == "__main__":
    sys.exit(main())
