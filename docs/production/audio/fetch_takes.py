#!/usr/bin/env python3
"""Download ElevenLabs takes for one cue and append a ledger line.

stdin JSON:
  {"key": "<sfx key>", "flow_id": "...", "node_id": "...",
   "takes": [{"generation_id": "...", "url": "<signed mp3 url>", "credits": 16.665}, ...]}

Writes takes to $SFX_TAKES_DIR/<key>/<n>_<generation_id>.mp3 (default: assets/audio/takes,
which is gitignored) and appends to docs/production/audio/sfx_ledger.jsonl.
Exit 1 if any take failed to download (re-poll the run for fresh URLs; never regenerate).
"""
import json
import os
import sys
import time
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
TAKES = os.environ.get("SFX_TAKES_DIR", os.path.join(ROOT, "assets/audio/takes"))
LEDGER = os.path.join(ROOT, "docs/production/audio/sfx_ledger.jsonl")


def main():
    job = json.load(sys.stdin)
    key = job["key"]
    out = os.path.join(TAKES, key)
    os.makedirs(out, exist_ok=True)
    ok = []
    for n, take in enumerate(job["takes"], 1):
        dest = os.path.join(out, f"{n}_{take['generation_id']}.mp3")
        try:
            with urllib.request.urlopen(take["url"], timeout=60) as r:
                data = r.read()
            if len(data) < 1000:
                raise ValueError(f"only {len(data)} bytes")
            open(dest, "wb").write(data)
            ok.append(take["generation_id"])
        except Exception as e:  # noqa: BLE001
            print(f"FAIL {key} take {n}: {e}", file=sys.stderr)
    entry = {
        "key": key, "flow_id": job["flow_id"], "node_id": job.get("node_id"),
        "generation_ids": [t["generation_id"] for t in job["takes"]],
        "downloaded": ok,
        "credits": round(sum(t.get("credits", 0) for t in job["takes"]), 3),
        "at": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
    }
    with open(LEDGER, "a") as f:
        f.write(json.dumps(entry) + "\n")
    print(f"{key}: {len(ok)}/{len(job['takes'])} takes")
    sys.exit(0 if len(ok) == len(job["takes"]) else 1)


if __name__ == "__main__":
    main()
