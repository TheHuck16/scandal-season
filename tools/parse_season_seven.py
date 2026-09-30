#!/usr/bin/env python3
"""Parse Season Seven chapter .md files (FULL DRAFT, awaiting Beth review) into Content/scenes-season7.json.

Source of truth: ~/workspace/season-seven-chapters/season-seven-chapter-*.md (STATUS FULL DRAFT).
Output: Content/scenes-season7.json conforming to Content/schemas/scenes-season7.schema.json.

Season Seven's scene format (grounded in the full-draft chapters 16-20; nothing invented):

  - Scene headers: "### LNN.SN · <type> · <participants>" where type is one of
    "plot beat", "[D]", "[F]", "[C]", "key decision", "ritual",
    "gazette sting", "cliffhanger".
  - Key decisions: type "key decision"; Purpose carries "★ KEY DECISION n/3 — TITLE.";
    options follow a "*★ KEY DECISION n/3 — Title:*" line as "- **Label** — *Stat+; flavor.* detail".
  - [F] fashion selections: a "*Choices (no coin cost; the game remembers):*" line
    followed by "- **Label**: description" bullets.
  - Turns: "> **Turns (N):**" declares the count; turn lines look like
    "> 1. [examine]text → Noted: ...;". Declared counts win; otherwise turn
    lines are counted.
  - Rituals sit at S30 with 21 turns, three A/B/C directions, and 20 x 5-coin pins.
  - Gazette stings sit at S35 (1 turn); cliffhangers at S40 (1 turn).
  - Animation: "*Animation: Shared.*", or "*Animation: Custom (Name).*" on tentpoles
    (Season Seven tentpoles: chapters 10, 20, 30).

The parser enforces the locked chapter container and fails loudly on violations:
  - exactly 40 scenes per chapter, numbered 1..40
  - exactly 3 key decisions per chapter (numbers 1, 2, 3), each with 3 options
  - exactly 6 [F] fashion selections per chapter, each with 3 choices
  - exactly 1 ritual (S30), 1 gazette sting (S35), 1 cliffhanger (S40) per chapter
  - every scene has a Purpose line and an animation call

Turn-band mismatches (ordinary scenes outside 3-7, stings/cliffhangers != 1,
rituals != 21) are reported as warnings, not failures: the prose sources are
authoritative.

Custom animation calls outside Season Seven's tentpole chapters (10, 20, 30)
are hard failures.
"""

import json
import re
import sys
from pathlib import Path

S7_TENTPOLES = {10, 20, 30}
SCHEMA_VERSION = "2.1.0"

CHAPTERS_DIR = Path.home() / "workspace" / "season-seven-chapters"
PROJECT_DIR = Path(__file__).resolve().parents[1]
OUT_PATH = PROJECT_DIR / "Content" / "scenes-season7.json"

HEADER_RE = re.compile(r"^### L(\d+)\.S(\d+) · (.+)$", re.M)
CHAPTER_TITLE_RE = re.compile(r'^## Chapter (\d+) — "(.+)"', re.M)
STATUS_RE = re.compile(r"^\*\*STATUS: ([A-Z][A-Z ]*?)\*\*", re.M)
# Schema enum for chapterStatus is FULL/PILOT only; a full-length draft maps to FULL.
STATUS_MAP = {"FULL": "FULL", "FULL DRAFT": "FULL", "PILOT": "PILOT"}
PURPOSE_RE = re.compile(r"^\*Purpose: (.*)\*$", re.M)
PROSE_PARA_RE = re.compile(r"^> \*\((.*?)\)\*$", re.M | re.S)
ANIM_RE = re.compile(r"^\*Animation: (Shared|Custom)(?: \((.+?)\))?\.\*$", re.M)
KD_PURPOSE_RE = re.compile(r"★ KEY DECISION (\d)/3 — ([A-Z][A-Z .',£0-9]*[A-Z'0-9])[:.] ")
KD_OPTIONS_HDR_RE = re.compile(r"^\*★ KEY DECISION \d/3 — (.+?):\*$", re.M)
KD_OPTION_RE = re.compile(r"^- \*\*(.+?)\*\* — (.*)$", re.M)
F_CHOICES_HDR_RE = re.compile(r"^\*Choices \(no coin cost; the game remembers\):\*$", re.M)
F_OPTION_RE = re.compile(r"^- \*\*(.+?)\*\*\s*[:—]\s+(.*)$", re.M)
TURNS_DECL_RE = re.compile(r"^\> \*\*Turns \((\d+)", re.M)
TURN_LINE_RE = re.compile(r"^> \d+\. \[[a-z ]+\]", re.M)
RITUAL_DIR_RE = re.compile(r"^> \*\*([ABC])\. (.+?)\*\*", re.M)
RITUAL_FIELD_RE = re.compile(r"^(Motif|Thread|Colorway|Accessories): (.*)$", re.M)

