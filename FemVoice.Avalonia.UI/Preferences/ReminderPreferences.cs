using System;

namespace FemVoice.Avalonia.Preferences;

/// <summary>
/// Read-only bridge from the persisted daily-reminder preferences to the dashboard nudge. Fail-safe: any read error
/// yields "reminders off", so a corrupt/missing prefs file never fabricates a nudge. The reminder itself is computed
/// by <c>TrainingReminderScheduler</c> from these settings + real session history.
/// </summary>
public static class ReminderPreferences
{
    /// <summary>Whether the user turned the daily training reminder on. Never throws (defaults off).</summary>
    public static bool RemindersEnabled()
    {
        try { return new UiPreferencesStore().Load().RemindersEnabled; }
        catch { return false; }
    }

    /// <summary>Preferred reminder time of day (local). Never throws (defaults 18:00).</summary>
    public static TimeSpan ReminderTimeOfDay()
    {
        try
        {
            int m = new UiPreferencesStore().Load().ReminderMinuteOfDay;
            if (m is < 0 or > 1439) m = 1080;
            return TimeSpan.FromMinutes(m);
        }
        catch { return TimeSpan.FromMinutes(1080); }
    }
}
