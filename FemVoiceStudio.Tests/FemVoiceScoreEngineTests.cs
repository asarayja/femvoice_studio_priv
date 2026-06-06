using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Unit tests for FemVoiceScoreEngine.
    /// Tests adaptive scoring, baseline calculation, trend detection, and plateau/regression detection.
    /// </summary>
    public class FemVoiceScoreEngineTests
    {
        #region In-Mock Repositories

        private class InMemoryScoreRepository : IScoreRepository
        {
            private readonly List<FemVoiceScoreSnapshot> _scores = new();
            private UserScoreBaseline? _baseline;

            public Task<IReadOnlyList<FemVoiceScoreSnapshot>> GetScoreHistoryAsync(int userId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
            {
                var result = _scores.Where(s => s.UserId == userId && s.Timestamp >= from && s.Timestamp <= to).ToList();
                return Task.FromResult<IReadOnlyList<FemVoiceScoreSnapshot>>(result);
            }

            public Task<IReadOnlyList<FemVoiceScoreSnapshot>> GetRecentScoresAsync(int userId, int count, CancellationToken cancellationToken = default)
            {
                var result = _scores.Where(s => s.UserId == userId).OrderByDescending(s => s.Timestamp).Take(count).ToList();
                return Task.FromResult<IReadOnlyList<FemVoiceScoreSnapshot>>(result);
            }

            public Task SaveScoreAsync(FemVoiceScoreSnapshot snapshot, CancellationToken cancellationToken = default)
            {
                _scores.Add(snapshot);
                return Task.CompletedTask;
            }

            public Task<UserScoreBaseline?> GetBaselineAsync(int userId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<UserScoreBaseline?>(_baseline);
            }

            public Task SaveBaselineAsync(UserScoreBaseline baseline, CancellationToken cancellationToken = default)
            {
                _baseline = baseline;
                return Task.CompletedTask;
            }

            public Task<IReadOnlyList<FemVoiceScoreSnapshot>> GetRollingHistoryAsync(int userId, int days, CancellationToken cancellationToken = default)
            {
                var cutoff = DateTime.Now.AddDays(-days);
                var result = _scores.Where(s => s.UserId == userId && s.Timestamp >= cutoff).OrderBy(s => s.Timestamp).ToList();
                return Task.FromResult<IReadOnlyList<FemVoiceScoreSnapshot>>(result);
            }
        }

        private class InMemoryUserRepository : IUserRepository
        {
            public Task<ScoringConfiguration?> GetScoringConfigurationAsync(int userId) => Task.FromResult<ScoringConfiguration?>(null);
            public Task SaveScoringConfigurationAsync(int userId, ScoringConfiguration config) => Task.CompletedTask;
            public Task<UserComfortZoneSettings?> GetComfortZoneSettingsAsync(int userId) => Task.FromResult<UserComfortZoneSettings?>(null);
            public Task SaveComfortZoneSettingsAsync(int userId, UserComfortZoneSettings settings) => Task.CompletedTask;
            public Task<UserHealthData?> GetUserHealthDataAsync(int userId) => Task.FromResult<UserHealthData?>(new UserHealthData { UserId = userId, HealthScore = 100 });
            public Task RecordStrainIncidentAsync(int userId, StrainIncident incident) => Task.CompletedTask;
            public Task<IReadOnlyList<StrainIncident>> GetRecentStrainIncidentsAsync(int userId, int days) => Task.FromResult<IReadOnlyList<StrainIncident>>(new List<StrainIncident>());
            public Task<UserToleranceData?> GetToleranceDataAsync(int userId) => Task.FromResult<UserToleranceData?>(null);
            public Task UpdateToleranceDataAsync(int userId, UserToleranceData data) => Task.CompletedTask;
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidRepositories_CreatesInstance()
        {
            var engine = new FemVoiceScoreEngine(new InMemoryScoreRepository(), new InMemoryUserRepository());
            Assert.NotNull(engine);
        }

        [Fact]
        public void Constructor_WithNullScoreRepository_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new FemVoiceScoreEngine(null!, new InMemoryUserRepository()));
        }

        [Fact]
        public void Constructor_WithNullUserRepository_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new FemVoiceScoreEngine(new InMemoryScoreRepository(), null!));
        }

        [Fact]
        public void Constructor_WithCustomConfiguration_UsesCustomWeights()
        {
            var config = new ScoringConfiguration { ResonanceWeight = 0.50, PitchWeight = 0.25, StabilityWeight = 0.15, HealthWeight = 0.10 };
            var engine = new FemVoiceScoreEngine(new InMemoryScoreRepository(), new InMemoryUserRepository(), config);
            Assert.Equal(0.50, engine.GetConfiguration().ResonanceWeight);
        }

        #endregion

        #region SetUserAsync Tests

        [Fact]
        public async Task SetUserAsync_WithValidUserId_LoadsBaseline()
        {
            var engine = new FemVoiceScoreEngine(new InMemoryScoreRepository(), new InMemoryUserRepository());
            await engine.SetUserAsync(1);
            var baseline = engine.GetCurrentBaseline();
            Assert.NotNull(baseline);
            Assert.Equal(1, baseline.UserId);
        }

        [Fact]
        public async Task SetUserAsync_WithInvalidUserId_ThrowsArgumentOutOfRangeException()
        {
            var engine = new FemVoiceScoreEngine(new InMemoryScoreRepository(), new InMemoryUserRepository());
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => engine.SetUserAsync(0));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => engine.SetUserAsync(-1));
        }

        #endregion

        #region CalculateScoreAsync Tests

        [Fact]
        public async Task CalculateScoreAsync_WithValidMetrics_ReturnsValidSnapshot()
        {
            var engine = new FemVoiceScoreEngine(new InMemoryScoreRepository(), new InMemoryUserRepository());
            await engine.SetUserAsync(1);
            var snapshot = await engine.CalculateScoreAsync(80, 70, 75, 90);
            Assert.NotNull(snapshot);
            Assert.Equal(1, snapshot.UserId);
            Assert.InRange(snapshot.RawScore, 0, 100);
            Assert.InRange(snapshot.AdaptiveScore, 0, 100);
        }

        [Fact]
        public async Task CalculateScoreAsync_WithoutUserSet_ThrowsInvalidOperationException()
        {
            var engine = new FemVoiceScoreEngine(new InMemoryScoreRepository(), new InMemoryUserRepository());
            await Assert.ThrowsAsync<InvalidOperationException>(() => engine.CalculateScoreAsync(80, 70, 75, 90));
        }

        [Fact]
        public async Task CalculateScoreAsync_WithMaxScores_Returns100()
        {
            var engine = new FemVoiceScoreEngine(new InMemoryScoreRepository(), new InMemoryUserRepository());
            await engine.SetUserAsync(1);
            var snapshot = await engine.CalculateScoreAsync(100, 100, 100, 100);
            Assert.Equal(100, snapshot.RawScore, 1);
        }

        [Fact]
        public async Task CalculateScoreAsync_WithMinScores_ReturnsClampedValue()
        {
            var engine = new FemVoiceScoreEngine(new InMemoryScoreRepository(), new InMemoryUserRepository());
            await engine.SetUserAsync(1);
            var snapshot = await engine.CalculateScoreAsync(0, 0, 0, 0);
            Assert.Equal(0, snapshot.RawScore, 0);
        }

        #endregion

        #region Weight Formula Tests

        [Fact]
        public void DefaultWeights_SumToOne()
        {
            var total = FemVoiceScoreEngine.DefaultResonanceWeight +
                        FemVoiceScoreEngine.DefaultPitchWeight +
                        FemVoiceScoreEngine.DefaultStabilityWeight +
                        FemVoiceScoreEngine.DefaultHealthWeight;
            Assert.Equal(1.0, total, 5);
        }

        [Fact]
        public async Task CalculateScoreAsync_UsesDefaultWeights_Correctly()
        {
            var engine = new FemVoiceScoreEngine(new InMemoryScoreRepository(), new InMemoryUserRepository());
            await engine.SetUserAsync(1);
            var snapshot = await engine.CalculateScoreAsync(100, 100, 100, 100);
            // Formula: 0.45*100 + 0.30*100 + 0.15*100 + 0.10*100 = 100
            Assert.Equal(100, snapshot.RawScore, 1);
        }

        [Fact]
        public async Task CalculateScoreAsync_AppliesResonanceWeightCorrectly()
        {
            var engine = new FemVoiceScoreEngine(new InMemoryScoreRepository(), new InMemoryUserRepository());
            await engine.SetUserAsync(1);
            // Expected: 0.45*100 + 0.30*0 + 0.15*0 + 0.10*0 = 45
            var snapshot = await engine.CalculateScoreAsync(100, 0, 0, 0);
            Assert.Equal(45, snapshot.RawScore, 1);
        }

        [Fact]
        public async Task CalculateScoreAsync_AppliesPitchWeightCorrectly()
        {
            var engine = new FemVoiceScoreEngine(new InMemoryScoreRepository(), new InMemoryUserRepository());
            await engine.SetUserAsync(1);
            // Expected: 0.45*0 + 0.30*100 + 0.15*0 + 0.10*0 = 30
            var snapshot = await engine.CalculateScoreAsync(0, 100, 0, 0);
            Assert.Equal(30, snapshot.RawScore, 1);
        }

        [Fact]
        public async Task CalculateScoreAsync_AppliesStabilityWeightCorrectly()
        {
            var engine = new FemVoiceScoreEngine(new InMemoryScoreRepository(), new InMemoryUserRepository());
            await engine.SetUserAsync(1);
            // Expected: 0.45*0 + 0.30*0 + 0.15*100 + 0.10*0 = 15
            var snapshot = await engine.CalculateScoreAsync(0, 0, 100, 0);
            Assert.Equal(15, snapshot.RawScore, 1);
        }

        [Fact]
        public async Task CalculateScoreAsync_AppliesHealthWeightCorrectly()
        {
            var engine = new FemVoiceScoreEngine(new InMemoryScoreRepository(), new InMemoryUserRepository());
            await engine.SetUserAsync(1);
            // Expected: 0.45*0 + 0.30*0 + 0.15*0 + 0.10*100 = 10
            var snapshot = await engine.CalculateScoreAsync(0, 0, 0, 100);
            Assert.Equal(10, snapshot.RawScore, 1);
        }

        #endregion

        #region Event Tests

        [Fact]
        public async Task ScoreUpdated_EventRaisedOnCalculation()
        {
            var engine = new FemVoiceScoreEngine(new InMemoryScoreRepository(), new InMemoryUserRepository());
            await engine.SetUserAsync(1);
            FemVoiceScoreSnapshot? capturedSnapshot = null;
            engine.ScoreUpdated += snapshot => capturedSnapshot = snapshot;
            var result = await engine.CalculateScoreAsync(80, 70, 75, 90);
            Assert.NotNull(capturedSnapshot);
            Assert.Equal(result.RawScore, capturedSnapshot.RawScore);
        }

        [Fact]
        public async Task ScoreUpdated_EventNotRaisedAfterDispose()
        {
            var engine = new FemVoiceScoreEngine(new InMemoryScoreRepository(), new InMemoryUserRepository());
            await engine.SetUserAsync(1);
            var eventRaised = false;
            engine.ScoreUpdated += _ => eventRaised = true;
            engine.Dispose();
            await engine.CalculateScoreAsync(80, 70, 75, 90);
            Assert.False(eventRaised);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public async Task FirstTimeUser_CreatesNewBaseline()
        {
            var engine = new FemVoiceScoreEngine(new InMemoryScoreRepository(), new InMemoryUserRepository());
            await engine.SetUserAsync(1);
            var baseline = engine.GetCurrentBaseline();
            Assert.NotNull(baseline);
            Assert.Equal(0, baseline.DataPointCount);
            Assert.Equal(0, baseline.BaselineScore);
        }

        [Fact]
        public async Task MultipleScoreCalculations_AccumulatesData()
        {
            var engine = new FemVoiceScoreEngine(new InMemoryScoreRepository(), new InMemoryUserRepository());
            await engine.SetUserAsync(1);
            await engine.CalculateScoreAsync(70, 70, 70, 70);
            await engine.CalculateScoreAsync(75, 75, 75, 75);
            await engine.CalculateScoreAsync(80, 80, 80, 80);
            var baseline = engine.GetCurrentBaseline();
            Assert.NotNull(baseline);
            Assert.Equal(3, baseline.DataPointCount);
        }

        [Fact]
        public async Task ScoreValuesAbove100_ClampedTo100()
        {
            var engine = new FemVoiceScoreEngine(new InMemoryScoreRepository(), new InMemoryUserRepository());
            await engine.SetUserAsync(1);
            var snapshot = await engine.CalculateScoreAsync(150, 150, 150, 150);
            Assert.Equal(100, snapshot.RawScore, 0);
        }

        [Fact]
        public async Task ScoreValuesBelow0_ClampedTo0()
        {
            var engine = new FemVoiceScoreEngine(new InMemoryScoreRepository(), new InMemoryUserRepository());
            await engine.SetUserAsync(1);
            var snapshot = await engine.CalculateScoreAsync(-50, -50, -50, -50);
            Assert.Equal(0, snapshot.RawScore, 0);
        }

        #endregion

        #region Configuration Override Tests

        [Fact]
        public void CustomConfiguration_OverridesDefaultWeights()
        {
            var customConfig = new ScoringConfiguration
            {
                ResonanceWeight = 0.60,
                PitchWeight = 0.20,
                StabilityWeight = 0.15,
                HealthWeight = 0.05
            };
            var engine = new FemVoiceScoreEngine(new InMemoryScoreRepository(), new InMemoryUserRepository(), customConfig);
            var config = engine.GetConfiguration();
            Assert.Equal(0.60, config.ResonanceWeight);
            Assert.Equal(0.20, config.PitchWeight);
            Assert.Equal(0.15, config.StabilityWeight);
            Assert.Equal(0.05, config.HealthWeight);
        }

        [Fact]
        public async Task CustomConfiguration_UsedInScoreCalculation()
        {
            var customConfig = new ScoringConfiguration
            {
                ResonanceWeight = 1.0,
                PitchWeight = 0,
                StabilityWeight = 0,
                HealthWeight = 0
            };
            var engine = new FemVoiceScoreEngine(new InMemoryScoreRepository(), new InMemoryUserRepository(), customConfig);
            await engine.SetUserAsync(1);
            // All weight on resonance, so only resonance matters
            var snapshot = await engine.CalculateScoreAsync(80, 50, 50, 50);
            Assert.Equal(80, snapshot.RawScore, 1);
        }

        #endregion

        #region Baseline Calculation Tests

        [Fact]
        public async Task Baseline_CalculatesCorrectlyWithMultipleScores()
        {
            var repo = new InMemoryScoreRepository();
            var engine = new FemVoiceScoreEngine(repo, new InMemoryUserRepository());
            await engine.SetUserAsync(1);
            
            // Add some historical scores
            for (int i = 0; i < 10; i++)
            {
                await engine.CalculateScoreAsync(60 + i, 60 + i, 60 + i, 60 + i);
            }
            
            var baseline = engine.GetCurrentBaseline();
            Assert.NotNull(baseline);
            Assert.True(baseline.DataPointCount >= 10);
        }

        #endregion
    }
}
