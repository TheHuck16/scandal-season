// reset-energy: recompute the player's energy from elapsed time.
// Energy is the only throttle on play; plot is never time-gated.
// Call on app foreground (and after any energy spend) -- never trust the client clock.
//
// POST /functions/v1/reset-energy
// Headers: Authorization: Bearer <supabase user JWT>, apikey: <anon key>
// Body: {} (no parameters; the server owns the clock)

import { handleOptions, json } from "../_shared/cors.ts";
import { getCallerId, serviceClient } from "../_shared/supabase.ts";

// PLACEHOLDER tuning: one energy point per N minutes. Finalize in design briefs.
const REGEN_MINUTES_PER_POINT = 5;

Deno.serve(async (req) => {
  const opts = handleOptions(req);
  if (opts) return opts;
  if (req.method !== "POST") return json({ error: "POST only" }, 405);

  const profileId = await getCallerId(req);
  if (!profileId) return json({ error: "Unauthorized" }, 401);

  const db = serviceClient();
  const { data: row, error } = await db
    .from("energy")
    .select("energy, max_energy, last_regen_at")
    .eq("profile_id", profileId)
    .single();

  if (error || !row) {
    // No row yet (e.g. brand-new profile): create a full bar.
    // PLACEHOLDER max: keep in sync with 002_wallets_and_energy.sql default.
    const { error: insErr } = await db
      .from("energy")
      .insert({ profile_id: profileId, energy: 100, max_energy: 100 });
    if (insErr) {
      console.error("reset-energy init failed:", insErr);
      return json({ error: "Could not initialize energy" }, 500);
    }
    return json({ energy: 100, max_energy: 100, seconds_until_next: 0 });
  }

  const now = Date.now();
  const elapsedMs = now - new Date(row.last_regen_at).getTime();
  const points = Math.floor(elapsedMs / (REGEN_MINUTES_PER_POINT * 60_000));

  if (points <= 0 || row.energy >= row.max_energy) {
    const msToNext =
      row.energy >= row.max_energy
        ? 0
        : REGEN_MINUTES_PER_POINT * 60_000 - (elapsedMs % (REGEN_MINUTES_PER_POINT * 60_000));
    return json({
      energy: row.energy,
      max_energy: row.max_energy,
      seconds_until_next: Math.ceil(msToNext / 1000),
    });
  }

  // Advance last_regen_at by exactly the consumed time so fractional progress
  // toward the next point is preserved.
  const newEnergy = Math.min(row.max_energy, row.energy + points);
  const consumedMs = Math.min(points, row.max_energy - row.energy) * REGEN_MINUTES_PER_POINT * 60_000;
  const newLastRegen = new Date(new Date(row.last_regen_at).getTime() + consumedMs).toISOString();

  const { error: updErr } = await db
    .from("energy")
    .update({ energy: newEnergy, last_regen_at: newLastRegen })
    .eq("profile_id", profileId);
  if (updErr) {
    console.error("reset-energy update failed:", updErr);
    return json({ error: "Could not update energy" }, 500);
  }

  const remainderMs = REGEN_MINUTES_PER_POINT * 60_000 - (elapsedMs - consumedMs);
  return json({
    energy: newEnergy,
    max_energy: row.max_energy,
    seconds_until_next: newEnergy >= row.max_energy ? 0 : Math.ceil(remainderMs / 1000),
  });
});
