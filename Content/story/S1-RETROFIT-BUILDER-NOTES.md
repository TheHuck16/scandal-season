# Season One Turn Retrofit — Builder Handoff Notes

**Date:** September 27, 2026  
**Status:** All 30 chapters retrofitted, verified, and parser-compatible.

## What changed

Every S1 scene now carries 7–10 inline player turns (key decisions: 3 option-turns;
rituals: 21 turns; Gazette stings: 1 reading turn). Turn format:

```
> (T1 · stance) *She reads the three papers...* — remembered: *the reading.*
```

Turn types: `look closer`, `stance`, `tone`, `choice`, `social maneuver`,
`dialogue`, `remembered micro-decision`. Each turn closes with a `remembered:` note.

Every purpose line now carries a `(Turns: N)` annotation, e.g.:
- `(Turns: 8)` — ordinary scenes (7–10)
- `(Turns: 3 — the decision)` — key decisions
- `(Turns: 21)` / `(Turns: 21 — the ritual)` — dressing rituals
- `(Turns: 1 — the reading)` — Gazette stings

## The parser (tools/parse_season_one.py)

The repo's `tools/parse_season_one.py` (pre-retrofit) **fails** on these files —
specifically `parse_ritual`, because the retrofit reformatted rituals into
pin-based layouts. An updated parser is included in this package at
`tools/parse_season_one.py`. Changes vs. the repo version:

1. **`parse_ritual` rewritten** — handles the retrofit's ritual format variants:
   - Occasion brief: 4 variants (`(published): "..."`, `OCCASION BRIEF:`, `Occasion brief.`, `Occasion brief — Title` + italic paragraph)
   - Directions: 6 variants (single-line `The 3 directions:`, `A — Name` blocks, `A. Name` headers, `DIRECTION A — Name`, T1-embedded `She commits: A — X; B — Y; or C — Z`, bulleted `- **A · Name**`)
   - Steps: 4 variants (pre-retrofit `→` chain, `**Pin N — Topic:**`, `(T N — 5 coins)` turns, bare `(T N)` turns)
   - Brief is required; directions/steps are best-effort (ch18's domino ritual is single-garment and honestly has no A/B/C).
2. **`parse_player_turns` added** — populates `playerTurns` + `turnNote` in
   scenes.json from the `(Turns: N)` purpose-line annotation. The schema
   (`scenes.schema.json`) and `ContentImporter` already define/consume these
   fields; the old parser never populated them.
3. `parse_fashion_choices` unchanged — returns `[]` for retrofitted [F] scenes
   (their options are now inline turns; see below).

**Tested:** parses all 1,200 scenes / 30 chapters cleanly → 90 key decisions
(270 options), 30 rituals, 30 fashion beats, 18 Custom animations, `playerTurns`
on all 1,200 scenes. Output validates against `scenes.schema.json`.

**To use:** copy over the repo's `tools/parse_season_one.py` (or merge the two
changed functions), drop these story files into `Content/story/`, run
`python3 tools/parse_season_one.py`.

## Turn content is in the prose (not yet in JSON)

The parser extracts structure (ids, types, synopsis, key decisions, rituals,
stings, turn *counts*). The turn-level *content* — the T1–T10 text, inline
options, and `remembered:` notes — lives in the `.md` prose. If the renderer
needs turns as structured JSON, the format is regular:

```
> (T<#> · <type>[ — <qualifier>]) *<turn text>* — remembered: *<note>.*
```

- Key-decision options remain `- **Label** — Detail` lines (unchanged).
- [F] fashion choices are now inline `(T# · choice)` turns with `;`-separated
  options inside the `*...*` (e.g. `*the scarlet ribbon — ...; the Hartwell blue — ...; no ribbon — ...*`).
- Ritual pins are `(T# · micro-decision)` / `**Pin N (T# · ...) — Topic:**` lines,
  each costing 5 coins.

## File fixes applied during retrofit QA

- `season-one-chapter-15.md`: restored dropped `**STATUS: FULL**` line.
- Chapters 22–27: fixed corrupted Gazette-sting headers (`### L<ch>.S39 · Gazette sting<digits>...` → clean).
- Chapter 27: restored standard `- **Label** — Detail` key-decision option format
  (writer had used `- **Label — sublabel.** Detail`).

## Verification

All 28 S1 files pass the structural verifier (`hidden_files/s1-turn-retrofit/verify_turns.py`):
40 scenes/chapter, 3 key decisions, 7–10 turns on ordinary scenes, 1 ritual +
1 sting + 1 cliffhanger per chapter, exactly one animation line per scene,
18 Custom calls (chapters 10/20/30 only), no Crown SKUs in rituals.
