using System;
using ScandalSeason.Domain.Progression;
using Xunit;

namespace ScandalSeason.Domain.Tests.Progression;

public sealed class SeasonProgressionTests
{
    [Fact]
    public void AdvanceSeason_ContinuesIndefinitely_BookOneIsTenSeasons()
    {
        var progression = new SeasonProgression();
        Assert.False(progression.IsBookOneComplete);

        for (int i = 0; i < 10; i++)
            progression.AdvanceSeason();
        Assert.True(progression.IsBookOneComplete);
        Assert.Equal(11, progression.CurrentSeasonNumber);

        // Seasons 11+ (Book Two and beyond): advancement never stops.
        for (int i = 0; i < 15; i++)
            progression.AdvanceSeason();
        Assert.Equal(26, progression.CurrentSeasonNumber);
        Assert.True(progression.IsBookOneComplete);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(15)]
    public void EventPass_RejectsDurationsOutside3To14Days(int days)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EventPass("pass-1", days, DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(14)]
    public void EventPass_AcceptsDurationsWithin3To14Days(int days)
    {
        var start = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var pass = new EventPass("pass-1", days, start);
        Assert.Equal(start.AddDays(days), pass.EndUtc);
        Assert.True(pass.IsActive(start.AddDays(1)));
        Assert.False(pass.IsActive(start.AddDays(days + 1)));
        Assert.True(pass.HasEnded(start.AddDays(days + 1)));
    }

    [Fact]
    public void DailyVote_TotalsVotesWithNoBrackets()
    {
        var vote = new DailyVote("vote-001");
        vote.RecordVote(VoteSide.LookA);
        vote.RecordVote(VoteSide.LookA);
        vote.RecordVote(VoteSide.LookB);

        Assert.Equal(2, vote.VotesLookA);
        Assert.Equal(1, vote.VotesLookB);
        Assert.Equal(3, vote.TotalVotes);
        Assert.Equal(VoteSide.LookA, vote.Leader);
    }

    [Fact]
    public void DailyVote_LeaderIsNullOnTie()
    {
        var vote = new DailyVote("vote-001");
        vote.RecordVote(VoteSide.LookA);
        vote.RecordVote(VoteSide.LookB);
        Assert.Null(vote.Leader);
        Assert.Equal(2, vote.TotalVotes);
    }
}
