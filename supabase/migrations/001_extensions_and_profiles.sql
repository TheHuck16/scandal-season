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
