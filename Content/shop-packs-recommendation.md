# Shop Pack Recommendations — IAP ladder + themed-pack generator

Status: RECOMMENDATIONS ONLY. Nothing here is built. Beth approves before any `shop-packs.json` is authored.

Direction: the shop is the user spend. These packs are built to convert — real accelerators (energy, boosters, Crowns), never filler. "Free to finish, faster to fund" holds throughout.

## 1. The two empty tiers

Both are one-time entry-ramp packs (per the locked one-time 5-tier ramp). Tuning anchor: pure Crown packs pay 317 Crowns @ $4.99 and 747 @ $9.99. Bundles trade Crown density for energy + goods, matching the locked pattern ($19.99 Daily Offer = 700 Crowns + 100 energy; $49.99 Pin Money = 2000 + 150 + chest).

### $4.99 — Morning Call (name LOCKED)

The morning kickstart. One purchase, meant to convert a first-time spender.

- 250 Crowns
- 80 energy, player's choice of pool (Atelier / Park / Conservatory)
- 1 Calling-Card chest: 3 random board items, tiers 2–3, Atelier + Park pools only (no L1, no coins — same eligibility shape as the weekly Atelier chest)

Why it sells: 80 energy is a genuine top-up without replacing the Crown refill ladder (first-rung value ≈ 12 Crowns) plus a chest that lands mid-tier items immediately. Cheaper in raw Crowns than the $4.99 pure-Crown pack (250 vs 317) — the energy + chest is the reason to buy the bundle instead.

### $9.99 — recommended name: The Promenade

The step-up. Same buyer, second purchase.

- 550 Crowns
- 150 energy, player's choice of pool
- 1 Milliner's Box: 5 random board items, tiers 2–4, Atelier + Park pools (no L1, no coins)
- 2 generator cooldown-rush tokens (1 token = 1 full generator rush, grounded at the 2-Crowns/min rush sink)

Why it sells: the chest + rush tokens do the heavy lifting — 150 energy stays a top-up so the Crown refill ladder remains the main energy faucet. Crown density ($55/dollar) sits correctly between Morning Call and the Daily Offer ($35/dollar).

Both packs: ZERO coins, zero Favors, zero Estate Funds, zero event currency. Coin firewall holds.

## 2. Legal booster catalog (the only things that may go in a pack)

Money buys Crowns and energy only — never coins, never story advancement. Every booster below is grounded in an existing economy mechanic:

| Booster | Grounded in | Notes |
|---|---|---|
| `energy` | refill ladder (15→150 Crowns / 100 energy) | pool: atelier / park / conservatory / choice. Recommend choice. |
| `crowns` | Crown packs | non-round denominations only (exchange-rate opacity law) |
| `item_chest` | Pin Money Atelier chest | board, count, tierMin 2, tierMax 5. Never L1, never coins. |
| `rush_token` | generator cooldown rush (2 Crowns/min) | 1 token = 1 full rush |
| `duplicate_voucher` | duplicate offer (6 Crowns × tier) | 1 voucher, tiers 1–5 |
| `event_skip` | event task skip (10 Crowns, max 3/event) | event weeks only |
| `exclusive_outfit` | Debutante's Chest bonus | cosmetic only; $49.99+ tiers; never a time-only reward (Triumph Gown etc. stay unpurchasable) |

Never packable, ever: coins, Favors (no purchase path by law), Estate Funds (Day-Book only), event currencies, story advancement, scores, Gazette verdicts.

## 3. Themed-pack generator — proposed schema for `Content/shop-packs.json`

Content-driven rotation. The permanent 5-tier ladder never changes; themed packs occupy one weekly featured slot.

```json
{
  "schemaVersion": 1,
  "priceTiers": [4.99, 9.99, 19.99, 49.99, 99.99],
  "featuredSlotsPerWeek": 1,
  "boosterCatalog": { "energy": {}, "crowns": {}, "item_chest": {}, "rush_token": {}, "duplicate_voucher": {}, "event_skip": {}, "exclusive_outfit": {} },
  "themedPacks": [
    {
      "id": "ranelagh-breakfast",
      "name": "The Ranelagh Breakfast",
      "priceTier": 4.99,
      "tagline": "A pleasure-garden morning: energy for the day's merging.",
      "contents": {
        "crowns": 250,
        "energy": { "amount": 150, "pool": "choice" },
        "boosters": [{ "type": "rush_token", "count": 1 }]
      },
      "seasons": ["spring", "summer"],
      "eventOnly": false,
      "maxConsecutiveWeeks": 2,
      "cooldownWeeks": 8
    }
  ],
  "validation": {
    "coinFirewall": "contents must not reference coins, favors, estate_funds, or event currency — importer rejects violations",
    "priceTiers": "no tier outside the 5 locked values",
    "naming": "never bare 'SCANDAL'; never 'whale'; never 'Pin Money Daily'"
  }
}
```

