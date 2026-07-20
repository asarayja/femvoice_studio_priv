using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using FemVoiceStudio.Data;
using FemVoice.Avalonia.Localization;

namespace FemVoice.Avalonia.ViewModels;

/// <summary>One session's detail row for the day view (real values from the saved session). Read-only.</summary>
public sealed record DaySessionRow(string Time, string Difficulty, string Duration, string Pitch, string Resonance, string Formant, string Score);

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
    /// <summary>Heading above the per-session table (WPF DayDetails "Øktdetaljer").</summary>
    public string SessionDetailsHeading => Localized.Get("DayDetails_SessionDetails", "Øktdetaljer");
    public bool HasSessions => Sessions.Count > 0;
    public string EmptyMessage => Localized.Get("DayDetails_NoSessions", "Ingen økter denne dagen.");
    public string Summary { get; private set; } = "";

    /// <summary>The four WPF summary cards: sessions, total minutes, average score, average pitch.</summary>
    public IReadOnlyList<AnalysisSummaryMetric> SummaryCards { get; private set; } = Array.Empty<AnalysisSummaryMetric>();

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
                s.DifficultyLevel.ToString(),
                $"{(int)Math.Round(s.DurationSeconds / 60.0)} min",
                s.AveragePitch > 0 ? $"{s.AveragePitch:F0} Hz ({s.MinPitch:F0}–{s.MaxPitch:F0})" : "—",
                s.ResonanceScore > 0 ? $"{s.ResonanceScore:F0}" : "—",
                s.AverageF1 > 0 || s.AverageF2 > 0 || s.AverageF3 > 0
                    ? $"F1 {s.AverageF1:F0} · F2 {s.AverageF2:F0} · F3 {s.AverageF3:F0}" : "—",
                $"{s.OverallScore:F0} / 100")).ToList();

            if (sessions.Count > 0)
            {
                int totalMin = (int)Math.Round(sessions.Sum(s => s.DurationSeconds) / 60.0);
                double avgScore = sessions.Average(s => s.OverallScore);
                var pitches = sessions.Select(s => s.AveragePitch).Where(p => p > 0).ToList();
                double avgPitch = pitches.Count > 0 ? pitches.Average() : 0;
                Summary = string.Format(Localized.Get("DayDetails_SummaryFormat", "{0} økter · snitt score {1} · total {2} min"), sessions.Count, avgScore.ToString("F0"), totalMin);
                SummaryCards = new List<AnalysisSummaryMetric>
                {
                    new(Localized.Get("Dashboard_Sessions", "Økter"), sessions.Count.ToString()),
                    new(Localized.Get("Dashboard_Minutes", "Minutter"), $"{totalMin} min"),
                    new(Localized.Get("DayDetails_AverageScore", "Gj. score"), $"{avgScore:F0} / 100"),
                    new(Localized.Get("DayDetails_AverageHz", "Hz gj.snitt"), avgPitch > 0 ? $"{avgPitch:F0} Hz" : "—"),
                };
            }
        }
        catch { Sessions = Array.Empty<DaySessionRow>(); }
    }
}
