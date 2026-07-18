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
    public string Title { get; }
    public string SampleDataNotice { get; }

    /// <summary>True when the charts/metrics are computed from real saved sessions (vs synthetic sample data).</summary>
    public bool HasRealData { get; }

    /// <summary>Always <c>true</c>: this page has no interactive analysis actions wired (import/export deferred).</summary>
    public bool AllActionsDeferred => true;

    public AnalysisViewModel() : this(null) { }

    public AnalysisViewModel(IDatabaseService? database)
    {
        Title = Localized.Get("Analysis_ScaffoldTitle", "Analyse / resonans");

        if (database is not null && TryBuildFromDatabase(database, out var series, out var metrics, out var notice))
        {
            Series = series;
            SummaryMetrics = metrics;
            SampleDataNotice = notice;
            HasRealData = true;
            return;
        }

        // Fallback: deterministic synthetic sample series (no random, no real audio) — purely illustrative.
        Series = new List<AnalysisSeries>
        {
            new(Localized.Get("Analysis_PitchTrend", "Tonehøyde-trend"),
                Localized.Get("Analysis_PitchTrendDesc", "Eksempel på tonehøyde over tid (syntetisk)."),
                Wave(42, 70, 0.55, 0.0), Localized.Get("Analysis_PitchTrendSummary", "Snitt ~165 Hz · eksempeldata")),
            new(Localized.Get("Analysis_Resonance", "Resonans"),
                Localized.Get("Analysis_ResonanceDesc", "Eksempel på resonans-indikator (syntetisk)."),
                Wave(30, 75, 0.32, 1.1), Localized.Get("Analysis_ResonanceSummary", "Middels framre resonans · eksempeldata")),
            new(Localized.Get("Analysis_Stability", "Stabilitet"),
                Localized.Get("Analysis_StabilityDesc", "Eksempel på stabilitet over tid (syntetisk)."),
                Wave(22, 88, 0.9, 0.4), Localized.Get("Analysis_StabilitySummary", "Stort sett stabil · eksempeldata")),
            new(Localized.Get("Analysis_Formant", "Formant / resonans-plassholder"),
                Localized.Get("Analysis_FormantDesc", "Plassholder for formant-/resonansvisning (syntetisk)."),
                Wave(35, 60, 0.22, 2.0), Localized.Get("Analysis_FormantSummary", "F1/F2-visning kommer senere · eksempeldata")),
        };
        SummaryMetrics = new List<AnalysisSummaryMetric>
        {
            new(Localized.Get("Analysis_Metric_AvgPitch", "Snitt tonehøyde"), "≈ 165 Hz (eksempel)"),
            new(Localized.Get("Analysis_Metric_Resonance", "Resonans"), Localized.Get("Analysis_Metric_ResonanceVal", "Middels (eksempel)")),
            new(Localized.Get("Analysis_Metric_Stability", "Stabilitet"), Localized.Get("Analysis_Metric_StabilityVal", "God (eksempel)")),
            new(Localized.Get("Analysis_Metric_Sessions", "Økter analysert"), Localized.Get("Analysis_Metric_SessionsVal", "— (ingen lagring)")),
        };
        SampleDataNotice = Localized.Get("Analysis_ScaffoldNotice",
            "Visning-bare analyse: alle grafer og tall er SYNTETISKE eksempeldata. Ekte analyse vises når du har lagrede økter.");
    }

    private static bool TryBuildFromDatabase(IDatabaseService database,
        out IReadOnlyList<AnalysisSeries> series, out IReadOnlyList<AnalysisSummaryMetric> metrics, out string notice)
    {
        series = Array.Empty<AnalysisSeries>();
        metrics = Array.Empty<AnalysisSummaryMetric>();
        notice = "";
        try
        {
            // GetRecentSessions is newest-first; reverse to oldest→newest for a left-to-right trend.
            var ordered = database.GetRecentSessions(30).AsEnumerable().Reverse().ToList();
            if (ordered.Count == 0)
            {
                metrics = new List<AnalysisSummaryMetric> { new("Økter analysert", "0 — ingen lagrede økter ennå") };
                notice = "Ingen lagrede økter ennå. Fullfør en økt på dashbordet, så vises ekte analyse her.";
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
                new("Tonehøyde-trend", "Snitt tonehøyde per lagret økt (eldst → nyest).",
                    ordered.Select(s => PitchToPx(s.AveragePitch)).ToList(),
                    avgPitch > 0 ? $"Snitt {avgPitch:F0} Hz over {ordered.Count} økter" : "Ingen stemme registrert"),
                new("Resonans-trend", "Resonans-score per lagret økt (ekte, fra resonansmotoren).",
                    resonances.Select(ScoreToPx).ToList(),
                    withResonance.Count > 0
                        ? $"Snitt {avgResonance:F0} / 100 over {withResonance.Count} økter med resonansdata"
                        : "Ingen resonansdata ennå — fullfør en økt for å registrere resonans"),
                new("Tonevariasjon (prosodi)", "Tonehøyde-variasjon (Hz std-avvik) per lagret økt — ekte måling.",
                    variations.Select(VariationToPx).ToList(),
                    withVariation.Count > 0
                        ? $"Snitt ± {avgVariation:F0} Hz over {withVariation.Count} økter"
                        : "Ingen prosodidata ennå — fullfør en økt for å måle tonevariasjon"),
                new("Score-trend", "FemVoice-score per lagret økt (komfortsone-treff).",
                    scores.Select(ScoreToPx).ToList(),
                    $"Snitt {avgScore:F0} · beste {bestScore:F0}"),
            };
            series = seriesList;
            metrics = new List<AnalysisSummaryMetric>
            {
                new("Økter analysert", ordered.Count.ToString()),
                new("Snitt tonehøyde", avgPitch > 0 ? $"{avgPitch:F0} Hz" : "—"),
                new("Snitt resonans", withResonance.Count > 0 ? $"{avgResonance:F0} / 100" : "— (ingen data ennå)"),
                new("Snitt tonevariasjon", withVariation.Count > 0 ? $"± {avgVariation:F0} Hz" : "— (ingen data ennå)"),
                new("Snitt score", $"{avgScore:F0} / 100"),
                new("Beste økt", $"{bestScore:F0} / 100"),
            };
            notice = "Ekte analyse beregnet fra dine faktiske lagrede økter.";
            return true;
        }
        catch { return false; }   // fall back to synthetic on any DB error
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

    // Deterministic sine-shaped bar heights in px within [4, ChartHeightPx] — synthetic, no random, no audio.
    private static IReadOnlyList<double> Wave(double amplitude, double mid, double freq, double phase)
    {
        const int count = 32;
        var bars = new double[count];
        for (int i = 0; i < count; i++)
        {
            double v = mid + amplitude * Math.Sin(i * freq + phase);
            bars[i] = Math.Clamp(v, 4, ChartHeightPx);
        }
        return bars;
    }
}
