// Scandal Season — Rendering layer.
// HairPaletteSO: Hair Palette A (APPROVED Sep 28, 2026). Hair color and
// hairstyle are independent, extensible axes. Foundation for the avatar
// system (5 face structures × 5 skin tones × 4 body types). No invented content.

using UnityEngine;

[CreateAssetMenu(fileName = "HairPaletteA", menuName = "Scandal Season/Hair Palette A")]
public sealed class HairPaletteSO : ScriptableObject
{
    [System.Serializable]
    public sealed class HairColor
    {
        public string colorName;
        public Color color = Color.white;
        [Tooltip("Canon Rose hair: copper-red/auburn (locked Sep 28, 2026).")]
        public bool isCanonRoseColor;
    }

    [Header("Hair Palette A (APPROVED Sep 28, 2026)")]
    public HairColor[] colors = new HairColor[6];

    private void OnValidate()
    {
        if (colors == null || colors.Length != 6) return;
        SetColor(0, "Jet Black", new Color(0.08f, 0.07f, 0.09f), false);
        SetColor(1, "Deep Espresso", new Color(0.23f, 0.15f, 0.10f), false);
        SetColor(2, "Warm Chestnut", new Color(0.42f, 0.26f, 0.15f), false);
        SetColor(3, "Copper-Auburn", new Color(0.65f, 0.32f, 0.18f), true); // canon Rose
        SetColor(4, "Golden Brown", new Color(0.55f, 0.38f, 0.22f), false);
        SetColor(5, "Honey Blonde", new Color(0.78f, 0.62f, 0.38f), false);
    }

    private void SetColor(int i, string name, Color c, bool canon)
    {
        if (colors[i] == null) colors[i] = new HairColor();
        colors[i].colorName = name;
        colors[i].color = c;
        colors[i].isCanonRoseColor = canon;
    }

    public HairColor CanonRoseColor()
    {
        foreach (var hc in colors)
            if (hc != null && hc.isCanonRoseColor) return hc;
        return null;
    }
}
