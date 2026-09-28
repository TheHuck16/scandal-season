// Scandal Season — Hartwell Park estate repair game (v1.0 spec, Sep 28 2026).
// EstateRegistrySO: the seven zones in locked order. Single source of truth
// for the estate map screen.

using UnityEngine;

[CreateAssetMenu(fileName = "EstateRegistry", menuName = "Scandal Season/Estate Registry")]
public sealed class EstateRegistrySO : ScriptableObject
{
    [Header("Seven zones in locked tier order (APPROVED Sep 28 2026)")]
    public EstateZoneSO[] zones = new EstateZoneSO[7];

    public EstateZoneSO GetZone(string zoneId)
    {
        foreach (var z in zones)
            if (z != null && z.zoneId == zoneId) return z;
        return null;
    }

    private void OnValidate()
    {
        // Locked ladder (Beth, Sep 28 2026): 100 / 200 / 300 / 450 / 600 / 800 / 1000
        // Revised route: Arrival Court → Kitchen Garden → Wild Garden → Folly →
        // Orangery → Stables → Ballroom. Stables moved to second-to-last.
        int[] ladder = { 100, 200, 300, 450, 600, 800, 1000 };
        string[] ids = { "arrival-court", "kitchen-garden", "wild-garden", "folly", "orangery", "stables", "ballroom" };
        if (zones == null || zones.Length != 7) return;
        for (int i = 0; i < 7; i++)
        {
            if (zones[i] == null) continue;
            if (zones[i].tierCost != ladder[i])
                Debug.LogWarning($"EstateRegistry: zone {i} tier {zones[i].tierCost} != locked {ladder[i]}.", this);
            if (zones[i].zoneId != ids[i])
                Debug.LogWarning($"EstateRegistry: zone {i} id '{zones[i].zoneId}' != locked '{ids[i]}'.", this);
        }
    }
}
