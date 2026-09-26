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
