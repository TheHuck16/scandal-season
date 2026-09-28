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
        Coins,
        /// <summary>Estate Funds — the estate's own currency. Earned only through
        /// estate-board play; never from orders, never purchasable, never
        /// Crown-convertible. Coins are entirely outside the estate.</summary>
        EstateFunds
    }

    /// <summary>Player wallet. Balances never go negative; failed spends return false.</summary>
    public sealed class Wallet
    {
        public int Crowns { get; private set; }
        public int Coins { get; private set; }
        public int EstateFunds { get; private set; }

        public Wallet(int crowns = 0, int coins = 0, int estateFunds = 0)
        {
            if (crowns < 0) throw new ArgumentOutOfRangeException(nameof(crowns));
            if (coins < 0) throw new ArgumentOutOfRangeException(nameof(coins));
            if (estateFunds < 0) throw new ArgumentOutOfRangeException(nameof(estateFunds));
            Crowns = crowns;
            Coins = coins;
            EstateFunds = estateFunds;
        }

        public void Grant(Currency currency, int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Use TrySpend to remove funds.");
            switch (currency)
            {
                case Currency.Crowns: Crowns += amount; break;
                case Currency.Coins: Coins += amount; break;
                case Currency.EstateFunds: EstateFunds += amount; break;
                default: throw new ArgumentOutOfRangeException(nameof(currency));
            }
        }

        public bool TrySpend(Currency currency, int amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Spend amount must be positive.");
            switch (currency)
            {
                case Currency.Crowns:
                    if (Crowns < amount) return false;
                    Crowns -= amount;
                    return true;
                case Currency.Coins:
                    if (Coins < amount) return false;
                    Coins -= amount;
                    return true;
                case Currency.EstateFunds:
                    if (EstateFunds < amount) return false;
                    EstateFunds -= amount;
                    return true;
                default: throw new ArgumentOutOfRangeException(nameof(currency));
            }
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
    /// Some items are time-only and never purchasable. Daily engagement grants
    /// NO coins — order fulfillment is the sole coin faucet (Beth, Sep 28 2026).
    /// </summary>
    public static class TimeBasedGrants
    {
    }

    /// <summary>
    /// Locked IAP pack lineup (Beth, Sep 28, 2026 — pack ladder v1.0, Beth's locks applied same day).
    /// Entry price is $4.99 — no sub-$4.99 tier. One-time ramp is 5 tiers (Morning Call entry
    /// through Debutante's Chest anchor). The Atelier chest appears weekly (one purchase per
    /// appearance) — neither one-time-only nor always-on.
    /// Money buys Crowns and energy ONLY — never coins, never Estate Funds, never event currency,
    /// never any future currency (Beth's locked law: we never, ever sell game currency beyond
    /// Crowns and energy). No story advancement for sale (the firewall).
    /// Raw energy grants stack above the 200 regen cap; refill packs are dead.
    /// Value anchored to the Debutante's Chest (~30-40 Crowns, ~5 energy per $).
    /// Energy-forward flavors may run energy to 8/$ with Crowns at or under 15/$.
    /// No "whale" language anywhere — player-facing or internal.
    /// Promo levers (flash bundles, treasure track, first-buy bonus, piggy bank,
    /// weekend Crown bonus) are LOCKED but build when scheduled.
    /// </summary>
    public static class StoreCatalog
    {
        public enum PackTier { OneTimeRamp, RepeatableShelf, Event, Weekly }

        public sealed class IapPack
        {
            public string Id { get; }
            public string PriceUsd { get; }
            public int Crowns { get; }
            public int Energy { get; }
            public string Bonus { get; }
            public PackTier Tier { get; }
            public int RampOrder { get; } // 1-5 for the one-time ramp; 0 otherwise

            public IapPack(string id, string priceUsd, int crowns, int energy,
                string bonus = "", PackTier tier = PackTier.RepeatableShelf, int rampOrder = 0)
            {
                Id = id; PriceUsd = priceUsd; Crowns = crowns; Energy = energy;
                Bonus = bonus; Tier = tier; RampOrder = rampOrder;
            }
        }

        // --- The one-time ramp (sequential unlock; buying tier N unlocks N+1) ---
        // Entry is $4.99 (Beth-locked Sep 28, 2026) — no sub-$4.99 tier exists.

        /// <summary>The Morning Call — $4.99 ENTRY (the sash converts, not the currency).</summary>
        public static readonly IapPack MorningCall = new IapPack(
            "morning_call", "$4.99", crowns: 200, energy: 25,
            bonus: "exclusive silk ribbon sash", tier: PackTier.OneTimeRamp, rampOrder: 1);

        /// <summary>The Promenade — unlocks after the Morning Call.</summary>
        public static readonly IapPack Promenade = new IapPack(
            "promenade", "$9.99", crowns: 400, energy: 50,
            bonus: "exclusive parasol + gloves set", tier: PackTier.OneTimeRamp, rampOrder: 2);

        /// <summary>The Assembly — unlocks after the Promenade.</summary>
        public static readonly IapPack Assembly = new IapPack(
            "assembly", "$19.99", crowns: 700, energy: 100,
            bonus: "exclusive day gown (non-story)", tier: PackTier.OneTimeRamp, rampOrder: 3);

        /// <summary>Pin Money — $49.99 value bundle (Crowns + energy, no chapter framing). One-time; no refresh, ever.</summary>
        public static readonly IapPack PinMoney = new IapPack(
            "pin_money", "$49.99", crowns: 2000, energy: 150,
            bonus: "1 atelier chest (6 random board items, tiers 2-5)",
            tier: PackTier.OneTimeRamp, rampOrder: 4);

        /// <summary>Debutante's Chest — the $99.99 anchor (replaces the old starter pack). One-time.</summary>
        public static readonly IapPack DebutantesChest = new IapPack(
            "debutantes_chest", "$99.99", crowns: 3000, energy: 500,
            bonus: "1 exclusive outfit", tier: PackTier.OneTimeRamp, rampOrder: 5);

        // --- Repeatable shelf ($9.99-$49.99; no subscriptions, no cadence gates) ---

        public static readonly IapPack AtelierCommission = new IapPack(
            "atelier_commission", "$9.99", crowns: 350, energy: 50, bonus: "balanced");
        public static readonly IapPack LongAfternoon = new IapPack(
            "long_afternoon", "$9.99", crowns: 100, energy: 80, bonus: "energy-forward");
        public static readonly IapPack RibbonBox = new IapPack(
            "ribbon_box", "$9.99", crowns: 250, energy: 40,
            bonus: "exclusive rotating accessory (atelier)");
        /// <summary>Daily offer — $19.99 daily special. Player-facing name TBD (never "Pin Money Daily").</summary>
        public static readonly IapPack DailyOffer = new IapPack(
            "daily_offer", "$19.99", crowns: 700, energy: 100, bonus: "balanced");
        public static readonly IapPack EveningEngagement = new IapPack(
            "evening_engagement", "$19.99", crowns: 200, energy: 160, bonus: "energy-forward");
        public static readonly IapPack DressmakersParcel = new IapPack(
            "dressmakers_parcel", "$19.99", crowns: 500, energy: 80,
            bonus: "exclusive rotating trim set (atelier)");
        public static readonly IapPack CountryHouse = new IapPack(
            "country_house", "$29.99", crowns: 1000, energy: 150, bonus: "balanced");
        public static readonly IapPack GrandTour = new IapPack(
            "grand_tour", "$29.99", crowns: 300, energy: 240, bonus: "energy-forward");
        public static readonly IapPack SeasonsEndowment = new IapPack(
            "seasons_endowment", "$49.99", crowns: 1800, energy: 250, bonus: "balanced");
        public static readonly IapPack JewelCase = new IapPack(
            "jewel_case", "$49.99", crowns: 1200, energy: 200,
            bonus: "exclusive rotating accessory set (atelier)");

        // --- Event packs (sold only during the event; never event currency or coins) ---

        public static readonly IapPack Invitation = new IapPack(
            "invitation", "$4.99", crowns: 160, energy: 25,
            bonus: "exclusive event cosmetic", tier: PackTier.Event);
        public static readonly IapPack GrandEntrance = new IapPack(
            "grand_entrance", "$19.99", crowns: 700, energy: 100,
            bonus: "exclusive event outfit", tier: PackTier.Event);

        public static readonly IapPack[] AllPacks =
        {
            MorningCall, Promenade, Assembly, PinMoney, DebutantesChest,
            AtelierCommission, LongAfternoon, RibbonBox, DailyOffer, EveningEngagement,
            DressmakersParcel, CountryHouse, GrandTour, SeasonsEndowment, JewelCase,
            Invitation, GrandEntrance,
        };

        // --- Weekly offers (cadence-gated; not part of the pack ladder) ---

        /// <summary>
        /// A weekly store offer. Cadence is the product: it appears once per week and
        /// allows a fixed number of purchases per appearance. Price is a display string
        /// because weekly offers are not bound to the pack ladder's value anchor.
        /// </summary>
        public sealed class WeeklyOffer
        {
            public string Id { get; }
            public string PriceUsd { get; }
            public string Bonus { get; }
            public int PurchasesPerAppearance { get; }

            public WeeklyOffer(string id, string priceUsd, string bonus, int purchasesPerAppearance)
            {
                Id = id; PriceUsd = priceUsd; Bonus = bonus;
                PurchasesPerAppearance = purchasesPerAppearance;
            }
        }

        /// <summary>The Atelier chest — appears WEEKLY (Beth-locked Sep 28, 2026).
        /// One purchase per appearance. Price pending Beth's call.</summary>
        public static readonly WeeklyOffer AtelierChestWeekly = new WeeklyOffer(
            "atelier_chest_weekly", "$TBD (Beth to set)",
            bonus: "1 atelier chest (6 random board items, tiers 2-5)",
            purchasesPerAppearance: 1);

        public static readonly WeeklyOffer[] AllWeeklyOffers = { AtelierChestWeekly };
    }
}
