// Scandal Season — Domain core. PLAIN C# ONLY: no UnityEngine references.
// Economy: Crowns (premium) + coins (soft), energy with regen, time-based grants.
// IRON RULE: time always earns the same rewards as money; money strictly expedites.
// No ads of any kind — see GameRules.AdsEnabled.

using System;
using System.Collections.Generic;

namespace ScandalSeason.Domain.Economy
{
    /// <summary>Locked game-wide rules. Change only with Beth's explicit sign-off.</summary>
    public static class GameRules
    {
        /// <summary>No ads of any kind. Not even rewarded opt-in. Revenue is IAP only.</summary>
        public const bool AdsEnabled = false;

        public const string PremiumCurrencyName = "Crowns";
        public const string SoftCurrencyName = "Coins";

        /// <summary>
        /// Book One = the 10-season arc (ruin → restoration → legacy). Seasons are
        /// effectively endless beyond it — the architecture must never cap them.
        /// </summary>
        public const int BookOneSeasons = 10;

        /// <summary>Event passes run 3–7 days, up to 2 weeks.</summary>
        public const int MinEventPassDays = 3;
        public const int MaxEventPassDays = 14;

        /// <summary>
        /// Season structure (locked): 30 chapters (levels) per season × 40 scenes
        /// per chapter = 1,200 scenes per season. Chapters are 1-based, 1..30.
        /// </summary>
        public const int ChaptersPerSeason = 30;
        public const int ScenesPerChapter = 40;

        /// <summary>Public fairness line (store copy + onboarding).</summary>
        public const string FairnessLine = "No ads. Time earns everything.";
    }

    public enum Currency
    {
        Crowns,
        Coins
    }

    /// <summary>Player wallet. Balances never go negative; failed spends return false.</summary>
    public sealed class Wallet
    {
        public int Crowns { get; private set; }
        public int Coins { get; private set; }

        public Wallet(int crowns = 0, int coins = 0)
        {
            if (crowns < 0) throw new ArgumentOutOfRangeException(nameof(crowns));
            if (coins < 0) throw new ArgumentOutOfRangeException(nameof(coins));
            Crowns = crowns;
            Coins = coins;
        }

        public void Grant(Currency currency, int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Use TrySpend to remove funds.");
            if (currency == Currency.Crowns) Crowns += amount;
            else Coins += amount;
        }

        public bool TrySpend(Currency currency, int amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Spend amount must be positive.");
            if (currency == Currency.Crowns)
            {
                if (Crowns < amount) return false;
                Crowns -= amount;
                return true;
            }
            if (Coins < amount) return false;
            Coins -= amount;
            return true;
        }
    }

    /// <summary>
    /// Energy — the only throttle on play. Plot is never time-gated.
    /// Regenerates on a fixed interval up to a cap; all times are UTC.
    /// </summary>
    public sealed class EnergySystem
    {
        public int MaxEnergy { get; }
        public TimeSpan RegenInterval { get; }
        public int Current { get; private set; }
        public DateTime LastRegenUtc { get; private set; }

        public EnergySystem(int maxEnergy, TimeSpan regenInterval, int startEnergy, DateTime startUtc)
        {
            if (maxEnergy < 1) throw new ArgumentOutOfRangeException(nameof(maxEnergy));
            if (regenInterval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(regenInterval));
            if (startEnergy < 0 || startEnergy > maxEnergy) throw new ArgumentOutOfRangeException(nameof(startEnergy));
            MaxEnergy = maxEnergy;
            RegenInterval = regenInterval;
            Current = startEnergy;
            LastRegenUtc = startUtc;
        }

        /// <summary>Advances regeneration up to <paramref name="nowUtc"/>.</summary>
        public void Refresh(DateTime nowUtc)
        {
            if (nowUtc <= LastRegenUtc) return;
            if (Current >= MaxEnergy)
            {
                // Pinned while full so no phantom regen accrues.
                LastRegenUtc = nowUtc;
                return;
            }
            long intervals = (nowUtc - LastRegenUtc).Ticks / RegenInterval.Ticks;
            if (intervals > 0)
            {
                Current = (int)Math.Min(MaxEnergy, (long)Current + intervals);
                LastRegenUtc = LastRegenUtc.AddTicks(intervals * RegenInterval.Ticks);
                if (Current >= MaxEnergy) LastRegenUtc = nowUtc;
            }
        }

        public bool TryConsume(int amount, DateTime nowUtc)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Refresh(nowUtc);
            if (Current < amount) return false;
            Current -= amount;
            return true;
        }

