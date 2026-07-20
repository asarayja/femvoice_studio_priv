using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Data;
using FemVoice.Avalonia.Localization;

namespace FemVoice.Avalonia.ViewModels;

/// <summary>One display-only analysis mini-chart: a title/description + converter-free bar heights (px) + a summary.</summary>
public sealed class AnalysisSeries
{
    public AnalysisSeries(string title, string description, IReadOnlyList<double> bars, string summary)
    {
        Title = title;
        Description = description;
        Bars = bars;
        Summary = summary;
    }

    public string Title { get; }
    public string Description { get; }
    /// <summary>Bar heights in px (0..ChartHeightPx), bottom-aligned — rendered converter-free like the runtime trace.</summary>
    public IReadOnlyList<double> Bars { get; }
    public string Summary { get; }
}

/// <summary>One summary metric row (label + value).</summary>
public sealed class AnalysisSummaryMetric
{
    public AnalysisSummaryMetric(string label, string value)
    {
        Label = label;
        Value = value;
    }

    public string Label { get; }
    public string Value { get; }
}

/// <summary>
/// Analysis / Resonance. When the real database is injected (production), it charts the user's REAL saved sessions
/// (pitch trend + score trend) and REAL summary stats from the DB — no demo data. With no database (headless/tests)
/// it falls back to the deterministic SYNTHETIC sample series. Read-only; no clinical logic changed.
/// </summary>
public sealed class AnalysisViewModel
{
    /// <summary>Fixed mini-chart height in px (bars are 0..this); the view binds bar heights to it.</summary>
    public const double ChartHeightPx = 120;

    public IReadOnlyList<AnalysisSeries> Series { get; }
    public IReadOnlyList<AnalysisSummaryMetric> SummaryMetrics { get; }
    /// <summary>Score-components breakdown (per-dimension averages) — WPF Analysis "Score components" parity.</summary>
    public IReadOnlyList<AnalysisSummaryMetric> ScoreComponents { get; private set; } = Array.Empty<AnalysisSummaryMetric>();
    public bool HasScoreComponents => ScoreComponents.Count > 0;
    public string ScoreComponentsHeading => Localized.Get("Analysis_ScoreComponents", "Score-komponenter");
    public string Title { get; }
    /// <summary>Header subtitle — shared WPF key (Analysis_Subtitle).</summary>
    public string Subtitle => Localized.Get("Analysis_Subtitle", "Resonans, pitch, intonasjon og helsetrender");
    public string SampleDataNotice { get; }
    /// <summary>Human-readable per-session summary (WPF Analysis "Økt-sammendrag"), generated from the real stats.</summary>
    public string SessionSummary { get; } = "";
    public bool HasSessionSummary => SessionSummary.Length > 0;
    public string SessionSummaryHeading => Localized.Get("Analysis_SessionSummary", "Økt-sammendrag");

    /// <summary>True when the charts/metrics are computed from real saved sessions (vs synthetic sample data).</summary>
    public bool HasRealData { get; }

    public AnalysisViewModel() : this(null) { }

    public AnalysisViewModel(IDatabaseService? database)
    {
        Title = Localized.Get("Analysis_Heading", "Dybdeanalyse");

        if (database is not null && TryBuildFromDatabase(database, out var series, out var metrics, out var components, out var notice, out var summary))
        {
            Series = series;
            SummaryMetrics = metrics;
            ScoreComponents = components;
            SampleDataNotice = notice;
            SessionSummary = summary;
            HasRealData = true;
            return;
        }

        // No real analysis to show: with no database (headless/tests) or a DB read error, show a truthful empty
        // state — NEVER fabricated example charts. Real analysis appears once there is a database with saved sessions.
        Series = Array.Empty<AnalysisSeries>();
        SummaryMetrics = Array.Empty<AnalysisSummaryMetric>();
        ScoreComponents = Array.Empty<AnalysisSummaryMetric>();
        SampleDataNotice = database is null
            ? Localized.Get("Analysis_NoDb", "Analyse krever databasen, som ikke er tilgjengelig i denne visningen.")
            : Localized.Get("Analysis_ReadError", "Kunne ikke lese analyse fra databasen.");
        HasRealData = false;
    }

