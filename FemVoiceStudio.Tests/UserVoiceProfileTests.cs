using System;
using FemVoiceStudio.Models;
using Xunit;

namespace FemVoiceStudio.Tests
{
    public class UserVoiceProfileTests
    {
        [Fact]
        public void NewProfile_HasSafeDefaults()
        {
            var profile = new UserVoiceProfile();

            Assert.Equal(1, profile.UserId);
            Assert.Equal(3, profile.TrainingFrequencyPerWeek);
            Assert.Equal(VoiceStyleGoal.Feminine, profile.PreferredVoiceStyle);
            Assert.False(profile.StressSensitiveMode);
            Assert.False(profile.ReducedVisualFeedback);

            // Ikke-kalibrert komfortsone er null til den faktisk måles.
            Assert.Null(profile.ComfortZoneMinPitch);
            Assert.Null(profile.ComfortZoneMaxPitch);
            Assert.Null(profile.ComfortZoneOptimalPitch);

            Assert.Equal(0, profile.BaselinePitch);
            Assert.Equal(0, profile.BaselineResonance);
            Assert.Equal(0, profile.BaselineComfort);
            Assert.Equal(0, profile.BaselineHealth);
        }

        [Theory]
        [InlineData("soft_feminine", VoiceStyleGoal.Feminine)]
        [InlineData("androgynous", VoiceStyleGoal.Androgynous)]
        [InlineData("dark_feminine", VoiceStyleGoal.DarkFeminine)]
        [InlineData("situational", VoiceStyleGoal.Situational)]
        public void FromGoalStyleKey_MapsKnownKeys(string key, VoiceStyleGoal expected)
        {
            Assert.Equal(expected, UserVoiceProfile.FromGoalStyleKey(key));
        }

        [Theory]
        [InlineData("bright_neutral")]
        [InlineData("custom")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        [InlineData("totally_unknown_value")]
        public void FromGoalStyleKey_UnknownOrEmpty_FallsBackToCustom(string? key)
        {
            Assert.Equal(VoiceStyleGoal.Custom, UserVoiceProfile.FromGoalStyleKey(key));
        }

        [Fact]
        public void FromGoalStyleKey_IsCaseAndWhitespaceInsensitive()
        {
            Assert.Equal(VoiceStyleGoal.DarkFeminine, UserVoiceProfile.FromGoalStyleKey("  Dark_Feminine  "));
        }

        [Theory]
        [InlineData(VoiceStyleGoal.Feminine, "soft_feminine")]
        [InlineData(VoiceStyleGoal.Androgynous, "androgynous")]
        [InlineData(VoiceStyleGoal.DarkFeminine, "dark_feminine")]
        [InlineData(VoiceStyleGoal.Situational, "situational")]
        [InlineData(VoiceStyleGoal.Custom, "custom")]
        public void ToGoalStyleKey_MapsEveryEnumValue(VoiceStyleGoal style, string expected)
        {
            Assert.Equal(expected, UserVoiceProfile.ToGoalStyleKey(style));
        }

        [Fact]
        public void GoalStyleKey_RoundTripsForCanonicalStyles()
        {
            // Custom har ingen 1:1 kanonisk kilde-nøkkel ('custom' -> Custom -> 'custom' holder),
            // men de fire kanoniske stilene må overleve en full rundtur.
            foreach (var style in new[]
                     {
                         VoiceStyleGoal.Feminine,
                         VoiceStyleGoal.Androgynous,
                         VoiceStyleGoal.DarkFeminine,
                         VoiceStyleGoal.Situational
                     })
            {
                var key = UserVoiceProfile.ToGoalStyleKey(style);
                Assert.Equal(style, UserVoiceProfile.FromGoalStyleKey(key));
            }
        }

        [Fact]
        public void SaveAndGet_RoundTripsAllFields()
        {
            var db = new TestDatabaseService();
            var created = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);

            var profile = new UserVoiceProfile
            {
                UserId = 1,
                CreatedAt = created,
                BaselinePitch = 182.5,
                BaselineResonance = 0.6,
                BaselineComfort = 0.8,
                BaselineHealth = 92.0,
                PreferredVoiceStyle = VoiceStyleGoal.Androgynous,
                TrainingFrequencyPerWeek = 5,
                ComfortZoneMinPitch = 160,
                ComfortZoneMaxPitch = 240,
                ComfortZoneOptimalPitch = 200,
                StressSensitiveMode = true,
                ReducedVisualFeedback = true
            };

            db.SaveUserVoiceProfile(profile);
            var loaded = db.GetUserVoiceProfile(1);

            Assert.NotNull(loaded);
            Assert.Equal(182.5, loaded!.BaselinePitch);
            Assert.Equal(0.6, loaded.BaselineResonance);
            Assert.Equal(0.8, loaded.BaselineComfort);
            Assert.Equal(92.0, loaded.BaselineHealth);
            Assert.Equal(VoiceStyleGoal.Androgynous, loaded.PreferredVoiceStyle);
            Assert.Equal(5, loaded.TrainingFrequencyPerWeek);
            Assert.Equal(160, loaded.ComfortZoneMinPitch);
            Assert.Equal(240, loaded.ComfortZoneMaxPitch);
            Assert.Equal(200, loaded.ComfortZoneOptimalPitch);
            Assert.True(loaded.StressSensitiveMode);
            Assert.True(loaded.ReducedVisualFeedback);
        }

        [Fact]
        public void Get_BeforeAnySave_ReturnsNull()
        {
            var db = new TestDatabaseService();

            Assert.Null(db.GetUserVoiceProfile(1));
        }

        [Fact]
        public void Save_SetsLastUpdated()
        {
            var db = new TestDatabaseService();
            var stale = DateTime.UtcNow.AddDays(-30);

            var profile = new UserVoiceProfile
            {
                UserId = 1,
                LastUpdated = stale
            };

            var before = DateTime.UtcNow;
            db.SaveUserVoiceProfile(profile);
            var after = DateTime.UtcNow;

            var loaded = db.GetUserVoiceProfile(1);
            Assert.NotNull(loaded);
            Assert.True(loaded!.LastUpdated >= before && loaded.LastUpdated <= after,
                "LastUpdated skal settes til lagringstidspunktet, ikke den gamle verdien.");
            Assert.True(loaded.LastUpdated > stale);
        }

        [Fact]
        public void Save_OverwritesExistingProfileForSameUser()
        {
            var db = new TestDatabaseService();

            db.SaveUserVoiceProfile(new UserVoiceProfile { UserId = 1, TrainingFrequencyPerWeek = 2 });
            db.SaveUserVoiceProfile(new UserVoiceProfile { UserId = 1, TrainingFrequencyPerWeek = 4 });

            var loaded = db.GetUserVoiceProfile(1);
            Assert.NotNull(loaded);
            Assert.Equal(4, loaded!.TrainingFrequencyPerWeek);
        }

        [Fact]
        public void SaveAndGet_NullComfortZone_IsPreserved()
        {
            var db = new TestDatabaseService();

            var profile = new UserVoiceProfile
            {
                UserId = 1,
                ComfortZoneMinPitch = null,
                ComfortZoneMaxPitch = null,
                ComfortZoneOptimalPitch = null
            };

            db.SaveUserVoiceProfile(profile);
            var loaded = db.GetUserVoiceProfile(1);

            Assert.NotNull(loaded);
            Assert.Null(loaded!.ComfortZoneMinPitch);
            Assert.Null(loaded.ComfortZoneMaxPitch);
            Assert.Null(loaded.ComfortZoneOptimalPitch);
        }
    }
}
