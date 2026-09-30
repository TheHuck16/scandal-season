#!/usr/bin/env bash
# S5 chapter verification — run after each writer completes.
# Usage: verify-s5.sh <chapter-file> [expected-scenes]
f="$1"
expected="${2:-40}"
echo "== $f"
c=$(grep -c "^### " "$f"); echo "scenes: $c (expected $expected) $([ "$c" = "$expected" ] && echo OK || echo MISMATCH)"
echo "purpose lines: $(grep -c '^\*Purpose:' "$f")"
echo "animation lines: $(grep -c '^\*Animation:' "$f")"
echo "custom: $(grep -c '^\*Animation: Custom' "$f")"
echo "key decisions: $(grep -c '★ KEY DECISION [0-9]/3 —' "$f" | head -1) markers; options: $(grep -c -- '- \*\*.*\*\* —' "$f")"
echo "em-dashes (prose only): $(grep -v '^> (T[0-9]' "$f" | grep -v '— remembered:' | grep -v '^- \*\*' | grep -o '—' | wc -l) (budget <350)"
echo "hard bans — 8000: $(grep -c "8,000" "$f") | in-the-series: $(grep -c "in the series" "$f") | writes-in-book: $(grep -c "she writes it in the book" "$f") | remembered-her: $(grep -c "remembered her" "$f")"
echo "status: $(grep -m1 'STATUS:' "$f")"
echo "scene order check:"
grep "^### " "$f" | sed 's/.*S\([0-9]*\) .*/\1/' | awk -v want="$expected" 'BEGIN{n=0} {n++; if ($1 != n) {print "ORDER BREAK at line " NR ": got " $1 ", want " n; bad=1}} END{if (!bad) print "order OK 1.."n}'