TYPE_MAP = {
    "plot beat": "plot-beat",
    "[d]": "dialogue",
    "[f]": "fashion-selection",
    "[c]": "chapter-climax",
    "key decision": "plot-beat",
    "ritual": "ritual",
    "gazette sting": "gazette-sting",
    "cliffhanger": "cliffhanger",
}


def parse_file(path):
    text = path.read_text(encoding="utf-8")
    ch_m = CHAPTER_TITLE_RE.search(text)
    if not ch_m:
        raise ValueError(f"{path.name}: no chapter title line")
    chapter = int(ch_m.group(1))
    chapter_title = ch_m.group(2)
    st_m = STATUS_RE.search(text)
    chapter_status = STATUS_MAP.get(st_m.group(1).strip(), "UNKNOWN") if st_m else "UNKNOWN"

    parts = re.split(r"(?m)^(?=### L\d+\.S\d+ )", text)
    scenes = []
    for part in parts:
        h = HEADER_RE.match(part)
        if not h:
            continue
        ch_no, sc_no, rest = int(h.group(1)), int(h.group(2)), h.group(3)
        if ch_no != chapter:
            raise ValueError(f"{path.name}: header chapter {ch_no} != {chapter}")
        # split type from participants on " · "
        segs = [s.strip() for s in rest.split(" · ")]
        raw_type = segs[0].lower()
        participants = []
        for s in segs[1:]:
            participants.extend([p.strip() for p in s.split(", ") if p.strip()])
        stype = TYPE_MAP.get(raw_type)
        if stype is None:
            raise ValueError(f"{path.name} S{sc_no}: unknown scene type {raw_type!r}")

        purpose_m = PURPOSE_RE.search(part)
        if not purpose_m:
            raise ValueError(f"{path.name} S{sc_no}: no Purpose line")
        purpose = purpose_m.group(1).strip()

        prose_paras = PROSE_PARA_RE.findall(part)
        prose = "\n".join(f"> *({p})*" for p in prose_paras)

        anim_m = ANIM_RE.search(part)
        if not anim_m:
            raise ValueError(f"{path.name} S{sc_no}: no Animation line")
        animation = anim_m.group(1)
        animation_note = anim_m.group(2) or ""
        if animation == "Custom" and chapter not in S7_TENTPOLES:
            raise ValueError(
                f"{path.name} S{sc_no}: Custom animation outside tentpoles {sorted(S7_TENTPOLES)}"
            )

        turns = None
        td = TURNS_DECL_RE.search(part)
        if td:
            turns = int(td.group(1))
        else:
            n_lines = len(TURN_LINE_RE.findall(part))
            turns = n_lines if n_lines else None

        scene = {
            "id": f"s7-c{chapter}-s{sc_no}",
            "season": 7,
            "chapter": chapter,
            "scene": sc_no,
            "chapterTitle": chapter_title,
            "chapterStatus": chapter_status,
            "type": stype,
            "synopsis": purpose,
            "prose": prose,
            "participants": participants,
            "animation": animation,
            "sourceFile": f"season-seven-chapters/{path.name}",
        }
        if animation_note:
            scene["animationNote"] = animation_note
        if turns is not None:
            scene["playerTurns"] = turns

        is_kd = raw_type == "key decision"
        if is_kd:
            kd_m = KD_PURPOSE_RE.search(purpose)
            if not kd_m:
                raise ValueError(f"{path.name} S{sc_no}: key decision without ★ marker in Purpose")
            hdr = KD_OPTIONS_HDR_RE.search(part)
            opts = KD_OPTION_RE.findall(part)
            if len(opts) < 3:
                raise ValueError(f"{path.name} S{sc_no}: key decision has {len(opts)} options (< 3)")
            scene["keyDecision"] = {
                "number": int(kd_m.group(1)),
                "title": kd_m.group(2).strip(),
                "options": [{"label": l.strip(), "detail": d.strip()} for l, d in opts[:3]],
            }

        if raw_type == "[f]":
            if not F_CHOICES_HDR_RE.search(part):
                raise ValueError(f"{path.name} S{sc_no}: [F] scene without Choices line")
            fopts = F_OPTION_RE.findall(part)
            if len(fopts) < 3:
                raise ValueError(f"{path.name} S{sc_no}: [F] scene has {len(fopts)} choices (< 3)")
            scene["fashionChoices"] = [
                {"label": l.strip(), "detail": d.strip()} for l, d in fopts[:3]
            ]

        if raw_type == "ritual":
            dirs = []
            for dm in RITUAL_DIR_RE.finditer(part):
                letter, name = dm.group(1), dm.group(2).strip()
                dirs.append(f"{letter}. {name}")
            if len(dirs) != 3:
                raise ValueError(f"{path.name} S{sc_no}: ritual has {len(dirs)} directions (!= 3)")
            ritual_obj = {"directions": dirs, "coinPerDecision": 5, "coinTotal": 100}
            ob_m = re.search(r'^\> \*\*Occasion brief \(published\):\*\* "(.*)"$', part, re.M)
            if ob_m:
                ritual_obj["occasionBrief"] = ob_m.group(1)
            scene["ritual"] = ritual_obj

        if raw_type == "gazette sting":
            sting_m = re.search(r'^> \*\*"(.+)"\*\*$', part, re.M)
            scene["sting"] = sting_m.group(1) if sting_m else purpose
        scenes.append(scene)

    # container enforcement
    if len(scenes) != 40:
        raise ValueError(f"{path.name}: {len(scenes)} scenes (!= 40)")
    nums = sorted(s["scene"] for s in scenes)
    if nums != list(range(1, 41)):
        raise ValueError(f"{path.name}: scene numbers not 1..40: {nums[:5]}...")
    kds = [s for s in scenes if "keyDecision" in s]
    if len(kds) != 3:
        raise ValueError(f"{path.name}: {len(kds)} key decisions (!= 3)")
    if sorted(s["keyDecision"]["number"] for s in kds) != [1, 2, 3]:
        raise ValueError(f"{path.name}: key decision numbers not 1,2,3")
    fs = [s for s in scenes if s["type"] == "fashion-selection"]
    if len(fs) != 6:
        raise ValueError(f"{path.name}: {len(fs)} [F] scenes (!= 6)")
    rits = [s for s in scenes if s["type"] == "ritual"]
    if len(rits) != 1 or rits[0]["scene"] != 30:
        raise ValueError(f"{path.name}: ritual not exactly one at S30")
    stings = [s for s in scenes if s["type"] == "gazette-sting"]
    if len(stings) != 1 or stings[0]["scene"] != 35:
        raise ValueError(f"{path.name}: sting not exactly one at S35")
    cliffs = [s for s in scenes if s["type"] == "cliffhanger"]
    if len(cliffs) != 1 or cliffs[0]["scene"] != 40:
        raise ValueError(f"{path.name}: cliffhanger not exactly one at S40")

    # turn-band warnings (non-fatal)
    warnings = []
    for s in scenes:
        t = s.get("playerTurns")
        if t is None:
            warnings.append(f"{path.name} S{s['scene']}: no turn count")
            continue
        typ = s["type"]
        if typ in ("gazette-sting", "cliffhanger"):
            if t != 1:
                warnings.append(f"{path.name} S{s['scene']}: {typ} has {t} turns (!= 1)")
        elif typ == "ritual":
            if t != 21:
                warnings.append(f"{path.name} S{s['scene']}: ritual has {t} turns (!= 21)")
        elif "keyDecision" in s:
            if t != 3:
                warnings.append(f"{path.name} S{s['scene']}: key decision has {t} turns (!= 3)")
        elif not (3 <= t <= 7):
            warnings.append(f"{path.name} S{s['scene']}: ordinary scene has {t} turns (band 3-7)")
    return chapter, scenes, warnings


def main():
    files = sorted(CHAPTERS_DIR.glob("season-seven-chapter-*.md"))
    if not files:
        print(f"no chapter files in {CHAPTERS_DIR}", file=sys.stderr)
        return 2
    all_scenes = []
    all_warnings = []
    for f in files:
        chapter, scenes, warnings = parse_file(f)
        print(f"chapter {chapter}: {len(scenes)} scenes OK")
        all_scenes.extend(scenes)
        all_warnings.extend(warnings)
    all_scenes.sort(key=lambda s: (s["chapter"], s["scene"]))
    doc = {
        "schemaVersion": SCHEMA_VERSION,
        "_comment": "Season Seven — 'The Sins'. Generated by tools/parse_season_seven.py; do not hand-edit.",
        "season": 7,
        "scenes": all_scenes,
    }
    OUT_PATH.write_text(json.dumps(doc, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"wrote {OUT_PATH} ({len(all_scenes)} scenes)")
    if all_warnings:
        print(f"{len(all_warnings)} turn-band warnings:")
        for w in all_warnings:
            print(f"  WARN {w}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
