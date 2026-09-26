// UNITY-DEPENDENT — thin Unity glue over ScandalSeason.Domain.Scoring.
// SECURITY: ScoringConfig (with hidden weights) must be supplied from the
// server at runtime. Never author weights in client assets or scenes.

using UnityEngine;
using ScandalSeason.Domain.Scoring;

public sealed class ScoringView : MonoBehaviour
{
    private readonly OutfitScorer _scorer = new OutfitScorer();

    /// <param name="config">
    /// Server-provided scoring config. Categories are shown to players;
    /// weights are hidden and must never be logged or serialized client-side.
    /// </param>
    public OutfitScoreResult Score(OutfitSubmission submission, ScoringConfig config) =>
        _scorer.Score(submission, config);
}
