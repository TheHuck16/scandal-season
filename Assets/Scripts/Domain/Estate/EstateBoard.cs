// Scandal Season — Domain core. PLAIN C# ONLY: no UnityEngine references.
// Estate game board (buildout v1.1, Sep 28 2026): the Estate Funds faucet.
// 6x6 secondary board, pair merges only, no energy, no timers, no fail state.
// Two chains that never cross-merge:
//   estate.work: Work Order -> Materials Cart -> Scaffolding -> Master Craftsmen -> Topped Out
//   estate.jobs: Small Job -> Tool Basket -> Tidy Barrow -> Order Restored -> The Estate Shines
// Payouts: L2=2, L3=5, L4=12 EF on merge; L5 tap = 30 EF (consumed);
// double-L5 merge = 75 EF. Small Jobs L5 tap = 1 EF + vignette.

using System;
using System.Collections.Generic;

namespace ScandalSeason.Domain.Estate
{
    /// <summary>Estate Funds payout table (locked Sep 28 2026).</summary>
    public static class EstatePayouts
    {
        public const int MergeToL2 = 2;
        public const int MergeToL3 = 5;
        public const int MergeToL4 = 12;
        public const int SignOffL5 = 30;
        public const int GrandRestoration = 75;
        public const int SmallJobsL5 = 1;
        public const int TrayOverflow = 1;
        public const int TrayCapacity = 12;

        public static int MergePayout(int resultLevel) => resultLevel switch
        {
            2 => MergeToL2,
            3 => MergeToL3,
            4 => MergeToL4,
            _ => 0
        };
    }

    /// <summary>One estate board item: chain id + level (1-based).</summary>
    public sealed class EstateItem
    {
        public string ChainId { get; }
        public int Level { get; }

        public const string WorkChain = "estate.work";
        public const string JobsChain = "estate.jobs";
        public const int MaxLevel = 5;

        public EstateItem(string chainId, int level)
        {
            if (string.IsNullOrWhiteSpace(chainId))
                throw new ArgumentException("Chain id is required.", nameof(chainId));
            if (level < 1 || level > MaxLevel)
                throw new ArgumentOutOfRangeException(nameof(level), "Level is 1-5.");
            ChainId = chainId;
            Level = level;
        }

        public bool IsWorkOrder => ChainId == WorkChain;
        public bool IsSmallJob => ChainId == JobsChain;
    }

    /// <summary>Outcome of an estate board action. Never throws for rule violations.</summary>
    public sealed class EstateActionResult
    {
        public bool Success { get; }
        public string? Error { get; }
        public int EstateFundsEarned { get; }
        public string? Vignette { get; }

        private EstateActionResult(bool success, string? error, int funds, string? vignette)
        {
            Success = success;
            Error = error;
            EstateFundsEarned = funds;
            Vignette = vignette;
        }

        public static EstateActionResult Ok(int funds = 0, string? vignette = null)
            => new EstateActionResult(true, null, funds, vignette);
        public static EstateActionResult Fail(string error)
            => new EstateActionResult(false, error, 0, null);
    }

    /// <summary>
    /// Estate game board: 6x6 grid + pigeonhole tray (capacity 12).
    /// Pair merges only. The Day-Book deals unlimited L1 Small Jobs.
    /// </summary>
    public sealed class EstateBoard
    {
        public const int Width = 6;
        public const int Height = 6;

        private readonly EstateItem?[,] _grid = new EstateItem?[Width, Height];
        private readonly Queue<EstateItem> _tray = new Queue<EstateItem>();

        public int TrayCount => _tray.Count;
        public int OccupiedCells
        {
            get
            {
                int n = 0;
                for (int x = 0; x < Width; x++)
                    for (int y = 0; y < Height; y++)
                        if (_grid[x, y] != null) n++;
                return n;
            }
        }

        public EstateItem? Get(int x, int y)
            => x >= 0 && x < Width && y >= 0 && y < Height ? _grid[x, y] : null;

