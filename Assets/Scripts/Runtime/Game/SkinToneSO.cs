// Scandal Season — Rendering layer.
// SkinToneSO: 5 independent skin tones (APPROVED Sep 28, 2026). Racially-neutral
// system: face structure × skin tone × body type are independent axes.
// The hair palette must serve darker skin tones, not only copper-auburn.

using UnityEngine;

[CreateAssetMenu(fileName = "SkinTones", menuName = "Scandal Season/Skin Tones")]
public sealed class SkinToneSO : ScriptableObject
{
    [System.Serializable]
    public sealed class SkinTone
    {
        public string toneName;
        public Color color = Color.white;
    }

    [Header("5 skin tones (APPROVED Sep 28, 2026)")]
    public SkinTone[] tones = new SkinTone[5];

    private void OnValidate()
    {
        if (tones == null || tones.Length != 5) return;
        SetTone(0, "Porcelain", new Color(0.96f, 0.88f, 0.82f));
        SetTone(1, "Warm Ivory", new Color(0.92f, 0.80f, 0.70f));
        SetTone(2, "Golden Honey", new Color(0.82f, 0.64f, 0.50f));
        SetTone(3, "Rich Amber", new Color(0.65f, 0.46f, 0.34f));
        SetTone(4, "Deep Mahogany", new Color(0.45f, 0.30f, 0.22f));
    }

    private void SetTone(int i, string name, Color c)
    {
        if (tones[i] == null) tones[i] = new SkinTone();
        tones[i].toneName = name;
        tones[i].color = c;
    }
}
