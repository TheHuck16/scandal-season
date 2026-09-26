// cast-vote: record one ballot per user per Daily Vote event.
// The event shows two looks side by side; votes are totaled (no VS mode, no brackets).
//
// POST /functions/v1/cast-vote
// Headers: Authorization: Bearer <supabase user JWT>, apikey: <anon key>
// Body: { "vote_event_id": "<uuid>", "choice": "a" | "b" }

import { handleOptions, json } from "../_shared/cors.ts";
import { getCallerId, serviceClient } from "../_shared/supabase.ts";

Deno.serve(async (req) => {
  const opts = handleOptions(req);
  if (opts) return opts;
  if (req.method !== "POST") return json({ error: "POST only" }, 405);

  const profileId = await getCallerId(req);
  if (!profileId) return json({ error: "Unauthorized" }, 401);

  const { vote_event_id, choice } = await req.json().catch(() => ({}));
  if (typeof vote_event_id !== "string" || (choice !== "a" && choice !== "b")) {
    return json({ error: "vote_event_id and choice ('a'|'b') are required" }, 400);
  }

  const db = serviceClient();

  const { data: event, error: eventErr } = await db
    .from("vote_events")
    .select("id, status, starts_at, ends_at")
    .eq("id", vote_event_id)
    .single();
  if (eventErr || !event) return json({ error: "Vote event not found" }, 404);

  const now = new Date();
  if (
    event.status !== "open" ||
    new Date(event.starts_at) > now ||
    new Date(event.ends_at) < now
  ) {
    return json({ error: "Voting is closed for this event" }, 409);
  }

  const { error: ballotErr } = await db.from("ballots").insert({
    vote_event_id,
    profile_id: profileId,
    choice,
  });
  if (ballotErr) {
    // Unique constraint (vote_event_id, profile_id): one ballot per user per event.
    if (ballotErr.code === "23505") {
      return json({ error: "Already voted in this event" }, 409);
    }
    console.error("cast-vote insert failed:", ballotErr);
    return json({ error: "Could not record vote" }, 500);
  }

  const { data: totals, error: totalsErr } = await db.rpc("get_vote_totals", {
    p_vote_event_id: vote_event_id,
  });
  if (totalsErr) {
    console.error("get_vote_totals failed:", totalsErr);
    return json({ voted: true, choice, totals: null });
  }
  return json({ voted: true, choice, totals: totals?.[0] ?? null });
});
