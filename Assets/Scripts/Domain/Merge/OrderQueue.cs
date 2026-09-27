// Scandal Season — Domain core. PLAIN C# ONLY: no UnityEngine references.
// Merge-board order queue: the free player's coin income engine.
//
// Locked Sep 27, 2026 (Beth):
//   - The board always carries open orders: up to 10 standing order slots (DEFAULT).
//   - When a slot empties, the generator deals a new random order after a short
//     delay (DEFAULT: 5 minutes).
//   - Orders span easy to hard (DEFAULT: a mix of low-, mid-, and high-tier requests).
//   - Reachability guarantee: a generated order never requests an item the player
//     could not merge into existence with unlimited time and energy — unlocked
//     chains only, always producible (any level 1..MaxChainLevel of an unlocked
//     chain is reachable by merging).
//   - Payouts scale with tier (tier = requested item's chain level):
//     commissions round(2*tier*3.0), custom orders round(2*tier*1.0).
//   - Authored story commissions sit alongside generated custom orders; their
//     payouts are authored and unchanged.
// Values marked DEFAULT are tunable via constructor parameters; the mix shape
// (low 1-3 / mid 4-7 / high 8-10) is the DEFAULT interpretation and can be
// replaced by supplying generated orders through AddCommission or by reusing
// GenerateCustomOrder with different bands.

using System;
using System.Collections.Generic;

namespace ScandalSeason.Domain.Merge
{
    /// <summary>
    /// One open order: a request for a specific merge-chain item plus its coin payout.
    /// </summary>
    public sealed class OrderRequest
    {
        public string OrderId { get; }
        public string ChainId { get; }
        public int Level { get; }
        public int CoinPayout { get; }

        /// <summary>True for authored story commissions; false for generated custom orders.</summary>
        public bool IsCommission { get; }

        public OrderRequest(string orderId, string chainId, int level, int coinPayout, bool isCommission)
        {
            if (string.IsNullOrWhiteSpace(orderId))
                throw new ArgumentException("Order id is required.", nameof(orderId));
            if (string.IsNullOrWhiteSpace(chainId))
                throw new ArgumentException("Chain id is required.", nameof(chainId));
            if (level < 1 || level > MergeBoard.MaxChainLevel)
                throw new ArgumentOutOfRangeException(nameof(level), $"Level must be 1..{MergeBoard.MaxChainLevel}.");
            if (coinPayout < 0)
                throw new ArgumentOutOfRangeException(nameof(coinPayout), "Payout cannot be negative.");
            OrderId = orderId;
            ChainId = chainId;
            Level = level;
            CoinPayout = coinPayout;
            IsCommission = isCommission;
        }
    }

    /// <summary>
    /// The merge board's standing order queue. Deterministic when constructed with
    /// a seed (tests, replays). All times are UTC.
    /// </summary>
    public sealed class OrderQueue
    {
        /// <summary>DEFAULT: up to 10 standing order slots.</summary>
        public const int DefaultMaxStandingOrders = 10;

        /// <summary>DEFAULT: a new order is dealt 5 minutes after a slot empties.</summary>
        public static readonly TimeSpan DefaultRefillDelay = TimeSpan.FromMinutes(5);

        // DEFAULT interpretation of the easy-to-hard mix: uniform pick of a band,
        // then uniform pick of a level inside it. Bands are chain levels.
        private const int LowBandMax = 3;
        private const int MidBandMax = 7;

        public int MaxStandingOrders { get; }
        public TimeSpan RefillDelay { get; }

        private readonly List<string> _unlockedChainIds;
        private readonly OrderRequest?[] _slots;
        private readonly DateTime[] _refillAtUtc;
        private readonly Random _random;
        private int _nextOrderSeq;

        public OrderQueue(
            IReadOnlyList<string> unlockedChainIds,
            int maxStandingOrders = DefaultMaxStandingOrders,
            TimeSpan? refillDelay = null,
            int? seed = null)
        {
            if (unlockedChainIds == null) throw new ArgumentNullException(nameof(unlockedChainIds));
            if (maxStandingOrders < 1) throw new ArgumentOutOfRangeException(nameof(maxStandingOrders));
            var delay = refillDelay ?? DefaultRefillDelay;
            if (delay < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(refillDelay));

            MaxStandingOrders = maxStandingOrders;
            RefillDelay = delay;
            _unlockedChainIds = new List<string>(unlockedChainIds);
            _slots = new OrderRequest?[maxStandingOrders];
            _refillAtUtc = new DateTime[maxStandingOrders];
            _random = seed.HasValue ? new Random(seed.Value) : new Random();

            // The board always carries open orders: fill every slot it can.
            for (int i = 0; i < maxStandingOrders; i++)
                DealIntoSlot(i);
        }

        public IReadOnlyList<string> UnlockedChainIds => _unlockedChainIds;

