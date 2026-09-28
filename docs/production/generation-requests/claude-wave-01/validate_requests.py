#!/usr/bin/env python3
"""
validate_requests.py -- stdlib-only validator for Claude generation wave 01 (AI-052).

Checks requests.json against the Muse asset prompt directory, which is read
with `git show <commit>:<path>` and never checked out or merged, and against
the asset pipeline contract (docs/production/ASSET_PIPELINE.md):
  1. The wave has exactly three requests: one Zeus character, one Poseidon
     character, and one board LAND or STRUCTURE. Card IDs are unique, none is
     excluded (zeus_ability_skyline_seer), and silhouette classes are distinct.
  2. Each request pins the Muse commit/path. Its card ID exists exactly once in
     the Muse directory, and name, faction, type, rarity, keywords,
     description, and SFX group match the Muse card field for field.
  3. The Meshy prompt is self-contained and shorter than 800 characters. It
     names its own card, never names another selected card, and carries the
     no-text/no-logo prohibitions. Provider model, type, and technical
     constraints are present.
  4. The ElevenLabs brief is verbatim the Muse SFX-group brief for the card's
     group (when the card's sfx_mode is "group").
  5. The staging directories are exactly assets/staging/{meshy,elevenlabs}/
     AI-052-<card_id>/. Each holds a tracking.json with every tracking field
     ASSET_PIPELINE.md requires, and those records agree with the request.
     Nothing may be past BRIEF without a job ID, and no charge may be recorded
     while the state is BRIEF.
  6. Review gates cover validation, cost approval, and preview, technical, and
     in-game review. README.md names each card ID and staging directory.

Exit codes: 0 = valid; 1 = validation failures; 2 = input error.
Run:  python validate_requests.py   (defaults resolve relative to this file)
"""

import argparse
import json
import os
import subprocess
import sys

WORK_ID = "AI-052"
MUSE_COMMIT = "fb19be14c07972fc95a34b5762842a647544e5c2"
MUSE_PATH = "docs/muse/sprint-01/asset-prompts/asset-prompt-directory.json"
EXCLUDED_CARD_IDS = frozenset(("zeus_ability_skyline_seer",))
MAX_PROMPT_CHARS = 800
ROLES = {
    "zeus_character": ("ZEUS", ("CHARACTER",)),
    "poseidon_character": ("POSEIDON", ("CHARACTER",)),
    "board_structure": (None, ("LAND", "STRUCTURE")),
}
SOURCE_FIELDS = ("name", "faction", "type", "rarity", "keywords", "description",
                 "sfx_mode", "sfx_group")
TRACKING_FIELDS = ("work_id", "card_id", "provider", "state", "job_id", "brief_version",
                   "credit_estimate", "credit_charge", "output_location", "reviewer",
                   "result", "submitted_at", "submitted_by")
QUEUE_STATES = ("BRIEF", "ESTIMATED", "SUBMITTED", "GENERATED", "REVIEW",
                "ACCEPTED", "INTEGRATED", "BLOCKED")
PROVIDER_DIRS = {"Meshy": "meshy", "ElevenLabs": "elevenlabs"}
MESHY_CONSTRAINTS = ("format", "up_axis", "forward_axis", "origin", "height_units",
                     "footprint", "topology")
REQUIRED_PROMPT_TERMS = ("no text", "logos")
# Phrases that make a prompt depend on context the provider cannot see.
NON_SELF_CONTAINED = ("see above", "as above", "ai-0", "muse", "readme", "previous prompt",
                      "same as")
REQUIRED_GATES = ("G0-validated", "G1-cost-approved", "G2-submitted-recorded",
                  "G3-preview-review", "G4-technical", "G5-in-game")

HERE = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))


class RequestError(Exception):
    """Input-level failure: absent or malformed file, unreadable Muse source."""


