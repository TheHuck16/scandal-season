// UNITY-DEPENDENT — thin Unity glue over ScandalSeason.Domain.Progression.
// Ten-season plan; event passes 3–14 days; Daily Vote totals side-by-side votes.

using UnityEngine;
using ScandalSeason.Domain.Progression;

public sealed class ProgressionView : MonoBehaviour
{
    private readonly SeasonProgression _progression = new SeasonProgression();

    public SeasonProgression Progression => _progression;

    public void AdvanceSeason() => _progression.AdvanceSeason();
}
