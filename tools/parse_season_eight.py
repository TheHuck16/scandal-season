#!/usr/bin/env python3
"""Parse Season Eight chapter .md files (STATUS FULL DRAFT) into Content/scenes-season8.json.

Source of truth: Content/story/season-eight-chapter-*.md and
Content/story/season-eight-chapters-1-3-pilot.md (both STATUS FULL DRAFT).
Output: Content/scenes-season8.json conforming to Content/schemas/scenes-season8.schema.json (v2.1.0).

Season Eight's scene-detailing format differs from Season One's in ways this parser
handles explicitly (all grounded in the chapter sources; nothing invented):

  - Scene headers may carry key-decision markers, beat qualifiers, and title
    suffixes, e.g. "### L7.S13 · ★ KEY DECISION 1/3 — LAVINIA'S ADVICE · ...",
    "### L3.S19 · [F] · ★ KEY DECISION 3/3 · Rose Hartwell",
    "### L30.S30 · [T] · Rose Hartwell — The Mirror Ritual".
  - Type is detected from the header tag OR a Purpose star-marker override
    (case-insensitive): GAZETTE STING, (FINAL/CHAPTER/SEASON) CLIFFHANGER,
    DRESSING RITUAL, FASHION-SELECTION.
  - Key decisions are marked "Purpose: ★ KEY DECISION n/3 — TITLE." or
    "Purpose: Beat N — ★ KEY DECISION n/3 — TITLE."; option lists may be
    prefixed with "> ".
  - Animation lines carry turn declarations and em-dash notes, e.g.
    "*Animation: Shared. · Player turns: 7 (T1–T7).*" or
    '*Animation: Custom "Recalculation" — ... · Player turns: 7 (T1–T7).*'.
  - Turn counts are recorded per scene (playerTurns): declared counts win
    ("(Turns: N)" in Purpose/body, "Player turns: N" in the Animation line);
    otherwise inline markers are counted ((Tn · …), ◇, **[Tn ·]**, *Turn N —*).
    S4 requires a turn count on every scene (declared or counted); a scene with
    none is a hard failure.
  - Dressing rituals may span multiple consecutive [C] scenes (Parts I–V in
    chapters 25–27, Parts I–II in 29–30). Each part carries its own ritual
    object (part label, this part's pins); the occasion brief and the three
    directions live on the part that states them. Validation counts ritual
    GROUPS (one per chapter), not [C] scenes.
  - [F] fashion choices are recorded only where the source lists diegetic
    bullet options; scenes that embed the choice as player turns keep an
    intentionally empty list.

The parser enforces the locked chapter container and fails loudly on violations:
  - exactly 40 scenes per chapter, numbered 1..40
  - exactly 3 marked key decisions per chapter (numbers 1, 2, 3), each with exactly 3 options
  - exactly 1 dressing-ritual GROUP per chapter
  - exactly 1 Gazette sting and 1 cliffhanger per chapter
  - every scene has a Purpose line and an animation call

Turn-band mismatches (ordinary scenes outside 7–10, stings ≠ 1, rituals ≠ 20–21,
ch30 S40 excepted) are reported as warnings, not failures: the design-side
handoff sanctions the exceptions and the prose sources are authoritative.

Custom animation calls outside Season Eight's tentpole chapters (11, 25)
are hard failures — the tentpole-only rule is design-locked.
"""

import json
import re
import sys
from pathlib import Path

S8_TENTPOLES = {16, 30}
SCHEMA_VERSION = "2.1.0"

TYPE_TAGS = {
    "[D]": "dialogue",
    "[F]": "fashion-selection",
    "[C]": "chapter-climax",
    "[T]": "texture",
}

