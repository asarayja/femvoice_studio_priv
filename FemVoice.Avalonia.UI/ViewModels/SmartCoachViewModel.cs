using System;
using FemVoiceStudio.Data;
using FemVoiceStudio.Services;
using FemVoice.Avalonia.Localization;

namespace FemVoice.Avalonia.ViewModels;

/// <summary>
/// Engine-backed SmartCoach page. Runs the REAL Core <see cref="SmartCoachEngine"/> (read-only) on the REAL database
/// to produce the daily recommendation, weekly session target, and status summary — the exact WPF logic, presented
/// in Avalonia. No clinical logic is changed. Fails safe: with no database (headless/tests) or an engine error it
/// shows a truthful "unavailable" state instead of throwing.
/// </summary>
public sealed class SmartCoachViewModel
{
    public string Title => Localized.Get("SmartCoach_Scaffold_Title", "SmartCoach");

    public bool EngineAvailable { get; }
    public string UnavailableNote { get; } = "";

    public string FocusLabel { get; } = "—";
    public string RecommendationText { get; } = "";
    public string DurationText { get; } = "";
    public string WeeklyTargetText { get; } = "";
    public string StatusSummary { get; } = "";
    public bool HasHealthWarning { get; }
    public string HealthWarningText { get; } = "";
    public string DataNote => Localized.Get("SmartCoach_RealDataNote",
        "Beregnet av den ekte SmartCoach-motoren på dine lagrede økter.");

    public SmartCoachViewModel(IDatabaseService? database, ILocalizationService? localization = null)
    {
        if (database is null)
        {
            EngineAvailable = false;
            UnavailableNote = Localized.Get("SmartCoach_NoDb",
                "SmartCoach krever databasen, som ikke er tilgjengelig i denne visningen.");
            return;
        }
        try
        {
            var engine = new SmartCoachEngine(database, localization ?? LocalizationService.Instance);
            var rec = engine.GenerateDailyRecommendation(1);
            FocusLabel = FocusText(rec.FocusArea);
            RecommendationText = rec.RecommendationText;
            DurationText = $"{rec.RecommendedDurationMinutes} min";
            HasHealthWarning = rec.HealthWarning;
            HealthWarningText = rec.HealthWarningText ?? "";
            WeeklyTargetText = $"{engine.GetWeeklySessionTarget(1)} økter/uke (mål)";
            StatusSummary = engine.GetStatusSummary(1);
            EngineAvailable = true;
        }
        catch (Exception ex)
        {
            EngineAvailable = false;
            UnavailableNote = Localized.Get("SmartCoach_Error", "SmartCoach er midlertidig utilgjengelig.") + $" ({ex.GetType().Name})";
        }
    }

    private static string FocusText(string? focusArea) => (focusArea ?? "").ToLowerInvariant() switch
    {
        "resonance" => "Resonans",
        "pitch" => "Tonehøyde",
        "intonation" => "Intonasjon",
        "breathing" => "Pust",
        _ => string.IsNullOrWhiteSpace(focusArea) ? "—" : focusArea!,
    };
}
