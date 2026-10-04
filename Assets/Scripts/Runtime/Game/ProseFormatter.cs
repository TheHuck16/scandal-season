// Scandal Season — Runtime game layer.
// ProseFormatter: converts authored scene prose into player-facing rich text.
// The content pipeline stores prose with lightweight editorial markup:
//   "> "      paragraph prefix (block-quote style from the source docs)
//   "*...*"   italics (single asterisks around a phrase)
//   "(Tn · label)" and "(Turns: N)"  turn annotations — editorial, never shown to the player
//   "— remembered: ..."  consequence notes — kept, they are story text
// This strips the editorial layer, normalizes unicode punctuation to the
// font's glyph coverage, and converts italics to Unity rich text.
using System.Text;
using System.Text.RegularExpressions;

public static class ProseFormatter
{
    // Matches "(T12 · some label)" turn annotations. The separator may be a
    // real middle dot (U+00B7) or the literal text "\xB7" (double-escaped in
    // the YAML source), so match permissively: "(T" + digits + anything.
    private static readonly Regex TurnAnnotation =
        new Regex(@"\(T\d+\b[^)]*\)", RegexOptions.Compiled);

    // Matches "(Turns: 8)" turn-count annotations (may span a line break in YAML).
    private static readonly Regex TurnsCountAnnotation =
        new Regex(@"\(Turns:\s*\d+\)", RegexOptions.Compiled);

    // Matches *italic* spans (single asterisks, not across newlines).
    private static readonly Regex Italics =
        new Regex(@"\*([^*\n]+)\*", RegexOptions.Compiled);

    /// <summary>
    /// Converts raw authored prose to player-facing Unity rich text.
    /// Safe to call on already-clean text (idempotent for clean input).
    /// </summary>
    public static string Format(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;

        // 1. Strip turn annotations: "(T1 · look closer)" and "(Turns: 8)" etc.
        string s = TurnAnnotation.Replace(raw, "");
        s = TurnsCountAnnotation.Replace(s, "");

        // 2. Normalize unicode punctuation to ASCII-safe equivalents. The
        // runtime font lacks these glyphs (they render as blank gaps).
        // French accented letters (é è ê ç â) are KEPT — the font has those.
        s = s.Replace("\u2014", "--")   // em-dash
             .Replace("\u2013", "-")    // en-dash
             .Replace("\u2212", "-")    // minus sign
             .Replace("\u201C", "\"")   // left double quote
             .Replace("\u201D", "\"")   // right double quote
             .Replace("\u2018", "'")    // left single quote
             .Replace("\u2019", "'")    // right single quote
             .Replace("\u2026", "...")  // ellipsis
             .Replace("\u2192", "->")   // right arrow
             .Replace("\u00D7", "x")    // multiplication sign
             .Replace("\u2248", "~")    // almost equal
             .Replace("\u2605", "*")    // star
             .Replace("\u25C7", "<>")   // diamond
             .Replace("\u27E8", "<")    // angle brackets
             .Replace("\u27E9", ">")
             .Replace("\u00A7", "Sec. ") // section sign
             .Replace("\u00B0", "deg");  // degree sign

        // 3. Strip the "> " paragraph prefix at the start of each line,
        //    plus any whitespace left behind by removed annotations.
        var sb = new StringBuilder(s.Length);
        int i = 0;
        bool atLineStart = true;
        while (i < s.Length)
        {
            if (atLineStart)
            {
                // Skip "> ", ">", and stray leading spaces from annotations.
                if (i + 1 < s.Length && s[i] == '>' && s[i + 1] == ' ') { i += 2; continue; }
                if (s[i] == '>') { i += 1; continue; }
                if (s[i] == ' ' || s[i] == '\t') { i += 1; continue; }
            }
            sb.Append(s[i]);
            atLineStart = (s[i] == '\n');
            i++;
        }
        s = sb.ToString();

        // 4. Convert *italics* to Unity rich text <i>...</i>.
        s = Italics.Replace(s, "<i>$1</i>");

        // 5. Collapse 3+ newlines to a paragraph break; trim leading/trailing space.
        s = Regex.Replace(s, @"\n{3,}", "\n\n");
        s = s.Trim();

        return s;
    }
}
