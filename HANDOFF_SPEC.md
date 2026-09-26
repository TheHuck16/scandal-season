# Design → Build Handoff Spec (Scandal Season)

Copy/paste the block below to the design agent when Beth's storyboard and design briefs are done. It tells that agent exactly how to package everything Ernest needs to begin building.

---

You are preparing a build handoff for Ernest, the build agent who will implement the Scandal Season game in Unity 6. Beth's storyboard and design briefs are done. Your job: package them into a complete, unambiguous handoff package he can build from directly. Do not write code. Do not create Unity files (no scenes, no prefabs, no YAML — he will reject them).

Deliver the handoff as Markdown files plus plain JSON data files, organized as:

- `00_README.md` — index of the package, what is decided vs. TBD
- `story/` — Book One (the 10-season arc: ruin → restoration → legacy) storyboard:
  major beats per season, with each season's hook opening a door to seasons 11+
  rather than just teasing the next season; Season 1 fully detailed scenes
  (a scene is a flexible beat: dialogue-heavy, fashion-selection, or
  dressing-for-the-chapter-climax)
- `content/` — merge item chains, outfits, characters
- `economy/` — energy, currencies, packs, passes, voting
- `art/` — art direction lock: style references, character look targets

Content requirements (be exhaustive; mark anything undecided TBD — never invent):

1. Merge chains: every item chain with id, name, levels (1..N), and art notes per level.
2. Outfits: every outfit with id, name, garment pieces, scoring categories, rarity, acquisition path (free path / premium / time-only — every premium outfit needs a credible free path), art notes.
3. Characters: full roster with role (rival / suitor / Gazette), arc notes across seasons. Locked: small recurring rival roster that dips in and out across seasons; all suitors honorable unless genuinely villainous; villains sly, never overt. Rose Hartwell's core: rebuilding the estate and earning her place in society.
4. Scenes: for each scene — season/chapter, type (dialogue / fashion-selection / chapter-climax), characters present, purpose, and whether it needs custom animation or can reuse the shared animation set.
5. Economy parameters: energy max, regen rate, energy costs per action; Crown pack tiers with names (increasingly fabulous per tier); coin sources and sinks; event pass (3–14 days) structure and rewards; premium season pass contents; whale starter pack contents (generous but bounded); which rewards are time-only (never purchasable at any price).
6. Daily Vote: cadence, categories shown to players (weights stay hidden — do not invent weights), rewards.
7. Vertical slice acceptance: define what the first playable slice must contain. If Beth hasn't decided, ask her — do not guess.

Locked rules — do not contradict these:

- No ads of any kind, not even rewarded opt-in. IAP only.
- Premium currency is Crowns; soft currency is coins. Never "rubies" or "gems."
- Time always earns the same rewards as money; money strictly expedites. Nothing is paywalled; waiting always restores play.
- Plot is never time-gated; energy is the only throttle.
- No VS mode — voting is side-by-side comparison, votes totaled.
- No avatar unlocks; rewards are outfits, hair, accessories.
- Scoring shows categories only; weights hidden.
- Gazette is judge and narrator, never a paywall or notification feed.
- 10 seasons; story time moves ~1–2 weeks per season.
- Original names and IP only; nothing mirroring Bridgerton plots or Hollywood Merge mechanics/language.

JSON files should be plain data. Ernest validates them against the project's content schemas and will report mismatches back — that is expected and fine.

Before finalizing, list every open question for Beth rather than guessing. Then hand Beth the package; she will pass it to Ernest.
