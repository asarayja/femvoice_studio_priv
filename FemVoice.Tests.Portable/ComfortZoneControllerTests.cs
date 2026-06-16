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
    /// Unit tests for ComfortZoneController.
    /// Tests zone expansion, contraction, safety lock, and multi-condition scenarios.
    /// </summary>
    public class ComfortZoneControllerTests
    {
        #region Mock Repositories

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

            public void Clear() { _scores.Clear(); _baseline = null; }

            public void AddScore(FemVoiceScoreSnapshot score) { _scores.Add(score); }
        }

        private class InMemoryUserRepository : IUserRepository
        {
            private UserComfortZoneSettings? _zoneSettings;
            private UserHealthData? _healthData;
            private UserToleranceData? _toleranceData;
            private readonly List<StrainIncident> _incidents = new();

            public Task<ScoringConfiguration?> GetScoringConfigurationAsync(int userId) => Task.FromResult<ScoringConfiguration?>(null);
            public Task SaveScoringConfigurationAsync(int userId, ScoringConfiguration config) => Task.CompletedTask;

            public Task<UserComfortZoneSettings?> GetComfortZoneSettingsAsync(int userId)
            {
                return Task.FromResult<UserComfortZoneSettings?>(_zoneSettings);
            }

            public Task SaveComfortZoneSettingsAsync(int userId, UserComfortZoneSettings settings)
            {
                _zoneSettings = settings;
                return Task.CompletedTask;
            }

            public void SetComfortZoneSettings(UserComfortZoneSettings settings) { _zoneSettings = settings; }

            public Task<UserHealthData?> GetUserHealthDataAsync(int userId)
            {
                return Task.FromResult<UserHealthData?>(_healthData);
            }

            public void SetHealthData(UserHealthData data) { _healthData = data; }

            public Task RecordStrainIncidentAsync(int userId, StrainIncident incident)
            {
                _incidents.Add(incident);
                return Task.CompletedTask;
            }

            public Task<IReadOnlyList<StrainIncident>> GetRecentStrainIncidentsAsync(int userId, int days)
            {
                var cutoff = DateTime.Now.AddDays(-days);
                var result = _incidents.Where(i => i.Timestamp >= cutoff).ToList();
                return Task.FromResult<IReadOnlyList<StrainIncident>>(result);
            }

            public Task<UserToleranceData?> GetToleranceDataAsync(int userId)
            {
                return Task.FromResult<UserToleranceData?>(_toleranceData);
            }

            public void SetToleranceData(UserToleranceData data) { _toleranceData = data; }

            public Task UpdateToleranceDataAsync(int userId, UserToleranceData data)
            {
                _toleranceData = data;
                return Task.CompletedTask;
            }
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidRepositories_CreatesInstance()
        {
            var controller = new ComfortZoneController(new InMemoryUserRepository(), new InMemoryScoreRepository());
            Assert.NotNull(controller);
        }

        [Fact]
        public void Constructor_WithNullUserRepository_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new ComfortZoneController(null!, new InMemoryScoreRepository()));
        }

        [Fact]
        public void Constructor_WithNullScoreRepository_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new ComfortZoneController(new InMemoryUserRepository(), null!));
        }

        [Fact]
        public void Constructor_WithCustomConfiguration_UsesCustomValues()
        {
            var config = new ZoneConfiguration { MinPitch = 150, MaxPitch = 300, ZoneWidth = 100 };
            var controller = new ComfortZoneController(new InMemoryUserRepository(), new InMemoryScoreRepository(), config);
            var loaded = controller.GetConfiguration();
            Assert.Equal(150, loaded.MinPitch);
            Assert.Equal(300, loaded.MaxPitch);
        }

        #endregion

        #region InitializeAsync Tests

        [Fact]
        public async Task InitializeAsync_WithValidUserId_LoadsZoneState()
        {
            var userRepo = new InMemoryUserRepository();
            var scoreRepo = new InMemoryScoreRepository();
            var controller = new ComfortZoneController(userRepo, scoreRepo);

            await controller.InitializeAsync(1);

            var state = controller.GetCurrentState();
            Assert.NotNull(state);
            Assert.Equal(1, state.UserId);
        }

        [Fact]
        public async Task InitializeAsync_WithInvalidUserId_ThrowsArgumentOutOfRangeException()
        {
            var controller = new ComfortZoneController(new InMemoryUserRepository(), new InMemoryScoreRepository());
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => controller.InitializeAsync(0));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => controller.InitializeAsync(-1));
        }

        #endregion

        #region Zone Expansion Tests

        [Fact]
        public async Task UpdateZoneAsync_With7ConsecutiveStableDays_AllowsExpansion()
        {
            var userRepo = new InMemoryUserRepository();
            var scoreRepo = new InMemoryScoreRepository();
            var controller = new ComfortZoneController(userRepo, scoreRepo);

            userRepo.SetHealthData(new UserHealthData { UserId = 1, HealthScore = 100 });
            userRepo.SetToleranceData(new UserToleranceData { UserId = 1, AverageToleratedWidth = 90, MaxToleratedWidth = 150 });

            // Add 7 stable days of scores
            for (int i = 0; i < 7; i++)
            {
                scoreRepo.AddScore(new FemVoiceScoreSnapshot
                {
                    UserId = 1,
                    ResonanceScore = 80,
                    PitchScore = 80,
                    StabilityScore = 80,
                    HealthModifier = 100,
                    AdaptiveScore = 75 + i,
                    Timestamp = DateTime.Now.AddDays(-7 + i)
                });
            }

            await controller.InitializeAsync(1);
            var state = await controller.UpdateZoneAsync(80, 80, 80, 80, 100);

            Assert.True(state.IsExpansionAllowed);
            Assert.Contains("expanded", state.Reason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UpdateZoneAsync_ZoneWidthExpandsByMax5Percent()
        {
            var userRepo = new InMemoryUserRepository();
            var scoreRepo = new InMemoryScoreRepository();
            var controller = new ComfortZoneController(userRepo, scoreRepo);

            userRepo.SetHealthData(new UserHealthData { UserId = 1, HealthScore = 100 });
            userRepo.SetToleranceData(new UserToleranceData { UserId = 1, AverageToleratedWidth = 100, MaxToleratedWidth = 200 });

            // Add 7 stable days
            for (int i = 0; i < 7; i++)
            {
                scoreRepo.AddScore(new FemVoiceScoreSnapshot
                {
                    UserId = 1,
                    ResonanceScore = 80,
                    StabilityScore = 80,
                    AdaptiveScore = 75,
                    Timestamp = DateTime.Now.AddDays(-7 + i)
                });
            }

            await controller.InitializeAsync(1);
            var initialState = controller.GetCurrentState();
            var initialWidth = initialState.ZoneWidth;

            var newState = await controller.UpdateZoneAsync(80, 80, 80, 80, 100);
            
            var maxExpansion = initialWidth * ComfortZoneController.MaxWeeklyExpansionRate;
            var actualExpansion = newState.ZoneWidth - initialWidth;
            Assert.True(actualExpansion <= maxExpansion + 0.1); // Allow small floating point variance
        }

        #endregion

        #region Zone Contraction Tests

        [Fact]
        public async Task UpdateZoneAsync_WithHealthScoreBelow70_ContractsZone()
        {
            var userRepo = new InMemoryUserRepository();
            var scoreRepo = new InMemoryScoreRepository();
            var controller = new ComfortZoneController(userRepo, scoreRepo);

            userRepo.SetHealthData(new UserHealthData { UserId = 1, HealthScore = 60 }); // Below 70

            await controller.InitializeAsync(1);
            var state = await controller.UpdateZoneAsync(50, 50, 50, 50, 60);

            Assert.False(state.IsExpansionAllowed);
            Assert.Contains("contracted", state.Reason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UpdateZoneAsync_ZoneContractsOnLowHealth()
        {
            var userRepo = new InMemoryUserRepository();
            var scoreRepo = new InMemoryScoreRepository();
            var controller = new ComfortZoneController(userRepo, scoreRepo);

            userRepo.SetHealthData(new UserHealthData { UserId = 1, HealthScore = 50 });

            await controller.InitializeAsync(1);
            var initialState = controller.GetCurrentState();
            var initialWidth = initialState.ZoneWidth;

            var newState = await controller.UpdateZoneAsync(50, 50, 50, 50, 50);
            
            Assert.True(newState.ZoneWidth < initialWidth);
        }

        #endregion

        #region Safety Lock Tests

        [Fact]
        public async Task RecordStrainIncidentAsync_WithCriticalStrain_EngagesLock()
        {
            var userRepo = new InMemoryUserRepository();
            var scoreRepo = new InMemoryScoreRepository();
            var controller = new ComfortZoneController(userRepo, scoreRepo);

            await controller.InitializeAsync(1);
            await controller.RecordStrainIncidentAsync(75, "pitch_press", 250);

            var state = controller.GetCurrentState();
            Assert.True(state.IsSafetyLocked);
            Assert.False(state.IsExpansionAllowed);
            Assert.Contains("strain", state.Reason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task RecordStrainIncidentAsync_WithModerateStrain_DoesNotEngageLock()
        {
            var userRepo = new InMemoryUserRepository();
            var scoreRepo = new InMemoryScoreRepository();
            var controller = new ComfortZoneController(userRepo, scoreRepo);

            await controller.InitializeAsync(1);
            await controller.RecordStrainIncidentAsync(50, "fatigue", 200);

            var state = controller.GetCurrentState();
            Assert.False(state.IsSafetyLocked);
        }

        [Fact]
        public async Task SafetyLock_LastsFor3Days()
        {
            var userRepo = new InMemoryUserRepository();
            var scoreRepo = new InMemoryScoreRepository();
            var controller = new ComfortZoneController(userRepo, scoreRepo);

            await controller.InitializeAsync(1);
            await controller.RecordStrainIncidentAsync(80, "critical", 280);

            var state = controller.GetCurrentState();
            Assert.Equal(ComfortZoneController.SafetyLockDurationDays, state.SafetyLockDaysRemaining);
        }

        #endregion

        #region Stability Tests

        [Fact]
        public async Task UpdateZoneAsync_WithLowStability_FreezesExpansion()
        {
            var userRepo = new InMemoryUserRepository();
            var scoreRepo = new InMemoryScoreRepository();
            var controller = new ComfortZoneController(userRepo, scoreRepo);

            userRepo.SetHealthData(new UserHealthData { UserId = 1, HealthScore = 100 });

            await controller.InitializeAsync(1);
            var state = await controller.UpdateZoneAsync(80, 80, 40, 80, 100); // Low stability

            Assert.False(state.IsExpansionAllowed);
            Assert.Contains("stability", state.Reason, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Individual Tolerance Tests

        [Fact]
        public async Task UpdateZoneAsync_IndividualizedWidth_Respected()
        {
            var userRepo = new InMemoryUserRepository();
            var scoreRepo = new InMemoryScoreRepository();
            var controller = new ComfortZoneController(userRepo, scoreRepo);

            // Set user settings with a width that allows tolerance data to take effect
            userRepo.SetComfortZoneSettings(new UserComfortZoneSettings
            {
                UserId = 1,
                PreferredMinPitch = 165,
                PreferredMaxPitch = 255,
                ZoneWidth = 120 // This should be respected since it's >= DefaultMinZoneWidth
            });

            userRepo.SetToleranceData(new UserToleranceData 
            { 
                UserId = 1, 
                AverageToleratedWidth = 120,
                MaxToleratedWidth = 150 
            });

            await controller.InitializeAsync(1);
            var state = controller.GetCurrentState();

            Assert.Equal(120, state.ZoneWidth);
        }

        #endregion

        #region Event Tests

        [Fact]
        public async Task ZoneUpdated_EventRaisedOnUpdate()
        {
            var userRepo = new InMemoryUserRepository();
            var scoreRepo = new InMemoryScoreRepository();
            var controller = new ComfortZoneController(userRepo, scoreRepo);

            await controller.InitializeAsync(1);

            ComfortZoneState? capturedState = null;
            controller.ZoneUpdated += state => capturedState = state;

            // Give a slight delay to ensure event fires
            await controller.UpdateZoneAsync(80, 80, 80, 80, 100);
            await Task.Delay(10); // Allow event handler to execute

            Assert.NotNull(capturedState);
        }

        [Fact]
        public async Task ZoneUpdated_EventNotRaisedAfterDispose()
        {
            var userRepo = new InMemoryUserRepository();
            var scoreRepo = new InMemoryScoreRepository();
            var controller = new ComfortZoneController(userRepo, scoreRepo);

            await controller.InitializeAsync(1);

            var eventRaised = false;
            controller.ZoneUpdated += _ => eventRaised = true;
            controller.Dispose();

            // After dispose, the controller should handle gracefully - test expects no exception
            try
            {
                await controller.UpdateZoneAsync(80, 80, 80, 80, 100);
            }
            catch (ObjectDisposedException)
            {
                // Expected - controller is disposed
            }

            Assert.False(eventRaised);
        }

        #endregion
    }
}
