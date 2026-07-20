using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using FemVoiceStudio.Data;
using FemVoice.Avalonia.Localization;   // Localized (safe read-only localization resolver)

namespace FemVoice.Avalonia.ViewModels;

/// <summary>One real exported session row (read from saved TrainingSessions). Display/export only.</summary>
public sealed record ReportExportRow(DateTime Date, int DurationMinutes, double AveragePitch, double OverallScore)
{
    /// <summary>One-line display of the session for the history list.</summary>
    public string Display => $"{Date:yyyy-MM-dd HH:mm}  ·  {DurationMinutes} min" +
        (AveragePitch > 0 ? $"  ·  {AveragePitch:F0} Hz" : "") + $"  ·  score {OverallScore:F0}";
}

/// <summary>
/// FUNCTIONAL Reports page. It reads the user's saved sessions and produces a real progress-summary preview, a real
/// session-history list, real CSV/plain-text export (native save dialog, via the view code-behind), and it opens the
/// real professional panels — Coach, Clinician, development Timeline and Case-review — each assembled read-only from
/// the same saved data and each exportable to PDF/CSV/JSON. Nothing here is a deferred placeholder.
/// </summary>
public sealed class ReportsViewModel
{
    public ReportsViewModel() : this(null) { }

    public ReportsViewModel(IDatabaseService? database, System.Action? openCoachPanel = null,
        System.Action? openClinicianPanel = null, System.Action? openTimelinePanel = null, System.Action? openCaseReviewPanel = null)
    {
        OpenCoachCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => openCoachPanel?.Invoke());
        CanOpenCoachPanel = openCoachPanel is not null;
        OpenClinicianCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => openClinicianPanel?.Invoke());
        CanOpenClinicianPanel = openClinicianPanel is not null;
        OpenTimelineCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => openTimelinePanel?.Invoke());
        CanOpenTimelinePanel = openTimelinePanel is not null;
        OpenCaseReviewCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => openCaseReviewPanel?.Invoke());
        CanOpenCaseReviewPanel = openCaseReviewPanel is not null;
        BuildPreview(database);

        Title = Localized.Get("Report_Title", "Rapporter");
        Intro = Localized.Get("Reports_Intro2",
            "Ekte fremgangssammendrag, økthistorikk og CSV/tekst-eksport fra dine lagrede økter, pluss de " +
            "profesjonelle panelene (coach, kliniker, tidslinje, case-gjennomgang) med PDF/CSV/JSON-eksport.");
    }

    public string Title { get; }
    public string Intro { get; }

    // ── Professional panels (all real; opened via the shell) ──────────────────────────────────────────────────────
    public string ProfessionalHeading => Localized.Get("Reports_ProfessionalHeading", "Profesjonelle paneler");
    public bool HasProfessionalPanels => CanOpenCoachPanel || CanOpenClinicianPanel || CanOpenTimelinePanel || CanOpenCaseReviewPanel;

    /// <summary>Opens the real read-only coach panel (assembled from saved sessions). Wired by the shell.</summary>
    public CommunityToolkit.Mvvm.Input.IRelayCommand OpenCoachCommand { get; }
    /// <summary>True when a coach-panel navigation callback was supplied (drives the button's visibility).</summary>
    public bool CanOpenCoachPanel { get; }
    public string OpenCoachLabel => Localized.Get("Reports_OpenCoachPanel", "Åpne coach-oversikt");

    /// <summary>Opens the real read-only clinician outcome panel (assembled from saved sessions). Wired by the shell.</summary>
    public CommunityToolkit.Mvvm.Input.IRelayCommand OpenClinicianCommand { get; }
    public bool CanOpenClinicianPanel { get; }
    public string OpenClinicianLabel => Localized.Get("Reports_OpenClinicianPanel", "Åpne klinisk oversikt");

    /// <summary>Opens the real read-only development-timeline panel (assembled from saved sessions). Wired by the shell.</summary>
    public CommunityToolkit.Mvvm.Input.IRelayCommand OpenTimelineCommand { get; }
    public bool CanOpenTimelinePanel { get; }
    public string OpenTimelineLabel => Localized.Get("Reports_OpenTimelinePanel", "Åpne utviklingstidslinje");

    /// <summary>Opens the real read-only case-review panel (OutcomeProfile for a chosen period). Wired by the shell.</summary>
    public CommunityToolkit.Mvvm.Input.IRelayCommand OpenCaseReviewCommand { get; }
    public bool CanOpenCaseReviewPanel { get; }
    public string OpenCaseReviewLabel => Localized.Get("Reports_OpenCaseReviewPanel", "Åpne case-gjennomgang");

    // ── Real session history (from the saved sessions) ───────────────────────────────────────────────────────────
    public string HistoryHeading => Localized.Get("Reports_SessionHistory", "Økthistorikk");
    /// <summary>True when there is real saved-session history to list.</summary>
    public bool HasHistory => ExportRows.Count > 0;

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
        sb.Append(Localized.Get("Reports_CsvHeader", "Dato,Varighet (min),Snitt tonehøyde (Hz),FemVoice-score")).Append("\r\n");
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
        sb.Append(Localized.Get("Reports_SessionsHeader", "Økter:")).Append("\r\n");
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
                PreviewBody = Localized.Get("Reports_NoSessionsPreview", "Ingen lagrede økter ennå. Fullfør en økt på dashbordet for å generere et sammendrag.");
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
                string.Format(Localized.Get("Reports_PeriodFormat", "Periode: {0} – {1}"), from.ToString("yyyy-MM-dd"), to.ToString("yyyy-MM-dd")) + "\n" +
                string.Format(Localized.Get("Reports_SessionCountTimeFormat", "Antall økter: {0} · Total tid: {1} min"), ordered.Count, totalMinutes) + "\n" +
                (avgPitch > 0 ? string.Format(Localized.Get("Reports_AvgPitchLineFormat", "Snitt tonehøyde: {0} Hz"), avgPitch.ToString("F0")) + "\n" : "") +
                string.Format(Localized.Get("Reports_ScoreLineFormat", "Snitt FemVoice-score: {0} / 100 · Beste økt: {1} / 100"), avgScore.ToString("F0"), bestScore.ToString("F0"));
            HasPreview = true;
        }
        catch { HasPreview = false; }
    }
}