        /// <summary>
        /// Grants purchased energy. LOCKED Sep 27: purchased energy stacks ABOVE
        /// the 200 regen cap with no upper limit. Regen never exceeds the cap.
        /// </summary>
        public void GrantPurchased(int amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Current += amount;
        }

        public TimeSpan TimeUntilNext(DateTime nowUtc)
        {
            Refresh(nowUtc);
            if (Current >= MaxEnergy) return TimeSpan.Zero;
            return (LastRegenUtc + RegenInterval) - nowUtc;
        }
    }

    /// <summary>
    /// A reward in the catalog. The time/engagement path is REQUIRED — this is the
    /// iron rule made structural: a purchase-only reward cannot be constructed valid.
    /// </summary>
    public sealed class CatalogEntry
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";

        /// <summary>Optional money path: buy outright with Crowns. Null = not purchasable.</summary>
        public int? CrownPrice { get; set; }

        /// <summary>REQUIRED time path: how long / what engagement earns the same reward.</summary>
        public TimeSpan TimeToEarn { get; set; }

        /// <summary>REQUIRED human-readable description of the free path, e.g. "Win 5 Daily Votes".</summary>
        public string EarnDescription { get; set; } = "";
    }

    public static class CatalogValidator
    {
        /// <summary>
        /// Enforces the iron rule: every entry must define a real time/engagement path.
        /// Throws on the first purchase-only (or path-less) entry found.
        /// </summary>
        public static void EnsureFreePathExists(IEnumerable<CatalogEntry> entries)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            foreach (var entry in entries)
            {
                if (entry.TimeToEarn <= TimeSpan.Zero)
                    throw new InvalidOperationException(
                        $"Catalog entry '{entry.Id}': TimeToEarn must be positive. Purchase-only rewards are forbidden.");
                if (string.IsNullOrWhiteSpace(entry.EarnDescription))
                    throw new InvalidOperationException(
                        $"Catalog entry '{entry.Id}': EarnDescription is required. Every reward needs a stated free path.");
            }
        }
    }

    /// <summary>
    /// Time-based grants: the mechanisms by which time (not money) earns rewards.
    /// Daily engagement grants coins; some items are time-only and never purchasable.
    /// </summary>
    public static class TimeBasedGrants
    {
        /// <summary>Coins granted per consecutive day of engagement (day 1-based, capped by the table).</summary>
        public static int DailyEngagementCoins(int consecutiveDay, int[] dailyTable)
        {
            if (dailyTable == null || dailyTable.Length == 0)
                throw new ArgumentException("Daily grant table is required.", nameof(dailyTable));
            if (consecutiveDay < 1) throw new ArgumentOutOfRangeException(nameof(consecutiveDay));
            int index = Math.Min(consecutiveDay, dailyTable.Length) - 1;
            return dailyTable[index];
        }
    }

    /// <summary>
    /// Locked IAP pack lineup (Beth, Sep 27, 2026). Money buys Crowns and energy
    /// ONLY — never coins, never story advancement (the firewall). Raw energy
    /// grants stack above the 200 regen cap; refill packs are dead.
    /// Promo levers (flash bundles, treasure track, first-buy bonus, piggy bank,
    /// weekend Crown bonus, $4.99 starter offer) are LOCKED but build when scheduled.
    /// </summary>
    public static class StoreCatalog
    {
        public sealed class IapPack
        {
            public string Id { get; }
            public string PriceUsd { get; }
            public int Crowns { get; }
            public int Energy { get; }
            public string Bonus { get; }

            public IapPack(string id, string priceUsd, int crowns, int energy, string bonus = "")
            {
                Id = id; PriceUsd = priceUsd; Crowns = crowns; Energy = energy; Bonus = bonus;
            }
        }

        /// <summary>Debutante's Chest — the $99.99 anchor (replaces the old starter pack).</summary>
        public static readonly IapPack DebutantesChest = new IapPack(
            "debutantes_chest", "$99.99", crowns: 3000, energy: 500,
            bonus: "1 exclusive outfit");

        /// <summary>Pin Money — $49.99 value bundle (Crowns + energy, no chapter framing).</summary>
        public static readonly IapPack PinMoney = new IapPack(
            "pin_money", "$49.99", crowns: 2000, energy: 150,
            bonus: "1 atelier chest (6 random board items, tiers 2-5)");

        /// <summary>Daily offer — $19.99 daily special. Player-facing name TBD (never "Pin Money Daily").</summary>
        public static readonly IapPack DailyOffer = new IapPack(
            "daily_offer", "$19.99", crowns: 700, energy: 100);

        public static readonly IapPack[] AllPacks = { DebutantesChest, PinMoney, DailyOffer };
    }
}
