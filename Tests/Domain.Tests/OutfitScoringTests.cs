using System.Linq;
using ScandalSeason.Domain.Scoring;
using Xunit;

namespace ScandalSeason.Domain.Tests.Scoring;

public sealed class OutfitScoringTests
{
    private static ScoringConfig TwoCategoryConfig(double weightA, double weightB)
    {
        var config = new ScoringConfig();
        config.Categories.Add(new ScoringCategory { Id = "elegance", DisplayName = "Elegance" });
        config.Categories.Add(new ScoringCategory { Id = "authenticity", DisplayName = "Regency Authenticity" });
        config.Weights.SetWeight("elegance", weightA);
        config.Weights.SetWeight("authenticity", weightB);
        return config;
    }

    [Fact]
    public void Score_ComputesWeightedTotal()
    {
        var config = TwoCategoryConfig(0.5, 0.5);
        var submission = new OutfitSubmission { OutfitId = "gown-1" };
        submission.CategoryScores["elegance"] = 80;
        submission.CategoryScores["authenticity"] = 60;

        var result = new OutfitScorer().Score(submission, config);

        Assert.Equal(70.0, result.TotalScore, precision: 4);
        Assert.Equal(2, result.Categories.Count);
        Assert.Equal("gown-1", result.OutfitId);
    }

    [Fact]
    public void Score_ShowsCategoriesButHidesWeights()
    {
        var config = TwoCategoryConfig(0.7, 0.3);
        var submission = new OutfitSubmission { OutfitId = "gown-1" };
        submission.CategoryScores["elegance"] = 90;
        submission.CategoryScores["authenticity"] = 50;

        var result = new OutfitScorer().Score(submission, config);

        // Category names and scores are visible...
        Assert.Contains(result.Categories, c => c.DisplayName == "Elegance" && c.Score == 90);
        // ...but the result object must not expose weights anywhere.
        var weighty = result.GetType().GetProperties()
            .Where(p => p.Name.Contains("Weight"))
            .ToList();
        Assert.Empty(weighty);
    }

    [Fact]
    public void Score_ThrowsOnMissingCategoryScore()
    {
        var config = TwoCategoryConfig(0.5, 0.5);
        var submission = new OutfitSubmission { OutfitId = "gown-1" };
        submission.CategoryScores["elegance"] = 80; // authenticity missing

        Assert.Throws<System.InvalidOperationException>(() =>
            new OutfitScorer().Score(submission, config));
    }

    [Fact]
    public void Config_ThrowsWhenWeightsDoNotSumToOne()
    {
        var config = TwoCategoryConfig(0.5, 0.6); // sums to 1.1
        var submission = new OutfitSubmission { OutfitId = "gown-1" };
        submission.CategoryScores["elegance"] = 80;
        submission.CategoryScores["authenticity"] = 60;

        Assert.Throws<System.InvalidOperationException>(() =>
            new OutfitScorer().Score(submission, config));
    }

    [Fact]
    public void Config_ThrowsWhenWeightMissingForCategory()
    {
        var config = new ScoringConfig();
        config.Categories.Add(new ScoringCategory { Id = "elegance", DisplayName = "Elegance" });
        // no weight set
        var submission = new OutfitSubmission { OutfitId = "gown-1" };
        submission.CategoryScores["elegance"] = 80;

        Assert.Throws<System.InvalidOperationException>(() =>
            new OutfitScorer().Score(submission, config));
    }
}
