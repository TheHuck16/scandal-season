// Scandal Season — Hartwell Park estate repair game (v1.0 spec, Sep 28 2026).
// EstateZoneSO: one restoration zone. Seven zones on the locked Estate Funds ladder.
// Zone order and costs APPROVED by Beth Sep 28 2026. Estate Funds only —
// coins are entirely outside the estate.

using UnityEngine;

[CreateAssetMenu(fileName = "EstateZone", menuName = "Scandal Season/Estate Zone")]
public sealed class EstateZoneSO : ScriptableObject
{
    [System.Serializable]
    public sealed class RepairTask
    {
        [Tooltip("Diegetic task name, e.g. 'repoint the folly's stonework'")]
        public string taskName;
        [Tooltip("Share of the zone's Estate Funds tier for this task")]
        public int fundCost;
        [Tooltip("Signature merge material required (chain output item ID)")]
        public string materialItemId;
        [Tooltip("How many of the material item")]
        public int materialCount = 1;
        [Tooltip("Primary chain that produces this material")]
        public string primaryChainId;
    }

    [System.Serializable]
    public sealed class TasteApproach
    {
        [Tooltip("Faithful, Fashionable, or Bold")]
        public string approach;
        [Tooltip("What the player does — the card describes ONLY the approach")]
        [TextArea(2, 4)]
        public string description;
        [Tooltip("Gazette response AFTER commitment — never shown on the card")]
        [TextArea(2, 4)]
        public string gazetteResponse;
        [Tooltip("Standing shift — constituencies, not scores")]
        [TextArea(1, 2)]
        public string standingNote;
    }

    [System.Serializable]
    public sealed class TaskTasteDecision
    {
        [Tooltip("Index into tasks[]")]
        public int taskIndex;
        public TasteApproach[] approaches = new TasteApproach[0];
    }

    [Header("Identity (LOCKED Sep 28 2026)")]
    public string zoneId;
    public string displayName;
    [Tooltip("Locked tier cost in Estate Funds")]
    public int tierCost;
    public int zoneOrder;

    [Header("Art plates")]
    public Sprite beforePlate;
    public Sprite finalPlate;
    [Tooltip("True when final art is approved; Ballroom ships gated on art")]
    public bool finalArtApproved = true;

    [Header("Repair tasks (3-4 per zone)")]
    public RepairTask[] tasks = new RepairTask[0];

    [Header("Taste decisions (vertical slice: Stables has 12 authored options)")]
    [Tooltip("One entry per task that offers a taste decision")]
    public TaskTasteDecision[] tasteDecisions = new TaskTasteDecision[0];

    [Header("Chains")]
    public string primaryChainId;
    public string secondaryChainId;

    [Header("Exposure")]
    [Tooltip("What the family buried here; advances the backstory mystery")]
    [TextArea(2, 4)]
    public string exposureBeat;

    [Header("Unlock gates")]
    [Tooltip("Zone 2 (Kitchen Garden) gates the Conservatory")]
    public bool unlocksConservatory;

    private void OnValidate()
    {
        if (tasks != null)
        {
            int total = 0;
            foreach (var t in tasks) total += t.fundCost;
            if (total != tierCost)
                Debug.LogWarning($"EstateZone {zoneId}: task fund costs sum to {total}, tier is {tierCost}.", this);
        }
    }
}
