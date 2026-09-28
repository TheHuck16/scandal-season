// Scandal Season — Rendering layer.
// FaceStructureSO: 5 racially-neutral face structures (APPROVED Sep 28, 2026).
// Canon Rose is face A (the default). Player base avatar from 3 independent
// axes; changeable anytime, never locked or gated.

using UnityEngine;

[CreateAssetMenu(fileName = "FaceStructures", menuName = "Scandal Season/Face Structures")]
public sealed class FaceStructureSO : ScriptableObject
{
    [System.Serializable]
    public sealed class FaceStructure
    {
        public string structureName;
        [Tooltip("Face A is canon Rose (the default).")]
        public bool isCanonRose;
        public string description;
    }

    [Header("5 face structures (APPROVED Sep 28, 2026)")]
    public FaceStructure[] structures = new FaceStructure[5];

    private void OnValidate()
    {
        if (structures == null || structures.Length != 5) return;
        SetStructure(0, "Face A", true, "Canon Rose: freckles, crooked half-smile, natural eyes");
        SetStructure(1, "Face B", false, "Oval, high cheekbones");
        SetStructure(2, "Face C", false, "Round, soft features");
        SetStructure(3, "Face D", false, "Angular, defined jaw");
        SetStructure(4, "Face E", false, "Heart-shaped, wide brow");
    }

    private void SetStructure(int i, string name, bool canon, string desc)
    {
        if (structures[i] == null) structures[i] = new FaceStructure();
        structures[i].structureName = name;
        structures[i].isCanonRose = canon;
        structures[i].description = desc;
    }

    public FaceStructure CanonRose()
    {
        foreach (var f in structures)
            if (f != null && f.isCanonRose) return f;
        return null;
    }
}
