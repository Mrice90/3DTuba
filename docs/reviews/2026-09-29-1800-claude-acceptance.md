# 2026-09-29 18:00 review — Claude (covering Astra) acceptance evidence

Run started 17:08 EDT from a fresh clone of `muse/sprint-01-content-audit` at `09b816f` in a temp folder, on Mathew's Windows 11 PC (Temurin 17.0.20.1, Python 3.10). Range reviewed: `b88c496` (12:00 records) → `09b816f`.

## CI (GitHub Actions API, every run on the branch since 12:00; all completed/success, none red)
| Run | Workflow | Head | Notes |
|---|---|---|---|
| 36608818362 | Verify | `8608fd1` | AI-071 |
| 36610477933 / 36610480091 / 36610477781 | Verify / Linux pkg / Windows pkg | `f4206a1` | AI-072 |
| 36610487040 / 36610487231 / 36610487204 | Verify / Linux / Windows | `b5825e3` | AI-072 |
| 36610492828 / 36610492808 / 36610492737 | Verify / Linux / Windows | `4bcf3ed` | AI-072 |
| 36610505068, 36610509490, 36610513247, 36610518730 | Verify | `ed7ce31`…`691ddda` | AI-072 validator/test/docs |
| 36610536507 | Verify | `633294d` | AI-073, first run on ubuntu-24.04 |
| 36610540429 / 36610540317 | Verify / Linux pkg | `caf4a42` | Linux pkg job label `ubuntu-24.04`; steps 4–10 (clean + 6 break modes) and 11 (AI-066 event dump) success |
| 36610544514 / 36610544470 | Verify / Windows pkg | `90afb2c` | Windows pkg steps 7–13 success |
| 36611220923, 36616334551, 36616350228 | Verify | `df1095e`, `0b9ae62`, `09b816f` | tip: supporting-tools on `ubuntu-24.04` and `windows-latest`, steps 9–11 (AI-062/063/064) success |

## AI-071
`build_presentation_manifest.py:72` `open(ASSET_DIR, encoding="utf-8")`, `:118` `open(path, "w", encoding="utf-8")`. Verify green on both OSes (above). **ACCEPTED.**

## AI-073
`verify.yml` matrix `[ubuntu-24.04, windows-latest]`, `checkout@v5`, `setup-node@v5`, `setup-python@v6`; `linux-packaging.yml` `ubuntu-24.04`, `checkout@v5`, `setup-java@v5`; `windows-packaging.yml` `checkout@v5`, `setup-java@v5`. All three workflows green on the new pins. **ACCEPTED.**

## AI-072 — independent Windows reproduction
- `fetch-source.bat` → `OK: source matches pin 992bc95c7164416ea0a25a4ce120f6ec0a0a167a`. `build-release.bat` exit 0 → `infinite-conquest-alpha-0.7.15.jar` SHA-256 `F89D4BA3…A02CD80D`.
- `javac -encoding UTF-8 -cp <jar> EventDump.java`, then `java -cp classes;<jar> EventDump <seed> <out> --mode=<m>` for seeds 1–20 × base/swap/mirror; each dump folded to JSON and checked with `board-events/validate.py`.
- Result: **60/60 VALID**. base seat0 (Zeus) 20/20. swap seat0 (Poseidon) 8 / seat1 (Zeus) 12. mirror seat0 12 / seat1 8. Seed 42 base = 235 events. This matches Rune's numbers exactly (Zeus 20/20, 12/20, mirror seat-0 12/20) and the CI 36519850577 seed-42 count.
- `run-balance.sh` itself was not used: its `classes:$JAR` classpath only works on Linux. See AI-074.
- **ACCEPTED** (tool, evidence and validator change `amount >= distance`).

## AI-064 — first real coverage report on Mathew's PC
`python coverage.py presentation-manifest.json "…\Infinite Conquest\3DTuba\assets\staging" out.md` → exit 0 on Windows (the AI-069 fix holds), but **0/139 models, 0/139 textures, 0/535 animations, 0/658 SFX** are found. The tool works, but the manifest's paths don't match the real staging layout:
- Models: the manifest expects `meshy/<card_id>.glb`; staging uses `meshy/<batch>/<card_id>.glb`.
- SFX: the manifest expects `sfx/<card_id>_<cue>.wav` with cues summon/move/attack/hit/death/ability/idle. The ElevenLabs lane writes `elevenlabs/AI-061-b02-<card_id>/picks/<card_id>_<cue>.wav` with cues **deploy/destroy** (not summon/death) plus `signature`. Raw takes are `_cNN.mp3`.
- Layout-agnostic count (recursive search by card_id): 30 GLB files cover **29/139 cards** (22 CHARACTER, 6 CAPITAL, 1 STRUCTURE). 542 audio files cover **18/139 cards**. There are 0 animations. Cue histogram: deploy 86, destroy 86, hit 82, idle 70, ability 60, attack 52, move 52.
- `batch-04-alpha-img2-3d/` has only MANIFEST.md and texture refs; the 11 GLBs are still outside staging.
- Verdict: **AI-064 ACCEPTED** (manifest + tool on both OSes; PC report run). Cue-name confirmation is answered: **mismatch** → AI-077.

## Other observations
- The alpha jar build is not byte-reproducible: SHA-256 was `f7d887e3…` (2026-09-28 23:30), committed `728c3fc1…`, and `f89d4ba3…` today, all from pin 992bc95. `build-release.bat` also rewrites the tracked `CHECKSUMS.sha256`, which dirties the checkout → AI-078.
- Local staging has `meshy/batch-03-rarity3-units/`, while AI-067's queue calls the lands "batch-03". The lands go to `batch-05-lands` locally to avoid a collision.
