// Scandal Season — Domain core. PLAIN C# ONLY: no UnityEngine references.
// Season progression (10-season plan), event passes (3–14 days), Daily Vote tallying.

using System;

namespace ScandalSeason.Domain.Progression
{
    /// <summary>
    /// Season structure. Book One is the 10-season arc (ruin → restoration → legacy),
    /// but seasons are effectively endless — this is a long, long game. The slow
    /// story clock, recurring rival roster, and the Gazette as perpetual
    /// judge/narrator all support indefinite seasons, so advancement never stops.
    /// </summary>
    public sealed class SeasonProgression
    {
        /// <summary>0-based index of the current season.</summary>
        public int CurrentSeasonIndex { get; private set; }

        /// <summary>True once the 10-season Book One arc is complete. Seasons continue after it.</summary>
        public bool IsBookOneComplete => CurrentSeasonIndex >= Economy.GameRules.BookOneSeasons;

        public int CurrentSeasonNumber => CurrentSeasonIndex + 1; // 1-based for display

        public void AdvanceSeason()
        {
            // Endless by design: there is always another season.
            CurrentSeasonIndex++;
        }
    }

    /// <summary>
    /// A limited-time event pass. Runs 3–14 days (see GameRules); duration is
    /// validated at construction so an out-of-range pass cannot exist.
    /// </summary>
    public sealed class EventPass
    {
        public string Id { get; }
        public int DurationDays { get; }
        public DateTimeOffset StartUtc { get; }
        public DateTimeOffset EndUtc => StartUtc.AddDays(DurationDays);

        public EventPass(string id, int durationDays, DateTimeOffset startUtc)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Pass id is required.", nameof(id));
            if (durationDays < Economy.GameRules.MinEventPassDays ||
                durationDays > Economy.GameRules.MaxEventPassDays)
                throw new ArgumentOutOfRangeException(nameof(durationDays),
                    $"Event passes run {Economy.GameRules.MinEventPassDays}–{Economy.GameRules.MaxEventPassDays} days; got {durationDays}.");
            Id = id;
            DurationDays = durationDays;
            StartUtc = startUtc;
        }

        public bool IsActive(DateTimeOffset nowUtc) => nowUtc >= StartUtc && nowUtc < EndUtc;
        public bool HasEnded(DateTimeOffset nowUtc) => nowUtc >= EndUtc;
    }

    public enum VoteSide
    {
        LookA,
        LookB
    }

    /// <summary>
    /// Daily Vote: two looks shown side-by-side, votes TOTALED. No brackets, no VS mode —
    /// this is comparison voting only, per the locked design.
    /// </summary>
    public sealed class DailyVote
    {
        public string Id { get; }

        private int _votesLookA;
        private int _votesLookB;

        public DailyVote(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Vote id is required.", nameof(id));
            Id = id;
        }

        public void RecordVote(VoteSide side)
        {
            if (side == VoteSide.LookA) _votesLookA++;
            else _votesLookB++;
        }

        public int VotesLookA => _votesLookA;
        public int VotesLookB => _votesLookB;
        public int TotalVotes => _votesLookA + _votesLookB;

        /// <summary>Current leader, or null while tied (or with no votes).</summary>
        public VoteSide? Leader
        {
            get
            {
                if (_votesLookA == _votesLookB) return null;
                return _votesLookA > _votesLookB ? VoteSide.LookA : VoteSide.LookB;
            }
        }
    }
}
