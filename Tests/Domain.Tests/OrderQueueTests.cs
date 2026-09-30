using System;
using System.Collections.Generic;
using ScandalSeason.Domain.Merge;
using Xunit;

namespace ScandalSeason.Domain.Tests.Merge;

public sealed class OrderQueueTests
{
    private static readonly DateTime T0 = new DateTime(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);
    private static readonly string[] Chains = { "atelier.notions", "atelier.fabric", "park.seeds" };

    // Each chain's own final level. Chains vary 10-15 (core to 19) and must not
    // all share the same length — the queue bounds every order against the
    // specific chain's final level, never a global cap.
    private static readonly Dictionary<string, int> ChainMaxLevels = new Dictionary<string, int>
    {
        ["atelier.notions"] = 12,
        ["atelier.fabric"] = 15,
        ["park.seeds"] = 10,
    };

    private static OrderQueue NewQueue(int? seed = 42) =>
        new OrderQueue(Chains, ChainMaxLevels, seed: seed);

    [Fact]
    public void Queue_StartsFull_WithDefaultSlotCount()
    {
        var q = NewQueue();
        Assert.Equal(OrderQueue.DefaultMaxStandingOrders, q.StandingOrderCount);
        Assert.Equal(10, OrderQueue.DefaultMaxStandingOrders);
    }

    [Fact]
    public void Queue_CustomSlotCount_Respected()
    {
        var q = new OrderQueue(Chains, ChainMaxLevels, maxStandingOrders: 3, seed: 1);
        Assert.Equal(3, q.StandingOrderCount);
    }

    [Fact]
    public void Fulfill_MatchingItem_PaysCustomOrderFormula()
    {
        var q = NewQueue();
        var order = q.GetOrder(0)!;
        Assert.False(order.IsCommission);
        bool ok = q.TryFulfillOrder(0, order.ChainId, order.Level, T0, out int payout);
        Assert.True(ok);
        Assert.Equal(2 * order.Level, payout); // round(2*tier*1.0)
        Assert.Equal(9, q.StandingOrderCount);
        Assert.Null(q.GetOrder(0));
    }

    [Fact]
    public void Fulfill_WrongItem_Rejected_SlotUnchanged()
    {
        var q = NewQueue();
        var order = q.GetOrder(1)!;
        int wrongLevel = order.Level == 1 ? 2 : 1;
        Assert.False(q.TryFulfillOrder(1, order.ChainId, wrongLevel, T0, out int payout));
        Assert.False(q.TryFulfillOrder(1, "park.carriages", order.Level, T0, out payout));
        Assert.Equal(0, payout);
        Assert.Equal(10, q.StandingOrderCount);
        Assert.NotNull(q.GetOrder(1));
    }

    [Fact]
    public void Fulfill_EmptySlot_Rejected()
    {
        var q = NewQueue();
        var order = q.GetOrder(0)!;
        Assert.True(q.TryFulfillOrder(0, order.ChainId, order.Level, T0, out _));
        Assert.False(q.TryFulfillOrder(0, order.ChainId, order.Level, T0, out _));
    }

    [Fact]
    public void Refill_OnlyAfterDelay()
    {
        var q = NewQueue();
        var order = q.GetOrder(2)!;
        Assert.True(q.TryFulfillOrder(2, order.ChainId, order.Level, T0, out _));
        Assert.Equal(9, q.StandingOrderCount);

        q.Refresh(T0.AddMinutes(4).AddSeconds(59));
        Assert.Equal(9, q.StandingOrderCount); // delay not yet elapsed

        q.Refresh(T0.AddMinutes(5));
        Assert.Equal(10, q.StandingOrderCount); // refilled
        Assert.NotNull(q.GetOrder(2));
    }

    [Fact]
    public void GeneratedOrders_OnlyRequestUnlockedChains_WithinLevelBounds()
    {
        for (int seed = 0; seed < 20; seed++)
        {
            var q = new OrderQueue(Chains, ChainMaxLevels, seed: seed);
            for (int i = 0; i < q.MaxStandingOrders; i++)
            {
                var order = q.GetOrder(i)!;
                Assert.Contains(order.ChainId, Chains);
                // Bounded by that chain's own final level, never a global cap.
                Assert.InRange(order.Level, 1, ChainMaxLevels[order.ChainId]);
            }
        }
    }

