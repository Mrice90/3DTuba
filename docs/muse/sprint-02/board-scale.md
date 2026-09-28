# Board-scale contract (AI-063)

The alpha plays on a **hex** board: `MatchRules.hex()` / `BoardGeometry.HEX`
at TubaExperiment pin `992bc95` — 24 hexes, 4 columns × 6 rows, odd-row
offset coordinates, six neighbours per hex, hex distance. Every asset budget
below is sized for **one hex tile**.

## Why this exists

`check_glb.py` is the only size contract in the pipeline and it covers
characters (1.8 units). Without per-type budgets, sculpts drift:
**Keraunos Spire** came out about 2.5 tiles tall and hides its neighbours;
**Abyssal Court** overhangs its tile. The 35 lands have not been generated
yet, so the contract lands before the land batch.

## Per-type budgets

| Type | Board footprint | Height budget | Stack role |
|------|----------------|---------------|------------|
| LAND | One full hex tile | Top surface ≤ 0.25 units | Foundation — the tile's top face must be flat and sturdy enough to **carry a unit or structure token standing on top of it** |
| STRUCTURE | ≤ 0.9 of a hex | ≤ 1.6 units | Sits **on top of** a land tile; keep the silhouette inside the tile so neighbouring hexes stay readable |
| CHARACTER | ≤ 0.8 of a hex | 1.8 units (per `check_glb.py`) | Stands **on top of** a land tile; must read clearly without overhanging neighbours |
| CAPITAL | One full hex | ≤ 2.2 units | Occupies its hex; the headquarters other pieces gather around (build-up staging: placement → rising → completed) |
| SPELL | None | None | No board presence — pure VFX |

## Machine-readable fields

`build_asset_directory.py` writes two fields per card in
`asset-prompt-directory.json`:

- `board_footprint`: `"1 hex tile"`, `"≤0.9 hex"`, `"≤0.8 hex"`, or `null` (SPELL)
- `height_budget`: `"≤0.25 units (top surface)"`, `"≤1.6 units"`, `"1.8 units"`, `"≤2.2 units"`, or `null` (SPELL)

Every **non-spell** card must have both fields non-null. A regression test
(`test_asset_directory.py`, run in `verify.yml`) enforces this.

## Prompt language

Each Meshy prompt states its type's scale budget and stack role in plain
language (appended by the generator; the hex wording is kept everywhere):

- LAND: *"Scale: one full hex tile; top surface ≤ 0.25 units, flat and sturdy
  enough to carry a unit token standing on top of it."*
- STRUCTURE: *"Scale: footprint ≤ 0.9 of a hex, height ≤ 1.6 units; sits on
  top of a land tile without overhanging neighbouring hexes."*
- CHARACTER: *"Scale: footprint ≤ 0.8 of a hex, 1.8 units tall; stands on top
  of a land tile."*
- CAPITAL: *"Scale: occupies one full hex, height ≤ 2.2 units; the
  headquarters other pieces gather around."*
- SPELL: *"Scale: no board footprint — pure VFX."*

## Acceptance

The meetings thread confirms the scale numbers before the next Meshy batch
uses them. Batches 01 and 02 are not regenerated because of this item; that
is the Meshy lane's call.
