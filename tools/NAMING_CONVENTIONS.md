# Scandal Season — Asset Naming Conventions

All asset names are **lowercase `snake_case`**, ASCII only, no spaces, no
special characters. Names double as Unity **Addressables addresses**, so they
must be unique, stable, and sortable. Rename a released asset and you break
live content — version instead (`_v02`), never overwrite.

## General rules

- `lowercase_snake_case` everywhere: files, GameObjects, Addressables keys.
- No spaces, no capitals, no umlauts/accents, no `# @ & %` etc.
- Keep names descriptive but short; put the most significant token first so
  listings sort usefully (`gmt_gown_...` not `red_empire_gown_...`).
- Iteration suffix: `_v01`, `_v02` … on work-in-progress assets only.
- Released assets are immutable. A change = a new versioned name + a content
  JSON update, never an in-place overwrite.
- Addressables group per asset class (`garments`, `characters`, `animations`,
  `textures`); the Addressables **key** is the asset name without extension.

## Garments — `gmt_<type>_<style>[_<detail>][_vNN]`

`<type>` is the garment class, `<style>` the design, `<detail>` optional
(colorway / variant).

| Type token | Meaning |
|---|---|
| `gown` | full dress (empire silhouette etc.) |
| `spencer` | short Spencer jacket |
| `pelisse` | long outer coat |
| `shawl` | shawl / wrap |
| `bonnet`, `cap` | headwear |
| `glove`, `shoe`, `reticule` | accessories |
| `overskirt` | separate overskirt layer |

Examples:

- `gmt_gown_empire_red`
- `gmt_gown_empire_red_v02`
- `gmt_spencer_navy`
- `gmt_pelisse_emerald`
- `gmt_bonnet_straw_ribbon`

Source files keep the same stem: `gmt_gown_empire_red.fbx` (MD export),
`gmt_gown_empire_red.blend` (Blender working file). Unity-ready output uses
the identical name — the folder distinguishes it (`/UnityReady/gmt_...`).

## Characters — `chr_<name>[_lod<N>]`

- `chr_rose_hartwell` — heroine / player avatar base
- `chr_rival_<name>` — e.g. `chr_rival_cecilia_vane`
- LODs: `chr_rose_hartwell_lod1`, `_lod2` (lod0 is implicit = full)

## Animations — `anim_<scope>_<action>[_<variant>]`

`<scope>` is `rose`, `rival_<name>`, or `generic` (retargetable).

- `anim_generic_walk`, `anim_generic_curtsy`, `anim_generic_turn`
- `anim_rose_idle_ballroom`
- `anim_rival_cecilia_vane_fan_snap`

One animation set is authored on the shared CC5 rig; garments never carry
their own animations.

## Textures — `tex_<asset>_<map>[_<variant>]`

Map suffixes:

| Suffix | Map |
|---|---|
| `_alb` | albedo / base color |
| `_nrm` | normal (OpenGL convention) |
| `_rgh` | roughness |
| `_mtl` | metallic |
| `_ao`  | ambient occlusion |
| `_em`  | emissive |

Examples: `tex_gmt_gown_empire_red_alb`, `tex_chr_rose_hartwell_nrm`.

Texture files: `<name>_<resolution>.png`, e.g.
`tex_gmt_gown_empire_red_alb_2048.png`. Mobile ceiling: 2048 for hero
garments, 1024 for the rest.

## Content JSON

Content files live in `Content/` and are named by system, **not** by asset:

- `Content/outfits.json`, `Content/merge_chains.json`,
  `Content/characters.json`, `Content/events.json` …

Each has a matching schema: `Content/schemas/outfits.schema.json`, etc.
(see `content_validator.py --help`).

## Quick checklist for a new garment

1. Model sheet approved → name assigned: `gmt_<type>_<style>_<detail>`
2. MD export: `source/gmt_..._v01.fbx`
3. Blender fit: `work/gmt_..._v01.blend` → `UnityReady/gmt_....fbx`
4. Textures: `tex_gmt_..._alb_2048.png` (+ `_nrm`, …)
5. Content entry in `Content/outfits.json` referencing the exact names above
6. `python3 content_validator.py` passes before the Unity import