def load_json(path, label):
    if not os.path.isfile(path):
        raise RequestError("{0} not found: {1}".format(label, path))
    try:
        with open(path, "r", encoding="utf-8") as handle:
            data = json.load(handle)
    except (json.JSONDecodeError, UnicodeDecodeError) as exc:
        raise RequestError("malformed {0} {1}: {2}".format(label, path, exc))
    if not isinstance(data, dict):
        raise RequestError("{0} must be a JSON object: {1}".format(label, path))
    return data


def load_muse_directory(commit=MUSE_COMMIT, path=MUSE_PATH, repo=REPO_ROOT):
    """Read the Muse directory with `git show` only; never checkout/merge."""
    safe_repo = repo.replace("\\", "/")
    try:
        proc = subprocess.run(["git", "-c", "safe.directory={0}".format(safe_repo),
                               "-C", repo, "show", "{0}:{1}".format(commit, path)],
                              capture_output=True, check=False)
    except OSError as exc:
        raise RequestError("git unavailable: {0}".format(exc))
    if proc.returncode != 0:
        raise RequestError("git show {0}:{1} failed: {2}".format(
            commit, path, proc.stderr.decode("utf-8", "replace").strip()))
    try:
        data = json.loads(proc.stdout.decode("utf-8"))
    except (json.JSONDecodeError, UnicodeDecodeError) as exc:
        raise RequestError("malformed Muse directory at {0}: {1}".format(commit, exc))
    if not isinstance(data.get("cards"), list) or not isinstance(data.get("sfx_groups"), dict):
        raise RequestError("Muse directory lacks cards/sfx_groups")
    return data


def staging_dir(provider_dir, card_id):
    return "assets/staging/{0}/{1}-{2}/".format(provider_dir, WORK_ID, card_id)


def load_tracking(requests, repo=REPO_ROOT):
    """{(provider, card_id): tracking dict or None when absent/malformed}."""
    tracking = {}
    for req in requests.get("requests") or []:
        if not isinstance(req, dict):
            continue
        for provider, pdir in PROVIDER_DIRS.items():
            path = os.path.join(repo, *staging_dir(pdir, req.get("card_id")).split("/"),
                                "tracking.json")
            try:
                tracking[(provider, req.get("card_id"))] = load_json(path, "tracking.json")
            except RequestError:
                tracking[(provider, req.get("card_id"))] = None
    return tracking


def _blank(value):
    return value is None or (isinstance(value, (str, list, dict)) and not value)


