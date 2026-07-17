using System;
using System.Collections.Generic;
using System.Linq;
using FemVoice.Avalonia.Localization;   // Localized (safe read-only localization resolver)

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

/// <summary>One display-only summary metric row (static placeholder; sample data).</summary>
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
/// DISPLAY-ONLY Analysis / Resonance scaffold. A purely static page: it holds NO services, NO commands,
/// is NOT IDisposable, starts no timers/subscriptions/capture/background work, and reads/writes NOTHING
/// (no database, no session history, no report export). It renders converter-free mini-charts from
/// SYNTHETIC, in-memory, deterministic sample data and static summary placeholders. No third-party charting, no clinical
/// scoring, no SmartCoach/progression, no Voice-Health/recovery. Labels resolve through the safe read-only
/// localization adapter (namespaced Analysis_* keys with Norwegian fallback).
/// </summary>
public sealed class AnalysisViewModel
{
    /// <summary>Fixed mini-chart height in px (bars are 0..this); the view binds Canvas/bar heights to it.</summary>
    public const double ChartHeightPx = 120;

    public AnalysisViewModel()
    {
        // Deterministic synthetic sample series (no random, no real audio) — purely illustrative.
        Series = new List<AnalysisSeries>
        {
            new(Localized.Get("Analysis_PitchTrend", "Tonehøyde-trend"),
                Localized.Get("Analysis_PitchTrendDesc", "Eksempel på tonehøyde over tid (syntetisk)."),
                Wave(amplitude: 42, mid: 70, freq: 0.55, phase: 0.0),
                Localized.Get("Analysis_PitchTrendSummary", "Snitt ~165 Hz · eksempeldata")),

            new(Localized.Get("Analysis_Resonance", "Resonans"),
                Localized.Get("Analysis_ResonanceDesc", "Eksempel på resonans-indikator (syntetisk)."),
                Wave(amplitude: 30, mid: 75, freq: 0.32, phase: 1.1),
                Localized.Get("Analysis_ResonanceSummary", "Middels framre resonans · eksempeldata")),

            new(Localized.Get("Analysis_Stability", "Stabilitet"),
                Localized.Get("Analysis_StabilityDesc", "Eksempel på stabilitet over tid (syntetisk)."),
                Wave(amplitude: 22, mid: 88, freq: 0.9, phase: 0.4),
                Localized.Get("Analysis_StabilitySummary", "Stort sett stabil · eksempeldata")),

            new(Localized.Get("Analysis_Formant", "Formant / resonans-plassholder"),
                Localized.Get("Analysis_FormantDesc", "Plassholder for formant-/resonansvisning (syntetisk)."),
                Wave(amplitude: 35, mid: 60, freq: 0.22, phase: 2.0),
                Localized.Get("Analysis_FormantSummary", "F1/F2-visning kommer senere · eksempeldata")),
        };

        SummaryMetrics = new List<AnalysisSummaryMetric>
        {
            new(Localized.Get("Analysis_Metric_AvgPitch", "Snitt tonehøyde"), "≈ 165 Hz (eksempel)"),
            new(Localized.Get("Analysis_Metric_Resonance", "Resonans"), Localized.Get("Analysis_Metric_ResonanceVal", "Middels (eksempel)")),
            new(Localized.Get("Analysis_Metric_Stability", "Stabilitet"), Localized.Get("Analysis_Metric_StabilityVal", "God (eksempel)")),
            new(Localized.Get("Analysis_Metric_Sessions", "Økter analysert"), Localized.Get("Analysis_Metric_SessionsVal", "— (ingen lagring)")),
        };

        // Use a scaffold-specific key (NOT the pre-existing clinical "Analysis_Title" RESX key, whose value
        // is the WPF window title "Analyse - FemVoice Studio") so the page header shows the intended text.
        Title = Localized.Get("Analysis_ScaffoldTitle", "Analyse / resonans");
        SampleDataNotice = Localized.Get("Analysis_ScaffoldNotice",
            "Visning-bare analyse: alle grafer og tall er SYNTETISKE eksempeldata. Ingenting leses fra eller " +
            "lagres i historikk/database, ingen rapporteksport, ingen klinisk scoring — ekte analyse kommer senere.");
    }

    public IReadOnlyList<AnalysisSeries> Series { get; }
    public IReadOnlyList<AnalysisSummaryMetric> SummaryMetrics { get; }
    public string Title { get; }
    public string SampleDataNotice { get; }

    /// <summary>Always <c>true</c>: this scaffold has no interactive analysis actions wired.</summary>
    public bool AllActionsDeferred => true;

    // Deterministic sine-shaped bar heights in px within [4, ChartHeightPx] — synthetic, no random, no audio.
    private static IReadOnlyList<double> Wave(double amplitude, double mid, double freq, double phase)
    {
        const int count = 32;
        var bars = new double[count];
        for (int i = 0; i < count; i++)
        {
            double v = mid + amplitude * Math.Sin(i * freq + phase);
            if (v < 4) v = 4;
            else if (v > ChartHeightPx) v = ChartHeightPx;
            bars[i] = Math.Round(v, 1);
        }
        return bars;
    }
}
