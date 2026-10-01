// Scandal Season — Runtime game layer.
// ProseFormatter: converts authored scene prose into player-facing rich text.
// The content pipeline stores prose with lightweight editorial markup:
//   "> "      paragraph prefix (block-quote style from the source docs)
//   "*...*"   italics (single asterisks around a phrase)
//   "(Tn · label)" and "(Turns: N)"  turn annotations — editorial, never shown to the player
//   "— remembered: ..."  consequence notes — kept, they are story text
// This strips the editorial layer and converts italics to Unity rich text.
using System.Text;
using System.Text.RegularExpressions;

public static class ProseFormatter
{
    // Matches "(T12 · some label)" turn annotations.
    private static readonly Regex TurnAnnotation =
        new Regex(@"\(T\d+\s*·[^)]*\)", RegexOptions.Compiled);

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

        // 2. Strip the "> " paragraph prefix at the start of each line,
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

        // 3. Convert *italics* to Unity rich text <i>...</i>.
        s = Italics.Replace(s, "<i>$1</i>");

        // 4. Collapse 3+ newlines to a paragraph break; trim leading/trailing space.
        s = Regex.Replace(s, @"\n{3,}", "\n\n");
        s = s.Trim();

        return s;
    }
}
