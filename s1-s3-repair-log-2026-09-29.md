# Seasons 1–3 — Canon-Drift Repair Log

**Date:** September 29, 2026 · **Ordered by:** Beth ("Address all deviations from the canon.")
**Audit:** `~/workspace/scandal-season/s1-s3-storyboard-audit-2026-09-29.md` (this log does not modify it)
**Method:** three season-fixers fanned out in parallel; coordinator verified every claim with independent greps, ran all three season parsers + the content validator, and reviewed the highest-risk diffs line by line.

**Already fixed before this repair run (not touched):** plot-arc Drummond line 161 (Drummond clause removed), S3 beat-map story duration (now "roughly two weeks"), Lord Edward added to the character roster. Finding 10 (S3 ch13 Bell/forgery joins) is not a deviation — audit recommends leave as-is; left as-is.

**Post-repair validation:** `parse_season_one.py`, `parse_season_two.py`, `parse_season_three.py` all ran clean (scenes JSON regenerated from the edited markdown); `content_validator.py`: **12 ok / 0 failed / 3 skipped** (jsonschema engine).

---

## MAJOR 1 — Remove Pyke from S2 (chapters 25–30): DONE

- **De-naming:** all 69 "Pyke" hits and 11 "Ambrose" hits removed in context across `Content/story/season-two-chapter-25.md` through `season-two-chapter-30.md` (ch25: 31, ch26: 5, ch27: 1, ch28: 1, ch29: 24, ch30: 6). Each hit read individually; replacements: "the buyer," "the Lantern's new proprietor," "the Lantern" (for "Pyke's Lantern"), "the gutter sheet," "the gutter's libel." The gutter-answer hook is intact everywhere — the Network's coin buying the gutter press stands; only the name went.
- **Key decision rename:** `season-two-chapter-25.md:493` (purpose line) and `:503` (header) — "★ KEY DECISION 3/3 — THE GUTTER PURCHASE". Still exactly 3 options (Investigate / Warn Lavinia / Prepare for libel war); only the Investigate option's "Rose follows Pyke" → "Rose follows the buyer" changed.
- **Doctrine passage:** `season-two-chapter-25.md:464–465` — now "She had never seen the Mercury's new proprietor… a purse, never a person. Sly. Never overt." **Deliberate deviation from the brief:** the brief prescribed "the Lantern's new proprietor," but ch25's purchase is the *Mercury* (the Lantern isn't bought until ch28/29); using the Mercury's proprietor keeps canon straight.
- **ch29 S40 pressroom:** `season-two-chapter-29.md:729–736` — header now "### L29.S40 · plot beat · the London Lantern". The figure is unnamed, face kept from the lamps, no dialogue ("A single nod from the shadow — the order, given without a voice."). Headline THE HARTWELL FRAUD kept; cliffhanger function preserved.
- **Mercury orphan (Finding 7):** `season-two-chapter-28.md:535` — one line woven into Bell's warning prose: "The *Mercury*, she added, stayed respectable under its new owner; the gutter work went to the cheaper paper."
- **S3 back-reference sweep:** grepped all S3 chapter files + beats file for "the name you learned" / "the name from Bath" / "last season's purchase" / "since Bath" / "last season" — nothing implies S2-era acquaintance; S3 genuinely introduces him (first at `season-three-chapter-beats.md:127`).
- **Verification:** `grep -c 'Pyke'` and `grep -c 'Ambrose'` = 0 across all S2 chapter files (case-sensitive and insensitive); `grep -c 'Pyke' scenes-season2.json` = 0; all touched chapters still 40 scenes.

## MINOR 3 — S1 reputation labels (Dangerous / Magnanimous / Leverage): DONE

- `Content/story/season-one-chapter-30.md:345–347` — labels formalized on the options: `**Present it whole** — *Dangerous.* …`, `**In pieces** — *Leverage.* …`, `**Let Quill present it** — *Magnanimous.* …` (mapping confirmed against option text: whole→Dangerous, in pieces→Leverage, through Quill→Magnanimous).
- `season-one-chapter-30.md:348` — game-remembers line records "The reputation, earned tonight, is hers too: Dangerous, Leverage, or Magnanimous — as she chose."
- `season-one-chapter-30.md:354` — outcome text: "The ton will have a word for the means by morning — Dangerous, Leverage, or Magnanimous — and the word, like the choice, will be hers."
- **S2 acknowledgment:** added at `season-two-chapters-1-3-pilot.md:57` (Letitia's dialogue, following the pilot's own "whichever you chose" variant convention): "And it has settled on a word for how you did it — Dangerous, Magnanimous, or Leverage, whichever you chose…"
- **Coordinator parser fix:** the fixer's first draft put the labels inside the bold option titles (`**Present it whole** *(Dangerous)*`), which broke `parse_season_one.py`'s option regex (`- **Label** — detail`); restructured to lead the detail text instead. Parser now clean.
- **Verification:** "Magnanimous" 3× in ch30 (was 0); ch30 still 40 scenes, still exactly 3 key decisions.

