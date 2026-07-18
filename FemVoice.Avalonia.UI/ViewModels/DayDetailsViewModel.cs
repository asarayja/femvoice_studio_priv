using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using FemVoiceStudio.Data;
using FemVoice.Avalonia.Localization;

namespace FemVoice.Avalonia.ViewModels;

/// <summary>One session's detail row for the day view (real values from the saved session). Read-only.</summary>
public sealed record DaySessionRow(string Time, string Duration, string Pitch, string Resonance, string Score);

/// <summary>
/// Day-details panel, ported from the WPF DayDetailsWindow: shows every REAL training session recorded on a chosen
/// day (time, duration, pitch range, resonance, score), read from the real database. Opened from the Calendar.
/// Read-only; no clinical logic changed; fails safe with no DB.
/// </summary>
public sealed class DayDetailsViewModel
{
    public DayDetailsViewModel() : this(null, DateTime.Today, null) { }

    public DayDetailsViewModel(IDatabaseService? database, DateTime date, Action? onBack = null)
    {
        Date = date;
        DateDisplay = date.ToString("dddd d. MMMM yyyy", CultureInfo.CurrentCulture);
        BackLabel = Localized.Get("Common_Back", "Tilbake");
        BackCommand = new RelayCommand(() => onBack?.Invoke());
        Load(database, date);
    }

    public DateTime Date { get; }
    public string DateDisplay { get; }
    public string BackLabel { get; }
    public IRelayCommand BackCommand { get; }
    public string Title => Localized.Get("DayDetails_Title", "Øktdetaljer");

    public IReadOnlyList<DaySessionRow> Sessions { get; private set; } = Array.Empty<DaySessionRow>();
    public bool HasSessions => Sessions.Count > 0;
    public string EmptyMessage => Localized.Get("DayDetails_Empty", "Ingen økter registrert denne dagen.");
    public string Summary { get; private set; } = "";

    private void Load(IDatabaseService? database, DateTime date)
    {
        if (database is null) return;
        try
        {
            // Query a generous UTC window around the local day and filter to the exact local date.
            var from = date.Date.AddDays(-1).ToUniversalTime();
            var to = date.Date.AddDays(2).ToUniversalTime();
            var sessions = database.GetTrainingSessions(from, to)
                .Where(s => s.StartTime.ToLocalTime().Date == date.Date)
                .OrderBy(s => s.StartTime)
                .ToList();

            Sessions = sessions.Select(s => new DaySessionRow(
                s.StartTime.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture),
                $"{(int)Math.Round(s.DurationSeconds / 60.0)} min",
                s.AveragePitch > 0 ? $"{s.AveragePitch:F0} Hz ({s.MinPitch:F0}–{s.MaxPitch:F0})" : "—",
                s.ResonanceScore > 0 ? $"{s.ResonanceScore:F0}" : "—",
                $"{s.OverallScore:F0} / 100")).ToList();

            if (sessions.Count > 0)
                Summary = $"{sessions.Count} økter · snitt score {sessions.Average(s => s.OverallScore):F0} · "
                        + $"total {(int)Math.Round(sessions.Sum(s => s.DurationSeconds) / 60.0)} min";
        }
        catch { Sessions = Array.Empty<DaySessionRow>(); }
    }
}
