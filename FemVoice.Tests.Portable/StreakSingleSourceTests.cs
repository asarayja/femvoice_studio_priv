using System;
using System.IO;
using FemVoiceStudio.Data;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using FemVoiceStudio.Tests;
using Xunit;

namespace FemVoice.Tests.Portable
{
    /// <summary>
    /// Every screen must show the same streak, and it must be true: computed from the saved sessions in the user's
    /// LOCAL days, never a stored counter that only changes when a session is saved.
    /// </summary>
    [Collection("Localization")]
    public class StreakSingleSourceTests
    {
        private static TrainingSession At(DateTime localStart) => new()
        {
            UserId = 1,
            StartTime = localStart.ToUniversalTime(),       // the app stores UTC instants
            EndTime = localStart.AddMinutes(5).ToUniversalTime(),
            AveragePitch = 180, OverallScore = 70, DifficultyLevel = DifficultyLevel.Nybegynner,
        };

        [Fact]
        public void AfterABreak_TheStreakIsZero_NotTheStaleStoredCounter()
        {
            var db = new TestDatabaseService();
            db.UpdateUserSettings(new UserSettings
            {
                CurrentDifficulty = DifficultyLevel.Nybegynner,
                CurrentStreak = 10,                                  // what the counter still says…
                LastSessionDate = DateTime.Now.AddDays(-14),         // …two weeks after the last session
                TotalSessionsCompleted = 10,
                AutoAdvanceLevel = true,
            });
            db.SaveTrainingSession(At(DateTime.Today.AddDays(-14).AddHours(18)));

            var status = new ProgressionService(db).GetProgressionStatus();

            Assert.Equal(0, status.CurrentStreak);
        }

        [Fact]
        public void TheStreakFollowsTheSavedSessions_WhateverTheCounterSays()
        {
            var db = new TestDatabaseService();
            db.UpdateUserSettings(new UserSettings
            {
                CurrentDifficulty = DifficultyLevel.Nybegynner,
                CurrentStreak = 0,                                   // e.g. sessions merged in from another device
                TotalSessionsCompleted = 0,
                AutoAdvanceLevel = true,
            });
            db.SaveTrainingSession(At(DateTime.Today.AddDays(-2).AddHours(18)));
            db.SaveTrainingSession(At(DateTime.Today.AddDays(-1).AddHours(18)));
            db.SaveTrainingSession(At(DateTime.Today.AddHours(9)));

            var status = new ProgressionService(db).GetProgressionStatus();

            Assert.Equal(3, status.CurrentStreak);
        }

        [Fact]
        public void RealDatabase_CountsASessionOnItsLocalDay_NotItsUtcDay()
        {
            string dir = Path.Combine(Path.GetTempPath(), "femvoice-streak-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var db = new DatabaseService(Path.Combine(dir, "streak.db"));   // absolute path → temp database
                // A session shortly after local midnight TODAY, and one at noon two days ago; nothing yesterday.
                // East of UTC (e.g. Norway) the 00:20 session's UTC calendar day is YESTERDAY, so counting UTC days
                // "found" a session yesterday and reported a 2-day streak that never happened.
                db.SaveTrainingSession(At(DateTime.Today.AddMinutes(20)));
                db.SaveTrainingSession(At(DateTime.Today.AddDays(-2).AddHours(12)));

                Assert.Equal(1, db.GetProgressionStats().Streak);   // today only — yesterday was skipped

                // Now practise at noon yesterday too: three sessions on three LOCAL days. In UTC days the 00:20
                // session collapses onto yesterday, which undercounted both the training days and the streak.
                db.SaveTrainingSession(At(DateTime.Today.AddDays(-1).AddHours(12)));

                Assert.Equal(3, db.GetProgressionStats().Streak);
                Assert.Equal(3, db.GetTrainingDaysCount(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow.AddDays(1)));
            }
            finally
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                try { Directory.Delete(dir, true); } catch { }
            }
        }
    }
}
