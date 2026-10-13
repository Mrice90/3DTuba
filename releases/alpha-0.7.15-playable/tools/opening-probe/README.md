# AI-108-OPENING — structures-first opening probe

Bot-vs-bot HEX matches against the pinned alpha JAR, set up like the rules
bridge, with the provisional structure-slot policy toggled on or off. Writes
one CSV row per player per personal turn (first 10 turns) and one row per
player per match. Results: `docs/muse/sprint-03/2026-10-09-ai-108-opening-probe.md`.

The slot rule is a re-implementation of the documented SP2 policy (supply 2
per structure, characters 1, Storm Titan / Trident Core 2, capitals and lands
0), applied by filtering the bot's legal list. It is not the SP2 overlay code.

```
./run.sh            # 1,000 seeds, all configs, prints the report tables
java -cp ../../infinite-conquest-alpha-0.7.15.jar:build/classes \
  OpeningProbe on HERO ZEUS POSEIDON 958 958        # one seed
```

Args: `<on|off> <HERO|MORTAL|DEMIGOD> <faction0> <faction1> <seedFrom> <seedTo> [base|mull|deck16|deck16mull]`.
`mull` applies opening-mulligan advice (keep a Land and a Structure);
`deck16` swaps the 4 costliest characters for 4 cheap structures.
