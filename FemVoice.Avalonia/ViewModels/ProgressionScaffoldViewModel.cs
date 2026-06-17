using System.Collections.Generic;
using FemVoice.Avalonia.Localization;   // Localized (safe read-only localization resolver)

namespace FemVoice.Avalonia.ViewModels;

/// <summary>One read-only progression-parameter row (label + synthetic placeholder value). Display-only.</summary>
public sealed class ScaffoldMetricRow
{
    public ScaffoldMetricRow(string label, string value) { Label = label; Value = value; }
    public string Label { get; }
    public string Value { get; }
}

/// <summary>
/// DEFERRED, display-only Progression scaffold. Mirrors the VISUAL structure of the WPF
/// <c>ProgressionDashboard</c> (level badge + progress bar + FemVoice-score tile + Resonance/Pitch/Intonation
/// parameter rows) with clearly SYNTHETIC placeholder values ("—") and a disabled action — it is NOT a
/// functional progression view.
///
/// Holds NO services and NO state; is NOT IDisposable; performs NO side effects. It does NOT reference or
/// instantiate ProgressionSafetyGate, no progression calculation, no scoring, no safety gate, no
/// analytics/DB/persistence. All values are static placeholders; navigating here calculates/saves nothing.
/// </summary>
public sealed class ProgressionScaffoldViewModel
{
    public string SurfaceName => "Progresjon";

    public string Title => Localized.Get("Shell_Nav_Progresjon", "Progresjon");
    public string DeferredBadge => Localized.Get("Scaffold_DeferredBadge", "Utsatt · kun visning");
    public string Intro => Localized.Get("Progression_Scaffold_Intro",
        "Progresjon er ikke aktiv i denne visningen. Layouten under viser den planlagte strukturen med "
        + "syntetiske plassholdere — ingen ekte nivåberegning, ingen sikkerhetsport, ingen lagring.");

    public string Placeholder => "—";

    // Level badge + progress (WPF: LevelName / LevelDescription / ProgressBarWidth / ProgressText) — synthetic.
    public string LevelBadge => "—";
    public string LevelName => Localized.Get("Progression_Scaffold_LevelName", "Nivå — (utsatt)");
    public string LevelDescription => Localized.Get("Progression_Scaffold_LevelDescription",
        "Nivå og fremgang beregnes ikke i denne forhåndsvisningen.");
    public string ProgressText => Localized.Get("Scaffold_ComingSoon", "Kommer senere");
    public double ProgressValue => 0;   // disabled/empty bar — no real progression

    // FemVoice-score tile (WPF: FemVoiceScore) — placeholder, no real score.
    public string ScoreValue => "—";
    public string ScoreLabel => Localized.Get("Dashboard_Score", "FemVoice-score");
    public string SyntheticNote => Localized.Get("Scaffold_Synthetic", "Syntetisk · ingen lagring");

    // Parameter rows (WPF: Resonance / Pitch / Intonation progress bars) — synthetic "—" values.
    public IReadOnlyList<ScaffoldMetricRow> Parameters { get; } = new List<ScaffoldMetricRow>
    {
        new(Localized.Get("Dashboard_Resonance", "Resonans"), "—"),
        new(Localized.Get("Dashboard_Pitch", "Tonehøyde"), "—"),
        new(Localized.Get("Dashboard_Intonation", "Intonasjon"), "—"),
    };

    public string SafetyNote => Localized.Get("Progression_Scaffold_SafetyNote",
        "Kun visning · ingen lagring · ingen klinisk endring · ingen sikkerhetsport eller progresjonslogikk kalt.");

    // Disabled action — WPF would open the full progression view; deferred here.
    public string ActionLabel => Localized.Get("Scaffold_ComingSoon", "Kommer senere");
    public bool ActionEnabled => false;

    public string Footnote => Localized.Get("Shell_DeferredFootnote",
        "Denne flaten utfører ingenting og endrer ingen klinisk/lagret tilstand.");
}