        /// <summary>Number of slots currently holding an open order.</summary>
        public int StandingOrderCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _slots.Length; i++)
                    if (_slots[i] != null) count++;
                return count;
            }
        }

        public OrderRequest? GetOrder(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Length)
                throw new ArgumentOutOfRangeException(nameof(slotIndex));
            return _slots[slotIndex];
        }

        /// <summary>
        /// Deals generated/custom and authored orders only against unlocked chains —
        /// the reachability guarantee. Throws if the chain is locked or unknown.
        /// </summary>
        public static void EnsureReachable(IReadOnlyList<string> unlockedChainIds, string chainId)
        {
            if (unlockedChainIds == null) throw new ArgumentNullException(nameof(unlockedChainIds));
            if (!_unlockedChainIdsContains(unlockedChainIds, chainId))
                throw new InvalidOperationException(
                    $"Order for chain '{chainId}' violates the reachability guarantee: chain is not unlocked.");
        }

        private static bool _unlockedChainIdsContains(IReadOnlyList<string> unlockedChainIds, string chainId)
        {
            for (int i = 0; i < unlockedChainIds.Count; i++)
                if (string.Equals(unlockedChainIds[i], chainId, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>Locked payout formula for authored story commissions.</summary>
        public static int CommissionPayout(int tier) => (int)Math.Round(2 * tier * 3.0);

        /// <summary>Locked payout formula for generated custom orders.</summary>
        public static int CustomOrderPayout(int tier) => (int)Math.Round(2 * tier * 1.0);

        /// <summary>
        /// Generates one custom order: a random unlocked chain, a level drawn from the
        /// easy-to-hard mix, payout per the locked custom-order formula.
        /// </summary>
        public static OrderRequest GenerateCustomOrder(
            IReadOnlyList<string> unlockedChainIds, Random random, int orderSeq)
        {
            if (unlockedChainIds == null) throw new ArgumentNullException(nameof(unlockedChainIds));
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (unlockedChainIds.Count == 0)
                throw new InvalidOperationException("Cannot generate an order: no chains unlocked.");

            string chainId = unlockedChainIds[random.Next(unlockedChainIds.Count)];

            int band = random.Next(3);
            int level = band == 0
                ? random.Next(1, LowBandMax + 1)
                : band == 1
                    ? random.Next(LowBandMax + 1, MidBandMax + 1)
                    : random.Next(MidBandMax + 1, MergeBoard.MaxChainLevel + 1);

            return new OrderRequest(
                $"order-{orderSeq}", chainId, level, CustomOrderPayout(level), isCommission: false);
        }

        /// <summary>
        /// Places an authored story commission into the first empty slot.
        /// Payout is authored (unchanged); the chain must still be unlocked.
        /// </summary>
        public void AddCommission(string orderId, string chainId, int level, int coinPayout)
        {
            EnsureReachable(_unlockedChainIds, chainId);
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null)
                {
                    _slots[i] = new OrderRequest(orderId, chainId, level, coinPayout, isCommission: true);
                    return;
                }
            }
            throw new InvalidOperationException("No empty order slot for the commission.");
        }

        /// <summary>Updates the unlocked-chain list; future generated orders draw from it.</summary>
        public void SetUnlockedChains(IReadOnlyList<string> unlockedChainIds)
        {
            if (unlockedChainIds == null) throw new ArgumentNullException(nameof(unlockedChainIds));
            _unlockedChainIds.Clear();
            _unlockedChainIds.AddRange(unlockedChainIds);
        }

        /// <summary>
        /// Attempts to fulfill the order in <paramref name="slotIndex"/> by delivering
        /// the requested item. On success the slot empties and a refill is scheduled
        /// <see cref="RefillDelay"/> out; the coin payout is returned for the caller to
        /// grant to the wallet. Never throws for rule violations — check the return.
        /// </summary>
        public bool TryFulfillOrder(int slotIndex, string chainId, int level, DateTime nowUtc, out int coinPayout)
        {
            coinPayout = 0;
            if (slotIndex < 0 || slotIndex >= _slots.Length) return false;
            var order = _slots[slotIndex];
            if (order == null) return false;
            if (!string.Equals(order.ChainId, chainId, StringComparison.Ordinal)) return false;
            if (order.Level != level) return false;

            coinPayout = order.CoinPayout;
            _slots[slotIndex] = null;
            _refillAtUtc[slotIndex] = nowUtc + RefillDelay;
            return true;
        }

        /// <summary>
        /// Deals new random orders into empty slots whose refill time has passed.
        /// Slots that cannot be dealt (no chains unlocked) stay empty.
        /// </summary>
        public void Refresh(DateTime nowUtc)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] != null) continue;
                if (nowUtc < _refillAtUtc[i]) continue;
                DealIntoSlot(i);
            }
        }

        private void DealIntoSlot(int slotIndex)
        {
            if (_unlockedChainIds.Count == 0) return;
            _slots[slotIndex] = GenerateCustomOrder(_unlockedChainIds, _random, _nextOrderSeq++);
        }
    }
}