HEADER_RE = re.compile(r"^### L(\d+)\.S(\d+) · (.+)$", re.M)
PURPOSE_RE = re.compile(r"^\*Purpose: (.*)\*$", re.M)
ANIM_RE = re.compile(r"^(?:> )?\*Animation: (Shared|Custom)\b(.*?)\.\*$", re.M)
KD_PURPOSE_RE = re.compile(r"★ KEY DECISION (\d)/3 — ([^.]+?)[.:]")
KD_OPTIONS_HDR_RE = re.compile(r"^(?:> )?\*★ KEY DECISION \d/3 — (.+?):\*$", re.M)
KD_OPTION_RE = re.compile(r"^(?:> )?- \*\*(.+?)\*\* — (.*)$")
TURNS_DECL_RE = re.compile(r"\(?Turns: (\d+)\b")
TURNS_PAREN_RE = re.compile(r"Turns \((\d+)[^)]*\):")  # S3 format: > **Turns (8):** or > **Turns (1 — the reward):**
NARR_TURNS_RE = re.compile(r"(?:Narrative turns|Pins)[^.\n]*?(\d+)\s*(?:pins|turns)?\b", re.I)
PLAYER_TURNS_RE = re.compile(r"Player turns: (\d+)")
TURN_MARKER_RES = [
    re.compile(r"\(T\d+ ·"),          # (T1 · look closer)
    re.compile(r"^> ◇ ", re.M),        # ◇ choice/examine/maneuver lines
    re.compile(r"\*\*\[T\d+ ·"),       # **[T1 · Look]**
    re.compile(r"^\*Turn \d+ —", re.M),# *Turn 1 — Stance:*
    re.compile(r"^> \*Turn \d+ —", re.M),
    re.compile(r"^> \d+\. \[[a-z ]+\]", re.M),  # > 1. [stance]... → Remembered: (chs 19–21, rituals)
    re.compile(r"^> \*Pin \d+ —", re.M), # *Pin 1 — Micro:* (ritual pins as turns)
    re.compile(r"\*\*Pin \d+ \("),      # **Pin 1 (T2 · micro-decision) —**
]


def parse_header(rest):
    """Return (type, participants, beat_qualifier, kd_number, title_suffix).

    The header may embed a key-decision marker, a beat qualifier, and a title
    suffix after the participants. Type detection is refined later by the
    Purpose star-marker override.
    """
    kd_number = None
    title_suffix = None
    kd_m = re.search(r"★ KEY DECISION (\d)/3", rest)
    if kd_m:
        kd_number = int(kd_m.group(1))
        rest = (rest[:kd_m.start()] + rest[kd_m.end():]).strip(" ·—-")
        # A header that carried ONLY the KD marker + participants (e.g.
        # "★ KEY DECISION 1/3 · Rose Hartwell") defaults to a plot beat.
        if rest and not rest.startswith("[") and "plot beat" not in rest.lower() \
                and "sting" not in rest.lower() and "cliffhanger" not in rest.lower():
            tail = rest
            if " — " in tail:
                tail, title_suffix = tail.split(" — ", 1)
            participants = [p.strip() for p in tail.split(", ") if p.strip()]
            return "plot-beat", participants, None, kd_number, title_suffix
    title_suffix = None
    if " — " in rest and not rest.startswith("["):
        pass  # handled below per-tag
    stype, participants, qualifier = None, [], None
    # Uppercase variants seen in ch8: "GAZETTE STING", "FINAL CLIFFHANGER".
    upper = rest.upper()
    if upper == "GAZETTE STING" or upper.startswith("GAZETTE STING · "):
        stype = "gazette-sting"
        tail = rest[len("GAZETTE STING"):].lstrip(" ·")
        participants = [p.strip() for p in tail.split(", ") if p.strip()]
    elif "CLIFFHANGER" in upper:
        stype = "cliffhanger"
        tail = re.sub(r"(?i)^final cliffhanger\s*·\s*", "", rest)
        tail = re.sub(r"(?i)^cliffhanger\s*·\s*", "", tail)
        participants = [p.strip() for p in tail.split(", ") if p.strip()]
    else:
        for tag, mapped in TYPE_TAGS.items():
            if rest == tag or rest.startswith(tag + " · "):
                stype = mapped
                tail = rest[len(tag):].lstrip(" ·") if rest != tag else ""
                # Title suffix: "Rose Hartwell — The Mirror Ritual"
                if " — " in tail:
                    tail, title_suffix = tail.split(" — ", 1)
                participants = [p.strip() for p in tail.split(", ") if p.strip()]
                break
        if stype is None:
            m = re.match(r"^plot beat(?: \(([^)]+)\))?(?: · (.*))?$", rest)
            if m:
                stype = "plot-beat"
                qualifier = m.group(1)
                tail = m.group(2) or ""
                if " — " in tail:
                    tail, title_suffix = tail.split(" — ", 1)
                participants = [p.strip() for p in tail.split(", ") if p.strip()]
    if stype is None:
        raise ValueError(f"Unrecognized scene header: {rest!r}")
    return stype, participants, qualifier, kd_number, title_suffix


