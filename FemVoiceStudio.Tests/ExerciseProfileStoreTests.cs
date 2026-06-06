using System;
using System.IO;
using System.Threading.Tasks;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace FemVoiceStudio.Tests
{
    public class ExerciseProfileStoreTests
    {
        [Fact]
        public async Task InMemoryStore_ReturnsSavedProfileForUserAndExercise()
        {
            var store = new InMemoryExerciseProfileStore();
            var profile = ExerciseTargetProfile.CreateResonanceVowels(
                targetResonanceMin: 0.62,
                targetResonanceMax: 0.90,
                stabilityThreshold: 0.60,
                requiredHoldSeconds: 4.5);

            await store.SaveAsync(new ExerciseProfileOverride
            {
                UserId = 2,
                ExerciseId = 7,
                Profile = profile,
                ReasonCode = "STABLE_RESONANCE_PROGRESS"
            });

            var loaded = await store.GetAsync(2, 7);

            Assert.NotNull(loaded);
            Assert.Equal("STABLE_RESONANCE_PROGRESS", loaded!.ReasonCode);
            AssertProfile(profile, loaded.Profile);
        }

        [Fact]
        public async Task SqliteStore_PersistsProfileAcrossInstances()
        {
            var databasePath = Path.Combine(Path.GetTempPath(), $"femvoice-profiles-{Guid.NewGuid():N}.db");
            var connectionString = $"Data Source={databasePath}";
            var profile = ExerciseTargetProfile.CreateStabilityTraining(
                targetResonanceMin: 0.48,
                targetResonanceMax: 0.86,
                stabilityThreshold: 0.74,
                requiredHoldSeconds: 6.5);

            try
            {
                var firstStore = new SqliteExerciseProfileStore(connectionString);
                await firstStore.SaveAsync(new ExerciseProfileOverride
                {
                    UserId = 1,
                    ExerciseId = 12,
                    Profile = profile,
                    ReasonCode = "STABILITY_READY"
                });

                var secondStore = new SqliteExerciseProfileStore(connectionString);
                var loaded = await secondStore.GetAsync(1, 12);

                Assert.NotNull(loaded);
                Assert.Equal("STABILITY_READY", loaded!.ReasonCode);
                Assert.Equal(12, loaded.ExerciseId);
                AssertProfile(profile, loaded.Profile);
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                if (File.Exists(databasePath))
                {
                    File.Delete(databasePath);
                }
            }
        }

        [Fact]
        public async Task SqliteStore_UpsertsExistingProfile()
        {
            var databasePath = Path.Combine(Path.GetTempPath(), $"femvoice-profiles-{Guid.NewGuid():N}.db");
            var connectionString = $"Data Source={databasePath}";

            try
            {
                var store = new SqliteExerciseProfileStore(connectionString);

                await store.SaveAsync(new ExerciseProfileOverride
                {
                    UserId = 1,
                    ExerciseId = 3,
                    Profile = ExerciseTargetProfile.CreateResonanceHumming(targetResonanceMin: 0.50),
                    ReasonCode = "FIRST"
                });

                var updatedProfile = ExerciseTargetProfile.CreateResonanceHumming(targetResonanceMin: 0.56);
                await store.SaveAsync(new ExerciseProfileOverride
                {
                    UserId = 1,
                    ExerciseId = 3,
                    Profile = updatedProfile,
                    ReasonCode = "UPDATED"
                });

                var loaded = await store.GetAsync(1, 3);

                Assert.NotNull(loaded);
                Assert.Equal("UPDATED", loaded!.ReasonCode);
                AssertProfile(updatedProfile, loaded.Profile);
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                if (File.Exists(databasePath))
                {
                    File.Delete(databasePath);
                }
            }
        }

        private static void AssertProfile(ExerciseTargetProfile expected, ExerciseTargetProfile actual)
        {
            Assert.Equal(expected.UsesResonance, actual.UsesResonance);
            Assert.Equal(expected.UsesPitch, actual.UsesPitch);
            Assert.Equal(expected.UsesStability, actual.UsesStability);
            Assert.Equal(expected.UsesIntensity, actual.UsesIntensity);
            Assert.Equal(expected.MinPitch, actual.MinPitch);
            Assert.Equal(expected.MaxPitch, actual.MaxPitch);
            Assert.Equal(expected.TargetResonanceMin, actual.TargetResonanceMin, 3);
            Assert.Equal(expected.TargetResonanceMax, actual.TargetResonanceMax, 3);
            Assert.Equal(expected.StabilityThreshold, actual.StabilityThreshold, 3);
            Assert.Equal(expected.RequiredHoldSeconds, actual.RequiredHoldSeconds, 3);
            Assert.Equal(expected.ClinicalPurposeKey, actual.ClinicalPurposeKey);
            Assert.Equal(expected.PhysicalFocusKey, actual.PhysicalFocusKey);
            Assert.Equal(expected.CommonMistakesKey, actual.CommonMistakesKey);
            Assert.Equal(expected.SafetyInfoKey, actual.SafetyInfoKey);
            Assert.Equal(expected.FeedbackModeKey, actual.FeedbackModeKey);
            Assert.Equal(expected.ThresholdStrategyKey, actual.ThresholdStrategyKey);
            Assert.Equal(expected.IndicatorPackageSummaryKey, actual.IndicatorPackageSummaryKey);
        }
    }
}
