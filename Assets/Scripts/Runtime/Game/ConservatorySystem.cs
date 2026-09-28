// Scandal Season — Conservatory floriography game (v1.0 spec, Sep 28 2026).
// ConservatorySystem: the five-step loop (Cultivate → Refine → Interpret →
// Arrange → Deliver). Vertical slice: compact board, generator families,
// v1 lexicon, authored commissions. Scoring is transparent; beauty is
// permissive — points lost for contradicting the brief, never for failing
// to imitate a hidden arrangement.

using System;
using System.Collections.Generic;
using UnityEngine;
using ScandalSeason.Domain.Economy;

public sealed class ConservatorySystem : MonoBehaviour
{
    [Header("Content")]
    public FlowerLexiconSO lexicon;
    public BouquetCommissionSO[] commissions = new BouquetCommissionSO[0];

    [Header("Wallet (wired by Boot)")]
    public GameManager gameManager;

    [Header("Energy")]
    [Tooltip("Conservatory runs its own energy pool (independent-board rule)")]
    public int conservatoryEnergy = 50;
    public int maxConservatoryEnergy = 50;

    public event Action<BouquetCommissionSO, ArrangementScore> CommissionDelivered;

    /// <summary>
    /// Transparent score: Brief fit / Composition / Harmony.
    /// Each axis scored live; stems affecting it highlighted by the view.
    /// </summary>
    [System.Serializable]
    public sealed class ArrangementScore
    {
        public int briefFit;      // 0-100: does it say what the brief asked?
        public int composition;   // 0-100: vessel capacity, silhouette, layering
        public int harmony;       // 0-100: stem meanings combine without contradiction
        public string dominantMessage;
        public string[] undertones = new string[0];
        public int Total => briefFit + composition + harmony;
    }

    public ArrangementScore ScoreArrangement(
        BouquetCommissionSO commission,
        string[] stemNames,
        string vesselId)
    {
        var score = new ArrangementScore();
        if (commission == null || lexicon == null) return score;

        // Brief fit: required note present, forbidden note absent.
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

        score.briefFit = 0;
        if (hasRequired) score.briefFit += 60;
        if (!hasForbidden) score.briefFit += 40;
        // Permissive: partial credit, never a concealed fail state.

        // Composition: vessel defined, stems within capacity.
        // Vertical slice: full marks when vessel is set and 1+ stems placed.
        score.composition = !string.IsNullOrEmpty(vesselId) && stemNames.Length > 0 ? 100 : 0;

        // Harmony: contradictions create ambiguity, never silent cancellation.
        // Vertical slice: deduct for direct required/forbidden clash only.
        score.harmony = (hasRequired && hasForbidden) ? 40 : 100;

        // Dominant message + undertones from stem meanings.
        if (meanings.Count > 0)
        {
            score.dominantMessage = meanings[0];
            var under = new List<string>();
            for (int i = 1; i < meanings.Count && i < 4; i++)
                under.Add(meanings[i]);
            score.undertones = under.ToArray();
        }

        return score;
    }

    /// <summary>
    /// Deliver a scored arrangement. Pays coins + standing + mastery.
    /// No bouquet exists merely to be sold — delivery must move a track.
    /// </summary>
    public void Deliver(BouquetCommissionSO commission, ArrangementScore score, string intentFlag)
    {
        if (commission == null || gameManager?.Wallet == null) return;

        // Rewards scale with total score (0-300).
        float quality = Mathf.Clamp01(score.Total / 300f);
        int coins = Mathf.RoundToInt(commission.coinReward * (0.5f + 0.5f * quality));
        gameManager.Wallet.Grant(Currency.Coins, coins);

        // Intent recorded as a compact story flag for later dialogue/Gazette.
        // (Story-flag plumbing lands with the narrative system.)
        Debug.Log($"Conservatory: delivered '{commission.commissionId}' to {commission.recipient} " +
                  $"with intent '{intentFlag}', score {score.Total}/300, +{coins} coins.");

        CommissionDelivered?.Invoke(commission, score);
    }

    /// <summary>
    /// Energy is spent on generating cultivation materials only —
    /// never on moving, previewing, undoing, or arranging.
    /// </summary>
    public bool TrySpendGenerationEnergy(int amount)
    {
        if (conservatoryEnergy < amount) return false;
        conservatoryEnergy -= amount;
        return true;
    }
}