def apply_purpose_overrides(stype, purpose):
    """Purpose star-markers (or a leading role declaration) override the header
    type. Continuity references like "Open from L3's cliffhanger" must NOT match."""
    up = purpose.upper()
    has_star = "★" in purpose
    if ("GAZETTE STING" in up and has_star) or up.startswith("GAZETTE STING"):
        return "gazette-sting"
    if ("CLIFFHANGER" in up and has_star) or up.startswith("FINAL CLIFFHANGER") \
            or up.startswith("CLIFFHANGER"):
        return "cliffhanger"
    if "DRESSING RITUAL" in up and has_star or up.startswith("RITUAL —") \
            or up.startswith("RITUAL -"):
        return "chapter-climax"
    if "FASHION-SELECTION" in up and has_star:
        return "fashion-selection"
    return stype


def parse_key_decision(body, header_kd_number):
    """Return (number, title, options) or None.

    Three source-grounded option layouts exist:
      1. Standard: "*★ KEY DECISION n/3 — Title:*" + "- **Opt** — detail" lines
         (option lines may carry a "> " prefix).
      2. Deliberation (ch27): a "(System: **A** / **B** / **C** ...)" line gives
         the option labels; details stay empty (never invented).
      3. Embedded (ch10–12): the Purpose line itself carries
         '— "Title." Opt1 (detail1) / Opt2 (detail2) / Opt3 (detail3).'
    """
    pm = KD_PURPOSE_RE.search(body)
    if not pm and header_kd_number is None:
        return None
    number = int(pm.group(1)) if pm else header_kd_number
    title = pm.group(2).strip() if pm else ""
    title = re.sub(r"\s+", " ", title)
    if title.isupper():
        title = title.title()

    options = []
    hm = KD_OPTIONS_HDR_RE.search(body)
    if hm:
        if not title:
            title = hm.group(1).strip()
        opt_start = hm.end()
    else:
        # S3 variant: no separate options header — options follow the Purpose
        # line directly (after the prose block). Start from the Purpose line.
        pm2 = PURPOSE_RE.search(body)
        opt_start = pm2.end() if pm2 else 0
    for line in body[opt_start:].splitlines():
        om = KD_OPTION_RE.match(line)
        if om:
            options.append({"label": om.group(1).strip(), "detail": om.group(2).strip()})
        elif line.strip() == "" or line.startswith("> *") or line.startswith("> **"):
            continue
        elif options:
            break
    if not options:
        # Layout 2: System line with **A** / **B** / **C**.
        sm = re.search(r"\(System:\s*(\*\*[^*]+\*\*(?:\s*/\s*\*\*[^*]+\*\*)+)", body)
        if sm:
            for lm in re.finditer(r"\*\*([^*]+)\*\*", sm.group(1)):
                options.append({"label": lm.group(1).strip(), "detail": ""})
    if not options and pm:
        # Layout 3: quoted title + slash-separated options in the Purpose line,
        # or "The decision: A / B / C" without a quoted title.
        purpose_line = PURPOSE_RE.search(body).group(1)
        qm = re.match(r'^"([^"]+)"[.?!]?\s*(.+)$', title)
        opt_text = None
        if qm and " / " in qm.group(2):
            title = qm.group(1).strip()
            opt_text = qm.group(2)
        else:
            dm = re.search(r"[Tt]he decision:\s*(.+?)\.\s*$", purpose_line)
            if dm and " / " in dm.group(1):
                opt_text = dm.group(1)
        if opt_text:
            for part in opt_text.split(" / "):
                part = part.strip().rstrip(".")
                om = re.match(r"^(.+?)\s*\(([^)]+)\)$", part)
                if om:
                    options.append({"label": om.group(1).strip(), "detail": om.group(2).strip()})
                elif part:
                    options.append({"label": part, "detail": ""})
    if len(options) != 3:
        raise ValueError(f"Key decision {number} ('{title}') has {len(options)} parseable options; S4 requires exactly 3.")
    return {"number": number, "title": title, "options": options}


