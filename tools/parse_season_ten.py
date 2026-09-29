#!/usr/bin/env python3
"""Parse Season Ten chapter .md files (STATUS FULL DRAFT) into Content/scenes-season10.json.

Source of truth: Content/story/season-ten-chapter-*.md (chapters 4-30) and
Content/story/season-ten-chapters-1-3-pilot.md (chapters 1-3), all STATUS FULL DRAFT.
Output: Content/scenes-season10.json conforming to Content/schemas/scenes-season10.schema.json (v2.1.0).

Season Ten's scene-detailing format (grounded in the chapter sources; nothing invented):

  - Scene headers: "### LNN.SN · <tags> · <participants>" where tags are [T], [D],
    [F], [C], or "★ KEY DECISION n/3"; S39 headers carry "Gazette sting",
    S40 headers carry "cliffhanger".
  - Type is refined by Purpose star-marker overrides (case-insensitive):
    GAZETTE STING, (FINAL) CLIFFHANGER, DRESSING RITUAL, FASHION-SELECTION.
  - Key decisions: Purpose "★ KEY DECISION n/3 — TITLE (how/which...). ... (Turns: 3 — the decision)";
    options follow a "*★ KEY DECISION n/3 — Title:*" line as "- **Label** — *remembered note.* detail".
  - Animation lines: "*Animation: Shared.*", "*Animation: Shared — note...*",
    or "*Animation: **Custom — the moment:** note...*" on tentpole chapters only
    (Season Ten tentpoles: chapters 5, 8, 29).
  - Turn counts: declared "(Turns: N)" in the Purpose line wins; a scene with no
    declared count is a hard failure (Season Ten declares every scene's turns).
  - Dressing rituals: exactly one [C] scene per chapter, always at S15, with an
    occasion brief, three A/B/C directions, twenty 5-coin pins (100 coins), and
    21 declared turns.

The parser enforces the locked chapter container and fails loudly on violations:
  - exactly 40 scenes per chapter, numbered 1..40
  - exactly 3 marked key decisions per chapter (numbers 1, 2, 3), each with exactly 3 options
  - exactly 1 dressing ritual per chapter, at S15
  - exactly 1 Gazette sting (S39) and 1 cliffhanger (S40) per chapter
  - every scene has a Purpose line, an animation call, and a declared turn count

Turn-band mismatches (ordinary scenes outside 7-10, key decisions != 3,
rituals != 21, stings != 1, cliffhangers outside 7-10) are reported as warnings,
not failures: the prose sources are authoritative.

Custom animation calls outside Season Ten's tentpole chapters (5, 8, 29)
are hard failures — the tentpole-only rule is design-locked.
"""

import json
import re
import sys
from pathlib import Path

S10_TENTPOLES = {5, 8, 29}
SCHEMA_VERSION = "2.1.0"
RITUAL_SCENE = 15
STING_SCENE = 39
CLIFFHANGER_SCENE = 40

TYPE_TAGS = {
    "[D]": "dialogue",
    "[F]": "fashion-selection",
    "[C]": "chapter-climax",
    "[T]": "texture",
}

HEADER_RE = re.compile(r"^### L(\d+)\.S(\d+) · (.+)$", re.M)
PURPOSE_RE = re.compile(r"^\*Purpose: (.*)\*$", re.M)
# S10 animation lines: "*Animation: Shared.*", "*Animation: Shared — note...*",
# "*Animation: **Custom — the moment:** note...*".
ANIM_RE = re.compile(r"^\*Animation: \*{0,2}(Shared|Custom)\b(.*?)\.\*$", re.M)
KD_PURPOSE_RE = re.compile(r"★ KEY DECISION (\d)/3 — ([^.(:]+?)(?:\s*\(|[.:])")
KD_OPTIONS_HDR_RE = re.compile(r"^(?:> )?\*★ KEY DECISION \d/3 — (.+?):\*$", re.M)
KD_OPTION_RE = re.compile(r"^(?:> )?- \*\*(.+?)\*\* — (.*)$")
TURNS_DECL_RE = re.compile(r"\(?Turns: (\d+)\b")
OCCASION_BRIEF_RE = re.compile(r"^> \*\*Occasion brief — (.+?):\*\*\s*\"(.*)\"\s*$", re.M)
RITUAL_DIR_RE = re.compile(r"^(?:> )?\*\*([ABC])\. ([^*]+?)\*\*(.*)$", re.M)
PIN_RE = re.compile(r"\*\*Pin (\d+) .*?—\s*(.*?)\*\*")
COINS_RE = re.compile(r"(\d+)\s+pins?\s*[×x]\s*(\d+)\s+coins?\s*=\s*(\d+)\s+coins?")


