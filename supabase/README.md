# Scandal Season — Supabase Backend

Supabase is the **authoritative backend** for Scandal Season (source of truth for
economy, inventory, and progression). Firebase will later cover analytics, crash
reporting, push, and remote config — it never owns game state.

Locked design rules baked into this schema:
- **No ads, IAP only.** Currencies: **Crowns** (premium) and **coins** (soft).
- **Iron rule:** time always earns the same rewards as money; money expedites.
- **Energy is the only throttle; plot is never time-gated.**
- **No VS mode.** Daily Vote is side-by-side comparison; votes are totaled.
- **Time-only rewards are never purchasable** — enforced in the catalog and in
  the `grant-time-reward` function, not just by convention.
- **Purchases are validated server-side** via Apple's App Store Server API.
- The **Gazette** is judge/narrator brand, never a paywall (no schema needed).

> All game content here is **PLACEHOLDER**. Beth is still finishing design
> briefs — replace every `PLACEHOLDER` value with real content before launch.

## Layout

```
supabase/
  schema.sql            # Generated: concatenation of migrations/ (do not edit directly)
  seed.sql              # PLACEHOLDER dev data
  migrations/
    000_helpers.sql             # handle_updated_at() trigger function
    001_extensions_and_profiles.sql
    002_wallets_and_energy.sql  # crowns/coins wallets, energy state
    003_inventory.sql           # outfits / hair / accessories
    004_progression.sql         # merge-board snapshots
    005_voting.sql              # vote events, ballots, get_vote_totals()
    006_passes_and_events.sql   # season passes, event state
    007_rewards_and_purchases.sql  # reward catalog, time rewards, products, purchase ledger
    008_apply_purchase.sql      # atomic idempotent purchase-credit function
  functions/
    _shared/cors.ts | supabase.ts | apple.ts
    validate-purchase/  # Apple App Store Server API v2 verification -> credits Crowns
    cast-vote/          # one ballot per user per vote event
    grant-time-reward/  # time-only rewards (refuses purchasable rewards)
    reset-energy/       # server-clock energy regen
```

## Setup

### 1. Create the Supabase project
1. New project at https://supabase.com/dashboard (pick a region near your players).
2. Save the **project URL**, **anon key**, and **service_role key**.

### 2. Run migrations
Via the Supabase CLI (recommended):
```bash
supabase link --project-ref <your-project-ref>
supabase db push        # applies migrations/ in order
```
Or paste `schema.sql` into the Dashboard SQL editor. `seed.sql` is optional dev data.

### 3. Configure secrets
Dashboard → Project Settings → Edge Functions → Secrets (or `supabase secrets set`):

| Secret | Purpose |
|---|---|
| `SUPABASE_URL` | Project URL (auto-set on hosted functions; set for local dev) |
| `SUPABASE_SERVICE_ROLE_KEY` | Server-side writes (auto-set on hosted; set for local dev) |
| `SUPABASE_ANON_KEY` | Verifying the caller's JWT |
| `APP_BUNDLE_ID` | Your iOS bundle id, e.g. `com.yourstudio.scandalseason` |
| `APPLE_ISSUER_ID` | App Store Connect API issuer ID |
| `APPLE_KEY_ID` | App Store Connect API key ID |
| `APPLE_PRIVATE_KEY` | Contents of the `.p8` private key (ES256) |
| `APPLE_ROOT_CA_PEM` | Apple Root CA - G3 PEM (download from apple.com/certificateauthority, pin it) |

### 4. Deploy functions
```bash
supabase functions deploy validate-purchase
supabase functions deploy cast-vote
supabase functions deploy grant-time-reward
supabase functions deploy reset-energy
```

### 5. Local dev (optional)
```bash
supabase start          # local Postgres + functions runtime
supabase functions serve validate-purchase  # etc.
```

## How the Unity client calls this

- **Auth:** Supabase Auth (Apple sign-in). Every function call sends
  `Authorization: Bearer <user JWT>` + `apikey: <anon key>`.
- **Reads (RLS-protected, direct REST):** wallets, energy, inventory, snapshots,
  vote events, passes, purchases — the client reads its own rows directly;
  RLS guarantees it can never see another player's.
- **Writes that move money or enforce rules (Edge Functions, never direct writes):**
  - `POST /functions/v1/validate-purchase` `{ signedTransaction }` → `{ applied, crowns, coins }`
  - `POST /functions/v1/cast-vote` `{ vote_event_id, choice: "a"|"b" }` → `{ voted, totals }`
  - `POST /functions/v1/grant-time-reward` `{ reward_key, reason? }` → `{ granted }`
  - `POST /functions/v1/reset-energy` `{}` → `{ energy, max_energy, seconds_until_next }`
- **Vote totals:** `rpc("get_vote_totals", { p_vote_event_id })` — public counts
  without exposing individual ballots.
- **Board snapshots:** client appends snapshots after meaningful progress; latest
  wins. Wallets/energy/inventory/purchases have **no client write policies** —
  only the service role (Edge Functions) mutates them.

## Pre-launch checklist
- [ ] Replace all PLACEHOLDER content (products, reward catalog, vote events).
- [ ] Test `validate-purchase` end-to-end against the **StoreKit sandbox** (TestFlight).
- [ ] Have the JWS chain-verification code in `_shared/apple.ts` reviewed; it is
      marked REVIEW-critical.
- [ ] Decide energy tuning (`REGEN_MINUTES_PER_POINT`, max energy) in `reset-energy`.
- [ ] Add snapshot pruning (scheduled function) once board history grows.