def parse_animation(body):
    m = ANIM_RE.search(body)
    if not m:
        raise ValueError("Scene is missing its *Animation:* line.")
    kind, note = m.group(1), (m.group(2) or "").strip()
    # Split off a trailing "· Player turns: N (...)" declaration; the note keeps
    # any bespoke reason (e.g. Custom "Recalculation" — ...).
    turns = None
    tm = PLAYER_TURNS_RE.search(note)
    if tm:
        turns = int(tm.group(1))
        note = PLAYER_TURNS_RE.sub("", note).strip(" ·—-()")
    note = re.sub(r"^[\(\-—\s]+", "", note).strip()
    note = re.sub(r"[\)\s\.]+$", "", note).strip()
    return kind, note, turns, tm.group(0) if tm else None


PROSE_PARA_RE = re.compile(r"^> \*\((.*)\)\*$", re.M)


def parse_prose(body):
    """Extract the complete approved prose: the full scene body including
    narrative paragraphs, turns, and dialogue lines.
    Excludes only Purpose and Animation metadata lines.
    Returns the complete text verbatim — no segmentation or restructuring."""
    lines = []
    for line in body.splitlines():
        s = line.strip()
        if not s:
            continue
        if re.match(r"^\*Purpose:", s):
            continue
        if re.match(r"^\*Animation:", s):
            continue
        lines.append(line)
    return "\n".join(lines).strip()


def parse_turns(body, anim_turns):
    """Return (player_turns, turn_note). Declared counts win; else count markers."""
    m = TURNS_DECL_RE.search(body)
    if m:
        note = m.group(0)
        return int(m.group(1)), note
    # S3 format: > **Turns (8):**
    m = TURNS_PAREN_RE.search(body)
    if m:
        note = m.group(0)
        return int(m.group(1)), note
    # Standalone "*Turns: 8 — all remembered.*" / "*Narrative turns: 8 — ...*" lines.
    m = re.search(r"^\*(?:Narrative turns|Turns): (\d+)[^*]*\.\*$", body, re.M)
    if m:
        return int(m.group(1)), m.group(0).strip("*")
    if anim_turns is not None:
        return anim_turns, "Player turns (Animation line)"
    count = 0
    for rx in TURN_MARKER_RES:
        count += len(rx.findall(body))
    if count:
        return count, f"counted {count} turn markers"
    return None, None


