#!/usr/bin/env python3
"""AI-079: rules-bridge protocol tests.

Builds RulesBridge against the pinned alpha JAR and proves:
  1. deterministic seed-42 output (two runs byte-identical),
  2. valid act advances revision and returns events/state/legal,
  3. stale and fabricated action ids are rejected with INVALID_ACTION,
  4. rejected acts mutate neither state nor revision,
  5. the bot auto-plays after the human ends turn,
  6. a scripted game reaches GAME_OVER,
  7. every emitted event passes AI-062 validation,
  8. opponent hand/deck identities are never exposed,
  9. AI-097: {"op":"hash"} returns a canonical 64-hex state hash and is
     read-only (NO_MATCH before new; hash changes when state changes),
 10. AI-097: seeded setup — two independent bridge processes with the
     same seed report the identical hash,
 11. AI-097: same seed + same intents -> identical hash sequence;
     a divergent intent -> divergent hash.

Also writes the golden transcript fixture (fixtures/golden-seed-42.jsonl).

Usage: test_bridge.py
Prereq: infinite-conquest-alpha-*.jar built (./build-release.sh).
Exit 0 on success, 1 on any failure.
"""
import json
import subprocess
import sys
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
RELEASE_DIR = SCRIPT_DIR.parent.parent
BOARD_EVENTS = RELEASE_DIR.parent.parent / "docs" / "muse" / "sprint-02" / "board-events"
FIXTURE_DIR = SCRIPT_DIR / "fixtures"
SEED = 42

JARS = sorted(RELEASE_DIR.glob("infinite-conquest-alpha-*.jar"))
if not JARS:
    print("test_bridge: no release JAR found — run ./build-release.sh first",
          file=sys.stderr)
    sys.exit(1)
JAR = JARS[0]
print(f"test_bridge: using {JAR}")

CLASSES = SCRIPT_DIR / "classes"
CLASSES.mkdir(exist_ok=True)
cp = subprocess.run(
    ["javac", "-encoding", "UTF-8", "-nowarn", "-cp", str(JAR),
     "-d", str(CLASSES), str(SCRIPT_DIR / "RulesBridge.java")],
    capture_output=True, text=True)
if cp.returncode != 0:
    print("test_bridge: javac failed", file=sys.stderr)
    print(cp.stderr, file=sys.stderr)
    sys.exit(1)
print("test_bridge: compiled")


class Bridge:
    def __init__(self):
        import os
        sep = os.pathsep
        self.proc = subprocess.Popen(
            ["java", "-cp", f"{CLASSES}{sep}{JAR}", "RulesBridge"],
            stdin=subprocess.PIPE, stdout=subprocess.PIPE,
            stderr=subprocess.PIPE, text=True, bufsize=1)
        self.seq = 0

    def call(self, op, **kw):
        self.seq += 1
        req = {"id": f"t{self.seq}", "op": op, **kw}
        self.proc.stdin.write(json.dumps(req) + "\n")
        self.proc.stdin.flush()
        line = self.proc.stdout.readline()
        if not line:
            err = self.proc.stderr.read()
            raise RuntimeError(f"bridge died; stderr tail: {err[-2000:]}")
        assert line.strip().startswith("{") and line.strip().endswith("}"), \
            f"stdout not a single JSON object: {line[:120]!r}"
        resp = json.loads(line)
        assert resp["id"] == req["id"], "response id must correlate"
        return resp

    def close(self):
        try:
            self.proc.stdin.close()
        except BrokenPipeError:
            pass
        self.proc.wait(timeout=60)


def check(cond, msg):
    if not cond:
        print(f"test_bridge: FAIL: {msg}", file=sys.stderr)
        sys.exit(1)
    print(f"test_bridge: ok: {msg}")


def is_hex64(s):
    return (isinstance(s, str) and len(s) == 64
            and all(c in "0123456789abcdef" for c in s))


def new_match(b, human_player=0):
    return b.call("new", seed=SEED, human_player=human_player,
                  human_faction="ZEUS" if human_player == 0 else "POSEIDON",
                  bot_faction="POSEIDON" if human_player == 0 else "ZEUS",
                  difficulty="HERO")


