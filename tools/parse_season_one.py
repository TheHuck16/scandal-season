#!/usr/bin/env python3
"""Parse Beth-signed-off Season One chapter .md files into Content/scenes.json.

Source of truth: Content/story/season-one-chapter-*.md (STATUS FULL) and
Content/story/season-one-chapters-1-3-pilot.md (STATUS PILOT).
Output: Content/scenes.json conforming to Content/schemas/scenes.schema.json (v2.1.0).

The parser enforces the locked chapter format and fails loudly on violations:
  - exactly 40 scenes per chapter, numbered 1..40
  - exactly 3 marked key decisions per chapter (numbers 1, 2, 3)
  - exactly 1 [C] dressing ritual with a parseable occasion brief
  - exactly 1 Gazette sting and 1 cliffhanger per chapter
  - every scene has a Purpose line and an animation call

Custom animation calls outside tentpole chapters (10, 20, 30) are reported as
warnings only -- the source is recorded faithfully; the design-side rule
("Custom is tentpole-only") conflicts with 6 signed-off signature beats and
awaits Beth's ruling. See the builder handoff notes.

Updated Sep 27 2026 for the S1 turn retrofit: parse_ritual now handles the
retrofitted pin-based ritual formats (occasion-brief variants, A/B/C direction
variants, Pin/T-coin turn variants) with best-effort extraction, and the parser
populates playerTurns/turnNote from each purpose line's (Turns: N) annotation.
"""

import json
import re
import sys
from pathlib import Path

TYPE_MAP = {
    "[D]": "dialogue",
    "[F]": "fashion-selection",
    "[C]": "chapter-climax",
    "[T]": "texture",
    "plot beat": "plot-beat",
    "Gazette sting": "gazette-sting",
    "cliffhanger": "cliffhanger",
}

TENTPOLES = {10, 20, 30}
HEADER_RE = re.compile(r"^### L(\d+)\.S(\d+) · (.+)$", re.M)


def parse_header(rest):
    """Return (type, participants, beat_qualifier)."""
    qualifier = None
    for tag, mapped in TYPE_MAP.items():
        if rest == tag:
            return mapped, [], qualifier
        if rest.startswith(tag + " · "):
            parts = rest[len(tag) + 3:].split(", ")
            return mapped, [p.strip() for p in parts if p.strip()], qualifier
    # Qualified plot beats, e.g. "plot beat (villain)".
    m = re.match(r"^plot beat \(([^)]+)\)(?: · (.*))?$", rest)
    if m:
        parts = m.group(2).split(", ") if m.group(2) else []
        return "plot-beat", [p.strip() for p in parts if p.strip()], m.group(1)
    raise ValueError(f"Unrecognized scene header: {rest!r}")


def parse_purpose(body):
    m = re.search(r"^\*Purpose: (.*)\*$", body, re.M)
    if not m:
        raise ValueError("Scene is missing its *Purpose:* line.")
    return m.group(1).strip()


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


def parse_animation(body):
    m = re.search(r"^\*Animation: (Shared|Custom)(?: \((.*)\))?\.\*$", body, re.M)
    if not m:
        raise ValueError("Scene is missing its *Animation:* line.")
    return m.group(1), (m.group(2) or "").strip()


def parse_key_decision(body):
    """Return (number, title, options) or None."""
    m = re.search(r"^\*★ KEY DECISION (\d)/3 — (.+?)[.:]\*$", body, re.M)
    if not m:
        return None
    number, title = int(m.group(1)), m.group(2).strip()
    options = []
    for line in body[m.end():].splitlines():
        om = re.match(r"^- \*\*(.+?)\*\* — (.*)$", line)
        if om:
            options.append({"label": om.group(1).strip(), "detail": om.group(2).strip()})
        elif line.strip() == "" or line.startswith(">"):
            continue
        elif options:
            break
    if not options:
        raise ValueError(f"Key decision {number} has no parseable options.")
    return {"number": number, "title": title, "options": options}


