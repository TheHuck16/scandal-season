// grant-time-reward: grant time-only rewards. These are NEVER purchasable at any
// price -- the catalog's `acquisition` field is the law, and this function
// refuses anything not marked 'time_only'.
//
// POST /functions/v1/grant-time-reward
// Headers: Authorization: Bearer <supabase user JWT>, apikey: <anon key>
// Body: { "reward_key": "<catalog key>", "reason": "optional PLACEHOLDER text" }

import { handleOptions, json } from "../_shared/cors.ts";
import { getCallerId, serviceClient } from "../_shared/supabase.ts";

Deno.serve(async (req) => {
  const opts = handleOptions(req);
  if (opts) return opts;
  if (req.method !== "POST") return json({ error: "POST only" }, 405);

  const profileId = await getCallerId(req);
  if (!profileId) return json({ error: "Unauthorized" }, 401);

  const { reward_key, reason } = await req.json().catch(() => ({}));
  if (typeof reward_key !== "string" || !reward_key) {
    return json({ error: "reward_key is required" }, 400);
  }

  const db = serviceClient();

  const { data: catalog, error: catalogErr } = await db
    .from("reward_catalog")
    .select("reward_key, acquisition, grants_item_type, grants_item_key")
    .eq("reward_key", reward_key)
    .single();
  if (catalogErr || !catalog) return json({ error: "Unknown reward" }, 404);

  // THE rule: only time-only rewards may be granted here. Anything purchasable
  // (or either-path) is refused -- it must flow through the store instead.
  if (catalog.acquisition !== "time_only") {
    return json(
      { error: "This reward is not a time-only reward and cannot be granted here" },
      403,
    );
  }

  const { error: grantErr } = await db.from("time_rewards").insert({
    profile_id: profileId,
    reward_key,
    reason: typeof reason === "string" ? reason : "PLACEHOLDER",
  });
  if (grantErr && grantErr.code !== "23505") {
    console.error("grant-time-reward failed:", grantErr);
    return json({ error: "Could not grant reward" }, 500);
  }
  const alreadyGranted = grantErr?.code === "23505";

  // Attach the cosmetic, if the catalog maps one.
  let itemGranted = false;
  if (!alreadyGranted && catalog.grants_item_type && catalog.grants_item_key) {
    const { error: itemErr } = await db.from("inventory_items").insert({
      profile_id: profileId,
      item_type: catalog.grants_item_type,
      item_key: catalog.grants_item_key,
      acquired_via: "time_reward",
    });
    if (itemErr && itemErr.code !== "23505") {
      console.error("inventory grant failed:", itemErr);
    } else {
      itemGranted = !itemErr;
    }
  }

  return json({ granted: !alreadyGranted, alreadyGranted, itemGranted, reward_key });
});
