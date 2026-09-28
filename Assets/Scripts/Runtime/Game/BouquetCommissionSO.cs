// Scandal Season — Conservatory floriography game (v1.0 spec, Sep 28 2026).
// BouquetCommissionSO: one authored commission. The brief defines the
// recipient, occasion, required note, forbidden note, and hidden opportunity.
// Scoring is transparent: Brief fit / Composition / Harmony.

using UnityEngine;

[CreateAssetMenu(fileName = "BouquetCommission", menuName = "Scandal Season/Bouquet Commission")]
public sealed class BouquetCommissionSO : ScriptableObject
{
    [Header("Brief")]
    public string commissionId;
    public string recipient;
    public string occasion;
    [Tooltip("The message the bouquet must send")]
    public string requiredNote;
    [Tooltip("The message the bouquet must NOT send")]
    public string forbiddenNote;
    [Tooltip("Optional hidden opportunity (coded clue, signal, barb)")]
    [TextArea(2, 4)]
    public string hiddenOpportunity;

    [Header("Intent flags (recorded as compact story flags)")]
    public bool allowsConciliatory;
    public bool allowsRomantic;
    public bool allowsCoded;
    public bool allowsProvocative;
    public bool allowsDeceptive;

    [Header("Rewards (coins + standing + mastery; no new currency)")]
    public int coinReward;
    public int standingReward;
    public int masteryReward;

    [Header("Gazette")]
    [Tooltip("When true, this commission produces a Gazette misunderstanding beat")]
    public bool gazetteMisunderstanding;
    [TextArea(2, 4)]
    public string gazetteText;
}