def parse_ritual(body):
    """Return ritual dict for [C] scenes.

    Handles both the pre-retrofit structured format and the retrofitted
    pin-based ritual format (S1 turn retrofit, Sep 2026). The occasion brief
    is required; directions and steps are extracted best-effort across the
    format variants the chapters use. The ContentImporter only requires one
    of brief/directions/steps/part to be present.
    """
    # --- Occasion brief (required) ---
    brief = None
    # Variant 1 (pre-retrofit + most retrofitted): **Occasion brief (published):** "..."
    m = re.search(r"\*\*Occasion brief \(published\):\*\* \"(.*?)\"", body, re.S)
    if m:
        brief = m.group(1).strip()
    # Variant 2: **OCCASION BRIEF:** <text>
    if not brief:
        m = re.search(r"(?m)^> \*\*OCCASION BRIEF:\*\* (.*?)$", body)
        if m:
            brief = m.group(1).strip()
    # Variant 3: **Occasion brief.** <text>
    if not brief:
        m = re.search(r"(?m)^> \*\*Occasion brief\.\*\* (.*?)$", body)
        if m:
            brief = m.group(1).strip()
    # Variant 4: **Occasion brief — <title>** followed by an italic paragraph
    # (the paragraph's closing asterisk is not always present)
    if not brief:
        m = re.search(r"(?m)^> \*\*Occasion brief — [^*]+\*\*\n> (.*?)$", body)
        if m:
            brief = m.group(1).strip().strip("*").strip()
    if not brief or len(brief) < 50:
        raise ValueError("Ritual occasion brief is missing or unparseable.")

    # --- Directions (best-effort) ---
    directions = []
    # Variant A (pre-retrofit): **The 3 directions:** d1 / d2 / d3 on one line
    m = re.search(r"\*\*The 3 directions:\*\* (.*?)(?:\n|$)", body)
    if m:
        directions = [d.strip() for d in m.group(1).split(" / ") if d.strip()]
    # Variant B: **The 3 directions:** then > **A — Name** / > **B — Name** / > **C — Name**
    if not directions:
        found = re.findall(r"(?m)^> \*\*([ABC]) — ([^*]+?)\*\*", body)
        dmap = {a: b.strip() for a, b in found}
        if all(k in dmap for k in "ABC"):
            directions = [f"{k}. {dmap[k]}" for k in "ABC"]
    # Variant C: > **A. Name** / > **B. Name** / > **C. Name**
    if not directions:
        found = re.findall(r"(?m)^> \*\*([ABC])\. ([^*]+?)\*\*", body)
        dmap = {a: b.strip() for a, b in found}
        if all(k in dmap for k in "ABC"):
            directions = [f"{k}. {dmap[k]}" for k in "ABC"]
    # Variant D: > **DIRECTION A — Name** / ...
    if not directions:
        found = re.findall(r"(?m)^> \*\*DIRECTION ([ABC]) — ([^*]+?)\*\*", body)
        dmap = {a: b.strip() for a, b in found}
        if all(k in dmap for k in "ABC"):
            directions = [f"{k}. {dmap[k]}" for k in "ABC"]
    # Variant E: T1 turn "She commits: A — X; B — Y; or C — Z"
    if not directions:
        m = re.search(
            r"She commits: A — (.+?); B — (.+?); or C — (.+?)(?:\.| — |\*)", body
        )
        if m:
            directions = [f"{k}. {m.group(i).strip()}"
                          for i, k in enumerate("ABC", start=1)]
    # Variant F: > - **A · Name** / > - **B · Name** / > - **C · Name**
    if not directions:
        found = re.findall(r"(?m)^> - \*\*([ABC]) · ([^*]+?)\*\*", body)
        dmap = {a: b.strip() for a, b in found}
        if all(k in dmap for k in "ABC"):
            directions = [f"{k}. {dmap[k]}" for k in "ABC"]
    # Note: some rituals (e.g. the ch18 domino) are single-garment and define
    # no A/B/C directions; directions stays empty for those (honest).

    # --- Steps (best-effort) ---
    steps = []
    steps_raw = ""
    coin_each = 5
    coin_total = 0
    # Variant 1 (pre-retrofit): **Decomposed steps (~N, M coins each, ~T coins):** s1→s2→...
    m = re.search(
        r"\*\*Decomposed steps \(~(\d+), (\d+) coins each, ~(\d+) coins\):\*\* (.*)",
        body,
    )
    if m:
        steps_raw = m.group(4).strip()
        steps = [s.strip() for s in steps_raw.split("→") if s.strip()]
        coin_each = int(m.group(2))
        coin_total = int(m.group(3))
    else:
        # Variant 2 (retrofit): > **Pin N (T.. · micro-decision) — Topic:**
        pins = re.findall(
            r"(?m)^> \*\*Pin (\d+) \(T\d+ · [^)]+\) — ([^*]+?):\*\*", body
        )
        if pins:
            pins.sort(key=lambda x: int(x[0]))
            steps = [p[1].strip() for p in pins]
        else:
            # Variant 3 (retrofit): > (T N · <type> — 5 coins) *topic — ...* — remembered:
            turns = re.findall(
                r"(?m)^> \(T(\d+) · [^)]*? — 5 coins\) \*(.+?)\* — remembered:", body
            )
            if turns:
                turns.sort(key=lambda x: int(x[0]))
                steps = [c.split("—")[0].strip() for _, c in turns]
            else:
                # Variant 4 (retrofit): > (T N · <type>) *topic — ...* — remembered:
                turns = re.findall(
                    r"(?m)^> \(T(\d+) · ([^)]+)\) \*(.+?)\* — remembered:", body
                )
                # T1 is the direction choice; pins are T2+.
                turns = [(int(n), c) for n, t, c in turns
                         if "5 coins" not in t and int(n) >= 2]
                if turns:
                    turns.sort(key=lambda x: x[0])
                    steps = [c.split("—")[0].strip() for _, c in turns]
        if steps:
            steps_raw = " → ".join(steps)
            coin_total = coin_each * len(steps)

    return {
        "occasionBrief": brief,
        "directions": directions,
        "stepsSummary": steps_raw,
        "steps": steps,
        "coinPerDecision": coin_each,
        "coinTotal": coin_total,
    }