def parse_header(rest):
    """Return (type, participants, beat_qualifier, kd_number, title_suffix)."""
    kd_number = None
    title_suffix = None
    kd_m = re.search(r"★ KEY DECISION (\d)/3", rest)
    if kd_m:
        kd_number = int(kd_m.group(1))
        rest = (rest[:kd_m.start()] + rest[kd_m.end():]).strip(" ·—-")
        # A header that carried ONLY the KD marker + participants (e.g.
        # "★ KEY DECISION 1/3 · Rose (alone)") defaults to a plot beat.
        if rest and not rest.startswith("[") and "plot beat" not in rest.lower() \
                and "sting" not in rest.lower() and "cliffhanger" not in rest.lower():
            tail = rest
            if " — " in tail:
                tail, title_suffix = tail.split(" — ", 1)
            participants = [p.strip() for p in tail.split(", ") if p.strip()]
            return "plot-beat", participants, None, kd_number, title_suffix
    upper = rest.upper()
    if upper == "GAZETTE STING" or upper.startswith("GAZETTE STING · "):
        tail = rest[len("GAZETTE STING"):].lstrip(" ·")
        participants = [p.strip() for p in tail.split(", ") if p.strip()]
        return "gazette-sting", participants, None, kd_number, title_suffix
    if "CLIFFHANGER" in upper:
        tail = re.sub(r"(?i)^final cliffhanger\s*·\s*", "", rest)
        tail = re.sub(r"(?i)^cliffhanger\s*·\s*", "", tail)
        participants = [p.strip() for p in tail.split(", ") if p.strip()]
        return "cliffhanger", participants, None, kd_number, title_suffix
    stype, participants, qualifier = None, [], None
    for tag, mapped in TYPE_TAGS.items():
        if rest == tag or rest.startswith(tag + " · "):
            stype = mapped
            tail = rest[len(tag):].lstrip(" ·") if rest != tag else ""
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
    """Purpose star-markers override the header type (case-insensitive)."""
    up = purpose.upper()
    has_star = "★" in purpose
    if ("GAZETTE STING" in up and has_star) or up.startswith("GAZETTE STING"):
        return "gazette-sting"
    if ("CLIFFHANGER" in up and has_star) or up.startswith("FINAL CLIFFHANGER") \
            or up.startswith("CLIFFHANGER"):
        return "cliffhanger"
    if ("DRESSING RITUAL" in up and has_star) or up.startswith("RITUAL —") \
            or up.startswith("RITUAL -"):
        return "chapter-climax"
    if "FASHION-SELECTION" in up and has_star:
        return "fashion-selection"
    return stype


def parse_key_decision(body, header_kd_number):
    """Return (number, title, options) or None. Options follow the
    "*★ KEY DECISION n/3 — Title:*" header as "- **Label** — detail" lines."""
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
    if len(options) != 3:
        raise ValueError(
            f"Key decision {number} ('{title}') has {len(options)} parseable options; "
            f"Season Ten requires exactly 3.")
    return {"number": number, "title": title, "options": options}


def parse_animation(body):
    m = ANIM_RE.search(body)
    if not m:
        raise ValueError("Scene is missing its *Animation:* line.")
    kind, note = m.group(1), (m.group(2) or "")
    # Clean bold-markup residue from "**Custom — the moment:**" lines and
    # split off any trailing turn declaration (not used in S10; turns are
    # declared in the Purpose line).
    note = note.replace("**", "").strip()
    note = re.sub(r"^[—\-–\s:]+", "", note).strip()
    note = re.sub(r"[\s.]+$", "", note).strip()
    return kind, note


