// Scandal Season — Domain core. PLAIN C# ONLY: no UnityEngine references.
// Outfit scoring: categories are VISIBLE to players, weights are HIDDEN and
// must never be serialized into client payloads, assets, or logs.

using System;
using System.Collections.Generic;

namespace ScandalSeason.Domain.Scoring
{
    /// <summary>A scoring category shown to the player (e.g. in the Daily Vote verdict).</summary>
    public sealed class ScoringCategory
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
    }

    /// <summary>
    /// Hidden per-category weights. Server/content-side only.
    /// SECURITY: never include this in anything shipped to the client.
    /// </summary>
    public sealed class ScoringWeights
    {
        private readonly Dictionary<string, double> _weights = new Dictionary<string, double>();

        public void SetWeight(string categoryId, double weight)
        {
            if (string.IsNullOrWhiteSpace(categoryId))
                throw new ArgumentException("Category id is required.", nameof(categoryId));
            if (weight < 0) throw new ArgumentOutOfRangeException(nameof(weight), "Weights cannot be negative.");
            _weights[categoryId] = weight;
        }

        public double GetWeight(string categoryId) =>
            _weights.TryGetValue(categoryId, out double w)
                ? w
                : throw new InvalidOperationException($"No weight configured for category '{categoryId}'.");

        public void Validate(IEnumerable<ScoringCategory> categories)
        {
            double total = 0;
            foreach (var category in categories)
            {
                if (!_weights.ContainsKey(category.Id))
                    throw new InvalidOperationException($"Missing hidden weight for category '{category.Id}'.");
                total += _weights[category.Id];
            }
            if (Math.Abs(total - 1.0) > 0.0001)
                throw new InvalidOperationException($"Category weights must sum to 1.0, got {total}.");
        }
    }

    public sealed class ScoringConfig
    {
        public List<ScoringCategory> Categories { get; } = new List<ScoringCategory>();
        public ScoringWeights Weights { get; } = new ScoringWeights();

        public void Validate()
        {
            if (Categories.Count == 0)
                throw new InvalidOperationException("At least one scoring category is required.");
            Weights.Validate(Categories);
        }
    }

    /// <summary>One judged outfit: raw 0–100 scores per category (from judges / content data).</summary>
    public sealed class OutfitSubmission
    {
        public string OutfitId { get; set; } = "";
        public Dictionary<string, int> CategoryScores { get; } = new Dictionary<string, int>();
    }

    public sealed class CategoryScoreResult
    {
        public string CategoryId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public int Score { get; set; }
    }

    /// <summary>
    /// Scoring verdict. Exposes per-category scores and the weighted total —
    /// but NEVER the weights themselves.
    /// </summary>
    public sealed class OutfitScoreResult
    {
        public string OutfitId { get; set; } = "";
        public List<CategoryScoreResult> Categories { get; } = new List<CategoryScoreResult>();
        public double TotalScore { get; set; }
    }

    public sealed class OutfitScorer
    {
        public OutfitScoreResult Score(OutfitSubmission submission, ScoringConfig config)
        {
            if (submission == null) throw new ArgumentNullException(nameof(submission));
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.Validate();

            var result = new OutfitScoreResult { OutfitId = submission.OutfitId };
            double total = 0;
            foreach (var category in config.Categories)
            {
                if (!submission.CategoryScores.TryGetValue(category.Id, out int raw))
                    throw new InvalidOperationException($"Submission '{submission.OutfitId}' is missing a score for category '{category.Id}'.");
                if (raw < 0 || raw > 100)
                    throw new InvalidOperationException($"Score for category '{category.Id}' must be 0–100, got {raw}.");
                total += raw * config.Weights.GetWeight(category.Id);
                result.Categories.Add(new CategoryScoreResult
                {
                    CategoryId = category.Id,
                    DisplayName = category.DisplayName,
                    Score = raw
                });
            }
            result.TotalScore = total;
            return result;
        }
    }
}