def parse_player_turns(purpose):
    """Return (playerTurns, turnNote) from the purpose line's (Turns: N) annotation.

    The S1 turn retrofit (Sep 2026) stamps every scene's purpose line with its
    authored turn count, e.g. "(Turns: 8)", "(Turns: 3 — the decision)",
    "(Turns: 21 — the ritual)". Returns (None, None) when absent (never
    fabricated).
    """
    m = re.search(r"\(Turns: (\d+)(?: — ([^)]+))?\)", purpose)
    if not m:
        return None, None
    note = "Declared turn count in the source purpose line."
    if m.group(2):
        note += f" Source qualifier: {m.group(2).strip()}."
    return int(m.group(1)), note


def parse_fashion_choices(body):
    choices = []
    for m in re.finditer(r"^- \*\*(.+?)\*\* — (.*)$", body, re.M):
        choices.append({"label": m.group(1).strip(), "detail": m.group(2).strip()})
    return choices


def parse_sting(body):
    m = re.search(r"^> \*(.*)\*$", body, re.M)
    if not m:
        raise ValueError("Gazette sting text is missing.")
    text = m.group(1).strip()
    # Most stings are a quoted Bell line; ch14's is a deliberate anti-sting ("None — ...").
    if len(text) >= 2 and text.startswith('"') and text.endswith('"'):
        text = text[1:-1].strip()
    return text


