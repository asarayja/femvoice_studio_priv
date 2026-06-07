using System;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    using SmartCoachWeeklyProgress = FemVoiceStudio.Data.SmartCoachWeeklyProgress;

    /// <summary>
    /// Tests for training-frequency as an active runtime input (Agent 2).
    ///
    /// The weekly session target is the user's OWN goal, sourced from
    /// <see cref="UserVoiceProfile.TrainingFrequencyPerWeek"/> — not a hardcoded
    /// constant. When no profile exists, the engine falls back to 3 (mirroring the
    /// UserVoiceProfile default and the WeeklyGoals table default), never crashing.
    /// </summary>
    public class SmartCoachFrequencyTests
    {
        private readonly TestDatabaseService _db = new();

        [Fact]
        public void GetWeeklySessionTarget_FollowsUserVoiceProfileFrequency()
        {
            _db.SaveUserVoiceProfile(new UserVoiceProfile
            {
                UserId = 1,
                TrainingFrequencyPerWeek = 5
            });
            var engine = new SmartCoachEngine(_db);

            Assert.Equal(5, engine.GetWeeklySessionTarget(1));
        }

        [Fact]
        public void GetWeeklySessionTarget_FallsBackToThreeWhenProfileMissing()
        {
            // No profile saved at all.
            var engine = new SmartCoachEngine(_db);

            Assert.Equal(3, engine.GetWeeklySessionTarget(1));
        }

        [Fact]
        public void GetWeeklySessionTarget_FallsBackToThreeWhenFrequencyNonPositive()
        {
            _db.SaveUserVoiceProfile(new UserVoiceProfile
            {
                UserId = 1,
                TrainingFrequencyPerWeek = 0 // un-set / invalid → use fallback, not 0
            });
            var engine = new SmartCoachEngine(_db);

            Assert.Equal(3, engine.GetWeeklySessionTarget(1));
        }

        [Fact]
        public void GetWeeklySessionTarget_HonoursAmbitiousFrequency()
        {
            _db.SaveUserVoiceProfile(new UserVoiceProfile
            {
                UserId = 1,
                TrainingFrequencyPerWeek = 7
            });
            var engine = new SmartCoachEngine(_db);

            Assert.Equal(7, engine.GetWeeklySessionTarget(1));
        }

        [Fact]
        public void GetWeeklySessionTarget_IsPerUser()
        {
            _db.SaveUserVoiceProfile(new UserVoiceProfile { UserId = 1, TrainingFrequencyPerWeek = 2 });
            _db.SaveUserVoiceProfile(new UserVoiceProfile { UserId = 2, TrainingFrequencyPerWeek = 6 });
            var engine = new SmartCoachEngine(_db);

            Assert.Equal(2, engine.GetWeeklySessionTarget(1));
            Assert.Equal(6, engine.GetWeeklySessionTarget(2));
        }

        // ------------------------------------------------------------------
        // The motivational "consistent practice" message now fires at the
        // user's OWN weekly cadence, not a hardcoded threshold of 5.
        // ------------------------------------------------------------------

        [Fact]
        public void MotivationalMessage_ConsistencyFires_WhenUserOwnFrequencyTargetMet()
        {
            // User aims for 3 sessions/week and did exactly 3 — celebrate their cadence.
            _db.SaveUserVoiceProfile(new UserVoiceProfile { UserId = 1, TrainingFrequencyPerWeek = 3 });
            _db.SaveWeeklyProgress(MidScoreWeek(sessions: 3));
            var engine = new SmartCoachEngine(_db);

            engine.GenerateMotivationalMessages(1);

            Assert.Equal(1, _db.GetUnreadMessageCount(1));
        }

        [Fact]
        public void MotivationalMessage_LowFrequencyUser_GetsConsistencyMessageBelowOldHardcodedFive()
        {
            // A 2-sessions/week user who hit their goal would have been SILENT under the
            // old hardcoded ">= 5" rule. Now their own target is honoured.
            _db.SaveUserVoiceProfile(new UserVoiceProfile { UserId = 1, TrainingFrequencyPerWeek = 2 });
            _db.SaveWeeklyProgress(MidScoreWeek(sessions: 2));
            var engine = new SmartCoachEngine(_db);

            engine.GenerateMotivationalMessages(1);

            Assert.Equal(1, _db.GetUnreadMessageCount(1));
        }

        [Fact]
        public void MotivationalMessage_HighFrequencyUser_NoConsistencyMessageUntilOwnTargetMet()
        {
            // User aims for 6 but only did 4 — under their own cadence. The consistency
            // branch should NOT fire (it falls through to the supportive weekly tip).
            // No shame framing is produced either way.
            _db.SaveUserVoiceProfile(new UserVoiceProfile { UserId = 1, TrainingFrequencyPerWeek = 6 });
            _db.SaveWeeklyProgress(MidScoreWeek(sessions: 4));
            var engine = new SmartCoachEngine(_db);

            engine.GenerateMotivationalMessages(1);

            // A message is still produced (the supportive weekly tip), but it must be the
            // tip, not the "consistent training" celebration.
            var messages = _db.GetMessages(1);
            Assert.Single(messages);
            Assert.Equal(
                LocalizationService.Instance.GetString("SmartCoach_Message_WeeklyTipTitle"),
                messages[0].Title);
        }

        // A weekly-progress row with a mid score (between 0 and 80) and zero
        // pitch/resonance change, so GenerateMotivationalMessages reaches the
        // sessions-count consistency branch rather than the achievement/improvement
        // branches that precede it.
        private static SmartCoachWeeklyProgress MidScoreWeek(int sessions)
            => new()
            {
                UserId = 1,
                WeekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek),
                SessionsCount = sessions,
                TotalMinutes = sessions * 5,
                AverageScore = 60,
                PitchChange = 0,
                ResonanceChange = 0,
                IntonationChange = 0,
                HealthScore = 100
            };
    }
}
