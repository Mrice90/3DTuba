# Infinite Conquest asset production pipeline

Updated 2026-09-27 EDT. This is the operating contract for Claude Code, Meshy, ElevenLabs, and the integrating game team.

## Worker roles

- Claude Code is an engineering and production-planning worker. Give it repository context, a bounded deliverable, exact output paths, and executable validation. It may inspect and improve manifests, importers, validators, and integration code.
- Meshy is a 3D production worker. Give it a complete asset brief; do not ask it design questions. Requests must specify subject, silhouette, style, scale, topology/material targets, pose or animation needs, output formats, and prohibited traits.
- ElevenLabs is an audio production worker. Give it a complete sound brief; do not ask it creative questions. Requests must specify event, emotional/material character, duration, variation count, format, loudness target, looping behavior, and prohibited content.
- Astra integrates, tests, records provenance, and decides whether an output is accepted, needs cleanup, or is rejected. Generated does not mean integrated or done.

## Queue states

`BRIEF` -> `ESTIMATED` -> `SUBMITTED` -> `GENERATED` -> `REVIEW` -> `ACCEPTED` -> `INTEGRATED`

Use `BLOCKED` with the exact blocker. Never resubmit an uncertain paid job. Record the provider, job or flow ID, brief version, credit estimate or charge when available, output location, reviewer, and result.

## Batch rules

1. Start with one representative asset family from Zeus or Poseidon.
2. Lock visual/audio direction using an in-game review before mass generation.
3. Keep each batch small enough to inspect and import in one checkpoint.
4. Models must have a clear board-readable silhouette, game-safe materials, known scale/orientation, optimized variants, and provenance.
5. Sound must be short, distinct in a dense tactical mix, free of speech unless explicitly requested, normalized during integration, and mapped to an exact game event.
6. Preserve the last working build. New assets enter a staging folder before replacing placeholders.

## Standard acceptance evidence

- Brief and version
- Provider job/flow/generation ID
- Original generated files and preview
- License/provenance record
- Technical inspection and cleanup notes
- Unity import result and measured size/performance
- In-game screenshot or audio review
- Final manifest row and commit

