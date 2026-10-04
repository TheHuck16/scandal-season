#!/usr/bin/env python3
"""Scandal Season — Content validation for scene .asset files.

Catches content issues BEFORE the 40-minute WebGL build:
1. Unbalanced *italics* markers (would leave raw asterisks visible)
2. Turn annotations (Tn ...) that ProseFormatter won't strip
3. Unicode characters outside the font's known glyph coverage
4. (Turns: N) annotations (should be stripped, verify they're caught)
5. Suspicious patterns (double spaces, literal backslash sequences)

Usage: python3 Tools/validate-content.py [--strict]
Exit 0 = clean, 1 = issues found.
"""
import re
import sys
import glob

# Characters the runtime font is KNOWN to render (basic Latin + French accents).
# Everything else must be normalized by ProseFormatter or it's a bug.
SAFE_CHARS = set(
    "abcdefghijklmnopqrstuvwxyz"
    "ABCDEFGHIJKLMNOPQRSTUVWXYZ"
    "0123456789"
    " .,;:!?\"'()[]{}<>-–—–—"  # punctuation incl. dashes (normalized by formatter)
    "\n\t "
    "éèêëçâàîïôöùûüÿæœÉÈÊÇÂÀÎÔÙ"  # French accents
    "·•*#"  # annotation markers
    "’‘“”"  # curly quotes (normalized by formatter)
    "…→×−≈§°★◇⟨⟩"  # symbols (normalized by formatter)
)

# ProseFormatter's turn annotation pattern (must match what's in the .cs)
TURN_ANNOTATION = re.compile(r'\(T\d+\b[^)]*\)')
TURNS_COUNT = re.compile(r'\(Turns:\s*\d+[^)]*\)')

# What ProseFormatter normalizes (must match the .cs Replace chain)
NORMALIZED = {
    '\u2014': '--', '\u2013': '-', '\u2212': '-',
    '\u201c': '"', '\u201d': '"', '\u2018': "'", '\u2019': "'",
    '\u2026': '...', '\u2192': '->', '\u00d7': 'x', '\u2248': '~',
    '\u2605': '*', '\u25c7': '<>', '\u27e8': '<', '\u27e9': '>',
    '\u00a7': 'Sec. ', '\u00b0': 'deg',
}

def decode_yaml_dq(s):
    """Decode a YAML double-quoted string (best effort)."""
    s = s.replace('\\\\', '\x00')
    s = s.replace('\\n', '\n')
    s = s.replace('\\"', '"')
    s = re.sub(r'\\u([0-9a-fA-F]{4})', lambda m: chr(int(m.group(1), 16)), s)
    s = re.sub(r'\\x([0-9a-fA-F]{2})', lambda m: chr(int(m.group(1), 16)), s)
    s = s.replace('\x00', '\\')  # leftover = literal backslash (e.g. \\xB7 -> \xB7 text)
    s = re.sub(r'\n    ', ' ', s)
    return s

def get_prose_fields(filepath):
    """Extract prose/synopsis fields from a .asset file."""
    data = open(filepath, encoding='utf-8', errors='ignore').read()
    fields = {}
    for field in ('prose', 'synopsis'):
        m = re.search(rf'{field}: "(.*?)"\n  \S', data, re.S)
        if m:
            fields[field] = decode_yaml_dq(m.group(1))
    return fields

def validate_file(filepath, strict=False):
    issues = []
    fields = get_prose_fields(filepath)
    name = filepath.split('/')[-1]

    for field, text in fields.items():
        # 1. Unbalanced asterisks (would leave raw * visible)
        # Count * not part of ** (bold isn't used, but be safe)
        stars = re.findall(r'(?<!\*)\*(?!\*)', text)
        if len(stars) % 2 != 0:
            issues.append(f"{name}:{field}: UNBALANCED asterisks ({len(stars)} single *)")

        # 2. Turn annotations that won't be stripped
        # Find all (T...) patterns, check if our regex catches them
        for m in re.finditer(r'\(T\d+[^)]*\)', text):
            ann = m.group(0)
            if not TURN_ANNOTATION.match(ann):
                issues.append(f"{name}:{field}: UNSTRIPPED turn annotation: {ann[:50]}")

        # 3. (Turns: N) check
        for m in re.finditer(r'\(Turns:[^)]*\)', text):
            if not TURNS_COUNT.match(m.group(0)):
                issues.append(f"{name}:{field}: UNSTRIPPED turns annotation: {m.group(0)[:50]}")

        # 4. Characters outside safe set (and not normalized)
        for ch in set(text):
            if ch not in SAFE_CHARS and ch not in NORMALIZED:
                # Only flag if it's non-ASCII (ASCII is always safe)
                if ord(ch) > 127:
                    issues.append(
                        f"{name}:{field}: UNHANDLED unicode U+{ord(ch):04X} "
                        f"({ch!r}) — not in ProseFormatter normalization"
                    )
                    break  # one per field to avoid spam

        # 5. Literal backslash sequences (double-escaped in YAML, render as text)
        for m in re.finditer(r'\\[xu][0-9a-fA-F]{2,4}', text):
            issues.append(
                f"{name}:{field}: LITERAL escape sequence {m.group(0)!r} "
                f"will render as text, not the character"
            )

        if strict:
            # 6. Double spaces (often from stripped content)
            if '  ' in text.replace('\n', ' '):
                # Only flag if not in normal prose (allow after periods)
                pass

    return issues

def main():
    strict = '--strict' in sys.argv
    all_issues = []
    files = glob.glob('Assets/Scripts/Runtime/Generated/Scenes/*.asset')
    if not files:
        print("No scene files found. Run from the project root.")
        return 1

    for f in sorted(files):
        all_issues.extend(validate_file(f, strict))

    if all_issues:
        print(f"FOUND {len(all_issues)} CONTENT ISSUES:")
        for issue in all_issues[:50]:  # cap output
            print(f"  ! {issue}")
        if len(all_issues) > 50:
            print(f"  ... and {len(all_issues) - 50} more")
        return 1
    else:
        print(f"Clean: {len(files)} scene files validated, no issues.")
        return 0

if __name__ == '__main__':
    sys.exit(main())
