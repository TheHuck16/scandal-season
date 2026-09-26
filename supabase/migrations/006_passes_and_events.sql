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