def parse_prose(body):
    """The complete scene body verbatim, excluding only Purpose and Animation
    metadata lines."""
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


def parse_ritual(body):
    """The single-part S15 dressing ritual: occasion brief, three A/B/C
    directions, twenty 5-coin pins."""
    ritual = {"occasionBrief": None, "directions": [], "steps": [],
              "coinPerDecision": 5, "coinTotal": 0}
    bm = OCCASION_BRIEF_RE.search(body)
    if not bm:
        raise ValueError("Ritual scene is missing its '> **Occasion brief — ...:**' line.")
    ritual["occasionBrief"] = f"{bm.group(1).strip()}. {bm.group(2).strip()}"
    for dm in RITUAL_DIR_RE.finditer(body):
        rest = dm.group(3).strip()
        ritual["directions"].append(
            f"{dm.group(1)}. {dm.group(2).strip()}" + (f" {rest}" if rest else ""))
    if len(ritual["directions"]) != 3:
        raise ValueError(f"Ritual has {len(ritual['directions'])} directions (!= 3).")
    pins = [(int(m.group(1)), m.group(2).strip()) for m in PIN_RE.finditer(body)]
    if len(pins) != 20:
        raise ValueError(f"Ritual has {len(pins)} pins (!= 20).")
    pins.sort()
    ritual["steps"] = [f"Pin {n}: {t}" for n, t in pins]
    cm = COINS_RE.search(body)
    if not cm:
        raise ValueError("Ritual is missing its pins × coins declaration.")
    ritual["coinPerDecision"] = int(cm.group(2))
    ritual["coinTotal"] = int(cm.group(3))
    return ritual


def parse_fashion_choices(body):
    choices = []
    for m in re.finditer(r"^(?:> )?- \*\*(.+?)\*\* — (.*)$", body, re.M):
        choices.append({"label": m.group(1).strip(), "detail": m.group(2).strip()})
    return choices


def parse_sting(body):
    # A quoted verdict on a single "> *" line.
    m = re.search(r'^> \*"((?:[^"]|\n)*?)"\*$', body, re.M)
    if m:
        text = " ".join(l.lstrip("> ").strip(" *") for l in m.group(1).splitlines())
        return re.sub(r"\s+", " ", text).strip()
    # Fallback: the longest quoted string in the S39 body.
    candidates = re.findall(r"\"([^\"]{30,})\"", body)
    if candidates:
        return re.sub(r"\s+", " ", max(candidates, key=len)).strip()
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
        m = re.search(r"^# (?:Scandal Season — )?Season Ten,? Chapter (\d+)\s*[—:]\s*[\"“](.*?)[\"”]", text, re.M)
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
        ritual_scenes = []
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
            animation, animation_note = parse_animation(body)
            if animation == "Custom" and chapter not in S10_TENTPOLES:
                raise ValueError(
                    f"{path.name} ch{chapter} sc{snum}: Custom animation outside Season Ten "
                    f"tentpole chapters (5/8/29) — tentpole-only rule is design-locked.")
            tm = TURNS_DECL_RE.search(body)
            if not tm:
                raise ValueError(
                    f"{path.name} ch{chapter} sc{snum}: no declared turn count '(Turns: N)'. "
                    f"Season Ten requires a turn count on all 1,200 scenes.")
            player_turns = int(tm.group(1))
            turn_note = tm.group(0)
            prose = parse_prose(body)
            scene = {
                "id": f"s10-c{chapter}-s{snum}",
                "season": 10,
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
                "playerTurns": player_turns,
                "turnNote": turn_note,
            }
            if beat_qualifier:
                scene["beatQualifier"] = beat_qualifier
            kd = parse_key_decision(body, header_kd)
            if kd:
                key_numbers.add(kd["number"])
                scene["keyDecision"] = kd
            if stype == "chapter-climax":
                ritual_scenes.append(snum)
                scene["ritual"] = parse_ritual(body)
            if stype == "fashion-selection":
                scene["fashionChoices"] = parse_fashion_choices(body)
            if stype == "gazette-sting":
                sting_count += 1
                scene["sting"] = parse_sting(body)
            if stype == "cliffhanger":
                cliff_count += 1
            # Turn-band audit (warnings only; the prose sources are authoritative).
            band_ok = True
            if stype == "gazette-sting":
                band_ok = player_turns == 1
            elif stype == "chapter-climax":
                band_ok = player_turns == 21
            elif "keyDecision" in scene:
                band_ok = player_turns == 3
            elif stype == "cliffhanger":
                band_ok = 7 <= player_turns <= 10
            else:
                band_ok = 7 <= player_turns <= 10
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
        if ritual_scenes != [RITUAL_SCENE]:
            raise ValueError(
                f"{path.name} ch{chapter}: ritual scenes = {ritual_scenes}, expected exactly [15].")
        stings = [s["scene"] for s in scenes if s["type"] == "gazette-sting" and s["chapter"] == chapter]
        cliffs = [s["scene"] for s in scenes if s["type"] == "cliffhanger" and s["chapter"] == chapter]
        if stings != [STING_SCENE] or cliffs != [CLIFFHANGER_SCENE]:
            raise ValueError(
                f"{path.name} ch{chapter}: stings at {stings} (expected [39]), "
                f"cliffhangers at {cliffs} (expected [40]).")
    return scenes, warnings