## MINOR 5 — S1 print-trade notebook payoff: DONE

- `Content/story/season-one-chapter-14.md:761`, at the forgery resolution: "The printer's name in the notebook — underlined twice, still unwitnessed — could stay unwitnessed now. The trade had answered her questions with shut doors and polished smiles; no pressman names a press to a girl with a notebook. Quill's seal had said what no compositor would." Honest version: the inquiry came back empty-handed and Quill's find rendered it moot. No new named entities.

## MINOR 6 — S1 "intercepted letter" seed: DONE

- `Content/story/season-one-chapter-30.md:814`, in the S39 Gazette-sting hook margins (the audit's ~810–813 region), distinct from the drawing-room quote: "And one sentence more: from a letter she had sealed herself, franked and posted — set in Bell's type, word for word. A letter, intercepted. → S2." Preferred S1 slot found; no S2 fallback needed. No new named entities.

## MINOR 8 — S3 Cecilia glimpse: DONE

- `Content/story/season-three-chapter-29.md:354`, inside L29.S18 (the courthouse steps — the vindication crowd): "At the crowd's far edge, a plain black carriage — and beside it, in a modest pelisse, a woman Rose knows at once: Cecilia, at the distance the seasons have kept between them, her shoulders squared to the town that discarded her. Rebuilding, then. Rose lets the distance hold." No contact, no dialogue, no POV driver, no plot consequences.
- **Verification:** "Cecilia" 1× in S3 chapter files (ch29); ch29 still 40 scenes.

## MINOR 9 — S3 Octavia balloon beat: DONE

- `Content/story/season-three-chapter-10.md:253`, end of L10.S16's prose (after Henry leaves her at the supper boxes): Octavia Wren comes to Rose's elbow; they stand together in the crowd — "Bought sky," Octavia says. "Darling, you are missing nothing." / "Then I miss it gladly," says Rose. "A rare unguarded hour — no errands, no watchpoints, the war set down between one breath and the next." Ends on the gesture (her hand finds Rose's sleeve, brief as a brushstroke). No scene or turn-count changes.
- **Verification:** "Octavia" 1× in ch10 (was 0); ch10 still 40 scenes.

## MINOR 12 — "old Marlowe" → Mr. Pettifer: DONE (via two-way rename)

- The task's collision gate fired correctly: ch4 already contained a second Pettifer — **a solicitor** (20 mentions: lines 55, 136, 182, 195–196, 199–201, 206, 208, 212, 214–216, 218, 409, 415, 503, 508, 607), with turn labels, key-decision text, and dialogue. Renaming the compositor to Mr. Pettifer outright would have put two Mr. Pettifers in one chapter.
- **Resolution (coordinator):** the ch4 solicitor is doing Mr. Quill's job — Rose's own solicitor, and Quill is already active in S3 from ch7 onward ("her lawyer," `season-three-chapter-07.md:37`) — so the solicitor became **Mr. Quill** (20 mentions; headers, turn labels "Quill+", choice text, dialogue all renamed in context), and the compositor became **Mr. Pettifer** (37 mentions), matching the beat map's sanctioned chapel father and his ch13/ch15 appearances ("Mr. Pettifer's chapel sets the *Lantern*," ch13:20). No new entities; both names canon.
- Full diff reviewed line by line; every replacement reads naturally ("Quill comes at eleven," "Pettifer's white head," "Pettifer's 'this way, miss'").
- **Verification:** `grep -c 'Marlowe'` = 0 in ch4 and across all S3 files; `grep -c 'Quill'` = 20 in ch4; `grep -c 'Pettifer'` = 37 in ch4; ch4 still 40 scenes.

---

## Deliberately left

- **Finding 10** (S3 ch13 Bell/forgery joins): not a deviation per the audit — left as-is per instructions.
- **S2 ch25:426 "a name that the trade whispered"** and **ch25:676 "The buyer's name, still a whisper"**: left — the trade whispers a name the reader never hears; consistent with the unnamed-buyer doctrine, no canon broken.
- **"Ambrose Pyke's *London Lantern*"** in `season-three-chapter-beats.md:7`: left — S3, where he legitimately exists.
- **Audit report** (`s1-s3-storyboard-audit-2026-09-29.md`): not modified. Note for the record: its finding 12 did not catch the ch4 solicitor-Pettifer collision; the collision is resolved above regardless.