    private static bool TryBuildFromDatabase(IDatabaseService database,
        out IReadOnlyList<AnalysisSeries> series, out IReadOnlyList<AnalysisSummaryMetric> metrics,
        out IReadOnlyList<AnalysisSummaryMetric> components, out string notice, out string sessionSummary)
    {
        series = Array.Empty<AnalysisSeries>();
        metrics = Array.Empty<AnalysisSummaryMetric>();
        components = Array.Empty<AnalysisSummaryMetric>();
        notice = "";
        sessionSummary = "";
        try
        {
            // GetRecentSessions is newest-first; reverse to oldest→newest for a left-to-right trend.
            var ordered = database.GetRecentSessions(30).AsEnumerable().Reverse().ToList();
            if (ordered.Count == 0)
            {
                metrics = new List<AnalysisSummaryMetric> { new(Localized.Get("Analysis_SessionsAnalyzed", "Økter analysert"), Localized.Get("Analysis_NoSessionsShort", "0 — ingen lagrede økter ennå")) };
                notice = Localized.Get("Analysis_NoSessionsNotice", "Ingen lagrede økter ennå. Fullfør en økt på dashbordet, så vises ekte analyse her.");
                return true;
            }

            var pitches = ordered.Select(s => s.AveragePitch).Where(p => p > 0).ToList();
            var scores = ordered.Select(s => s.OverallScore).ToList();
            double avgPitch = pitches.Count > 0 ? pitches.Average() : 0;
            double avgScore = scores.Average();
            double bestScore = scores.Max();

            // Real resonance trend (now saved per session by the dashboard's ResonanceProxyEngine). Sessions recorded
            // before that feature carry 0; report the average over sessions that actually have resonance data.
            var resonances = ordered.Select(s => s.ResonanceScore).ToList();
            var withResonance = resonances.Where(r => r > 0).ToList();
            double avgResonance = withResonance.Count > 0 ? withResonance.Average() : 0;

            // Real prosody: pitch variation (Hz std-dev) per session — a genuine measurement of the real pitch.
            var variations = ordered.Select(s => s.PitchVariation).ToList();
            var withVariation = variations.Where(v => v > 0).ToList();
            double avgVariation = withVariation.Count > 0 ? withVariation.Average() : 0;

            var seriesList = new List<AnalysisSeries>
            {
                new(Localized.Get("Analysis_PitchTrend", "Tonehøyde-trend"), Localized.Get("Analysis_PitchTrendDesc", "Snitt tonehøyde per lagret økt (eldst → nyest)."),
                    ordered.Select(s => PitchToPx(s.AveragePitch)).ToList(),
                    avgPitch > 0 ? string.Format(Localized.Get("Analysis_AvgPitchOverFormat", "Snitt {0} Hz over {1} økter"), avgPitch.ToString("F0"), ordered.Count) : Localized.Get("Analysis_NoVoiceRecorded", "Ingen stemme registrert")),
                new(Localized.Get("Analysis_ResonanceTrend", "Resonans-trend"), Localized.Get("Analysis_ResonanceTrendDesc", "Resonans-score per lagret økt (ekte, fra resonansmotoren)."),
                    resonances.Select(ScoreToPx).ToList(),
                    withResonance.Count > 0
                        ? string.Format(Localized.Get("Analysis_AvgResonanceOverFormat", "Snitt {0} / 100 over {1} økter med resonansdata"), avgResonance.ToString("F0"), withResonance.Count)
                        : Localized.Get("Analysis_NoResonanceData", "Ingen resonansdata ennå — fullfør en økt for å registrere resonans")),
                new(Localized.Get("Analysis_ProsodyTrend", "Tonevariasjon (prosodi)"), Localized.Get("Analysis_ProsodyTrendDesc", "Tonehøyde-variasjon (Hz std-avvik) per lagret økt — ekte måling."),
                    variations.Select(VariationToPx).ToList(),
                    withVariation.Count > 0
                        ? string.Format(Localized.Get("Analysis_AvgVariationOverFormat", "Snitt ± {0} Hz over {1} økter"), avgVariation.ToString("F0"), withVariation.Count)
                        : Localized.Get("Analysis_NoProsodyData", "Ingen prosodidata ennå — fullfør en økt for å måle tonevariasjon")),
                new(Localized.Get("Analysis_ScoreTrend", "Score-trend"), Localized.Get("Analysis_ScoreTrendDesc", "FemVoice-score per lagret økt (komfortsone-treff)."),
                    scores.Select(ScoreToPx).ToList(),
                    string.Format(Localized.Get("Analysis_AvgScoreBestFormat", "Snitt {0} · beste {1}"), avgScore.ToString("F0"), bestScore.ToString("F0"))),
            };
            series = seriesList;
            metrics = new List<AnalysisSummaryMetric>
            {
                new(Localized.Get("Analysis_SessionsAnalyzed", "Økter analysert"), ordered.Count.ToString()),
                new(Localized.Get("Dashboard_AveragePitch", "Snitt tonehøyde"), avgPitch > 0 ? $"{avgPitch:F0} Hz" : "—"),
                new(Localized.Get("Analysis_AvgResonanceLabel", "Snitt resonans"), withResonance.Count > 0 ? $"{avgResonance:F0} / 100" : Localized.Get("Analysis_NoDataYet", "— (ingen data ennå)")),
                new(Localized.Get("Analysis_AvgVariationLabel", "Snitt tonevariasjon"), withVariation.Count > 0 ? $"± {avgVariation:F0} Hz" : Localized.Get("Analysis_NoDataYet", "— (ingen data ennå)")),
                new(Localized.Get("Analysis_AvgScoreLabel", "Snitt score"), $"{avgScore:F0} / 100"),
                new(Localized.Get("Analysis_BestSession", "Beste økt"), $"{bestScore:F0} / 100"),
            };

            // Score-components (per-dimension) — WPF Analysis "rings". Prefer the REAL 7-dimension VoiceIntelligence
            // records the dashboard writes; fall back to the pitch/resonance TrainingSession approximation when none.
            components = BuildScoreComponents(database, avgPitch, avgResonance, withResonance.Count > 0);

            notice = Localized.Get("Analysis_RealDataNote", "Ekte analyse beregnet fra dine faktiske lagrede økter.");
            // Human-readable per-session summary (WPF Analysis "Økt-sammendrag") from the real aggregates.
            sessionSummary = avgPitch > 0
                ? string.Format(Localized.Get("Analysis_SummaryFormat", "{0} økter analysert · snitt tonehøyde {1} Hz · snitt score {2} · beste {3}."), ordered.Count, avgPitch.ToString("F0"), avgScore.ToString("F0"), bestScore.ToString("F0"))
                : string.Format(Localized.Get("Analysis_SummaryNoVoiceFormat", "{0} økter analysert · snitt score {1} · beste {2} (ingen stemme registrert)."), ordered.Count, avgScore.ToString("F0"), bestScore.ToString("F0"));
            return true;
        }
        catch { return false; }   // fall back to synthetic on any DB error
    }

