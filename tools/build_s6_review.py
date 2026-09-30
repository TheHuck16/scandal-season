#!/usr/bin/env python3
"""Rebuild Season Six review DOCX chapters 21-23 with the Rackham canon repair.

Surgical replacement: unzips the delivered review volume, replaces only the
body-XML region covering chapters 21, 22, 23 (the chapters changed by the
canon repair), and re-zips. All styles, title page, contents links, numbering,
headers/footers are preserved from the original package.

Paragraph styles replicate the original builder exactly:
  Heading1 (+bookmark chapter_N) / Status / plain meta (Format key, Turns,
  Canon applied) / Metadata (Occasion) / What-changes (shaded Metadata) /
  Heading2 (scene headers) / Scenepurpose / Narrative / Choiceheading /
  ListBullet / Animation.
"""
import html
import os
import re
import shutil
import sys
import zipfile

STORY = os.path.expanduser("~/workspace/scandal-season/Content/story")
DOCX = os.path.expanduser(
    "~/workspace/your_files/Season Six \u2014 The Fortune \u2014 Complete Review Draft.docx"
)
REPAIRED = (21, 22, 23)


def esc(t):
    return html.escape(t, quote=False)


def runs(text):
    """Split inline markdown into (kind, text) runs. kind: b / i / n."""
    parts = []
    for tok in re.split(r"(\*\*.+?\*\*)", text):
        if len(tok) > 4 and tok.startswith("**") and tok.endswith("**"):
            parts.append(("b", tok[2:-2]))
        else:
            for t2 in re.split(r"(\*[^*\n]+?\*)", tok):
                if len(t2) > 2 and t2.startswith("*") and t2.endswith("*"):
                    parts.append(("i", t2[1:-1]))
                elif t2:
                    parts.append(("n", t2))
    return [p for p in parts if p[1]]


def run_xml(kind, text):
    if kind == "b":
        rpr = "<w:rPr><w:b/><w:i w:val=\"0\"/></w:rPr>"
    elif kind == "i":
        rpr = "<w:rPr><w:i/></w:rPr>"
    else:
        rpr = "<w:rPr><w:i w:val=\"0\"/></w:rPr>"
    sp = ' xml:space="preserve"' if text[0].isspace() or text[-1].isspace() else ""
    return "<w:r>%s<w:t%s>%s</w:t></w:r>" % (rpr, sp, esc(text))


def para(style_ppr, text):
    return "<w:p>%s%s</w:p>" % (style_ppr, "".join(run_xml(k, t) for k, t in runs(text)))


PAGEBREAK = '<w:p><w:r><w:br w:type="page"/></w:r></w:p>'

H1_PPR = ('<w:pPr><w:pStyle w:val="Heading1"/>'
          '<w:pBdr><w:bottom w:val="single" w:sz="10" w:space="3" '
          'w:color="AA7A34"/></w:pBdr></w:pPr>')
STATUS_PPR = ('<w:pPr><w:pStyle w:val="Status"/><w:shd w:fill="EEF2F5"/>'
              '<w:pBdr><w:left w:val="single" w:sz="12" w:space="6" '
              'w:color="365E7D"/></w:pBdr></w:pPr>')
META_PPR = '<w:pPr><w:pStyle w:val="Metadata"/></w:pPr>'
WC_PPR = ('<w:pPr><w:pStyle w:val="Metadata"/><w:shd w:fill="F7F8FA"/>'
          '<w:pBdr><w:left w:val="single" w:sz="12" w:space="6" '
          'w:color="AA7A34"/></w:pBdr><w:ind w:left="173" w:right="86"/></w:pPr>')
H2_PPR = '<w:pPr><w:pStyle w:val="Heading2"/></w:pPr>'
PURP_PPR = '<w:pPr><w:pStyle w:val="Scenepurpose"/></w:pPr>'
NARR_PPR = ('<w:pPr><w:pStyle w:val="Narrative"/>'
            '<w:pBdr><w:left w:val="single" w:sz="6" w:space="6" '
            'w:color="C6D0D9"/></w:pBdr></w:pPr>')
CHOICE_PPR = '<w:pPr><w:pStyle w:val="Choiceheading"/></w:pPr>'
BULLET_PPR = '<w:pPr><w:pStyle w:val="ListBullet"/></w:pPr>'
ANIM_PPR = '<w:pPr><w:pStyle w:val="Animation"/></w:pPr>'
PLAIN_PPR = "<w:pPr/>"


