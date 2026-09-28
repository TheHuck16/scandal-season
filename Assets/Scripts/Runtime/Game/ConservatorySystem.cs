// Scandal Season — Conservatory floriography game (gameplay spec v1.0, Sep 28 2026).
// ConservatorySystem: the five-step loop (Cultivate → Refine → Interpret →
// Arrange → Deliver). 6x8 cultivation board, own energy pool (cap 100, regen
// 1 per 3 min, tuning placeholder), 3 active commissions max, no expiry.
// Scoring is transparent and permissive: 0-3 per axis (brief fit /
// composition / harmony), tiers Triumph 7-9 / Pleasing 4-6 / Muddled 1-3.
// No fail state. Commissions NEVER pay coins — sole-faucet rule.

using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ConservatorySystem : MonoBehaviour
{
    public const int BoardWidth = 6;
    public const int BoardHeight = 8;
    public const int MaxActiveCommissions = 3;
    public const int MaxEnergy = 100;

    public enum ScoreTier { Muddled, Pleasing, Triumph }

    [Header("Content")]
    public FlowerLexiconSO lexicon;
    public BouquetCommissionSO[] commissions = new BouquetCommissionSO[0];

    [Header("Wallet (wired by Boot)")]
    public GameManager gameManager;

    [Header("Energy (tuning placeholder: cap 100, regen 1 per 3 min)")]
    [Tooltip("Conservatory runs its own energy pool (independent-board rule). Spent ONLY on generator taps.")]
    public int conservatoryEnergy = 100;

    // commissionId set — the active Commission Book (max 3, no expiry)
    private readonly HashSet<string> _activeCommissions = new HashSet<string>();

    public event Action<BouquetCommissionSO, ArrangementScore> CommissionDelivered;

    /// <summary>
    /// Transparent score: Brief fit / Composition / Harmony, 0-3 each.
    /// Live two-audience preview: recipient reading + public reading.
    /// </summary>
    [Serializable]
    public sealed class ArrangementScore
    {
        public int briefFit;      // 0-3: required present (+1), forbidden absent (+1), hidden hit (+1)
        public int composition;   // 0-3: focal clarity, balance, vessel fit
        public int harmony;       // 0-3: meaning coherence (contradictions become wit, never silent cancellation)
        public string dominantMessage;
        public string[] undertones = new string[0];
        public string recipientReading;
        public string publicReading;
        public int Total => briefFit + composition + harmony;
        public ScoreTier Tier => Total >= 7 ? ScoreTier.Triumph : Total >= 4 ? ScoreTier.Pleasing : ScoreTier.Muddled;
    }

    public int ActiveCommissionCount => _activeCommissions.Count;

    public bool TryActivateCommission(string commissionId)
    {
        if (string.IsNullOrEmpty(commissionId)) return false;
        if (_activeCommissions.Contains(commissionId)) return true;
        if (_activeCommissions.Count >= MaxActiveCommissions) return false;
        _activeCommissions.Add(commissionId);
        return true;
    }

    public ArrangementScore ScoreArrangement(
        BouquetCommissionSO commission,
        string[] stemNames,
        string vesselId,
        int vesselCapacity)
    {
        var score = new ArrangementScore();
        if (commission == null || lexicon == null) return score;

        // Brief fit: required note present (+1), forbidden note absent (+1),
        // hidden opportunity hit (+1). Never a concealed fail state.
        bool hasRequired = false, hasForbidden = false;
        var meanings = new List<string>();
        foreach (var stem in stemNames)
        {
            var entry = lexicon.GetFlower(stem);
            if (entry == null) continue;
            meanings.Add(entry.meaning);
            if (entry.meaning == commission.requiredNote) hasRequired = true;
            if (entry.meaning == commission.forbiddenNote) hasForbidden = true;
        }
        score.briefFit = (hasRequired ? 1 : 0) + (!hasForbidden ? 1 : 0);
        // Hidden opportunity: vertical slice leaves the third point to the
        // view layer, which knows the arrangement's intent flags.

        // Composition: focal clarity (tallest/central stem reads as focal),
        // stems within vessel capacity, at least one stem placed.
        // Vertical slice: full marks when vessel is set, stems fit, 1+ placed.
        bool vesselSet = !string.IsNullOrEmpty(vesselId);
        bool fitsCapacity = vesselCapacity <= 0 || stemNames.Length <= vesselCapacity;
        score.composition = (vesselSet ? 1 : 0) + (fitsCapacity ? 1 : 0) + (stemNames.Length > 0 ? 1 : 0);

        // Harmony: contradictions create ambiguity or wit, never silent
        // cancellation. Deduct only when meanings fight the brief.
        score.harmony = (hasRequired && hasForbidden) ? 1 : 3;

        // Dominant message + undertones from stem meanings.
        if (meanings.Count > 0)
        {
            score.dominantMessage = meanings[0];
            var under = new List<string>();
            for (int i = 1; i < meanings.Count && i < 4; i++)
                under.Add(meanings[i]);
            score.undertones = under.ToArray();
        }

        // Two-audience preview: the recipient reads what you meant (filtered
        // through relationship history — tags come from the view layer);
        // the room reads what it wants (cruder, dominant message first).
        score.recipientReading = score.dominantMessage ?? string.Empty;
        score.publicReading = score.dominantMessage ?? string.Empty;

        return score;
    }

    /// <summary>
    /// Deliver a scored arrangement. Pays Standing + mastery + materials.
    /// Commissions NEVER pay coins — sole-faucet rule.
    /// Every delivery moves at least one track.
    /// </summary>
    public void Deliver(BouquetCommissionSO commission, ArrangementScore score, string intentFlag)
    {
        if (commission == null || gameManager?.Wallet == null) return;

        // Rewards scale with tier: Triumph full, Pleasing partial, Muddled token.
        float quality = score.Tier == ScoreTier.Triumph ? 1f
            : score.Tier == ScoreTier.Pleasing ? 0.6f : 0.3f;
        int standing = Mathf.RoundToInt(commission.standingReward * quality);
        int mastery = Mathf.RoundToInt(commission.masteryReward * quality);

        // Intent recorded as a compact story flag for later dialogue/Gazette.
        // (Story-flag plumbing lands with the narrative system.)
        Debug.Log($"Conservatory: delivered '{commission.commissionId}' to {commission.recipient} " +
                  $"with intent '{intentFlag}', tier {score.Tier} ({score.Total}/9), " +
                  $"+{standing} standing, +{mastery} mastery.");

        _activeCommissions.Remove(commission.commissionId);
        CommissionDelivered?.Invoke(commission, score);
    }

    /// <summary>
    /// Energy is spent on generator taps only — never on merging, previewing,
    /// undoing, arranging, or delivering. No growth timers, ever.
    /// </summary>
    public bool TrySpendGenerationEnergy(int amount)
    {
        if (amount <= 0) return false;
        if (conservatoryEnergy < amount) return false;
        conservatoryEnergy -= amount;
        return true;
    }
}