def validate(requests, muse, tracking, readme_text=None):
    """Return a list of error strings (empty == valid)."""
    errors = []
    cards = {}
    for card in muse.get("cards", []):
        cards.setdefault(card.get("id"), []).append(card)
    groups = muse.get("sfx_groups", {})

    if requests.get("work_id") != WORK_ID:
        errors.append("work_id {0!r} != {1!r}".format(requests.get("work_id"), WORK_ID))
    src = requests.get("muse_source") or {}
    if src.get("commit") != MUSE_COMMIT or src.get("path") != MUSE_PATH:
        errors.append("muse_source must pin {0}:{1}".format(MUSE_COMMIT, MUSE_PATH))

    # 1. wave shape
    reqs = requests.get("requests")
    if not isinstance(reqs, list):
        return errors + ["requests missing or not a list"]
    if len(reqs) != 3:
        errors.append("wave must hold exactly 3 requests, found {0}".format(len(reqs)))
    reqs = [r for r in reqs if isinstance(r, dict)]
    roles = [r.get("role") for r in reqs]
    if sorted(roles) != sorted(ROLES):
        errors.append("roles {0} != one each of {1}".format(roles, sorted(ROLES)))
    ids = [r.get("card_id") for r in reqs]
    if len(set(ids)) != len(ids):
        errors.append("duplicate card_id in wave: {0}".format(ids))
    for card_id in ids:
        if card_id in EXCLUDED_CARD_IDS:
            errors.append("{0}: card is excluded from this wave".format(card_id))
    silhouettes = [r.get("silhouette_class") for r in reqs]
    if any(_blank(s) for s in silhouettes) or len(set(silhouettes)) != len(silhouettes):
        errors.append("silhouette_class must be present and distinct: {0}".format(silhouettes))
    names = {r.get("card_id"): (r.get("source") or {}).get("name") for r in reqs}

    for req in reqs:
        card_id = req.get("card_id")
        tag = card_id or "?"

        # 2. source fidelity
        matches = cards.get(card_id, [])
        if len(matches) != 1:
            errors.append("{0}: Muse directory holds {1} cards with this id (need 1)".format(
                tag, len(matches)))
            continue
        muse_card = matches[0]
        source = req.get("source") or {}
        if source.get("commit") != MUSE_COMMIT or source.get("path") != MUSE_PATH:
            errors.append("{0}: source must pin {1}:{2}".format(tag, MUSE_COMMIT, MUSE_PATH))
        for field in SOURCE_FIELDS:
            if source.get(field) != muse_card.get(field):
                errors.append("{0}: source.{1} {2!r} != Muse {3!r}".format(
                    tag, field, source.get(field), muse_card.get(field)))
        faction, types = ROLES.get(req.get("role"), (None, ()))
        if faction and muse_card.get("faction") != faction:
            errors.append("{0}: role {1} needs faction {2}".format(tag, req.get("role"), faction))
        if types and muse_card.get("type") not in types:
            errors.append("{0}: role {1} needs type in {2}".format(tag, req.get("role"), types))
        if req.get("staging_key") != "{0}-{1}".format(WORK_ID, card_id):
            errors.append("{0}: staging_key must be {1}-{0}".format(tag, WORK_ID))

        # 3. Meshy request
        meshy = req.get("meshy") or {}
        for key in ("provider", "type", "model", "prompt"):
            if _blank(meshy.get(key)):
                errors.append("{0}: meshy.{1} missing".format(tag, key))
        if meshy.get("provider") not in (None, "Meshy"):
            errors.append("{0}: meshy.provider must be Meshy".format(tag))
        prompt = meshy.get("prompt") or ""
        lower = prompt.lower()
        if len(prompt) >= MAX_PROMPT_CHARS:
            errors.append("{0}: meshy.prompt is {1} chars (must be < {2})".format(
                tag, len(prompt), MAX_PROMPT_CHARS))
        if names.get(card_id) and names[card_id] not in prompt:
            errors.append("{0}: meshy.prompt does not name {1!r}".format(tag, names[card_id]))
        for other_id, other_name in names.items():
            if other_id != card_id and other_name and other_name in prompt:
                errors.append("{0}: meshy.prompt names another wave card {1!r}".format(
                    tag, other_name))
        for term in REQUIRED_PROMPT_TERMS:
            if term not in lower:
                errors.append("{0}: meshy.prompt lacks prohibition {1!r}".format(tag, term))
        for phrase in NON_SELF_CONTAINED:
            if phrase in lower:
                errors.append("{0}: meshy.prompt is not self-contained ({1!r})".format(
                    tag, phrase))
        constraints = meshy.get("technical_constraints") or {}
        missing = [k for k in MESHY_CONSTRAINTS if _blank(constraints.get(k))]
        if missing:
            errors.append("{0}: meshy.technical_constraints missing {1}".format(tag, missing))
        if constraints.get("format") not in (None, "glb"):
            errors.append("{0}: meshy format must be glb".format(tag))

        # 4. ElevenLabs request
        audio = req.get("elevenlabs") or {}
        for key in ("provider", "type", "model"):
            if _blank(audio.get(key)):
                errors.append("{0}: elevenlabs.{1} missing".format(tag, key))
        if muse_card.get("sfx_mode") == "group":
            group = muse_card.get("sfx_group")
            if audio.get("sfx_group") != group:
                errors.append("{0}: elevenlabs.sfx_group {1!r} != Muse {2!r}".format(
                    tag, audio.get("sfx_group"), group))
            want = (groups.get(group) or {}).get("brief")
            if want is None or audio.get("sfx_brief") != want:
                errors.append("{0}: elevenlabs.sfx_brief is not verbatim Muse group {1}".format(
                    tag, group))
            if audio.get("sfx_group_card_count") != (groups.get(group) or {}).get("card_count"):
                errors.append("{0}: elevenlabs.sfx_group_card_count != Muse".format(tag))
        elif audio.get("sfx_brief") != muse_card.get("sfx_brief"):
            errors.append("{0}: elevenlabs.sfx_brief is not verbatim Muse card brief".format(tag))

        # 5. staging + tracking
        for provider, pdir in PROVIDER_DIRS.items():
            want_dir = staging_dir(pdir, card_id)
            record = tracking.get((provider, card_id))
            label = "{0} {1} tracking.json".format(tag, provider)
            if record is None:
                errors.append("{0}: missing or malformed at {1}".format(label, want_dir))
                continue
            absent = [f for f in TRACKING_FIELDS if f not in record]
            if absent:
                errors.append("{0}: missing tracking fields {1}".format(label, absent))
            expect = {"work_id": WORK_ID, "card_id": card_id, "provider": provider,
                      "output_location": want_dir,
                      "brief_version": requests.get("brief_version")}
            for field, value in expect.items():
                if record.get(field) != value:
                    errors.append("{0}: {1} {2!r} != {3!r}".format(
                        label, field, record.get(field), value))
            state = record.get("state")
            if state not in QUEUE_STATES:
                errors.append("{0}: state {1!r} not a queue state".format(label, state))
            if state not in ("BRIEF", "ESTIMATED", "BLOCKED") and _blank(record.get("job_id")):
                errors.append("{0}: job_id required once submitted".format(label))
            if state in ("BRIEF", "ESTIMATED") and record.get("credit_charge") is not None:
                errors.append("{0}: credit_charge recorded before submission".format(label))

    # 6. review gates + README
    gates = [g.get("gate") for g in requests.get("review_gates") or [] if isinstance(g, dict)]
    missing_gates = [g for g in REQUIRED_GATES if g not in gates]
    if missing_gates:
        errors.append("review_gates missing {0}".format(missing_gates))
    for gate in requests.get("review_gates") or []:
        if isinstance(gate, dict) and any(_blank(gate.get(k)) for k in ("stage", "owner", "check")):
            errors.append("review gate {0!r} needs stage/owner/check".format(gate.get("gate")))
    if readme_text is not None:
        for req in reqs:
            for needle in [req.get("card_id")] + [staging_dir(p, req.get("card_id"))
                                                  for p in PROVIDER_DIRS.values()]:
                if needle and needle not in readme_text:
                    errors.append("README.md does not mention {0!r}".format(needle))
        if MUSE_COMMIT not in readme_text:
            errors.append("README.md does not pin Muse commit {0}".format(MUSE_COMMIT))

    return errors


