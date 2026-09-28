// Scandal Season — Conservatory floriography game (v1.0 spec, Sep 28 2026).
// FlowerLexiconSO: the v1 lexicon. Six locked plates + the Season Three
// white rose. Meanings are fixed; story scenes must not contradict them.

using UnityEngine;

[CreateAssetMenu(fileName = "FlowerLexicon", menuName = "Scandal Season/Flower Lexicon")]
public sealed class FlowerLexiconSO : ScriptableObject
{
    [System.Serializable]
    public sealed class FlowerEntry
    {
        [Tooltip("Plate number (I-VI) or 'S3' for the season signature")]
        public string plate;
        public string flowerName;
        [Tooltip("Fixed meaning — story must not contradict")]
        public string meaning;
        [Tooltip("Hand-tinted plate art")]
        public Sprite plateArt;
        [Tooltip("True once the player has learned this meaning")]
        public bool discovered;
    }

    [Header("v1 lexicon (LOCKED Sep 28 2026)")]
    public FlowerEntry[] flowers = new FlowerEntry[7];

    public FlowerEntry GetFlower(string flowerName)
    {
        foreach (var f in flowers)
            if (f != null && f.flowerName == flowerName) return f;
        return null;
    }

    public string GetMeaning(string flowerName)
    {
        var f = GetFlower(flowerName);
        if (f == null) return string.Empty;
        // Discovery rule: unknown meanings show as ? until learned.
        return f.discovered ? f.meaning : "?";
    }

    private void OnValidate()
    {
        // Locked plate meanings. Do not change without Beth's sign-off.
        SetFlower(0, "I", "Red Rose", "Declare your love");
        SetFlower(1, "II", "White Violet", "Modesty");
        SetFlower(2, "III", "Hawthorn Blossom", "Hope");
        SetFlower(3, "IV", "Striped Carnation", "Refusal");
        SetFlower(4, "V", "Ivy", "Fidelity");
        SetFlower(5, "VI", "Yellow Carnation", "Disdain");
        SetFlower(6, "S3", "White Rose", "A clean name");
    }

    private void SetFlower(int i, string plate, string name, string meaning)
    {
        if (flowers[i] == null) flowers[i] = new FlowerEntry();
        flowers[i].plate = plate;
        flowers[i].flowerName = name;
        flowers[i].meaning = meaning;
    }
}
