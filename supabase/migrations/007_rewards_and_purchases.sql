-- 007_rewards_and_purchases.sql
-- Reward catalog (what can exist), time-only rewards (never purchasable),
-- IAP product catalog (Crowns packs), and the purchase ledger.

-- Catalog of all grantable rewards. acquisition controls the legal paths:
-- 'time_only' rewards can ONLY be granted by the grant-time-reward function.
create table public.reward_catalog (
  reward_key text primary key,
  acquisition text not null check (acquisition in ('time_only', 'purchase', 'either')),
  description text not null default 'PLACEHOLDER',
  -- Optional cosmetic granted alongside the reward record.
  grants_item_type text check (grants_item_type in ('outfit', 'hair', 'accessory')),
  grants_item_key text
);

alter table public.reward_catalog enable row level security;

create policy "Authenticated users can read the reward catalog"
  on public.reward_catalog for select
  to authenticated
  using (true);

-- Time-only reward grants. Uniqueness = a player earns each one once.
create table public.time_rewards (
  id uuid primary key default gen_random_uuid(),
  profile_id uuid not null references public.profiles(id) on delete cascade,
  reward_key text not null references public.reward_catalog(reward_key),
  reason text not null default 'PLACEHOLDER',
  granted_at timestamptz not null default now(),
  constraint time_rewards_unique unique (profile_id, reward_key)
);

create index time_rewards_profile_idx on public.time_rewards (profile_id);

alter table public.time_rewards enable row level security;

create policy "Users can read their own time rewards"
  on public.time_rewards for select
  using (auth.uid() = profile_id);

-- Grants happen only via the grant-time-reward Edge Function, which refuses
-- anything not marked 'time_only' in the catalog. No client writes.

-- IAP product catalog: App Store product IDs mapped to Crowns.
-- The client NEVER tells the server how many Crowns to credit; the server
-- looks the product up here after validating the Apple transaction.
create table public.products (
  product_id text primary key,
  crowns integer not null check (crowns > 0),
  price_tier text not null default 'PLACEHOLDER',
  description text not null default 'PLACEHOLDER'
);

alter table public.products enable row level security;

create policy "Authenticated users can read products"
  on public.products for select
  to authenticated
  using (true);

-- Purchase ledger. apple_transaction_id is unique => idempotent: retrying the
-- same validated transaction can never double-credit Crowns.
create table public.purchases (
  id uuid primary key default gen_random_uuid(),
  profile_id uuid not null references public.profiles(id) on delete cascade,
  apple_transaction_id text not null,
  product_id text not null references public.products(product_id),
  crowns_credited integer not null,
  environment text not null check (environment in ('Sandbox', 'Production')),
  raw_payload jsonb not null default '{}'::jsonb,
  created_at timestamptz not null default now(),
  constraint purchases_apple_txn_unique unique (apple_transaction_id)
);

create index purchases_profile_idx on public.purchases (profile_id);

alter table public.purchases enable row level security;

create policy "Users can read their own purchases"
  on public.purchases for select
  using (auth.uid() = profile_id);

-- Rows are written only by the validate-purchase Edge Function. No client writes.