## 4. Twelve starter themed packs

All names Regency-appropriate, trademark-safe. Contents follow the catalog above; Crown density degrades as goods are added (the locked bundle pattern).

| # | Name | Tier | Contents sketch |
|---|---|---|---|
| 1 | The Ranelagh Breakfast | $4.99 | 250 Crowns, 80 energy (choice), 1 rush token |
| 2 | Gunter's Ices | $4.99 | 200 Crowns, 100 energy (choice), summer seasonal |
| 3 | Vauxhall by Night | $9.99 | 550 Crowns, 150 energy (choice), 2 rush tokens |
| 4 | A Call at the Milliner's | $9.99 | 500 Crowns, 80 energy (atelier), 5-item Atelier chest (tiers 2–4) |
| 5 | The Circulating Library | $9.99 | 600 Crowns, 80 energy (choice), 1 duplicate voucher |
| 6 | The Gazette Challenge Kit | $9.99 | 400 Crowns, 100 energy (choice), 2 event skips — event weeks only |
| 7 | Rotten Row at Dawn | $19.99 | 700 Crowns, 150 energy (park), 6-item Park chest (tiers 2–4) |
| 8 | The Assembly Rooms | $19.99 | 800 Crowns, 150 energy (choice), 1 duplicate voucher, 2 rush tokens |
| 9 | Hartwell Harvest Hamper | $19.99 | 700 Crowns, 100 park + 100 conservatory energy, 6-item Park chest |
| 10 | The Promenade Chest | $49.99 | 2000 Crowns, 250 energy (choice), 8-item chest (tiers 2–5), 3 rush tokens |
| 11 | An Opera Box for the Evening | $49.99 | 2200 Crowns, 300 energy (choice), 1 exclusive fashion plate |
| 12 | The Season Ball Trunk | $99.99 | 3000 Crowns, 600 energy (choice), 1 exclusive outfit, 2 duplicate vouchers — ball events only |

## 5. Freshness rules

- One featured themed pack per week. The permanent ladder is always available underneath.
- A theme runs max 2 consecutive weeks, then cools down ≥ 8 weeks before return.
- 12 themes = a full quarter of weekly rotation with zero repeats.
- Seasonal/event tags are hard gates: Gunter's Ices never appears in winter; event-only packs never appear off-event.
- Never the same name + contents inside a rolling 12 weeks.
- Performance: track conversion per theme. Bottom quartile retires to the vault (≥ 6 months). Top quartile may return on a 4-week cooldown.
- Content calendar is planned quarterly; 2–3 new themes authored per quarter so the pool grows instead of recycling.
- Stale-avoidance is structural, not manual: the cooldown + vault + quarterly intake means no theme can overstay.

## 6. Decisions needed from Beth

1. Approve the $4.99 Morning Call contents (§1).
2. Approve the $9.99 contents and the name "The Promenade" (alternates: The Morning Room, The Park Gates).
3. Energy grant: player's choice of pool, or fixed/split? (Recommend choice — it sells better.)
4. Approve the 12 starter themes, or cut/rename any.
5. "Daily Offer" at $19.99 is named for a cadence but sits in a one-time ramp — confirm one-time, or make it genuinely daily?
6. Green-light authoring `Content/shop-packs.json` from the schema in §3.

## House-favor tuning law (Beth, Sep 29 2026)
Packs must be genuinely worth buying AND still favor the house. The bundle trade is: fewer Crowns than the pure-Crown pack at the same tier, plus soft-currency extras (energy, chests, rush tokens) that cost the house ~nothing but feel generous to the player. Check every future themed pack against this: the Crown discount must exceed the real cost of the extras. Never invert it.

## Energy-dearness tuning law (Beth, Sep 29 2026)
Energy in packs stays dear: players can buy energy with Crowns in-game via the refill ladder, so packs must never cannibalize that sink. Pack energy is a top-up, never a refill — roughly half or less of what a naive "good value" grant would be. The Crown ladder remains the main energy faucet.
