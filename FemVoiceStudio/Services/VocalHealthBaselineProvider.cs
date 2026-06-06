using System;
using System.Linq;
using FemVoiceStudio.Data;

namespace FemVoiceStudio.Services
{
    public sealed record VocalHealthBaseline
    {
        public int UserId { get; init; } = 1;
        public double BaselineResonance { get; init; } = 0.70;
        public double BaselineStability { get; init; } = 0.70;
        public string ConfidenceLevel { get; init; } = "default";
        public DateTime? CalculatedAt { get; init; }
        public string Source { get; init; } = "Default";
    }

    public sealed class VocalHealthBaselineProvider
    {
        private const double DefaultBaseline = 0.70;
        private readonly IDatabaseService _database;

        public VocalHealthBaselineProvider(IDatabaseService database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public VocalHealthBaseline GetBaseline(int userId = 1)
        {
            var smartCoachBaseline = _database.GetSmartCoachBaseline(userId);
            var recentSessions = _database.GetRecentSessions(10, userId);

            var resonance = NormalizeScore(smartCoachBaseline?.BaselineResonanceScore)
                ?? AverageNormalized(recentSessions.Select(s => s.ResonanceScore))
                ?? DefaultBaseline;

            var stability = AverageNormalized(recentSessions.Select(s =>
                    s.VoiceHealthScore > 0 ? s.VoiceHealthScore : s.OverallScore))
                ?? DefaultBaseline;

            return new VocalHealthBaseline
            {
                UserId = userId,
                BaselineResonance = ClampBaseline(resonance),
                BaselineStability = ClampBaseline(stability),
                ConfidenceLevel = smartCoachBaseline?.ConfidenceLevel ?? InferConfidence(recentSessions.Count),
                CalculatedAt = smartCoachBaseline?.CalculatedAt,
                Source = smartCoachBaseline != null ? "SmartCoachBaseline+RecentSessions" : "RecentSessions"
            };
        }

        public VocalHealthSupervisorOptions CreateVocalHealthOptions(int userId = 1)
        {
            var baseline = GetBaseline(userId);
            return new VocalHealthSupervisorOptions
            {
                BaselineResonance = baseline.BaselineResonance,
                BaselineStability = baseline.BaselineStability
            };
        }

        public HydrationAdvisorOptions CreateHydrationOptions(int userId = 1)
        {
            var baseline = GetBaseline(userId);
            return new HydrationAdvisorOptions
            {
                BaselineResonance = baseline.BaselineResonance,
                BaselineStability = baseline.BaselineStability
            };
        }

        private static double? AverageNormalized(IEnumerable<double> values)
        {
            var normalized = values
                .Select(v => NormalizeScore(v))
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();

            return normalized.Count == 0 ? null : normalized.Average();
        }

        private static double? NormalizeScore(double? value)
        {
            if (!value.HasValue || value.Value <= 0 || double.IsNaN(value.Value))
                return null;

            return value.Value > 1
                ? value.Value / 100.0
                : value.Value;
        }

        private static double ClampBaseline(double value)
            => Math.Clamp(value, 0.35, 0.95);

        private static string InferConfidence(int sessionCount)
            => sessionCount >= 10 ? "medium" : sessionCount >= 3 ? "low" : "default";
    }
}
