using System;
using FemVoiceStudio.Data;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    public class VocalHealthBaselineProviderTests
    {
        [Fact]
        public void GetBaseline_UsesPersistentSmartCoachResonanceBaseline()
        {
            var database = new TestDatabaseService();
            database.SetSmartCoachBaseline(new SmartCoachBaseline
            {
                UserId = 1,
                BaselineResonanceScore = 82,
                ConfidenceLevel = "high",
                CalculatedAt = new DateTime(2026, 5, 29)
            });
            database.SaveTrainingSession(Session(voiceHealth: 76, overall: 70));

            var provider = new VocalHealthBaselineProvider(database);

            var baseline = provider.GetBaseline();

            Assert.Equal(0.82, baseline.BaselineResonance, 3);
            Assert.Equal(0.76, baseline.BaselineStability, 3);
            Assert.Equal("high", baseline.ConfidenceLevel);
            Assert.Equal("SmartCoachBaseline+RecentSessions", baseline.Source);
        }

        [Fact]
        public void GetBaseline_FallsBackToRecentSessionsWhenSmartCoachBaselineMissing()
        {
            var database = new TestDatabaseService();
            database.SaveTrainingSession(Session(resonance: 64, voiceHealth: 72, overall: 70));
            database.SaveTrainingSession(Session(resonance: 74, voiceHealth: 78, overall: 70));
            database.SaveTrainingSession(Session(resonance: 72, voiceHealth: 75, overall: 70));

            var provider = new VocalHealthBaselineProvider(database);

            var baseline = provider.GetBaseline();

            Assert.Equal(0.70, baseline.BaselineResonance, 2);
            Assert.Equal(0.75, baseline.BaselineStability, 2);
            Assert.Equal("low", baseline.ConfidenceLevel);
        }

        [Fact]
        public void CreateOptions_AppliesPersonalBaselineToHealthAndHydration()
        {
            var database = new TestDatabaseService();
            database.SetSmartCoachBaseline(new SmartCoachBaseline
            {
                UserId = 1,
                BaselineResonanceScore = 60,
                ConfidenceLevel = "medium"
            });
            database.SaveTrainingSession(Session(voiceHealth: 80, overall: 70));

            var provider = new VocalHealthBaselineProvider(database);

            var healthOptions = provider.CreateVocalHealthOptions();
            var hydrationOptions = provider.CreateHydrationOptions();

            Assert.Equal(0.60, healthOptions.BaselineResonance, 3);
            Assert.Equal(0.80, healthOptions.BaselineStability, 3);
            Assert.Equal(0.60, hydrationOptions.BaselineResonance, 3);
            Assert.Equal(0.80, hydrationOptions.BaselineStability, 3);
        }

        private static TrainingSession Session(
            double resonance = 70,
            double voiceHealth = 0,
            double overall = 70)
            => new()
            {
                UserId = 1,
                StartTime = DateTime.Now,
                EndTime = DateTime.Now.AddMinutes(5),
                ResonanceScore = resonance,
                VoiceHealthScore = voiceHealth,
                OverallScore = overall
            };
    }
}
