using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using FemVoiceStudio.Data;
using FemVoice.Avalonia.Localization;   // Localized (safe read-only localization resolver)

namespace FemVoice.Avalonia.ViewModels;

/// <summary>One real exported session row (read from saved TrainingSessions). Display/export only.</summary>
public sealed record ReportExportRow(DateTime Date, int DurationMinutes, double AveragePitch, double OverallScore);

/// <summary>One display-only reports/professional card: title + description + a deferred status. Always inert.</summary>
public sealed class ReportsCard
{
    public ReportsCard(string title, string description, string status)
    {
        Title = title;
        Description = description;
        Status = status;
    }

    public string Title { get; }
    public string Description { get; }
    public string Status { get; }
    /// <summary>Always <c>false</c> — every report/professional action is deferred/inert in this scaffold.</summary>
    public bool IsEnabled => false;
}

/// <summary>
/// DISPLAY-ONLY Reports / Professional-workflow scaffold. A purely static page: it holds NO services, NO
/// commands, is NOT IDisposable, starts no timers/subscriptions/capture/background work, opens no file
/// dialogs, and reads/writes NOTHING (no database, no session history, no report generation, no export).
/// It shows static placeholder cards mirroring the WPF reports/professional surfaces, every action deferred.
/// No clinical scoring, SmartCoach/progression, Voice-Health/recovery, or diagnostics. Labels resolve through
/// the safe read-only localization adapter (namespaced keys with Norwegian fallback).
/// </summary>
public sealed class ReportsViewModel
{
    public ReportsViewModel() : this(null) { }