def parse_chapter_file(path):
    text = path.read_text(encoding="utf-8")
    status_m = re.search(r"STATUS: (FULL|PILOT)", text)
    if not status_m:
        raise ValueError(f"{path.name}: no STATUS line.")
    status = status_m.group(1)

    # Chapter boundaries: single-chapter files use the H1; the pilot uses ## headers.
    boundaries = [(m.start(), m.group(1), m.group(2))
                  for m in re.finditer(r"^## Chapter (\d+) — \"(.*?)\"", text, re.M)]
    if not boundaries:
        m = re.search(r"^# Scandal Season — Season One, Chapter (\d+): \"(.*?)\"", text, re.M)
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
        key_numbers = set()
        ritual_count = sting_count = cliff_count = 0
        for h in headers:
            lchap, snum, rest = int(h.group(1)), int(h.group(2)), h.group(3)
            if lchap != chapter:
                raise ValueError(f"{path.name}: header L{lchap}.S{snum} inside chapter {chapter}.")
            body = segment[h.end():segment.find("### L", h.end())
                           if segment.find("### L", h.end()) != -1 else len(segment)]
            stype, participants, beat_qualifier = parse_header(rest)
            purpose = parse_purpose(body)
            animation, animation_note = parse_animation(body)
            if animation == "Custom" and chapter not in TENTPOLES:
                warnings.append(
                    f"{path.name} ch{chapter} sc{snum}: Custom animation outside "
                    f"tentpole chapters (10/20/30) -- recorded as written.")
            scene = {
                "id": f"s1-c{chapter}-s{snum}",
                "season": 1,
                "chapter": chapter,
                "scene": snum,
                "chapterTitle": chap_title,
                "chapterStatus": status,
                "type": stype,
                "synopsis": purpose,
                "prose": parse_prose(body),
                "participants": participants,
                "animation": animation,
                "animationNote": animation_note,
                "sourceFile": f"Content/story/{path.name}",
            }
            if beat_qualifier:
                scene["beatQualifier"] = beat_qualifier
            player_turns, turn_note = parse_player_turns(purpose)
            if player_turns is not None:
                scene["playerTurns"] = player_turns
                scene["turnNote"] = turn_note
            kd = parse_key_decision(body)
            if kd:
                key_numbers.add(kd["number"])
                scene["keyDecision"] = kd
            if stype == "chapter-climax":
                ritual_count += 1
                scene["ritual"] = parse_ritual(body)
            if stype == "fashion-selection":
                scene["fashionChoices"] = parse_fashion_choices(body)
            if stype == "gazette-sting":
                sting_count += 1
                scene["sting"] = parse_sting(body)
            if stype == "cliffhanger":
                cliff_count += 1
            scenes.append(scene)
        numbers = sorted(s["scene"] for s in scenes if s["chapter"] == chapter)
        if numbers != list(range(1, 41)):
            raise ValueError(f"{path.name} ch{chapter}: scene numbers are not 1..40.")
        if key_numbers != {1, 2, 3}:
            raise ValueError(f"{path.name} ch{chapter}: key decisions = {sorted(key_numbers)}, expected 1,2,3.")
        if ritual_count != 1:
            raise ValueError(f"{path.name} ch{chapter}: {ritual_count} rituals, expected 1.")
        if sting_count != 1 or cliff_count != 1:
            raise ValueError(
                f"{path.name} ch{chapter}: stings={sting_count} cliffhangers={cliff_count}, expected 1 each.")
    return scenes, warnings


def main():
    repo = Path(__file__).resolve().parent.parent
    story_dir = repo / "Content" / "story"
    out_path = repo / "Content" / "scenes.json"
    files = sorted(story_dir.glob("season-one-chapter-*.md"))
    pilot = story_dir / "season-one-chapters-1-3-pilot.md"
    if pilot.exists():
        files.append(pilot)
    if not files:
        sys.exit("No chapter .md files found in Content/story/.")

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

    payload = {
        "schemaVersion": "2.1.0",
        "_comment": (
            "Generated by tools/parse_season_one.py from Beth-signed-off chapter .md files "
            "(STATUS FULL / PILOT). Do not hand-edit; re-run the parser. Prose lives in "
            "Content/story/*.md; this file carries the engine-readable structure."
        ),
        "season": 1,
        "scenes": all_scenes,
    }
    out_path.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    for w in all_warnings:
        print(f"WARNING: {w}", file=sys.stderr)
    print(f"Parsed {len(all_scenes)} scenes across {len(chapters)} chapters -> {out_path.name}")
    print(f"Key decisions: {sum(1 for s in all_scenes if 'keyDecision' in s)} "
          f"({sum(len(s['keyDecision']['options']) for s in all_scenes if 'keyDecision' in s)} options)")
    print(f"Rituals: {sum(1 for s in all_scenes if 'ritual' in s)}, "
          f"Fashion beats: {sum(1 for s in all_scenes if s['type'] == 'fashion-selection')}, "
          f"Custom animation: {sum(1 for s in all_scenes if s['animation'] == 'Custom')}")
    if all_warnings:
        print(f"{len(all_warnings)} warnings (see above).", file=sys.stderr)


if __name__ == "__main__":
    main()