def parse_ritual(body, chapter_occasion=None):
    """Return the ritual dict for a [C] scene part. Brief/directions live on
    whichever part states them; each part carries its own pins.
    chapter_occasion: fallback brief from the chapter header's **Occasion:** line."""
    ritual = {"part": None, "occasionBrief": None, "directions": [],
              "stepsSummary": None, "steps": [], "coinPerDecision": 5, "coinTotal": 0}
    # Part label: Purpose "Part I:" / "Part II —" or header "(Part I)".
    pm = re.search(r"Part ([IVX]+)\b", body)
    if pm:
        ritual["part"] = f"Part {pm.group(1)}"
    # Occasion brief: "> **Occasion brief — Title**" + following italic prose,
    # or "> **OCCASION BRIEF — Title**" (uppercase variant, prose optional).
    bm = re.search(r"\*\*Occasion brief[ —\-]+(.*?)\*\*\s*\n((?:> .*\n?)+)", body)
    if bm:
        title = bm.group(1).strip()
        prose = "\n".join(l.lstrip("> ").strip(" *") for l in bm.group(2).strip().splitlines())
        prose = re.sub(r"\s+", " ", prose).strip()
        ritual["occasionBrief"] = f"{title}. {prose}" if prose else title
    else:
        bm = re.search(r"^> \*\*OCCASION BRIEF[—:\s]+(.+?)\*\*$", body, re.M)
        if bm:
            ritual["occasionBrief"] = bm.group(1).strip()
        else:
            # Purpose-stated briefs: "the brief is VINDICATION."
            bm2 = re.search(r"[Tt]he brief is ([^.]+)\.", body)
            if bm2:
                ritual["occasionBrief"] = f"The brief is {bm2.group(1).strip()}."
    # Chapter-header fallback: the **Occasion:** line is the canonical brief
    # source for S2 rituals (per Beth Sep 27, 2026).
    if not ritual["occasionBrief"] and chapter_occasion:
        ritual["occasionBrief"] = chapter_occasion
    # Directions: "**A. Name**" / "**B. Name**" / "**C. Name**" sections,
    # "> **DIRECTION A — ...**", or "> - **The Quiet Frame** — ..." options.
    dirs = re.findall(r"^\*\*([A-C])\. ([^*]+?)\*\*", body, re.M)
    if dirs:
        ritual["directions"] = [d[1].strip() for d in sorted(dirs)]
    if not ritual["directions"]:
        for dm in re.finditer(r"^> \*\*DIRECTION ([A-C])[—:\s]+(.+?)\*\*(.*)$", body, re.M):
            ritual["directions"].append(f"Direction {dm.group(1)}: {dm.group(2).strip()}{dm.group(3).strip()}")
    if not ritual["directions"]:
        for dm in re.finditer(r"^> - \*\*(.+?)\*\*\s*[—:]\s*(.+)$", body, re.M):
            ritual["directions"].append(f"{dm.group(1).strip()}: {dm.group(2).strip()}")
    # Pins: "**Pin N (Tn · micro-decision) —...**", "*Pin N — Micro:* ...",
    # or "> ◇ **pin** — *slot* — **A/B/C**".
    pins = []
    for m in re.finditer(r"\*\*Pin (\d+) .*?—\s*(.*?)\*\*", body):
        pins.append((int(m.group(1)), m.group(2).strip()))
    if not pins:
        for m in re.finditer(r"^(?:> )?\*Pin (\d+) — (?:Micro|Choice|Stance|Maneuver|Look-closer):\* (.*?)(?: \*Remembered:.*)?$", body, re.M):
            pins.append((int(m.group(1)), m.group(2).strip()))
    if not pins:
        for i, m in enumerate(re.finditer(r"^> ◇ \*\*pin\*\*\s*[—:]\s*(.+)$", body, re.M), 1):
            pins.append((i, m.group(1).strip()))
    pins.sort()
    ritual["steps"] = [f"Pin {n}: {t}" for n, t in pins]
    # Coins: "20 pins × 5 coins = 100 coins" or "5 coins each".
    cm = re.search(r"(\d+)\s+pins?\s*[×x]\s*(\d+)\s+coins?\s*=\s*(\d+)\s+coins?", body)
    if cm:
        ritual["coinPerDecision"] = int(cm.group(2))
        ritual["coinTotal"] = int(cm.group(3))
    else:
        cm2 = re.search(r"(\d+) coins each", body)
        if cm2:
            ritual["coinPerDecision"] = int(cm2.group(1))
    return ritual


def parse_fashion_choices(body):
    choices = []
    for m in re.finditer(r"^(?:> )?- \*\*(.+?)\*\* — (.*)$", body, re.M):
        choices.append({"label": m.group(1).strip(), "detail": m.group(2).strip()})
    return choices


