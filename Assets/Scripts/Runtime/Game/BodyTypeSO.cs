// Scandal Season — Rendering layer.
// BodyTypeSO: 4 body types, same height (APPROVED Sep 28, 2026). Every garment
// must fit and flatter all four. LOCKED production rule.

using UnityEngine;

[CreateAssetMenu(fileName = "BodyTypes", menuName = "Scandal Season/Body Types")]
public sealed class BodyTypeSO : ScriptableObject
{
    [System.Serializable]
    public sealed class BodyType
    {
        public string typeName;
        [Tooltip("Proportional scale factors for garment fitting.")]
        public Vector3 proportions = Vector3.one;
        public string description;
    }

    [Header("4 body types, same height (APPROVED Sep 28, 2026)")]
    public BodyType[] types = new BodyType[4];

    private void OnValidate()
    {
        if (types == null || types.Length != 4) return;
        SetType(0, "Willowy", new Vector3(0.92f, 1.0f, 0.92f), "Slender, elongated line");
        SetType(1, "Classic", new Vector3(1.0f, 1.0f, 1.0f), "Balanced proportions");
        SetType(2, "Curvaceous", new Vector3(1.08f, 1.0f, 1.08f), "Full bust and hip");
        SetType(3, "Athletic", new Vector3(1.02f, 1.0f, 0.98f), "Toned, strong shoulder");
    }

    private void SetType(int i, string name, Vector3 props, string desc)
    {
        if (types[i] == null) types[i] = new BodyType();
        types[i].typeName = name;
        types[i].proportions = props;
        types[i].description = desc;
    }
}
