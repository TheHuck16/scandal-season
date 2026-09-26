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
