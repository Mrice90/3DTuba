# event-dump (AI-066)

Headless tool that runs seeded **Zeus-vs-Poseidon AI matches** against the
pinned alpha and dumps the real engine event stream as **JSONL** in the AI-062
board-events wire format (`docs/muse/sprint-02/board-events/event-schema.json`).

## Layout

- `EventDump.java` — the tool. Both players are HERO bots with seeded RNGs;
  unstructured Java event detail strings are parsed into `card_id` / `from` /
  `to` / `amount`, and synthetic `DAMAGE_DEALT` / `CAPITAL_HIT` events are
  derived per `board-events.md` §22. See the class Javadoc for the adaptation
  notes (spell `to` fallback, seq renumbering, exhaustion exclusion).
- `run.sh` — compiles against the release JAR, runs 3 seeds
  (`42`, `1234`, `98765`) into `dumps/dump-seed-<seed>.jsonl`, and validates
  each against `validate.py`. Fails closed on any validation error.
- `dumps/` — generated output (not committed).

## Usage

```sh
# from releases/alpha-0.7.15-playable — build the JAR first if needed:
./build-release.sh
./tools/event-dump/run.sh [out-dir]
```

One seed by hand:

```sh
JAR=$(ls infinite-conquest-alpha-*.jar | head -1)
javac -cp "$JAR" -d tools/event-dump/classes tools/event-dump/EventDump.java
java -cp "tools/event-dump/classes:$JAR" EventDump 42 /tmp/dump-42.jsonl
```

Validate a dump (validate.py expects a JSON array, so fold the JSONL first):

```sh
python3 -c "import json,sys; json.dump([json.loads(l) for l in open(sys.argv[1]) if l.strip()], open(sys.argv[2],'w'))" \
  dumps/dump-seed-42.jsonl /tmp/dump-42.json
python3 ../../docs/muse/sprint-02/board-events/validate.py \
  ../../docs/muse/sprint-02/board-events/event-schema.json /tmp/dump-42.json
```

## Determinism

Same seed → same match → same dump bytes. Both bots use `new Random(seed ^
constant)`; the engine itself is seeded from the match seed (deck shuffle,
coin flip). CI runs the same three seeds every time.
