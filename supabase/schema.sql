-- schema.sql
-- Generated from migrations/ (concatenated in order). Do not edit directly; edit the migration files.

-- ============================================================
-- migrations/000_helpers.sql
-- ============================================================
-- Shared updated_at trigger function. Created here so later migrations can use it.

create or replace function public.handle_updated_at()
returns trigger
language plpgsql
as $$
begin
  new.updated_at = now();
  return new;
end;
$$;

-- ============================================================
-- migrations/001_extensions_and_profiles.sql
-- ============================================================
-- 001_extensions_and_profiles.sql
-- Extensions + profiles table (one row per player).
-- All game content in later migrations is placeholder-ready; no real game data here.

create extension if not exists "pgcrypto";

-- Profiles: one per Supabase auth user. Avatar is player-customized (no avatar unlocks sold).
create table public.profiles (
  id uuid primary key references auth.users(id) on delete cascade,
  display_name text not null default 'PLACEHOLDER',
  avatar_config jsonb not null default '{}'::jsonb,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create trigger profiles_updated_at
  before update on public.profiles
  for each row execute function public.handle_updated_at();

alter table public.profiles enable row level security;

create policy "Users can read their own profile"
  on public.profiles for select
  using (auth.uid() = id);

create policy "Users can insert their own profile"
  on public.profiles for insert
  with check (auth.uid() = id);

create policy "Users can update their own profile"
  on public.profiles for update
  using (auth.uid() = id)
  with check (auth.uid() = id);

-- ============================================================
-- migrations/002_wallets_and_energy.sql
-- ============================================================
-- 002_wallets_and_energy.sql
-- Currencies: Crowns (premium, IAP) and coins (soft). Iron rule is enforced in
-- application logic: time always earns the same rewards as money; money expedites.
-- Energy is the only throttle on play; plot is never time-gated.

create table public.wallets (
  profile_id uuid primary key references public.profiles(id) on delete cascade,
  crowns integer not null default 0 check (crowns >= 0),
  coins integer not null default 0 check (coins >= 0),
  updated_at timestamptz not null default now(),
  -- Balances only ever change via Edge Functions (service role) or controlled RPCs,
  -- never by direct client writes.
  constraint wallets_no_negative check (crowns >= 0 and coins >= 0)
);

create trigger wallets_updated_at
  before update on public.wallets
  for each row execute function public.handle_updated_at();

alter table public.wallets enable row level security;

create policy "Users can read their own wallet"
  on public.wallets for select
  using (auth.uid() = profile_id);

-- No insert/update/delete policies for authenticated users: only the service role
-- (Edge Functions) may mutate wallets.

create table public.energy (
  profile_id uuid primary key references public.profiles(id) on delete cascade,
  energy integer not null default 100 check (energy >= 0),
  max_energy integer not null default 100 check (max_energy > 0),
  -- PLACEHOLDER tuning: regen rate lives in the reset-energy Edge Function.
  last_regen_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create trigger energy_updated_at
  before update on public.energy
  for each row execute function public.handle_updated_at();

alter table public.energy enable row level security;

create policy "Users can read their own energy"
  on public.energy for select
  using (auth.uid() = profile_id);

-- No client write policies: energy is mutated server-side only (reset-energy function).

-- ============================================================
-- migrations/003_inventory.sql
-- ============================================================
-- 003_inventory.sql
-- Player inventory: outfits, hair, accessories. Rewards emphasize cosmetics;
-- there are no avatar unlocks (players customize their own avatar).

create table public.inventory_items (
  id uuid primary key default gen_random_uuid(),
  profile_id uuid not null references public.profiles(id) on delete cascade,
  item_type text not null check (item_type in ('outfit', 'hair', 'accessory', 'PLACEHOLDER')),
  item_key text not null,
  acquired_via text not null check (acquired_via in (
    'crown_purchase', 'coin_purchase', 'time_reward', 'event', 'pass', 'PLACEHOLDER'
  )),
  acquired_at timestamptz not null default now(),
  -- One copy of each cosmetic per player.
  constraint inventory_unique_item unique (profile_id, item_key)
);

create index inventory_items_profile_idx on public.inventory_items (profile_id);
create index inventory_items_type_idx on public.inventory_items (profile_id, item_type);

alter table public.inventory_items enable row level security;

create policy "Users can read their own inventory"
  on public.inventory_items for select
  using (auth.uid() = profile_id);

-- Inserts happen server-side (purchase/reward functions). Clients never write directly.

-- ============================================================
-- migrations/004_progression.sql
-- ============================================================
-- 004_progression.sql
-- Merge-board progression snapshots. The Unity client owns the live board state;
-- Supabase stores authoritative snapshots so progress survives reinstalls and
-- so server-side validation has a source of truth.

create table public.board_snapshots (
  id uuid primary key default gen_random_uuid(),
  profile_id uuid not null references public.profiles(id) on delete cascade,
  season_number integer not null default 1,
  chapter_key text not null default 'PLACEHOLDER',
  scene_key text not null default 'PLACEHOLDER',
  board jsonb not null default '{}'::jsonb,
  created_at timestamptz not null default now()
);

create index board_snapshots_profile_idx on public.board_snapshots (profile_id, created_at desc);

alter table public.board_snapshots enable row level security;

create policy "Users can read their own snapshots"
  on public.board_snapshots for select
  using (auth.uid() = profile_id);

create policy "Users can insert their own snapshots"
  on public.board_snapshots for insert
  with check (auth.uid() = profile_id);

-- Clients append snapshots; history is kept (latest = max created_at).
-- Pruning old snapshots can be added later via a scheduled function.

-- ============================================================
-- migrations/005_voting.sql
-- ============================================================
-- 005_voting.sql
-- Daily Vote: side-by-side comparison only (no VS mode, no brackets).
-- Votes are totaled; scoring shows categories, weights stay hidden.

create table public.vote_events (
  id uuid primary key default gen_random_uuid(),
  title text not null default 'PLACEHOLDER',
  status text not null default 'open' check (status in ('open', 'closed')),
  -- The two looks shown side by side. Shape is a placeholder contract for the client.
  pair_a jsonb not null default '{}'::jsonb,
  pair_b jsonb not null default '{}'::jsonb,
  starts_at timestamptz not null default now(),
  ends_at timestamptz not null default now() + interval '1 day',
  created_at timestamptz not null default now()
);

create index vote_events_status_idx on public.vote_events (status, ends_at);

alter table public.vote_events enable row level security;

create policy "Authenticated users can read vote events"
  on public.vote_events for select
  to authenticated
  using (true);

-- Ballots: exactly one per user per vote event, enforced by unique constraint.
create table public.ballots (
  id uuid primary key default gen_random_uuid(),
  vote_event_id uuid not null references public.vote_events(id) on delete cascade,
  profile_id uuid not null references public.profiles(id) on delete cascade,
  choice text not null check (choice in ('a', 'b')),
  created_at timestamptz not null default now(),
  constraint ballots_one_per_user_per_event unique (vote_event_id, profile_id)
);

create index ballots_event_idx on public.ballots (vote_event_id);

alter table public.ballots enable row level security;

create policy "Users can read their own ballots"
  on public.ballots for select
  using (auth.uid() = profile_id);

-- Ballot inserts go through the cast-vote Edge Function (validates event is open).
-- No direct client insert policy.

-- Public totals via a SECURITY DEFINER function so clients can read counts without
-- reading other players' individual ballots. (A plain view would inherit RLS.)
create or replace function public.get_vote_totals(p_vote_event_id uuid)
returns table (votes_a bigint, votes_b bigint, votes_total bigint)
language sql
security definer
set search_path = public
as $$
  select
    count(*) filter (where choice = 'a')::bigint,
    count(*) filter (where choice = 'b')::bigint,
    count(*)::bigint
  from public.ballots
  where vote_event_id = p_vote_event_id;
$$;

grant execute on function public.get_vote_totals(uuid) to authenticated;

-- ============================================================
-- migrations/006_passes_and_events.sql
-- ============================================================
-- 006_passes_and_events.sql
-- Season passes (season-long pass stays premium-priced) and event state
-- (events run ~3-7 days up to 2 weeks).

create table public.season_passes (
  id uuid primary key default gen_random_uuid(),
  profile_id uuid not null references public.profiles(id) on delete cascade,
  season_number integer not null,
  tier text not null check (tier in ('free', 'premium')),
  purchased_at timestamptz,
  expires_at timestamptz not null,
  constraint season_pass_one_per_season unique (profile_id, season_number)
);

create index season_passes_profile_idx on public.season_passes (profile_id);

alter table public.season_passes enable row level security;

create policy "Users can read their own passes"
  on public.season_passes for select
  using (auth.uid() = profile_id);

-- Pass grants happen server-side after validated purchase. No client writes.

create table public.event_state (
  id uuid primary key default gen_random_uuid(),
  profile_id uuid not null references public.profiles(id) on delete cascade,
  event_key text not null,
  progress jsonb not null default '{}'::jsonb,
  updated_at timestamptz not null default now(),
  constraint event_state_unique unique (profile_id, event_key)
);

create index event_state_profile_idx on public.event_state (profile_id);

create trigger event_state_updated_at
  before update on public.event_state
  for each row execute function public.handle_updated_at();

alter table public.event_state enable row level security;

create policy "Users can read their own event state"
  on public.event_state for select
  using (auth.uid() = profile_id);

create policy "Users can upsert their own event state"
  on public.event_state for insert
  with check (auth.uid() = profile_id);

create policy "Users can update their own event state"
  on public.event_state for update
  using (auth.uid() = profile_id)
  with check (auth.uid() = profile_id);

-- ============================================================
-- migrations/007_rewards_and_purchases.sql
-- ============================================================
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

-- ============================================================
-- migrations/008_apply_purchase.sql
-- ============================================================
-- 008_apply_purchase.sql
-- Atomic, idempotent purchase application. Called only by the validate-purchase
-- Edge Function via the service role. Inserts the purchase ledger row and credits
-- the wallet in one transaction; a retried transaction can never double-credit.

create or replace function public.apply_purchase(
  p_profile_id uuid,
  p_apple_transaction_id text,
  p_product_id text,
  p_crowns integer,
  p_environment text,
  p_payload jsonb
)
returns table (applied boolean, crowns integer, coins integer)
language plpgsql
security definer
set search_path = public
as $$
declare
  v_purchase_id uuid;
  v_applied boolean := false;
begin
  -- Ledger first. ON CONFLICT DO NOTHING + RETURNING tells THIS call whether it
  -- won the insert; a retry (or a concurrent duplicate) gets no row back and
  -- therefore never credits the wallet.
  insert into public.purchases (
    profile_id, apple_transaction_id, product_id, crowns_credited, environment, raw_payload
  )
  values (p_profile_id, p_apple_transaction_id, p_product_id, p_crowns, p_environment, p_payload)
  on conflict (apple_transaction_id) do nothing
  returning id into v_purchase_id;

  v_applied := v_purchase_id is not null;

  if v_applied then
    -- NOTE: output parameters are named crowns/coins, so the column refs here
    -- must be table-qualified or PL/pgSQL raises "column reference is ambiguous".
    update public.wallets
      set crowns = public.wallets.crowns + p_crowns
      where profile_id = p_profile_id;
    if not found then
      insert into public.wallets (profile_id, crowns, coins)
      values (p_profile_id, p_crowns, 0);
    end if;
  end if;

  return query
    select v_applied, w.crowns, w.coins
    from public.wallets w
    where w.profile_id = p_profile_id;
end;
$$;

-- Only the service role (Edge Functions) may call this. Authenticated clients cannot.
revoke all on function public.apply_purchase(uuid, text, text, integer, text, jsonb) from public;
grant execute on function public.apply_purchase(uuid, text, text, integer, text, jsonb) to service_role;

