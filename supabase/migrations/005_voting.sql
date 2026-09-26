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