def main():
    repo = Path(__file__).resolve().parent.parent
    story_dir = repo / "Content" / "story"
    out_path = repo / "Content" / "scenes-season10.json"
    files = sorted(story_dir.glob("season-ten-chapter-*.md"))
    pilot = story_dir / "season-ten-chapters-1-3-pilot.md"
    if pilot.exists():
        files.append(pilot)
    if not files:
        sys.exit("No Season Ten chapter .md files found in Content/story/.")

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
        """Strip None values and empty-string notes from the JSON output.
        Empty lists are meaningful and are preserved."""
        if isinstance(obj, dict):
            return {k: clean(v) for k, v in obj.items() if v is not None and v != ""}
        if isinstance(obj, list):
            return [clean(v) for v in obj]
        return obj

    payload = {
        "schemaVersion": SCHEMA_VERSION,
        "_comment": (
            "Generated by tools/parse_season_ten.py from Season Ten chapter .md files "
            "(STATUS FULL DRAFT). Do not hand-edit; re-run the parser. Prose lives in "
            "Content/story/season-ten-*.md; this file carries the engine-readable structure."
        ),
        "season": 10,
        "scenes": [clean(s) for s in all_scenes],
    }
    out_path.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    for w in all_warnings:
        print(f"WARNING: {w}", file=sys.stderr)
    n_kd = sum(1 for s in all_scenes if "keyDecision" in s)
    n_opt = sum(len(s["keyDecision"]["options"]) for s in all_scenes if "keyDecision" in s)
    n_turns = sum(1 for s in all_scenes if "playerTurns" in s)
    print(f"Parsed {len(all_scenes)} scenes across {len(chapters)} chapters -> {out_path.name}")
    print(f"Key decisions: {n_kd} ({n_opt} options)")
    print(f"Rituals: {sum(1 for s in all_scenes if 'ritual' in s)}, "
          f"Fashion beats: {sum(1 for s in all_scenes if s['type'] == 'fashion-selection')}, "
          f"Stings: {sum(1 for s in all_scenes if s['type'] == 'gazette-sting')}, "
          f"Cliffhangers: {sum(1 for s in all_scenes if s['type'] == 'cliffhanger')}, "
          f"Custom animation: {sum(1 for s in all_scenes if s['animation'] == 'Custom')}")
    print(f"Scenes with turn counts: {n_turns}/{len(all_scenes)}")
    if all_warnings:
        print(f"{len(all_warnings)} turn-band warnings (see above).", file=sys.stderr)


if __name__ == "__main__":
    main()
