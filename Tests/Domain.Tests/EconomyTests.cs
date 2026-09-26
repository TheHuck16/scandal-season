using System;
using System.Collections.Generic;
using ScandalSeason.Domain.Economy;
using Xunit;

namespace ScandalSeason.Domain.Tests.Economy;

public sealed class EconomyTests
{
    [Fact]
    public void GameRules_AdsAreDisabled()
    {
        Assert.False(GameRules.AdsEnabled); // No ads of any kind. Ever.
        Assert.Equal(10, GameRules.BookOneSeasons);
        Assert.Equal(30, GameRules.ChaptersPerSeason); // 30 chapters per season...
        Assert.Equal(40, GameRules.ScenesPerChapter);  // ...× 40 scenes per chapter.
        Assert.Equal("Crowns", GameRules.PremiumCurrencyName);
        Assert.Equal("Coins", GameRules.SoftCurrencyName);
    }

    [Fact]
    public void Wallet_SpendFailsWhenInsufficient_AndBalanceUnchanged()
    {
        var wallet = new Wallet(crowns: 10, coins: 5);
        Assert.False(wallet.TrySpend(Currency.Crowns, 11));
        Assert.False(wallet.TrySpend(Currency.Coins, 6));
        Assert.Equal(10, wallet.Crowns);
        Assert.Equal(5, wallet.Coins);
    }

    [Fact]
    public void Wallet_GrantThenSpend()
    {
        var wallet = new Wallet();
        wallet.Grant(Currency.Crowns, 100);
        wallet.Grant(Currency.Coins, 50);
        Assert.True(wallet.TrySpend(Currency.Crowns, 40));
        Assert.True(wallet.TrySpend(Currency.Coins, 50));
        Assert.Equal(60, wallet.Crowns);
        Assert.Equal(0, wallet.Coins);
    }

    [Fact]
    public void Energy_RegeneratesOverTimeUpToCap()
    {
        var start = new DateTime(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);
        var energy = new EnergySystem(maxEnergy: 10, regenInterval: TimeSpan.FromMinutes(5), startEnergy: 7, startUtc: start);

        energy.Refresh(start.AddMinutes(16)); // 3 full intervals

        Assert.Equal(10, energy.Current); // capped at max
    }

    [Fact]
    public void Energy_ConsumeFailsWhenInsufficient()
    {
        var start = new DateTime(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);
        var energy = new EnergySystem(10, TimeSpan.FromMinutes(5), 3, start);

        Assert.False(energy.TryConsume(4, start));
        Assert.Equal(3, energy.Current);
        Assert.True(energy.TryConsume(3, start));
        Assert.Equal(0, energy.Current);
    }

    [Fact]
    public void Energy_PinsTimerWhileFull()
    {
        var start = new DateTime(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);
        var energy = new EnergySystem(10, TimeSpan.FromMinutes(5), 10, start);

        energy.Refresh(start.AddHours(2)); // long idle while full: no phantom credit
        Assert.True(energy.TryConsume(1, start.AddHours(2)));
        Assert.Equal(9, energy.Current);
        // Next regen is a full interval after the spend, not sooner.
        Assert.True(energy.TimeUntilNext(start.AddHours(2)) >= TimeSpan.FromMinutes(4.9));
    }

    [Fact]
    public void CatalogValidator_RejectsPurchaseOnlyEntry()
    {
        var entries = new List<CatalogEntry>
        {
            new CatalogEntry
            {
                Id = "gown-1",
                DisplayName = "Regency Gown",
                CrownPrice = 499,
                TimeToEarn = TimeSpan.Zero, // no free path — forbidden
                EarnDescription = ""
            }
        };
        Assert.Throws<InvalidOperationException>(() => CatalogValidator.EnsureFreePathExists(entries));
    }

    [Fact]
    public void CatalogValidator_AcceptsEntryWithTimePath()
    {
        var entries = new List<CatalogEntry>
        {
            new CatalogEntry
            {
                Id = "gown-1",
                DisplayName = "Regency Gown",
                CrownPrice = 499, // money expedites...
                TimeToEarn = TimeSpan.FromDays(7), // ...but time earns the same reward.
                EarnDescription = "Complete 7 Daily Votes"
            },
            new CatalogEntry
            {
                Id = "hair-1",
                DisplayName = "Pinned Updo",
                CrownPrice = null, // time-only: never purchasable
                TimeToEarn = TimeSpan.FromDays(3),
                EarnDescription = "Log in 3 days in a row"
            }
        };
        CatalogValidator.EnsureFreePathExists(entries); // must not throw
    }

    [Fact]
    public void TimeBasedGrants_DailyTableCapsAtMax()
    {
        int[] table = { 100, 150, 200 };
        Assert.Equal(100, TimeBasedGrants.DailyEngagementCoins(1, table));
        Assert.Equal(200, TimeBasedGrants.DailyEngagementCoins(3, table));
        Assert.Equal(200, TimeBasedGrants.DailyEngagementCoins(30, table)); // capped
    }
}
