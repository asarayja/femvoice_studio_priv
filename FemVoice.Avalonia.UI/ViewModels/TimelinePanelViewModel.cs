using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using FemVoiceStudio.Data;
using FemVoiceStudio.Models;               // OutcomeProfile, TimelineReport, TimelineEntry
using FemVoiceStudio.Services;             // ReportAssembler, OutcomeProfileBuilder, engines, stores
using FemVoice.Avalonia.Localization;

namespace FemVoice.Avalonia.ViewModels;

/// <summary>One timeline row: a trend window (period + composite mean + session count + direction). Read-only.</summary>
public sealed record TimelineRow(string Label, string Period, string Direction, string Detail);

/// <summary>
/// REAL voice-development TIMELINE panel (the 4th WPF report type), ported read-only. Assembles a real
/// <c>OutcomeProfile</c> → <c>TimelineReport</c> from saved sessions via the frozen Core pipeline and renders the
/// development timeline (trend windows: period, composite mean, session count, direction). Fully guarded → truthful
/// "not enough data" state; nothing written, no clinical change. Opened from the Reports page. Exportable
/// (PDF/CSV/JSON) via the Core ExportWriter like the Coach/Clinician panels.
/// </summary>
public sealed class TimelinePanelViewModel
{
    public TimelinePanelViewModel() : this(null, null) { }

    public TimelinePanelViewModel(IDatabaseService? database, Action? onBack = null)
    {
        Title = Localized.Get("Report_Timeline", "Tidslinje");
        BackLabel = Localized.Get("Common_Back", "Tilbake");
        Disclaimer = Localized.Get("Timeline_Panel_Disclaimer",
            "Utviklingstidslinje sammenstilt fra dine lagrede økter (kun lesing). Beskrivende — aldri en " +
            "sikkerhets- eller treningsport. Ingen klinisk endring, ingenting lagres.");
        BackCommand = new RelayCommand(() => onBack?.Invoke());
        TryBuild(database);
    }

    public string Title { get; }
    public string BackLabel { get; }
    public string Disclaimer { get; }
    public IRelayCommand BackCommand { get; }

    public bool HasReport { get; private set; }
    /// <summary>The assembled TimelineReport (or null) — used by the View to export via the Core ExportWriter.</summary>
    public object? Report { get; private set; }
    public string EmptyMessage { get; private set; } =
        Localized.Get("Timeline_Panel_Empty", "Ikke nok data ennå. Fullfør flere økter over tid for å bygge en utviklingstidslinje.");

    public string ReportTitle { get; private set; } = "";
    public IReadOnlyList<TimelineRow> Entries { get; private set; } = Array.Empty<TimelineRow>();

    private void TryBuild(IDatabaseService? database)
    {
        if (database is null) return;
        try
        {
            SessionAnalyticsStore analytics = database is DatabaseService concrete
                ? new SessionAnalyticsStore(new SqliteSessionAnalyticsRepository(concrete.ConnectionString))
                : new SessionAnalyticsStore(new InMemorySessionAnalyticsRepository());

            var builder = new OutcomeProfileBuilder(
                new SmartCoachEngine(database),
                new ExerciseEffectivenessEngine(analytics),
                new LongitudinalInsightEngine());

            DateTime now = DateTime.UtcNow;
            OutcomeProfile outcome = builder
                .AssembleFromStoreAsync(database, null, new RecoveryIntelligenceService(), analytics, now, userId: 1)
                .GetAwaiter().GetResult();

            TimelineReport report = new ReportAssembler().BuildTimelineReport(outcome, now.AddDays(-30), now, now);   // WPF canonical 30-day window
            Report = report;
            ReportTitle = report.Title ?? "";

            Entries = (report.TimelineEntries ?? Array.Empty<TimelineEntry>())
                .Select(e => new TimelineRow(
                    string.IsNullOrWhiteSpace(e.Label) ? "—" : e.Label,
                    $"{e.Window.From.ToLocalTime():yyyy-MM-dd} – {e.Window.To.ToLocalTime():yyyy-MM-dd}",
                    string.IsNullOrWhiteSpace(e.Direction) ? "—" : e.Direction,   // already localized by the Core assembler
                    $"Snitt {e.Window.CompositeMean.ToString("F0", CultureInfo.InvariantCulture)} · {e.Window.SessionCount} økter"))
                .ToList();

            HasReport = Entries.Count > 0;
        }
        catch
        {
            HasReport = false;
        }
    }

}
