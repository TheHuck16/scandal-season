# Scandal Season — Art & Content Pipeline Tools

Solo-builder automation for the Scandal Season Unity 6 pipeline: Marvelous
Designer garments → Blender fitting → Unity-ready FBX, plus JSON content
validation. See `NAMING_CONVENTIONS.md` for asset naming rules.

## Layout

```
tools/
├── blender/
│   ├── weight_transfer.py      # single-garment fit (runs INSIDE Blender)
│   ├── batch_export.py         # batch wrapper (runs in your normal Python)
│   └── batch_config.example.json
├── content_validator.py        # Content/*.json vs schemas CLI
├── NAMING_CONVENTIONS.md
├── requirements.txt
└── README.md
```

## Prerequisites

- **Blender 3.6+** installed with `blender` on PATH (only needed for the
  `blender/` scripts; they run in Blender's own bundled Python via `bpy`).
- **Python 3.10+** for `batch_export.py` and `content_validator.py`.
- `pip install -r requirements.txt` — enables full JSON-Schema validation.
  Without `jsonschema`, the validator runs a built-in structural subset
  checker and tells you so.

## 1. Fit a single garment — `blender/weight_transfer.py`

Runs **inside Blender**, headless. It imports the MD garment FBX and the CC5
body FBX, checks the cm/DAZ-Studio scale convention and T-pose (fails loudly
otherwise), removes faces hidden inside the body, decimates to a triangle
budget, transfers skin weights body → garment, binds the garment to the body
armature, and exports a Unity-ready FBX.

```bash
blender --background --python tools/blender/weight_transfer.py -- \
    --garment  ./md_exports/gmt_gown_empire_red.fbx \
    --body     ./characters/chr_rose_hartwell.fbx \
    --output   ./unity_ready/gmt_gown_empire_red.fbx \
    --tri-budget 12000
```

Useful flags:

| Flag | Effect |
|---|---|
| `--skip-interior-removal` | keep hidden faces (not recommended) |
| `--flag-interior-only` | tag interior verts in `INTERIOR_FLAG` instead of deleting, for manual review |
| `--no-decimate` | fail if the garment is already over budget instead of decimating |
| `--bake-space-transform` | bake world transform into the FBX — try this if the garment looks mis-scaled in Unity |

The script prints `RESULT_JSON:{...}` on success for machine parsing, and
`ERROR: <what to fix>` on stderr with exit code 2 on bad input. It imports
cleanly without Blender (for inspection/linting) but refuses to run the
pipeline outside it.

## 2. Batch-fit many garments — `blender/batch_export.py`

Runs in your **normal Python**; it shells out to Blender once per garment and
writes a per-file report JSON.

Config-file mode (copy and edit `batch_config.example.json`):

```bash
python3 tools/blender/batch_export.py --config my_batch.json
```

Folder-scan mode (every `*.fbx` in the folder, one budget):

```bash
python3 tools/blender/batch_export.py \
    --input-dir ./md_exports \
    --body ./characters/chr_rose_hartwell.fbx \
    --output-dir ./unity_ready \
    --tri-budget 12000 \
    --report ./unity_ready/batch_report.json
```

Exit codes: `0` all garments ok · `1` at least one failed (see the report) ·
`2` bad config or Blender not found (nothing attempted).

## 3. Validate game content — `content_validator.py`

Checks `Content/*.json` against `Content/schemas/*.schema.json`
(`Content/<stem>.json` ↔ `schemas/<stem>.schema.json`). Run it before every
Unity import — the schemas are the contract the Unity editor scripts rely on.

```bash
pip install -r tools/requirements.txt   # once
python3 tools/content_validator.py
python3 tools/content_validator.py --content-dir ./Content \
    --schema-dir ./Content/schemas --strict \
    --report-json ./validation_report.json
```

- `--strict`: a content file with no matching schema fails instead of being skipped.
- Exit codes: `0` all valid · `1` failures · `2` missing dirs / unreadable files.

> Status: `Content/schemas/` does not exist yet. The CLI defaults to
> `<project>/Content/schemas` and accepts `--schema-dir` for wherever the
> schemas end up. Author the schemas (outfits, merge chains, characters,
> events…) before wiring this into CI.

## Typical garment session

```bash
# 1. Fit everything new from Marvelous Designer
python3 tools/blender/batch_export.py --config batches/week12.json

# 2. Check the report, review flagged garments in Blender if needed
# 3. Validate any content JSON changes
python3 tools/content_validator.py --strict

# 4. Import ./unity_ready/*.fbx into Unity (Addressables group: garments)
```

## What AI can / cannot do here (per the pipeline research)

- **Automated reliably:** weight transfer, decimation, export, batch runs,
  content validation, interior-face rough removal.
- **Beth reviews:** drape quality, joint weight cleanup (shoulders/armpits),
  clipping QA on min/max body morphs, final garment approval.
- **Still manual:** pattern design judgment and drape tuning in Marvelous
  Designer — no shippable AI text-to-3D-garment tool exists as of Sep 2026.
