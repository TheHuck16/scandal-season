// Scandal Season — Hartwell Park estate repair game (v1.0 spec, Sep 28 2026).
// EstateZoneSO: one restoration zone. Seven zones on the locked coin ladder.
// Zone order and costs APPROVED by Beth Sep 28 2026.

using UnityEngine;

[CreateAssetMenu(fileName = "EstateZone", menuName = "Scandal Season/Estate Zone")]
public sealed class EstateZoneSO : ScriptableObject
{
    [System.Serializable]
    public sealed class RepairTask
    {
        [Tooltip("Diegetic task name, e.g. 'repoint the folly's stonework'")]
        public string taskName;
        [Tooltip("Share of the zone's coin tier for this task")]
        public int coinCost;
        [Tooltip("Signature merge material required (chain output item ID)")]
        public string materialItemId;
        [Tooltip("How many of the material item")]
        public int materialCount = 1;
        [Tooltip("Primary chain that produces this material")]
        public string primaryChainId;
    }

    [Header("Identity (LOCKED Sep 28 2026)")]
    public string zoneId;
    public string displayName;
    [Tooltip("Locked tier cost in coins")]
    public int tierCost;
    public int zoneOrder;

    [Header("Art plates")]
    public Sprite beforePlate;
    public Sprite finalPlate;
    [Tooltip("True when final art is approved; Ballroom ships gated on art")]
    public bool finalArtApproved = true;

    [Header("Repair tasks (3-5 per zone)")]
    public RepairTask[] tasks = new RepairTask[0];

    [Header("Chains")]
    public string primaryChainId;
    public string secondaryChainId;

    [Header("Exposure")]
    [Tooltip("What the family buried here; advances the backstory mystery")]
    [TextArea(2, 4)]
    public string exposureBeat;

    [Header("Unlock gates")]
    [Tooltip("Zone 3 (Kitchen Garden) gates the Conservatory")]
    public bool unlocksConservatory;

    private void OnValidate()
    {
        if (tasks != null)
        {
            int total = 0;
            foreach (var t in tasks) total += t.coinCost;
            if (total != tierCost)
                Debug.LogWarning($"EstateZone {zoneId}: task coin costs sum to {total}, tier is {tierCost}.", this);
        }
    }
}