    [Fact]
    public void GeneratedOrders_CoverEasyToHardMix()
    {
        var seenLow = false; var seenMid = false; var seenHigh = false;
        for (int seed = 0; seed < 50 && !(seenLow && seenMid && seenHigh); seed++)
        {
            var q = new OrderQueue(Chains, ChainMaxLevels, seed: seed);
            for (int i = 0; i < q.MaxStandingOrders; i++)
            {
                int level = q.GetOrder(i)!.Level;
                if (level <= 3) seenLow = true;
                else if (level <= 7) seenMid = true;
                else seenHigh = true;
            }
        }
        Assert.True(seenLow && seenMid && seenHigh);
    }

    [Fact]
    public void PayoutFormulas_MatchLockedValues()
    {
        // Payouts scale with tier across the full legal chain-length range (10-19).
        for (int tier = 1; tier <= 19; tier++)
        {
            Assert.Equal(6 * tier, OrderQueue.CommissionPayout(tier)); // round(2*tier*3.0)
            Assert.Equal(2 * tier, OrderQueue.CustomOrderPayout(tier)); // round(2*tier*1.0)
        }
    }

    [Fact]
    public void AddCommission_AuthoredPayout_PlacesInEmptySlot()
    {
        var q = NewQueue();
        var order = q.GetOrder(0)!;
        Assert.True(q.TryFulfillOrder(0, order.ChainId, order.Level, T0, out _));
        q.AddCommission("commission-rose-gown", "atelier.fabric", 8, 500);
        var placed = q.GetOrder(0)!;
        Assert.True(placed.IsCommission);
        Assert.Equal(500, placed.CoinPayout);
        Assert.Equal("commission-rose-gown", placed.OrderId);

        // Fulfilling the commission pays the authored amount, unchanged.
        Assert.True(q.TryFulfillOrder(0, "atelier.fabric", 8, T0, out int payout));
        Assert.Equal(500, payout);
    }

    [Fact]
    public void AddCommission_LockedChain_ViolatesReachabilityGuarantee()
    {
        var q = NewQueue();
        var order = q.GetOrder(0)!;
        Assert.True(q.TryFulfillOrder(0, order.ChainId, order.Level, T0, out _));
        Assert.Throws<InvalidOperationException>(
            () => q.AddCommission("bad", "park.carriages", 3, 100));
    }

    [Fact]
    public void AddCommission_LevelBeyondChainFinal_Throws()
    {
        // park.seeds runs 10 levels; a level-11 commission is unproducible.
        var q = NewQueue();
        var order = q.GetOrder(0)!;
        Assert.True(q.TryFulfillOrder(0, order.ChainId, order.Level, T0, out _));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => q.AddCommission("bad", "park.seeds", 11, 100));
        // At the chain's own final level is fine.
        q.AddCommission("ok", "park.seeds", 10, 100);
        Assert.Equal("ok", q.GetOrder(0)!.OrderId);
    }

    [Fact]
    public void Queue_WithNoUnlockedChains_StaysEmpty_UntilChainsUnlock()
    {
        var q = new OrderQueue(Array.Empty<string>(), ChainMaxLevels, seed: 7);
        Assert.Equal(0, q.StandingOrderCount);
        q.SetUnlockedChains(Chains);
        q.Refresh(T0);
        Assert.Equal(10, q.StandingOrderCount);
    }

    [Fact]
    public void Deterministic_WithSeed()
    {
        var a = NewQueue(seed: 99);
        var b = NewQueue(seed: 99);
        for (int i = 0; i < a.MaxStandingOrders; i++)
        {
            var oa = a.GetOrder(i)!; var ob = b.GetOrder(i)!;
            Assert.Equal(oa.ChainId, ob.ChainId);
            Assert.Equal(oa.Level, ob.Level);
            Assert.Equal(oa.CoinPayout, ob.CoinPayout);
        }
    }

    [Fact]
    public void Fulfill_InvalidSlotIndex_Rejected()
    {
        var q = NewQueue();
        Assert.False(q.TryFulfillOrder(-1, "atelier.notions", 1, T0, out _));
        Assert.False(q.TryFulfillOrder(10, "atelier.notions", 1, T0, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => q.GetOrder(10));
    }
}
