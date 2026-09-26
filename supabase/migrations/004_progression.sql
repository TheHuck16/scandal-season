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
