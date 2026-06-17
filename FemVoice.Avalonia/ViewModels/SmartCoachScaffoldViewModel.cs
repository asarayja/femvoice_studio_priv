using FemVoice.Avalonia.Localization;   // Localized (safe read-only localization resolver)

namespace FemVoice.Avalonia.ViewModels;

/// <summary>
/// DEFERRED, display-only SmartCoach scaffold. Mirrors the VISUAL structure of the WPF
/// <c>SmartCoachDashboardView</c> (today's-focus card + streak/sessions/health stat tiles) with clearly
/// SYNTHETIC placeholder values ("—") and a disabled action — it is NOT a functional SmartCoach.
///
/// Holds NO services and NO state; is NOT IDisposable; performs NO side effects. It does NOT reference or
/// instantiate SmartCoachEngine, no scoring, no safety gate, no analytics/DB/persistence, no microphone, and
/// computes NO recommendations. All values are static placeholders; navigating here starts/saves nothing.
/// </summary>
public sealed class SmartCoachScaffoldViewModel
{
    public string SurfaceName => "SmartCoach";

    // Use the product name "SmartCoach" (one word) consistently — the Core key resolves to "Smart Coach" (two
    // words), which is inconsistent with the nav label; a scaffold key keeps it deterministic.
    public string Title => Localized.Get("SmartCoach_Scaffold_Title", "SmartCoach");
    public string DeferredBadge => Localized.Get("Scaffold_DeferredBadge", "Utsatt · kun visning");
    public string Intro => Localized.Get("SmartCoach_Scaffold_Intro",
        "SmartCoach er ikke aktiv i denne visningen. Layouten under viser den planlagte strukturen med "
        + "syntetiske plassholdere — ingen ekte anbefalinger, ingen klinisk vurdering.");

    // Today's focus card (WPF: TodayFocus chip + TodayRecommendation) — synthetic placeholders.
    public string TodayFocusLabel => Localized.Get("SmartCoach_TodayFocus", "Dagens fokus");
    public string TodayFocusValue => Localized.Get("Scaffold_Pending", "Utsatt");
    public string TodayRecommendation => Localized.Get("SmartCoach_Scaffold_Recommendation",
        "Ingen anbefaling vises — SmartCoach beregner ingenting i denne forhåndsvisningen (syntetisk, ingen klinisk endring).");

    // Stat tiles (WPF: CurrentStreak / SessionsThisWeek / HealthScore) — placeholder "—", no real numbers.
    public string Placeholder => "—";
    // Scaffold keys keep the three tile labels consistent (the Core SmartCoach_Sessions/_Health keys resolve to
    // the terse lowercase "økter"/"helse", which clashes with "Dager på rad").
    public string StreakLabel => Localized.Get("SmartCoach_Scaffold_StreakLabel", "Dager på rad");
    public string SessionsLabel => Localized.Get("SmartCoach_Scaffold_SessionsLabel", "Økter denne uken");
    public string HealthLabel => Localized.Get("SmartCoach_Scaffold_HealthLabel", "Helsescore");
    public string SyntheticNote => Localized.Get("Scaffold_Synthetic", "Syntetisk · ingen lagring");

    // Read-only safety note (WPF shows a health warning banner; here it is a static, non-clinical note).
    public string SafetyNote => Localized.Get("SmartCoach_Scaffold_SafetyNote",
        "Kun visning · ingen lagring · ingen mikrofon · ingen klinisk vurdering eller sikkerhetslogikk.");

    // Disabled action — WPF would start a coached session; deferred here.
    public string ActionLabel => Localized.Get("Scaffold_ComingSoon", "Kommer senere");
    public bool ActionEnabled => false;

    public string Footnote => Localized.Get("Shell_DeferredFootnote",
        "Denne flaten utfører ingenting og endrer ingen klinisk/lagret tilstand.");
}