def parse_sting(body):
    # Layout 1: a quoted Bell line, possibly spanning several "> *" lines.
    m = re.search(r'^> \*"((?:[^"]|\n)*?)"\*$', body, re.M)
    if m:
        text = " ".join(l.lstrip("> ").strip(" *") for l in m.group(1).splitlines())
        return re.sub(r"\s+", " ", text).strip()
    # Layout 2: a Gazette editorial — "> **THE GAZETTE — ...**" followed by an
    # italic paragraph (unquoted). The paragraph may not immediately follow the
    # S3 format: the sting is a quoted verdict. It appears in three positions:
    #   1. Standalone line: > *"The Lantern prints heat..."*
    #   2. Inside a parenthetical: > *(The Gazette's sting — ...: "Miss Hartwell..." — Bell)*
    #   3. Mid-paragraph: > *(PAD — Bell's verdict: ... "Miss Hartwell has made..." The reward: ...)*
    # Find the longest quoted string in the S39 body (the verdict).
    candidates = re.findall(r"\"([^\"]{30,})\"", body)
    if candidates:
        # The verdict is typically the longest quoted string
        text = max(candidates, key=len)
        text = re.sub(r"\s+", " ", text).strip()
        return text
    # Fallback for S39s with no quoted verdict (ch20, ch21): the Purpose line
    # carries the verdict summary.
    pm = re.search(r"\*Purpose:\s*(.+?)\*", body)
    if pm:
        text = pm.group(1).strip()
        # Remove turn-count parentheticals
        text = re.sub(r"\s*\(Turns:[^)]+\)", "", text)
        text = re.sub(r"\s+", " ", text).strip()
        if len(text) > 20:
            return text
    # Fallback: S2-style **THE GAZETTE** header (for pilot compatibility)
    m = re.search(r"^> \*\*THE GAZETTE[^\n]*\*\*", body, re.M)
    if m:
        for line in body[m.end():].splitlines():
            if line.startswith("> *") and not line.startswith("> *("):
                text = line.lstrip("> ").strip(" *")
                text = re.sub(r"\s+", " ", text).strip()
                if len(text) > 40:
                    return text
    raise ValueError("Gazette sting text is missing.")


