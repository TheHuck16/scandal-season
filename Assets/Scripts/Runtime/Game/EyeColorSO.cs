// Scandal Season — Rendering layer.
// EyeColorSO: 6 eye colors (APPROVED Sep 28, 2026, avatar creator UI v3).
// Part of the 3-axis avatar system. Changeable anytime, never gated.

using UnityEngine;

[CreateAssetMenu(fileName = "EyeColors", menuName = "Scandal Season/Eye Colors")]
public sealed class EyeColorSO : ScriptableObject
{
    [System.Serializable]
    public sealed class EyeColor
    {
        public string colorName;
        public Color color = Color.white;
    }

    [Header("6 eye colors (APPROVED Sep 28, 2026)")]
    public EyeColor[] colors = new EyeColor[6];

    private void OnValidate()
    {
        if (colors == null || colors.Length != 6) return;
        SetColor(0, "Hazel", new Color(0.45f, 0.35f, 0.22f));
        SetColor(1, "Green", new Color(0.30f, 0.45f, 0.30f));
        SetColor(2, "Blue", new Color(0.35f, 0.50f, 0.65f));
        SetColor(3, "Grey", new Color(0.50f, 0.52f, 0.55f));
        SetColor(4, "Brown", new Color(0.32f, 0.22f, 0.15f));
        SetColor(5, "Amber", new Color(0.60f, 0.42f, 0.20f));
    }

    private void SetColor(int i, string name, Color c)
    {
        if (colors[i] == null) colors[i] = new EyeColor();
        colors[i].colorName = name;
        colors[i].color = c;
    }
}