def main(argv=None):
    parser = argparse.ArgumentParser(description="Validate Claude generation wave 01.")
    parser.add_argument("--requests", default=os.path.join(HERE, "requests.json"))
    parser.add_argument("--readme", default=os.path.join(HERE, "README.md"))
    parser.add_argument("--repo", default=REPO_ROOT)
    args = parser.parse_args(argv)
    try:
        requests = load_json(args.requests, "requests.json")
        muse = load_muse_directory(repo=args.repo)
        if not os.path.isfile(args.readme):
            raise RequestError("README not found: {0}".format(args.readme))
        with open(args.readme, "r", encoding="utf-8") as handle:
            readme_text = handle.read()
    except RequestError as exc:
        print("INPUT ERROR: {0}".format(exc), file=sys.stderr)
        return 2
    errors = validate(requests, muse, load_tracking(requests, args.repo), readme_text)
    if errors:
        print("{0} problem(s) found:".format(len(errors)))
        for err in errors:
            print("  - {0}".format(err))
        return 1
    for req in requests["requests"]:
        print("  {0:<20} {1:<32} meshy prompt {2} chars".format(
            req["role"], req["card_id"], len(req["meshy"]["prompt"])))
    print("WAVE VALID: {0} claude-wave-01 matches Muse {1} and pipeline contract".format(
        WORK_ID, MUSE_COMMIT[:7]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
