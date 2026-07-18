using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Data;
using FemVoiceStudio.Services;
using FemVoice.Avalonia.Localization;

namespace FemVoice.Avalonia.ViewModels;

/// <summary>
/// Engine-backed Progression page. Reads the REAL training level (UserSettings + <see cref="LevelClassificationSystem"/>),
/// the REAL recent-session score/pitch averages, and the REAL <see cref="ProgressionService"/> summary from the real
/// database — no demo data, the same sources WPF uses. Read-only; no clinical logic changed. Fails safe with no DB.
/// </summary>
public sealed class ProgressionViewModel
{
    public string Title => Localized.Get("Main_Progression", "Progresjon");

    public bool EngineAvailable { get; }
    public string UnavailableNote { get; } = "";

    public string LevelName { get; } = "—";
    public string LevelEmoji { get; } = "";
    public string LevelDescription { get; } = "";
    public string FemVoiceScoreText { get; } = "—";
    public double FemVoiceScore { get; }
    public int SessionCount { get; }
    public string SessionCountText { get; } = "—";
    public string AveragePitchText { get; } = "—";
    public string Summary { get; } = "";
    public string RecommendedDifficultyText { get; } = "";
    public string DataNote => Localized.Get("Progression_RealDataNote",
        "Nivå, poeng og sammendrag fra dine faktiske lagrede økter (ekte progresjonsmotor).");

    public ProgressionViewModel(IDatabaseService? database, ILocalizationService? localization = null)
    {
        if (database is null)
        {
            EngineAvailable = false;
            UnavailableNote = Localized.Get("Progression_NoDb",
                "Progresjon krever databasen, som ikke er tilgjengelig i denne visningen.");
            return;
        }
        try
        {
            var settings = database.GetUserSettings();
            var level = (TrainingLevel)settings.CurrentDifficulty;
            LevelName = LevelClassificationSystem.GetLevelName(level);
            LevelEmoji = LevelClassificationSystem.GetLevelEmoji(level);
            LevelDescription = LevelClassificationSystem.GetLevelFocus(level);

            IReadOnlyList<FemVoiceStudio.Models.TrainingSession> recent = database.GetRecentSessions(20);
            SessionCount = recent.Count;
            SessionCountText = $"{recent.Count} lagrede økter";
            if (recent.Count > 0)
            {
                FemVoiceScore = Math.Round(recent.Average(s => s.OverallScore));
                FemVoiceScoreText = $"{FemVoiceScore:F0}";
                AveragePitchText = $"{recent.Average(s => s.AveragePitch):F0} Hz";
            }

            var ps = new ProgressionService(database, localization ?? LocalizationService.Instance);
            Summary = ps.GetProgressionSummary();
            RecommendedDifficultyText = $"Anbefalt nivå: {ps.GetRecommendedDifficulty()}";

            EngineAvailable = true;
        }
        catch (Exception ex)
        {
            EngineAvailable = false;
            UnavailableNote = Localized.Get("Progression_Error", "Progresjon er midlertidig utilgjengelig.") + $" ({ex.GetType().Name})";
        }
    }
}