        /// <summary>Deposits a Work Order (or Rush Order L2) from a fulfilled main-board order.</summary>
        public EstateActionResult DepositWorkOrder(bool rush = false)
        {
            var item = new EstateItem(EstateItem.WorkChain, rush ? 2 : 1);
            return PlaceOrTray(item);
        }

        /// <summary>The Day-Book: unlimited L1 Small Jobs, always free.</summary>
        public EstateActionResult DealSmallJob()
            => PlaceOrTray(new EstateItem(EstateItem.JobsChain, 1));

        private EstateActionResult PlaceOrTray(EstateItem item)
        {
            var free = FindFreeCell();
            if (free.HasValue)
            {
                _grid[free.Value.x, free.Value.y] = item;
                return EstateActionResult.Ok();
            }
            if (_tray.Count < EstatePayouts.TrayCapacity)
            {
                _tray.Enqueue(item);
                return EstateActionResult.Ok();
            }
            // Tray full: auto-redeem for 1 EF. Nothing is ever wasted.
            return EstateActionResult.Ok(EstatePayouts.TrayOverflow);
        }

        /// <summary>Moves one tray item back onto the board when space frees.</summary>
        public EstateActionResult RestoreFromTray()
        {
            if (_tray.Count == 0) return EstateActionResult.Fail("Tray is empty.");
            var free = FindFreeCell();
            if (!free.HasValue) return EstateActionResult.Fail("Board is full.");
            _grid[free.Value.x, free.Value.y] = _tray.Dequeue();
            return EstateActionResult.Ok();
        }

        /// <summary>
        /// Pair merge: two items of the same chain AND level combine into one
        /// of level+1. L5+L5 (work chain) = Grand Restoration (75 EF).
        /// Small Jobs L5 has no Grand Restoration — keep it humble.
        /// </summary>
        public EstateActionResult TryMerge(int x1, int y1, int x2, int y2)
        {
            var a = Get(x1, y1);
            var b = Get(x2, y2);
            if (a == null || b == null) return EstateActionResult.Fail("Both cells must hold items.");
            if (x1 == x2 && y1 == y2) return EstateActionResult.Fail("Cannot merge an item with itself.");
            if (a.ChainId != b.ChainId || a.Level != b.Level)
                return EstateActionResult.Fail("Pair merges require the same chain and level.");
            if (a.Level >= EstateItem.MaxLevel)
            {
                // Double-L5 work chain: Grand Restoration.
                if (a.IsWorkOrder)
                {
                    _grid[x1, y1] = null;
                    _grid[x2, y2] = null;
                    return EstateActionResult.Ok(EstatePayouts.GrandRestoration);
                }
                return EstateActionResult.Fail("Small Jobs L5 does not merge further.");
            }

            _grid[x1, y1] = null;
            _grid[x2, y2] = null;
            _grid[x1, y1] = new EstateItem(a.ChainId, a.Level + 1);
            return EstateActionResult.Ok(EstatePayouts.MergePayout(a.Level + 1));
        }

        /// <summary>
        /// Tap an L5: work chain signs off for 30 EF (consumed); jobs chain
        /// pays 1 EF + a vignette line (consumed).
        /// </summary>
        public EstateActionResult TapMaxLevel(int x, int y, string? jobsVignette = null)
        {
            var item = Get(x, y);
            if (item == null) return EstateActionResult.Fail("No item there.");
            if (item.Level != EstateItem.MaxLevel) return EstateActionResult.Fail("Only finished (L5) pieces can be collected.");

            _grid[x, y] = null;
            if (item.IsWorkOrder)
                return EstateActionResult.Ok(EstatePayouts.SignOffL5);
            return EstateActionResult.Ok(EstatePayouts.SmallJobsL5, jobsVignette);
        }

        public bool Move(int fromX, int fromY, int toX, int toY)
        {
            var item = Get(fromX, fromY);
            if (item == null || Get(toX, toY) != null) return false;
            _grid[fromX, fromY] = null;
            _grid[toX, toY] = item;
            return true;
        }

        private (int x, int y)? FindFreeCell()
        {
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    if (_grid[x, y] == null) return (x, y);
            return null;
        }
    }
}
