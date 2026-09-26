-- seed.sql
-- PLACEHOLDER seed data for local/dev use only. Every row below is clearly
-- marked PLACEHOLDER and must be replaced with real design-brief content.
-- The placeholder profile UUID must match a real auth.users id to be useful;
-- create a dev user in Supabase Auth and substitute its id.

-- PLACEHOLDER products: Crown IAP packs. Replace product_id with the real
-- App Store Connect product IDs and crowns with the real grant amounts.
insert into public.products (product_id, crowns, price_tier, description) values
  ('PLACEHOLDER_crowns_pack_1', 100, 'PLACEHOLDER', 'PLACEHOLDER: smallest Crown pack'),
  ('PLACEHOLDER_crowns_pack_2', 550, 'PLACEHOLDER', 'PLACEHOLDER: medium Crown pack'),
  ('PLACEHOLDER_crowns_pack_3', 1200, 'PLACEHOLDER', 'PLACEHOLDER: large Crown pack')
on conflict (product_id) do nothing;

-- PLACEHOLDER reward catalog. 'time_only' rows can never be purchased at any price.
insert into public.reward_catalog (reward_key, acquisition, description, grants_item_type, grants_item_key) values
  ('PLACEHOLDER_time_reward_1', 'time_only', 'PLACEHOLDER: earned by consistent daily play', 'outfit', 'PLACEHOLDER_outfit_key_1'),
  ('PLACEHOLDER_time_reward_2', 'time_only', 'PLACEHOLDER: earned by season completion', 'hair', 'PLACEHOLDER_hair_key_1'),
  ('PLACEHOLDER_purchasable_reward_1', 'purchase', 'PLACEHOLDER: special-edition outfit, also has a free path', 'outfit', 'PLACEHOLDER_outfit_key_2')
on conflict (reward_key) do nothing;

-- PLACEHOLDER vote event. Replace pair_a/pair_b with the real look payloads.
insert into public.vote_events (title, status, pair_a, pair_b, starts_at, ends_at) values
  ('PLACEHOLDER: Daily Vote', 'open',
   '{"look": "PLACEHOLDER_A"}'::jsonb,
   '{"look": "PLACEHOLDER_B"}'::jsonb,
   now(), now() + interval '1 day')
on conflict do nothing;

-- PLACEHOLDER player rows. Replace the UUID with a real dev auth.users id.
-- Without a matching auth.users row these inserts will fail on the FK; that is expected.
-- insert into public.profiles (id, display_name) values
--   ('00000000-0000-0000-0000-000000000000', 'PLACEHOLDER Dev Player');
-- insert into public.wallets (profile_id, crowns, coins) values
--   ('00000000-0000-0000-0000-000000000000', 0, 500);
-- insert into public.energy (profile_id, energy, max_energy) values
--   ('00000000-0000-0000-0000-000000000000', 100, 100);