def parse_chapter_file(path):
    text = path.read_text(encoding="utf-8")
    status_m = re.search(r"STATUS: (FULL DRAFT|FULL|PILOT)", text)
    if not status_m:
        raise ValueError(f"{path.name}: no STATUS line.")
    status = status_m.group(1)

    boundaries = [(m.start(), m.group(1), m.group(2))
                  for m in re.finditer(r"^## Chapter (\d+) — \"(.*?)\"", text, re.M)]
    if not boundaries:
        m = re.search(r"^# (?:Scandal Season — )?Season Eight,? Chapter (\d+)\s*[—:]\s*[\"“](.*?)[\"”]", text, re.M)
        if not m:
            raise ValueError(f"{path.name}: no chapter title line.")
        boundaries = [(0, m.group(1), m.group(2))]

    scenes = []
    warnings = []
    for idx, (start, chap_num, chap_title) in enumerate(boundaries):
        chapter = int(chap_num)
        end = boundaries[idx + 1][0] if idx + 1 < len(boundaries) else len(text)
        segment = text[start:end]
        headers = list(HEADER_RE.finditer(segment))
        if len(headers) != 40:
            raise ValueError(f"{path.name} ch{chapter}: {len(headers)} scenes, expected 40.")
        # Chapter-header **Occasion:** line is the canonical brief source for
        # S2 rituals (per Beth Sep 27, 2026). Extract once per chapter.
        chapter_header = segment[:headers[0].start()]
        occasion_m = re.search(r"^>?\s*\*\*Occasion:\*\*\s*(.+?)(?:\s*\*\*[A-Z][^*]*:\*\*|\s*$)", chapter_header, re.M)
        chapter_occasion = occasion_m.group(1).strip() if occasion_m else None
        key_numbers = set()
        ritual_groups = 0
        in_ritual_run = False
        sting_count = cliff_count = 0
        for h in headers:
            lchap, snum, rest = int(h.group(1)), int(h.group(2)), h.group(3)
            if lchap != chapter:
                raise ValueError(f"{path.name}: header L{lchap}.S{snum} inside chapter {chapter}.")
            nxt = segment.find("### L", h.end())
            body = segment[h.end():nxt if nxt != -1 else len(segment)]
            stype, participants, beat_qualifier, header_kd, _ = parse_header(rest)
            pm = PURPOSE_RE.search(body)
            if not pm:
                raise ValueError(f"{path.name} ch{chapter} sc{snum}: missing *Purpose:* line.")
            purpose = pm.group(1).strip()
            stype = apply_purpose_overrides(stype, purpose)
            animation, animation_note, anim_turns, _ = parse_animation(body)
            if animation == "Custom" and chapter not in S8_TENTPOLES:
                raise ValueError(
                    f"{path.name} ch{chapter} sc{snum}: Custom animation outside Season Eight "
                    f"tentpole chapters (11/25) — tentpole-only rule is design-locked.")
            player_turns, turn_note = parse_turns(body, anim_turns)
            if player_turns is None:
                raise ValueError(
                    f"{path.name} ch{chapter} sc{snum}: no turn count (declared or counted). "
                    f"S4 requires turn counts on all 1,200 scenes.")
            prose = parse_prose(body)
            scene = {
                "id": f"s4-c{chapter}-s{snum}",
                "season": 8,
                "chapter": chapter,
                "scene": snum,
                "chapterTitle": chap_title,
                "chapterStatus": status,
                "type": stype,
                "synopsis": purpose,
                "prose": prose,
                "participants": participants,
                "animation": animation,
                "animationNote": animation_note,
                "sourceFile": f"Content/story/{path.name}",
            }
            if player_turns is not None:
                scene["playerTurns"] = player_turns
            if turn_note:
                scene["turnNote"] = turn_note
            if beat_qualifier:
                scene["beatQualifier"] = beat_qualifier
            kd = parse_key_decision(body, header_kd)
            if kd:
                key_numbers.add(kd["number"])
                scene["keyDecision"] = kd
            if stype == "chapter-climax":
                if not in_ritual_run:
                    ritual_groups += 1
                    in_ritual_run = True
                scene["ritual"] = parse_ritual(body, chapter_occasion)
            else:
                in_ritual_run = False
            if stype == "fashion-selection":
                scene["fashionChoices"] = parse_fashion_choices(body)
            if stype == "gazette-sting":
                sting_count += 1
                scene["sting"] = parse_sting(body)
            if stype == "cliffhanger":
                cliff_count += 1
            # Turn-band audit (warnings only; the prose sources are authoritative).
            # Key-decision scenes are sanctioned 3-turn deliberations. Ritual
            # parts are audited as a group (post-pass below), not per part.
            if player_turns is not None and "keyDecision" not in scene \
                    and stype != "chapter-climax":
                band_ok = True
                if stype == "gazette-sting":
                    band_ok = player_turns == 1
                elif stype == "cliffhanger" and not (chapter == 30 and snum == 40):
                    band_ok = 7 <= player_turns <= 10
                elif stype not in ("gazette-sting", "cliffhanger"):
                    band_ok = 7 <= player_turns <= 10 or "deliberation" in purpose.lower()
                if not band_ok:
                    warnings.append(
                        f"{path.name} ch{chapter} sc{snum} ({stype}): {player_turns} turns "
                        f"[{turn_note}] — outside the expected band; recorded as written.")
            scenes.append(scene)
        numbers = sorted(s["scene"] for s in scenes if s["chapter"] == chapter)
        if numbers != list(range(1, 41)):
            raise ValueError(f"{path.name} ch{chapter}: scene numbers are not 1..40.")
        if key_numbers != {1, 2, 3}:
            raise ValueError(f"{path.name} ch{chapter}: key decisions = {sorted(key_numbers)}, expected 1,2,3.")
        if ritual_groups != 1:
            raise ValueError(f"{path.name} ch{chapter}: {ritual_groups} ritual groups, expected 1.")
        if sting_count != 1 or cliff_count != 1:
            raise ValueError(
                f"{path.name} ch{chapter}: stings={sting_count} cliffhangers={cliff_count}, expected 1 each.")
        # Ritual-group turn audit: the GROUP should total ~20 turns.
        ritual_scenes = [s for s in scenes if s["type"] == "chapter-climax" and s["chapter"] == chapter]
        if ritual_scenes and all(s.get("playerTurns") for s in ritual_scenes):
            group_total = sum(s["playerTurns"] for s in ritual_scenes)
            # Multi-part rituals are a sanctioned "authored subdivision" exception;
            # single-part rituals should land at 20–21 turns.
            if len(ritual_scenes) == 1 and not (20 <= group_total <= 21):
                warnings.append(
                    f"{path.name} ch{chapter}: ritual totals {group_total} turns "
                    f"— outside the 20–21 band; recorded as written.")
    return scenes, warnings