    public ReportsViewModel(IDatabaseService? database, System.Action? openCoachPanel = null, System.Action? openClinicianPanel = null)
    {
        OpenCoachCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => openCoachPanel?.Invoke());
        CanOpenCoachPanel = openCoachPanel is not null;
        OpenClinicianCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => openClinicianPanel?.Invoke());
        CanOpenClinicianPanel = openClinicianPanel is not null;
        BuildPreview(database);
        string deferred = Localized.Get("Reports_DeferredStatus", "Utsatt — kommer senere");
        string sample = Localized.Get("Reports_SampleStatus", "Eksempel (ikke lagret)");

        Cards = new List<ReportsCard>
        {
            new(Localized.Get("Reports_Preview", "Rapport-forhåndsvisning"),
                Localized.Get("Reports_PreviewDesc", "Forhåndsvisning av rapport (eksempeltekst, ingen ekte generering)."), sample),

            new(Localized.Get("Reports_ProgressSummary", "Fremgangssammendrag"),
                Localized.Get("Reports_ProgressSummaryDesc", "Sammendrag av fremgang (plassholder, eksempeldata)."), sample),

            new(Localized.Get("Reports_SessionHistory", "Økthistorikk"),
                Localized.Get("Reports_SessionHistoryDesc", "Tidligere økter — krever lagring senere (ingen historikk lest)."), deferred),

            new(Localized.Get("Reports_Clinician", "Klinikerpanel"),
                Localized.Get("Reports_ClinicianDesc", "Profesjonelt klinikerpanel (utsatt, ingen funksjonalitet)."), deferred),

            new(Localized.Get("Reports_Coach", "Veilederpanel"),
                Localized.Get("Reports_CoachDesc", "Profesjonelt veilederpanel (utsatt, ingen funksjonalitet)."), deferred),

            new(Localized.Get("Reports_Saksgjennomgang", "Saksgjennomgang"),
                Localized.Get("Reports_SaksgjennomgangDesc", "Saksgjennomgang for fagperson (utsatt, ingen funksjonalitet)."), deferred),

            new(Localized.Get("Reports_Calendar", "Kalender / historikk"),
                Localized.Get("Reports_CalendarDesc", "Kalender- og historikkvisning (utsatt, ingen lagring)."), deferred),

            new(Localized.Get("Reports_Exports", "Eksport"),
                Localized.Get("Reports_ExportsDesc", "Eksport av rapporter (utsatt — ingen fildialog, ingen filer skrives)."), deferred),
        };

        Title = Localized.Get("Reports_ScaffoldTitle", "Rapporter og profesjonelle verktøy");
        ScaffoldNotice = Localized.Get("Reports_ScaffoldNotice",
            "Fremgangssammendraget og CSV/tekst-eksporten over er ekte (leser dine lagrede økter). De kliniske " +
            "fagpanelene (kliniker/veileder/saksgjennomgang), PDF-generering og full 4×3-rapporteksport er " +
            "fortsatt utsatt — de krever klinisk rapportsammenstilling og kommer i en senere fase.");
    }

    public IReadOnlyList<ReportsCard> Cards { get; }
    public string Title { get; }
    public string ScaffoldNotice { get; }

    /// <summary>Opens the real read-only coach panel (assembled from saved sessions). Wired by the shell.</summary>
    public CommunityToolkit.Mvvm.Input.IRelayCommand OpenCoachCommand { get; }
    /// <summary>True when a coach-panel navigation callback was supplied (drives the button's visibility).</summary>
    public bool CanOpenCoachPanel { get; }
    public string OpenCoachLabel => Localized.Get("Reports_OpenCoachPanel", "Åpne veilederpanel");

    /// <summary>Opens the real read-only clinician outcome panel (assembled from saved sessions). Wired by the shell.</summary>
    public CommunityToolkit.Mvvm.Input.IRelayCommand OpenClinicianCommand { get; }
    public bool CanOpenClinicianPanel { get; }
    public string OpenClinicianLabel => Localized.Get("Reports_OpenClinicianPanel", "Åpne klinikerpanel");

    /// <summary>Always <c>true</c>: every card/action in the scaffold is deferred/inert.</summary>
    public bool AllActionsDeferred => Cards.All(c => !c.IsEnabled);

    // ── Real progress-summary report PREVIEW (from the real DB) ────────────────────────────────────────────────
    /// <summary>True when a real progress-summary preview was built from saved sessions.</summary>
    public bool HasPreview { get; private set; }
    public string PreviewTitle { get; private set; } = "Fremgangssammendrag";
    public string PreviewBody { get; private set; } = "";

    // ── Real report EXPORT (CSV / text of the saved sessions) ──────────────────────────────────────────────────
    /// <summary>Real per-session rows read from the DB, oldest→newest (empty with no DB / no sessions).</summary>
    public IReadOnlyList<ReportExportRow> ExportRows { get; private set; } = Array.Empty<ReportExportRow>();
    /// <summary>True when there is real session data to export (drives the enabled Export buttons).</summary>
    public bool CanExport => ExportRows.Count > 0;
    public string ExportLabel => Localized.Get("Reports_ExportCsv", "Eksporter CSV");
    public string ExportTextLabel => Localized.Get("Reports_ExportText", "Eksporter tekst");
    public string SuggestedCsvName => "femvoice-rapport.csv";
    public string SuggestedTextName => "femvoice-rapport.txt";

    /// <summary>Build the real CSV export (header + one row per saved session). Invariant culture, RFC-4180 quoting
    /// via <see cref="Csv"/>. Pure — no I/O; the View writes the returned text to a user-picked file.</summary>
    public string BuildCsv()
    {
        var sb = new StringBuilder();
        sb.Append("Dato,Varighet (min),Snitt tonehøyde (Hz),FemVoice-score\r\n");
        foreach (var r in ExportRows)
            sb.Append(Csv(r.Date.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))).Append(',')
              .Append(r.DurationMinutes.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(r.AveragePitch.ToString("F0", CultureInfo.InvariantCulture)).Append(',')
              .Append(r.OverallScore.ToString("F0", CultureInfo.InvariantCulture)).Append("\r\n");
        return sb.ToString();
    }

    /// <summary>Build the real plain-text report (the same summary shown in the preview + a per-session list).</summary>
    public string BuildText()
    {
        var sb = new StringBuilder();
        sb.Append(PreviewTitle).Append("\r\n\r\n").Append(PreviewBody).Append("\r\n\r\n");
        sb.Append("Økter:\r\n");
        foreach (var r in ExportRows)
            sb.Append("  ").Append(r.Date.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))
              .Append("  ·  ").Append(r.DurationMinutes).Append(" min")
              .Append(r.AveragePitch > 0 ? $"  ·  {r.AveragePitch:F0} Hz" : "")
              .Append($"  ·  score {r.OverallScore:F0}\r\n");
        return sb.ToString();
    }

    // Minimal RFC-4180 CSV cell quoting (quote when the cell contains a comma/quote/newline; double embedded quotes).
    private static string Csv(string cell)
        => cell.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + cell.Replace("\"", "\"\"") + "\"" : cell;

    // Build a real, read-only progress-summary report preview from the user's saved sessions. NO file export, NO
    // clinical report assembler (that needs the OutcomeProfile/notes/audit pipeline + file dialogs — a follow-up);
    // this is the honest report CONTENT the DB can already produce. Fails safe (no preview) with no DB / on error.
    private void BuildPreview(IDatabaseService? database)
    {
        if (database is null) return;
        try
        {
            var sessions = database.GetRecentSessions(50);
            if (sessions.Count == 0)
            {
                HasPreview = true;
                PreviewBody = "Ingen lagrede økter ennå. Fullfør en økt på dashbordet for å generere et sammendrag.";
                return;
            }
            var ordered = sessions.OrderBy(s => s.StartTime).ToList();
            ExportRows = ordered.Select(s => new ReportExportRow(
                s.StartTime.ToLocalTime(),
                (int)Math.Round(s.DurationSeconds / 60.0),
                s.AveragePitch,
                s.OverallScore)).ToList();
            var pitches = ordered.Select(s => s.AveragePitch).Where(p => p > 0).ToList();
            double avgPitch = pitches.Count > 0 ? pitches.Average() : 0;
            double avgScore = ordered.Average(s => s.OverallScore);
            double bestScore = ordered.Max(s => s.OverallScore);
            int totalMinutes = (int)Math.Round(ordered.Sum(s => s.DurationSeconds) / 60.0);
            DateTime from = ordered.First().StartTime.ToLocalTime();
            DateTime to = ordered.Last().StartTime.ToLocalTime();

            PreviewBody =
                $"Periode: {from:yyyy-MM-dd} – {to:yyyy-MM-dd}\n" +
                $"Antall økter: {ordered.Count} · Total tid: {totalMinutes} min\n" +
                (avgPitch > 0 ? $"Snitt tonehøyde: {avgPitch:F0} Hz\n" : "") +
                $"Snitt FemVoice-score: {avgScore:F0} / 100 · Beste økt: {bestScore:F0} / 100";
            HasPreview = true;
        }
        catch { HasPreview = false; }
    }
}
