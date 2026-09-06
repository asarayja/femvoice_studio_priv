using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoice.Tests.Portable
{
    /// <summary>
    /// The reminder policy must be humane: nudge when a session is genuinely owed and the user's time has come, but
    /// never twice a day, never past the weekly goal, and never when switched off. Pure inputs → deterministic state.
    /// </summary>
    public class TrainingReminderSchedulerTests
    {
        // Wednesday 2026-09-09, 19:00 local; preferred reminder 18:00; goal 3 days/week.
        private static readonly DateTime Wed1900 = new(2026, 9, 9, 19, 0, 0);
        private static readonly TimeSpan At18 = new(18, 0, 0);

        [Fact]
        public void Disabled_NudgesNothing()
        {
            var s = TrainingReminderScheduler.Evaluate(false, 3, At18, new List<DateTime>(), Wed1900);
            Assert.Equal(ReminderState.Disabled, s.State);
        }

        [Fact]
        public void OwedAndPastPreferredTime_IsDue()
        {
            // No sessions this week, 19:00 > 18:00 → due now.
            var s = TrainingReminderScheduler.Evaluate(true, 3, At18, new List<DateTime>(), Wed1900);
            Assert.Equal(ReminderState.Due, s.State);
            Assert.Equal(3, s.RemainingThisWeek);
            Assert.False(s.TrainedToday);
            Assert.Equal(new DateTime(2026, 9, 9, 18, 0, 0), s.NextReminderLocal);
        }

        [Fact]
        public void OwedButBeforePreferredTime_IsUpcoming()
        {
            var before = new DateTime(2026, 9, 9, 8, 0, 0);   // 08:00 < 18:00
            var s = TrainingReminderScheduler.Evaluate(true, 3, At18, new List<DateTime>(), before);
            Assert.Equal(ReminderState.Upcoming, s.State);
            Assert.Equal(new DateTime(2026, 9, 9, 18, 0, 0), s.NextReminderLocal);
        }

        [Fact]
        public void TrainedToday_IsDone_AndNeverNudgesAgainToday()
        {
            var sessions = new List<DateTime> { new(2026, 9, 9, 9, 30, 0) };   // trained this morning
            var s = TrainingReminderScheduler.Evaluate(true, 3, At18, sessions, Wed1900);
            Assert.Equal(ReminderState.Done, s.State);
            Assert.True(s.TrainedToday);
            Assert.Equal(1, s.SessionsThisWeek);
        }

        [Fact]
        public void WeeklyGoalMet_IsDone_EvenIfNotTrainedToday()
        {
            // Mon + Tue (twice on Tue) = 2 distinct in-week days, plus a session from the PREVIOUS week that must not
            // count. Goal 3 → one day still owed today → Due.
            var owed = new List<DateTime>
            {
                new(2026, 9, 6, 9, 0, 0),    // Sun of the previous week (week starts Mon 2026-09-07) — excluded
                new(2026, 9, 7, 18, 0, 0),   // Mon
                new(2026, 9, 8, 18, 0, 0),   // Tue
                new(2026, 9, 8, 20, 0, 0),   // Tue again — does not add a day
            };
            var owedStatus = TrainingReminderScheduler.Evaluate(true, 3, At18, owed, Wed1900);
            Assert.Equal(ReminderState.Due, owedStatus.State);
            Assert.Equal(2, owedStatus.SessionsThisWeek);
            Assert.Equal(1, owedStatus.RemainingThisWeek);

            // A third distinct in-week day (today) meets the goal → Done.
            var met = new List<DateTime>(owed) { new DateTime(2026, 9, 9, 7, 0, 0) };
            var metStatus = TrainingReminderScheduler.Evaluate(true, 3, At18, met, Wed1900);
            Assert.Equal(ReminderState.Done, metStatus.State);
            Assert.True(metStatus.TrainedToday);
            Assert.Equal(3, metStatus.SessionsThisWeek);
            Assert.Equal(0, metStatus.RemainingThisWeek);
        }

        [Fact]
        public void ManySessionsInOneDay_DoNotConsumeTheWholeWeek()
        {
            // Five recordings on Monday only. Goal 3 days/week → Monday counts once, so a session is still owed today.
            var sessions = Enumerable.Range(0, 5)
                .Select(i => new DateTime(2026, 9, 7, 10 + i, 0, 0)).ToList();
            var s = TrainingReminderScheduler.Evaluate(true, 3, At18, sessions, Wed1900);
            Assert.Equal(1, s.SessionsThisWeek);          // one DAY, not five sessions
            Assert.Equal(2, s.RemainingThisWeek);
            Assert.Equal(ReminderState.Due, s.State);
        }

        [Fact]
        public void LastWeeksSessions_DoNotCountThisWeek()
        {
            var sessions = new List<DateTime> { new(2026, 9, 2, 18, 0, 0) };   // previous Wednesday
            var s = TrainingReminderScheduler.Evaluate(true, 3, At18, sessions, Wed1900);
            Assert.Equal(0, s.SessionsThisWeek);
            Assert.Equal(ReminderState.Due, s.State);
        }

        [Fact]
        public void StartOfWeek_IsMonday()
        {
            // 2026-09-09 is a Wednesday → week starts Monday 2026-09-07.
            Assert.Equal(new DateTime(2026, 9, 7), TrainingReminderScheduler.StartOfWeek(Wed1900));
            // Sunday belongs to the week that started the preceding Monday.
            Assert.Equal(new DateTime(2026, 9, 7), TrainingReminderScheduler.StartOfWeek(new DateTime(2026, 9, 13)));
        }

        [Fact]
        public void GoalIsClampedIntoRange()
        {
            var s = TrainingReminderScheduler.Evaluate(true, 99, At18, new List<DateTime>(), Wed1900);
            Assert.Equal(7, s.WeeklyGoal);
        }
    }
}
