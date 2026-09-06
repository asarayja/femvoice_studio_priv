using System;
using System.Collections.Generic;
using System.Linq;

namespace FemVoiceStudio.Services
{
    /// <summary>What the reminder surface should do right now.</summary>
    public enum ReminderState
    {
        /// <summary>Reminders are turned off — show nothing.</summary>
        Disabled,
        /// <summary>Nothing to nudge: already trained today, or the weekly goal is already met.</summary>
        Done,
        /// <summary>A session is still owed this week and today, but the preferred reminder time hasn't arrived yet.</summary>
        Upcoming,
        /// <summary>A session is owed and the preferred time has passed — nudge the user now.</summary>
        Due,
    }

    /// <summary>The evaluated reminder situation. All counts are for the CURRENT local week (Mon–Sun).</summary>
    public readonly record struct ReminderStatus(
        ReminderState State,
        // Distinct training DAYS so far this week (not session rows) — the goal is days/week.
        int SessionsThisWeek,
        int WeeklyGoal,
        int RemainingThisWeek,
        bool TrainedToday,
        DateTime? NextReminderLocal);

    /// <summary>
    /// Pure, deterministic policy for the daily training reminder. It answers "should we nudge the user to train, and
    /// if not now, when?" from the user's weekly goal, their preferred reminder time, and their real recent session
    /// history — no timers, no I/O, no platform calls, so it is fully unit-testable and identical on every platform.
    ///
    /// Design goals (humane, habit-supporting, NOT nagging):
    ///   • Never nudge twice in a day — once a session is logged today, the state is <see cref="ReminderState.Done"/>.
    ///   • Never nudge past the weekly goal — once <c>SessionsThisWeek &gt;= WeeklyGoal</c>, also <see cref="ReminderState.Done"/>.
    ///   • Only surface a nudge AFTER the user's chosen time of day (so it fits their routine).
    /// The scheduler itself does not fire notifications; a caller (dashboard nudge today; an OS-notification bridge
    /// later) decides how to present <see cref="ReminderState.Due"/>.
    /// </summary>
    public static class TrainingReminderScheduler
    {
        /// <summary>
        /// Evaluate the reminder situation. <paramref name="weeklyGoal"/> is the training days/week (clamped 1–7);
        /// <paramref name="preferredTime"/> is the local time of day to remind; <paramref name="recentSessionLocalTimes"/>
        /// is recent session start times in LOCAL time (order irrelevant); <paramref name="nowLocal"/> is the current
        /// local time.
        /// </summary>
        public static ReminderStatus Evaluate(bool enabled, int weeklyGoal, TimeSpan preferredTime,
            IReadOnlyList<DateTime> recentSessionLocalTimes, DateTime nowLocal)
        {
            int goal = Math.Clamp(weeklyGoal, 1, 7);
            var sessions = recentSessionLocalTimes ?? Array.Empty<DateTime>();

            DateTime weekStart = StartOfWeek(nowLocal.Date);            // Monday 00:00 of the current week
            DateTime today = nowLocal.Date;
            // Count distinct training DAYS, not rows: the weekly goal is "days per week"
            // (UiPreferences.TrainingFrequency), so several short recordings on one day must not consume the whole week.
            int sessionsThisWeek = sessions.Where(t => t.Date >= weekStart && t.Date <= today)
                                           .Select(t => t.Date).Distinct().Count();
            bool trainedToday = sessions.Any(t => t.Date == today);
            int remaining = Math.Max(0, goal - sessionsThisWeek);

            DateTime preferredToday = today + preferredTime;
            DateTime preferredTomorrow = today.AddDays(1) + preferredTime;

            if (!enabled)
                return new ReminderStatus(ReminderState.Disabled, sessionsThisWeek, goal, remaining, trainedToday, null);

            // Nothing owed: trained today, or the weekly goal is already met. Next nudge is tomorrow (if the week still
            // has room) — surfaced for display only; the state stays Done so nothing pops today.
            if (trainedToday || remaining == 0)
            {
                DateTime? next = remaining > 0 ? preferredTomorrow : (DateTime?)null;
                return new ReminderStatus(ReminderState.Done, sessionsThisWeek, goal, remaining, trainedToday, next);
            }

            // A session is owed today. Before the preferred time → Upcoming (today); at/after → Due (now).
            if (nowLocal < preferredToday)
                return new ReminderStatus(ReminderState.Upcoming, sessionsThisWeek, goal, remaining, trainedToday, preferredToday);

            return new ReminderStatus(ReminderState.Due, sessionsThisWeek, goal, remaining, trainedToday, preferredToday);
        }

        /// <summary>Monday 00:00 of the week containing <paramref name="date"/> (ISO week start; culture-independent).</summary>
        public static DateTime StartOfWeek(DateTime date)
        {
            int diff = ((int)date.DayOfWeek + 6) % 7;   // Monday=0 … Sunday=6
            return date.Date.AddDays(-diff);
        }
    }
}