def heading_para(n, title, bid):
    return ('<w:p><w:bookmarkStart w:id="%d" w:name="chapter_%d"/>%s'
            '<w:r><w:t>%s</w:t></w:r><w:bookmarkEnd w:id="%d"/></w:p>'
            % (bid, n, H1_PPR, esc(title), bid))


def status_para():
    return ('<w:p>%s%s%s</w:p>' % (
        STATUS_PPR,
        run_xml("b", "STATUS: FULL DRAFT"),
        run_xml("n", " \u2014 awaiting Beth review."),
    ))


def label_para(ppr, label, rest):
    return "<w:p>%s%s%s</w:p>" % (ppr, run_xml("b", label + ":"),
                                  "".join(run_xml(k, t) for k, t in runs(" " + rest)))


def parse_chapter(path):
    """Return (number, title, [paragraph xml strings]) for a chapter file."""
    lines = open(path, encoding="utf-8").read().split("\n")
    num, title = None, None
    paras = []
    for line in lines:
        s = line.rstrip()
        if not s.strip():
            continue
        if s.startswith("# ") and num is None:
            m = re.match(r'# Scandal Season \u2014 Season Six, Chapter (\d+): "(.*)"', s)
            num, title = int(m.group(1)), "Chapter %s \u2014 %s" % (m.group(1), m.group(2))
            continue
        if s.startswith("## ") or s.startswith("**STATUS:"):
            continue
        if s.startswith("### "):
            paras.append("<w:p>%s%s</w:p>" % (H2_PPR, run_xml("n", s[4:])))
            continue
        if s.startswith("> "):
            paras.append(para(NARR_PPR, s[2:]))
            continue
        if s.startswith("- "):
            paras.append(para(BULLET_PPR, s[2:]))
            continue
        if s.startswith("**") and s[2] != "*":
            m = re.match(r"\*\*([^*]+):\*\*\s?(.*)", s)
            if m:
                label, rest = m.group(1), m.group(2)
                if label == "What changes for Rose":
                    paras.append(label_para(WC_PPR, label, rest))
                elif label in ("Occasion", "Preparation ritual", "Purse shelf"):
                    paras.append(label_para(META_PPR, label, rest))
                else:
                    paras.append(label_para(PLAIN_PPR, label, rest))
                continue
        if s.startswith("*") and s.endswith("*"):
            inner = s[1:-1]
            if inner.startswith("Purpose:"):
                paras.append(para(PURP_PPR, inner))
            elif inner.startswith("Animation:"):
                paras.append(para(ANIM_PPR, inner))
            elif "\u2605 KEY DECISION" in inner:
                paras.append(para(CHOICE_PPR, inner))
            else:
                paras.append(para(NARR_PPR, inner))
            continue
        raise ValueError("Unclassified line in %s: %r" % (path, s[:90]))
    return num, title, paras


def chapter_xml(path):
    num, title, paras = parse_chapter(path)
    out = [PAGEBREAK, heading_para(num, title, num), status_para()]
    out.extend(paras)
    return "".join(out)


def main():
    tmp = "/tmp/s6rebuild"
    if os.path.exists(tmp):
        shutil.rmtree(tmp)
    os.makedirs(tmp)
    with zipfile.ZipFile(DOCX, "r") as z:
        z.extractall(tmp)
    doc = os.path.join(tmp, "word", "document.xml")
    x = open(doc, encoding="utf-8").read()

    # Region: page-break paragraph before chapter_21's heading -> page-break
    # paragraph before chapter_24's heading (exclusive).
    bm21 = x.find('w:name="chapter_21"')
    bm24 = x.find('w:name="chapter_24"')
    if bm21 < 0 or bm24 < 0:
        sys.exit("bookmarks not found")
    pb = '<w:p><w:r><w:br w:type="page"/></w:r></w:p>'
    start = x.rfind(pb, 0, bm21)
    end = x.rfind(pb, 0, bm24)
    if start < 0 or end < 0 or end <= start:
        sys.exit("page-break boundaries not found")

    new_body = "".join(
        chapter_xml(os.path.join(STORY, "season-six-chapter-%d.md" % n))
        for n in REPAIRED
    )
    x = x[:start] + new_body + x[end:]
    open(doc, "w", encoding="utf-8").write(x)

    with zipfile.ZipFile(DOCX, "w", zipfile.ZIP_DEFLATED) as z:
        for root, _dirs, files in os.walk(tmp):
            for f in files:
                full = os.path.join(root, f)
                z.write(full, os.path.relpath(full, tmp))
    print("rebuilt", DOCX)


if __name__ == "__main__":
    main()
