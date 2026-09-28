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
    public void StoreCatalog_PackLadderAnchoredToDebutantesChest()
    {
        // Anchor: ~30-40 Crowns and ~5 energy per dollar.
        var anchor = StoreCatalog.DebutantesChest;
        Assert.Equal(17, StoreCatalog.AllPacks.Length);

        foreach (var pack in StoreCatalog.AllPacks)
        {
            decimal price = decimal.Parse(pack.PriceUsd.TrimStart('$'));
            decimal crownsPerDollar = pack.Crowns / price;
            decimal energyPerDollar = pack.Energy / price;
            bool energyForward = pack.Bonus.Contains("energy-forward");

            if (energyForward)
            {
                // Energy-forward flavors: energy to 8/$, Crowns at or under 15/$.
                Assert.True(energyPerDollar <= 8.5m,
                    $"{pack.Id}: energy-forward energy {energyPerDollar:F1}/$ exceeds 8/$");
                Assert.True(crownsPerDollar <= 15.5m,
                    $"{pack.Id}: energy-forward crowns {crownsPerDollar:F1}/$ exceeds 15/$");
            }
            else if (pack.Bonus.Contains("atelier"))
            {
                // Atelier flavors sell the exclusive cosmetic; currency may run
                // under the anchor since the accessory carries the value.
                Assert.True(crownsPerDollar <= 45m,
                    $"{pack.Id}: atelier crowns {crownsPerDollar:F1}/$ exceeds anchor ceiling");
            }
            else
            {
                // Balanced: anchored to the Debutante's Chest rates.
                Assert.True(crownsPerDollar >= 25m && crownsPerDollar <= 45m,
                    $"{pack.Id}: crowns {crownsPerDollar:F1}/$ outside anchor band");
                Assert.True(energyPerDollar >= 3m && energyPerDollar <= 7m,
                    $"{pack.Id}: energy {energyPerDollar:F1}/$ outside anchor band");
            }
        }
    }

    [Fact]
    public void StoreCatalog_OneTimeRampIsSequential()
    {
        var ramp = StoreCatalog.AllPacks
            .Where(p => p.Tier == StoreCatalog.PackTier.OneTimeRamp)
            .OrderBy(p => p.RampOrder)
            .ToArray();
        Assert.Equal(5, ramp.Length);
        for (int i = 0; i < ramp.Length; i++)
            Assert.Equal(i + 1, ramp[i].RampOrder);
        Assert.Equal("morning_call", ramp[0].Id);
        Assert.Equal("debutantes_chest", ramp[4].Id);
    }

    [Fact]
    public void StoreCatalog_EntryIsFourNinetyNine()
    {
        // Beth-locked Sep 28, 2026: entry price is $4.99 — no sub-$4.99 tier exists.
        var entry = StoreCatalog.AllPacks
            .Where(p => p.Tier == StoreCatalog.PackTier.OneTimeRamp)
            .OrderBy(p => p.RampOrder)
            .First();
        Assert.Equal("$4.99", entry.PriceUsd);
        Assert.All(StoreCatalog.AllPacks, p =>
            Assert.NotEqual("$1.99", p.PriceUsd));
    }

    [Fact]
    public void StoreCatalog_AtelierChestIsWeekly()
    {
        // Beth-locked Sep 28, 2026: the Atelier chest appears weekly —
        // neither one-time-only nor always-on. One purchase per appearance.
        var weekly = StoreCatalog.AllWeeklyOffers;
        Assert.Single(weekly);
        Assert.Equal("atelier_chest_weekly", weekly[0].Id);
        Assert.Equal(1, weekly[0].PurchasesPerAppearance);
    }

    [Fact]
    public void StoreCatalog_NoWhaleLanguage()
    {
        // The guardrail bans "whale" outright — player-facing and internal.
        foreach (var pack in StoreCatalog.AllPacks)
        {
            Assert.DoesNotContain("whale", pack.Id, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("whale", pack.Bonus, StringComparison.OrdinalIgnoreCase);
        }
    }
}
