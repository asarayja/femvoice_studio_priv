using System;
using System.IO;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    public class LocalVoiceGoalProfileStoreTests
    {
        [Fact]
        public void SaveProfile_RoundTripsUserGoal()
        {
            var directory = CreateTempDirectory();
            try
            {
                var store = new LocalVoiceGoalProfileStore(directory);
                var profile = new VoiceGoalProfile
                {
                    UserId = 7,
                    GoalStyleKey = "androgynous",
                    PrimaryFocus = "intonation",
                    PracticeContexts = "daily",
                    SafetyPreferences = "comfort_first",
                    PreferredCueStyle = "gentle"
                };

                store.SaveProfile(profile);

                var loaded = store.GetProfile(7);

                Assert.NotNull(loaded);
                Assert.Equal("androgynous", loaded.GoalStyleKey);
                Assert.Equal("intonation", loaded.PrimaryFocus);
                Assert.Equal("daily", loaded.PracticeContexts);
                Assert.Equal("comfort_first", loaded.SafetyPreferences);
                Assert.Equal("gentle", loaded.PreferredCueStyle);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public void GetProfile_WithCorruptProfile_ReturnsNull()
        {
            var directory = CreateTempDirectory();
            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "user-1.json"), "{not valid json");

                var store = new LocalVoiceGoalProfileStore(directory);

                Assert.Null(store.GetProfile(1));
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        private static string CreateTempDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), $"FemVoiceStudioTests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            return directory;
        }
    }
}
