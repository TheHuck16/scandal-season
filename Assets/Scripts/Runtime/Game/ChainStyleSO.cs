// Scandal Season — Rendering layer.
// ChainStyleSO: Board F visual identity. Chain-family colors, roman numerals,
// and glow tiers per the approved merge-board-chain-bible.md. No invented content.

using UnityEngine;

[CreateAssetMenu(fileName = "ChainStyle", menuName = "Scandal Season/Chain Style")]
public sealed class ChainStyleSO : ScriptableObject
{
    [System.Serializable]
    public sealed class ChainStyle
    {
        public string chainId;
        public string displayName;
        [Tooltip("Family color: gold, pearl, blush, ivory, garden")]
        public Color familyColor = Color.white;
        [Tooltip("Accent for stage badges and glows")]
        public Color accentColor = Color.white;
        public string[] stageRomanNumerals = { "I", "II", "III", "IV", "V" };
    }

    [Header("Board F chains (APPROVED Sep 27, 2026)")]
    public ChainStyle[] chains = new ChainStyle[5];

    [Header("Board chrome")]
    public Color boardBackground = new Color(1f, 0.98f, 0.94f); // Ivory silk
    public Color uiGold = new Color(0.83f, 0.69f, 0.35f); // Warm gold
    public Color cellEmpty = new Color(1f, 1f, 1f, 0.5f);

    public ChainStyle GetStyle(string chainId)
    {
        foreach (var c in chains)
            if (c.chainId == chainId) return c;
        return null;
    }

    public string RomanNumeral(int stage)
    {
        if (stage < 1 || stage > 5) return stage.ToString();
        // Default numerals; per-chain overrides in ChainStyle.stageRomanNumerals
        string[] numerals = { "I", "II", "III", "IV", "V" };
        return numerals[stage - 1];
    }

    private void OnValidate()
    {
        if (chains == null || chains.Length != 5)
            return;
        // Enforce Board F chain order and family colors
        SetChain(0, "needlework", "Needlework", new Color(0.83f, 0.69f, 0.22f), new Color(1f, 0.85f, 0.4f));
        SetChain(1, "pearls", "Pearls", new Color(0.95f, 0.93f, 0.88f), new Color(0.85f, 0.82f, 0.75f));
        SetChain(2, "ribbon", "Ribbon", new Color(0.96f, 0.75f, 0.78f), new Color(0.9f, 0.55f, 0.6f));
        SetChain(3, "lace", "Lace", new Color(1f, 1f, 0.97f), new Color(0.92f, 0.9f, 0.82f));
        SetChain(4, "posy", "Posy", new Color(0.55f, 0.75f, 0.5f), new Color(0.35f, 0.6f, 0.35f));
    }

    private void SetChain(int i, string id, string name, Color family, Color accent)
    {
        if (chains[i] == null) chains[i] = new ChainStyle();
        chains[i].chainId = id;
        chains[i].displayName = name;
        chains[i].familyColor = family;
        chains[i].accentColor = accent;
    }
}