def validate_events(events, label, per_response=None):
    check(events, f"{label}: events emitted")
    if per_response:
        for i, resp_events in enumerate(per_response):
            seqs = [e["seq"] for e in resp_events]
            check(seqs == list(range(len(resp_events))),
                  f"{label}[{i}]: per-response seq 0..N-1")
    for i, e in enumerate(events):
        e["seq"] = i
    tmp = SCRIPT_DIR / "tmp-events.json"
    tmp.write_text(json.dumps(events), encoding="utf-8")
    vp = subprocess.run(
        [sys.executable, str(BOARD_EVENTS / "validate.py"),
         str(BOARD_EVENTS / "event-schema.json"), str(tmp)],
        capture_output=True, text=True)
    tmp.unlink()
    if vp.returncode != 0:
        print(vp.stdout[-3000:], file=sys.stderr)
        print(vp.stderr[-2000:], file=sys.stderr)
    check(vp.returncode == 0, f"{label}: AI-062 validation ({len(events)} events)")
    return events


def check_redaction(state, human):
    for pl in state["players"]:
        if pl["seat"] == human:
            check(pl["hand"], "own hand fully visible")
            for c in pl["hand"]:
                check(c["card_id"] and c["instance_id"], "own hand has identities")
        else:
            check(pl["hand"] == [], "opponent hand identities redacted")
            check(pl["hand_count"] > 0, "opponent hand count present")
    check(state["you"] == human, "state.you marks the human seat")


def end_turn(b, tag):
    legal = b.call("legal")
    check(legal["ok"], f"{tag}: legal ok")
    end = next(a for a in legal["legal"] if a["type"] == "end_turn")
    r = b.call("act", action_id=end["id"])
    check(r["ok"], f"{tag}: end turn act ok")
    return r