    // Per-dimension score components. Real 7-dimension VoiceIntelligence records (written per session by the dashboard)
    // are preferred; each dimension with a value is averaged. Falls back to the pitch/resonance approximation when no
    // VI records exist yet. A dimension with no data shows "—" (no fabrication).
    private static IReadOnlyList<AnalysisSummaryMetric> BuildScoreComponents(
        IDatabaseService database, double avgPitch, double avgResonance, bool hasResonance)
    {
        try
        {
            if (database is FemVoiceStudio.Data.DatabaseService concrete)
            {
                var analytics = new FemVoiceStudio.Services.SessionAnalyticsStore(
                    new FemVoiceStudio.Services.SqliteSessionAnalyticsRepository(concrete.ConnectionString));
                var recs = analytics.GetSessionsAsync(DateTime.UtcNow.AddDays(-90), DateTime.UtcNow.AddDays(1), 1)
                    .GetAwaiter().GetResult();
                if (recs.Count > 0)
                {
                    string Avg(System.Func<FemVoiceStudio.Services.SessionAnalyticsRecord, double> sel)
                    {
                        var vals = recs.Select(sel).Where(v => v > 0).ToList();
                        return vals.Count > 0 ? $"{Math.Round(vals.Average())} / 100" : "—";
                    }
                    return new List<AnalysisSummaryMetric>
                    {
                        new(Localized.Get("Dashboard_Resonance", "Resonans"), Avg(r => r.ResonanceScore100)),
                        new(Localized.Get("Dashboard_Pitch", "Tonehøyde"), Avg(r => r.PitchScore100)),
                        new(Localized.Get("Dashboard_Intonation", "Intonasjon"), Avg(r => r.IntonationScore100)),
                        new(Localized.Get("Dashboard_VoiceHealth", "Stemmehelse"), Avg(r => r.AverageHealthScore)),
                        new(Localized.Get("Dashboard_Comfort", "Komfort"), Avg(r => r.ComfortScore100)),
                        new(Localized.Get("Dashboard_Recovery", "Restitusjon"), Avg(r => r.RecoveryScore100)),
                        new(Localized.Get("Dashboard_Consistency", "Jevnhet"), Avg(r => r.ConsistencyScore100)),
                    };
                }
            }
        }
        catch { /* fall through to the approximation */ }

        // Fallback (no VI records): the pitch/resonance the TrainingSession round-trip carries.
        return new List<AnalysisSummaryMetric>
        {
            new(Localized.Get("Dashboard_Pitch", "Tonehøyde"), avgPitch > 0 ? $"{avgPitch:F0} Hz" : "—"),
            new(Localized.Get("Dashboard_Resonance", "Resonans"), hasResonance ? $"{avgResonance:F0} / 100" : "—"),
        };
    }

    // Map a pitch (Hz) into the chart's px range (≈100–300 Hz → 4..ChartHeightPx), and a 0–100 score to px.
    private static double PitchToPx(double pitchHz)
    {
        if (pitchHz <= 0) return 4;
        double norm = (pitchHz - 100.0) / (300.0 - 100.0);
        return Math.Clamp(norm, 0, 1) * (ChartHeightPx - 4) + 4;
    }

    private static double ScoreToPx(double score) => Math.Clamp(score, 0, 100) / 100.0 * (ChartHeightPx - 4) + 4;

    // Map a pitch-variation (Hz std-dev, typically ~0–60 Hz) into the chart px range.
    private static double VariationToPx(double hz) => Math.Clamp(hz / 60.0, 0, 1) * (ChartHeightPx - 4) + 4;
}
