// Scandal Season — Estate game board runtime (buildout v1.1, Sep 28 2026).
// EstateBoardSystem: wires the EstateBoard domain to the wallet and views.
// The board is the SOLE faucet of Estate Funds. No energy, no timers, no fail state.
// Fed by main-board fulfilled orders (1 L1 Work Order each; ~1 in 20 is a
// gold-sealed Rush Order L2). The Day-Book deals unlimited L1 Small Jobs.

using System;
using UnityEngine;
using ScandalSeason.Domain.Estate;
using ScandalSeason.Domain.Economy;

public sealed class EstateBoardSystem : MonoBehaviour
{
    [Header("Wallet (wired by Boot)")]
    public GameManager gameManager;

    [Header("Rush Orders (Beth-approved Sep 28 2026)")]
    [Tooltip("~1 in 20 fulfilled orders arrives gold-sealed as L2")]
    [Range(0f, 1f)]
    public float rushOrderChance = 0.05f;

    public readonly EstateBoard Board = new EstateBoard();

    public event Action<int> EstateFundsEarned; // amount
    public event Action<string> SmallJobVignette; // vignette line

    private System.Random _rng = new System.Random();

    /// <summary>
    /// Called by the main board when an order is fulfilled. Deposits one L1
    /// Work Order (or a gold-sealed Rush Order L2, ~1 in 20).
    /// </summary>
    public void OnOrderFulfilled()
    {
        bool rush = _rng.NextDouble() < rushOrderChance;
        var result = Board.DepositWorkOrder(rush);
        BankResult(result);
    }

    /// <summary>The Day-Book: tap to deal an L1 Small Job. Unlimited, always free.</summary>
    public void DealSmallJob()
    {
        BankResult(Board.DealSmallJob());
    }

    public void RestoreFromTray()
    {
        BankResult(Board.RestoreFromTray());
    }

    public void TryMerge(int x1, int y1, int x2, int y2)
    {
        BankResult(Board.TryMerge(x1, y1, x2, y2));
    }

    /// <summary>
    /// Tap an L5: work chain signs off (30 EF); jobs chain pays 1 EF + vignette.
    /// The vignette pool is authored content — the view supplies the line.
    /// </summary>
    public void TapMaxLevel(int x, int y, string? jobsVignette = null)
    {
        var result = Board.TapMaxLevel(x, y, jobsVignette);
        if (result.Success && !string.IsNullOrEmpty(result.Vignette))
            SmallJobVignette?.Invoke(result.Vignette);
        BankResult(result);
    }

    public bool Move(int fromX, int fromY, int toX, int toY)
        => Board.Move(fromX, fromY, toX, toY);

    private void BankResult(EstateActionResult result)
    {
        if (!result.Success)
        {
            Debug.LogWarning($"EstateBoard: {result.Error}");
            return;
        }
        if (result.EstateFundsEarned > 0 && gameManager?.Wallet != null)
        {
            gameManager.Wallet.Grant(Currency.EstateFunds, result.EstateFundsEarned);
            EstateFundsEarned?.Invoke(result.EstateFundsEarned);
        }
    }
}