def main():
    transcripts = []
    for run in range(2):
        b = Bridge()
        try:
            r = new_match(b)
            check(r["ok"] and r["revision"] == 0, f"run{run}: new ok rev 0")
            check_redaction(r["state"], 0)
            script = [r]
            for _ in range(2):
                legal = b.call("legal")
                pick = next((a for a in legal["legal"]
                             if a["type"] != "end_turn"), None)
                if pick is None:
                    break
                ra = b.call("act", action_id=pick["id"])
                check(ra["ok"], f"run{run}: act {pick['type']} ok")
                script.append(ra)
            script.append(end_turn(b, f"run{run}"))
            transcripts.append(script)
        finally:
            b.close()
    t0 = json.dumps(transcripts[0], sort_keys=True)
    t1 = json.dumps(transcripts[1], sort_keys=True)
    check(t0 == t1, "seed-42 output deterministic across runs")

    FIXTURE_DIR.mkdir(exist_ok=True)
    golden = FIXTURE_DIR / "golden-seed-42.jsonl"
    with golden.open("w", encoding="utf-8") as f:
        for resp in transcripts[0]:
            f.write(json.dumps(resp, sort_keys=True) + "\n")
    print(f"test_bridge: ok: golden fixture written "
          f"({golden.stat().st_size} bytes)")

    b = Bridge()
    try:
        r = new_match(b)
        rev0 = r["revision"]
        legal0 = r["legal"]
        check(legal0, "legal actions listed")
        first_id = legal0[0]["id"]
        check(first_id.startswith(f"r{rev0}-a"), "action id revision-scoped")

        ra = b.call("act", action_id=first_id)
        check(ra["ok"], "valid act ok")
        check(ra["revision"] == rev0 + 1, "revision advanced on valid act")
        check(ra["events"] and ra["state"] and ra["legal"],
              "success carries events+state+legal")
        state_after = json.dumps(ra["state"], sort_keys=True)
        rev1 = ra["revision"]

        bad = b.call("act", action_id=first_id)
        check(not bad["ok"] and bad["error_code"] == "INVALID_ACTION",
              "stale action id rejected INVALID_ACTION")
        check(bad["revision"] == rev1, "no revision mutation on rejection")
        probe = b.call("legal")
        check(json.dumps(probe["state"], sort_keys=True) == state_after,
              "no state mutation on stale rejection")

        bad = b.call("act", action_id=f"r{rev1}-a99999")
        check(not bad["ok"] and bad["error_code"] == "INVALID_ACTION",
              "fabricated action id rejected INVALID_ACTION")
        check(bad["revision"] == rev1, "no revision mutation on fake rejection")

        bad = b.call("frobnicate")
        check(not bad["ok"] and bad["error_code"] == "BAD_REQUEST",
              "unknown op rejected BAD_REQUEST")

        r2 = end_turn(b, "bot-auto")
        bot_events = [e for e in r2["events"] if e["player"] == 1]
        check(bot_events, "bot auto-played after human end turn")
        check(r2["state"]["active_player"] == 0
              or r2["state"]["phase"] == "GAME_OVER",
              "turn returned to human (or game over)")

        all_events = []
        per_resp = []
        for resp in transcripts[0]:
            per_resp.append(resp["events"])
            all_events.extend(resp["events"])
        validate_events(all_events, "scripted play", per_resp)
    finally:
        b.close()

    b = Bridge()
    try:
        r = new_match(b)
        game_events = list(r["events"])
        turns = 0
        while r["state"]["phase"] != "GAME_OVER" and turns < 400:
            r = end_turn(b, f"gameover-t{turns}")
            game_events.extend(r["events"])
            turns += 1
        check(r["state"]["phase"] == "GAME_OVER",
              f"scripted game reached GAME_OVER ({turns} human turns)")
        check(r["state"]["winner"] in (0, 1),
              f"winner decided: {r['state']['winner']}")
        check(any(e["event"] == "GAME_OVER" for e in game_events),
              "GAME_OVER event emitted")
        validate_events(game_events, "full game")
        print(f"test_bridge: ok: full game {len(game_events)} events, "
              f"winner {r['state']['winner']}")
    finally:
        b.close()

    # Property 9: AI-097 {"op":"hash"} — canonical state hash, read-only.
    b = Bridge()
    try:
        pre = b.call("hash")
        check(not pre["ok"] and pre["error_code"] == "NO_MATCH",
              "hash before new rejected NO_MATCH")
        r = new_match(b)
        h1 = b.call("hash")
        check(h1["ok"] and h1["revision"] == r["revision"],
              "hash ok, revision correlates with new")
        check(is_hex64(h1["state_hash"]),
              "hash is canonical 64-char lowercase hex")
        check(isinstance(h1["turn"], int) and h1["turn"] >= 1,
              "hash carries the current turn")
        h2 = b.call("hash")
        check(h2["state_hash"] == h1["state_hash"]
              and h2["revision"] == h1["revision"],
              "hash is read-only: repeat call, same hash, no revision bump")
        legal = b.call("legal")
        pick = next(a for a in legal["legal"] if a["type"] != "end_turn")
        ra = b.call("act", action_id=pick["id"])
        h3 = b.call("hash")
        check(h3["ok"] and h3["revision"] == ra["revision"]
              and h3["state_hash"] != h1["state_hash"],
              "hash sensitive to state change, revision tracks latest act")
    finally:
        b.close()

    # Property 10: AI-097 seeded setup — two independent bridge processes
    # with the same seed report the identical canonical hash.
    seed_hashes = []
    for i in range(2):
        b = Bridge()
        try:
            new_match(b)
            h = b.call("hash")
            check(h["ok"] and is_hex64(h["state_hash"]),
                  f"seed-setup[{i}]: hash ok on fresh process")
            seed_hashes.append(h["state_hash"])
        finally:
            b.close()
    check(seed_hashes[0] == seed_hashes[1],
          "seeded setup: same seed on two processes -> identical state hash")

    # Property 11: AI-097 lockstep determinism — same seed + same intents
    # -> identical hash sequence; one divergent intent -> divergent hash.
    def scripted_hashes(diverge_second=False):
        bb = Bridge()
        hs = []
        try:
            new_match(bb)
            hs.append(bb.call("hash")["state_hash"])
            for step in range(2):
                legal = bb.call("legal")
                non_end = [a for a in legal["legal"]
                           if a["type"] != "end_turn"]
                if diverge_second and step == 1:
                    pick = next(a for a in legal["legal"]
                                if a["type"] == "end_turn")
                elif non_end:
                    pick = non_end[0]
                else:
                    pick = next(a for a in legal["legal"]
                                if a["type"] == "end_turn")
                bb.call("act", action_id=pick["id"])
                hs.append(bb.call("hash")["state_hash"])
            return hs
        finally:
            bb.close()

    seq_a = scripted_hashes()
    seq_b = scripted_hashes()
    check(len(seq_a) == 3 and seq_a == seq_b,
          "same seed + same intents -> identical hash sequence")
    seq_c = scripted_hashes(diverge_second=True)
    check(seq_c[0] == seq_a[0] and seq_c[1] == seq_a[1]
          and seq_c[2] != seq_a[2],
          "divergent intent -> divergent hash (shared prefix identical)")

    print("test_bridge: PASS (11/11 properties)")


if __name__ == "__main__":
    main()