def main():
    repo = Path(__file__).resolve().parent.parent
    story_dir = repo / "Content" / "story"
    out_path = repo / "Content" / "scenes-season8.json"
    files = sorted(story_dir.glob("season-eight-chapter-*.md"))
    pilot = story_dir / "season-eight-chapters-1-3-pilot.md"
    if pilot.exists():
        files.append(pilot)
    # Never pick up the beats companion doc.
    files = [f for f in files if "beats" not in f.name]
    if not files:
        sys.exit("No Season Eight chapter .md files found in Content/story/.")

    all_scenes, all_warnings = [], []
    for path in files:
        scenes, warnings = parse_chapter_file(path)
        all_scenes.extend(scenes)
        all_warnings.extend(warnings)

    seen_ids = set()
    for s in all_scenes:
        if s["id"] in seen_ids:
            sys.exit(f"Duplicate scene id: {s['id']}")
        seen_ids.add(s["id"])
    all_scenes.sort(key=lambda s: (s["chapter"], s["scene"]))

    chapters = sorted({s["chapter"] for s in all_scenes})
    if chapters != list(range(1, 31)):
        sys.exit(f"Expected chapters 1..30, found {chapters}.")

    def clean(obj):
        """Strip None values from the JSON output. Empty lists are meaningful
        (e.g. an intentionally empty fashionChoices) and are preserved."""
        if isinstance(obj, dict):
            return {k: clean(v) for k, v in obj.items() if v is not None}
        if isinstance(obj, list):
            return [clean(v) for v in obj]
        return obj

    payload = {
        "schemaVersion": SCHEMA_VERSION,
        "_comment": (
            "Generated by tools/parse_season_eight.py from Season Eight chapter .md files "
            "(STATUS FULL DRAFT). Do not hand-edit; re-run the parser. Prose lives in "
            "Content/story/season-eight-*.md; this file carries the engine-readable structure."
        ),
        "season": 8,
        "scenes": [clean(s) for s in all_scenes],
    }
    out_path.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    for w in all_warnings:
        print(f"WARNING: {w}", file=sys.stderr)
    n_turns = sum(1 for s in all_scenes if "playerTurns" in s)
    print(f"Parsed {len(all_scenes)} scenes across {len(chapters)} chapters -> {out_path.name}")
    print(f"Key decisions: {sum(1 for s in all_scenes if 'keyDecision' in s)} "
          f"({sum(len(s['keyDecision']['options']) for s in all_scenes if 'keyDecision' in s)} options)")
    print(f"Ritual parts: {sum(1 for s in all_scenes if 'ritual' in s)}, "
          f"Fashion beats: {sum(1 for s in all_scenes if s['type'] == 'fashion-selection')}, "
          f"Custom animation: {sum(1 for s in all_scenes if s['animation'] == 'Custom')}")
    print(f"Scenes with turn counts: {n_turns}/{len(all_scenes)}")
    if all_warnings:
        print(f"{len(all_warnings)} turn-band warnings (see above).", file=sys.stderr)


if __name__ == "__main__":
    main()

# NOTE (Sep 29, 2026): Season Eight is a from-scratch rewrite (the Sep 28 drafts
# were lost). There are no accepted turn-band warnings yet; warnings the parser
# emits should be reviewed against the prose before being accepted here.
