// validate-purchase: verify a StoreKit 2 signedTransaction with Apple's
// App Store Server API and credit Crowns. Idempotent: the same Apple
// transaction can never credit twice.
//
// POST /functions/v1/validate-purchase
// Headers: Authorization: Bearer <supabase user JWT>, apikey: <anon key>
// Body: { "signedTransaction": "<JWS from StoreKit 2>" }
//
// Secrets required:
//   APP_BUNDLE_ID, APPLE_ISSUER_ID, APPLE_KEY_ID, APPLE_PRIVATE_KEY (p8),
//   APPLE_ROOT_CA_PEM (Apple Root CA - G3, pinned)

import { handleOptions, json } from "../_shared/cors.ts";
import { getCallerId, serviceClient } from "../_shared/supabase.ts";
import {
  getTransactionInfo,
  verifyTransactionJws,
  type ServerApiConfig,
} from "../_shared/apple.ts";

Deno.serve(async (req) => {
  const opts = handleOptions(req);
  if (opts) return opts;
  if (req.method !== "POST") return json({ error: "POST only" }, 405);

  const profileId = await getCallerId(req);
  if (!profileId) return json({ error: "Unauthorized" }, 401);

  const { signedTransaction } = await req.json().catch(() => ({}));
  if (typeof signedTransaction !== "string" || !signedTransaction) {
    return json({ error: "signedTransaction is required" }, 400);
  }

  const bundleId = Deno.env.get("APP_BUNDLE_ID");
  const rootCaPem = Deno.env.get("APPLE_ROOT_CA_PEM");
  const serverCfg: ServerApiConfig = {
    issuerId: Deno.env.get("APPLE_ISSUER_ID") ?? "",
    keyId: Deno.env.get("APPLE_KEY_ID") ?? "",
    privateKeyPem: Deno.env.get("APPLE_PRIVATE_KEY") ?? "",
    bundleId: bundleId ?? "",
  };
  if (!bundleId || !rootCaPem || !serverCfg.issuerId || !serverCfg.keyId || !serverCfg.privateKeyPem) {
    console.error("validate-purchase: missing Apple secrets");
    return json({ error: "Server misconfigured" }, 500);
  }

  try {
    // 1. Verify the JWS signature + cert chain locally.
    const txn = await verifyTransactionJws(signedTransaction, bundleId, rootCaPem);

    // 2. Authoritative confirmation with Apple's server API.
    //    Use the ORIGINAL transaction id so renewals/subscription events map
    //    to the first purchase; for consumables transactionId == originalTransactionId.
    const confirmed = await getTransactionInfo(
      serverCfg,
      txn.originalTransactionId,
      txn.environment,
    );

    // 3. Crowns come from OUR products table, never from the client or the token.
    const db = serviceClient();
    const { data: product, error: productErr } = await db
      .from("products")
      .select("product_id, crowns")
      .eq("product_id", confirmed.productId)
      .single();
    if (productErr || !product) {
      console.error("Unknown product_id from Apple:", confirmed.productId);
      return json({ error: "Unknown product" }, 400);
    }

    // 4. Atomic, idempotent credit. applied=false means this exact Apple
    //    transaction was already processed (safe to treat as success on retry).
    const { data, error } = await db.rpc("apply_purchase", {
      p_profile_id: profileId,
      p_apple_transaction_id: confirmed.originalTransactionId,
      p_product_id: product.product_id,
      p_crowns: product.crowns,
      p_environment: confirmed.environment,
      p_payload: confirmed as unknown as Record<string, unknown>,
    });
    if (error) {
      console.error("apply_purchase failed:", error);
      return json({ error: "Failed to apply purchase" }, 500);
    }
    const row = (data as Array<{ applied: boolean; crowns: number; coins: number }>)[0];
    return json({
      applied: row.applied,
      alreadyProcessed: !row.applied,
      crowns: row.crowns,
      coins: row.coins,
      productId: product.product_id,
    });
  } catch (e) {
    // Signature/chain failures and Apple API errors land here. Never credit.
    console.error("validate-purchase rejected:", e);
    return json({ error: "Transaction could not be verified" }, 402);
  }
});
