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
