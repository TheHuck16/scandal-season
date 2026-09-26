# Scandal Season — Unity 6 Client Scaffold

Commercial iOS-first merge-3 story game. Solo-built with AI assistance.

> **Status:** systems and schemas only. All game content is `PLACEHOLDER` until Beth
> finishes the design briefs. Do not invent story, characters, or outfit content.

## Locked design rules (baked into this scaffold)

- **Unity 6, C#.** iOS first, Android later.
- **No ads of any kind** — not even rewarded opt-in. Revenue is IAP only.
- Currencies: **Crowns** (premium) and **coins** (soft).
- **Iron rule:** time always earns the same rewards as money; money strictly expedites.
  Every catalog entry must define a time/engagement path (`CatalogValidator` enforces this;
  purchase-only rewards throw at validation time).
- **Energy is the only throttle.** Plot is never time-gated.
- **No VS mode.** Daily Vote shows two looks side-by-side and totals the votes.
- Scoring reveals **categories** but **hides weights** (weights never ship to the client).
- Rewards are **outfits, hair, accessories** — no avatar unlocks.
- **Book One is a 10-season arc** (ruin → restoration → legacy), but seasons are
  **effectively endless** — the architecture never caps them. Event passes run **3–14 days**.
- **Season structure (locked): 30 × 40** — 30 chapters (levels) per season × 40 scenes
  per chapter (1,200 scenes/season). Chapters are 1-based, 1–30. The importer warns
  on chapters below the 40-scene target. The story clock moves insanely slowly
  (≈1–2 weeks of story time per season); density of lived experience, not plot
  velocity, carries it.
- **Every season's ending changes something real** — won, lost, chosen, transformed —
  never just plot mechanics. Chapters can be as simple as a promenade, but the
  dialogue must carry weight and every scene needs a reason to exist.
- **Fashion and beauty are central, never decoration.** Fashion is a courtship
  mechanic (every choice carries social consequence); dressing-for-the-chapter-climax
  is ritual; Élise's atelier is a story engine; beauty is Rose's instrument of power
  and the game scores taste. Every season's plot needs its fashion spine:
  commissions, fittings, triumph gowns.
- **Floriography is a signature thread** across all seasons — flowers as messages,
  warnings, declarations; a language the story speaks (content briefs define the lexicon).
- **Recurring period set-pieces:** Vauxhall Gardens galas, masquerades (recurring across
  seasons, not one-offs), an Almack's-style subscription assembly with blackballing
  gatekeepers, panoramas, menageries, mummy-unwrapping parties. (Thames frost fair on
  hold pending the 1815/1816 question.)
- **Event/side-game ideas** (Beth): equestrian pass, yacht pass, hot air balloon event —
  as season event passes. Every side game must pass the **Conservatory standard** or it
  doesn't ship.
- **Casting rules:** all suitors are honorable — routes develop through friendship;
  villains are sly, never overt. Rivals are a small recurring roster, not a new face
  every season. Canon: **Major Lord Henry Beaumont** ("Henry Vane" retired).
- The **Gazette** is the judge/narrator brand.

## Architecture

```
Assets/Scripts/Domain/    →  ScandalSeason.Domain (asmdef, noEngineReferences)
                             Plain C#. Merge rules, outfit scoring, economy, energy,
                             season progression, vote tallying. ZERO Unity dependencies.
Assets/Scripts/Runtime/   →  ScandalSeason.Runtime (Unity glue)
                             Thin MonoBehaviours + ScriptableObject definitions.
                             No game logic — everything delegates to Domain.
Assets/Editor/            →  ScandalSeason.Editor (Editor only)
                             JSON → ScriptableObject importers with console validation.
Content/                  →  Versioned, schema-validated JSON (draft 2020-12 schemas
                             in Content/schemas/). Unity never reads this at runtime;
                             the Editor importer converts it to ScriptableObjects.
Tests/                    →  Plain .NET test projects for the Domain core.
                             `dotnet test` runs here AND in CI (GameCI cannot easily
                             unit-test the Domain assembly, so .NET is the source of truth).
```

## Rules for AI agents working in this repo

1. **Never hand-edit Unity scene/prefab YAML or GUIDs.** Scenes and prefabs are
   built in the Unity Editor by Beth. Agents work in `Domain/`, `Runtime/` scripts,
   `Editor/` scripts, and `Content/` JSON only.
2. **Domain has zero Unity dependencies.** If a change needs `UnityEngine`, it does
   not belong in `Domain/` — put it in `Runtime/` as a thin wrapper.
3. **Content is JSON-first.** New items, outfits, scenes, vote events, and passes are
   authored in `Content/*.json`, validated against `Content/schemas/`, and imported
   via **Scandal Season → Import Content JSON** in the Unity Editor.
4. **Weights stay hidden.** `ScoringWeights` must never be serialized into a client
   payload, asset bundle, or log. Category names and scores are public; weights are not.
5. **Iron rule is load-bearing.** Any new reward/catalog entry must include its
   time/engagement path or `CatalogValidator` will reject it.

## Getting started

1. Install **Unity 6** (pin the exact LTS in `.github/workflows/ci.yml` — currently a
   `PLACEHOLDER` version) with iOS Build Support.
2. Open this folder as a Unity project.
3. **Scandal Season → Import Content JSON** to generate ScriptableObjects from
   `Content/*.json` into `Assets/Scripts/Runtime/Generated/`.
4. Run the .NET domain tests: `dotnet test Tests/Domain.Tests` (see below).

## Testing the domain core (no Unity needed)

```bash
# From the repo root. Uses the .NET SDK (installed at ~/workspace/tools/dotnet on the dev VM).
dotnet test Tests/Domain.Tests/ScandalSeason.Domain.Tests.csproj
```

The Domain sources are compiled directly from `Assets/Scripts/Domain/**/*.cs` into a
`netstandard2.1` library, so the tests exercise the exact code Unity compiles.

## Unity tests (GameCI)

- `Assets/Tests/EditMode/` (`ScandalSeason.Tests.EditMode` asmdef, Editor-only) —
  locked-rule assertions compiled against the real Domain assembly inside Unity.
- `Assets/Tests/PlayMode/` (`ScandalSeason.Tests.PlayMode` asmdef) — smoke tests
  proving the thin views initialize with no content loaded.
- `Packages/manifest.json` includes `com.unity.test-framework` so the GameCI
  `unity-test-runner` job finds them. Unity auto-generates `.meta` files for the
  new assemblies on first import — do not hand-create them.

## Content pipeline

- Schemas: `Content/schemas/*.schema.json` (JSON Schema draft 2020-12).
- Samples: `Content/*.json` — `PLACEHOLDER` content, valid against the schemas.
- CI validates every JSON file against its schema with Python `jsonschema`.
- The Editor importer re-validates structurally (required fields, season >= 1 —
  seasons are endless, Book One is 1–10 — pass durations 3–14 days, chain level
  sequences) and surfaces errors in the Unity console via `Debug.LogError`.

## CI (`.github/workflows/ci.yml`)

1. `domain-tests` — `dotnet test` on Ubuntu.
2. `validate-content` — JSON Schema validation of `Content/*.json`.
3. `unity-tests` — GameCI test runner (EditMode + PlayMode).
4. `build-ios` — GameCI exports the iOS Xcode project on Ubuntu; IPA
   signing/archive is a separate macOS step with Beth's Apple credentials.

Required secrets: `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`.
Pin the real Unity 6 version in the workflow before first use.
