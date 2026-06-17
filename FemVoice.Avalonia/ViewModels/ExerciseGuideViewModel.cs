using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FemVoiceStudio.Services;   // VoiceFeminizationExerciseService, EnhancedExercise

namespace FemVoice.Avalonia.ViewModels;

/// <summary>
/// Exercise Guide list. Reads the shared, UI-free catalog (VoiceFeminizationExerciseService — pure,
/// no DB, no WPF) and exposes read-only cards. Opening a card asks the shell to show the detail.
/// The exercise catalog itself is not modified.
/// </summary>
public partial class ExerciseGuideViewModel : ObservableObject
{
    private readonly Action<EnhancedExercise> _openDetail;

    public ExerciseGuideViewModel(VoiceFeminizationExerciseService service, Action<EnhancedExercise> openDetail)
    {
        _openDetail = openDetail;
        Exercises = service.GetAllEnhancedExercises()
            .Select(e => new ExerciseCardViewModel(e))
            .ToList();
        Categories = Exercises
            .Select(c => c.Category)
            .Distinct()
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<ExerciseCardViewModel> Exercises { get; }
    public IReadOnlyList<string> Categories { get; }
    public int Count => Exercises.Count;
    public string Heading => $"Øvelsesguide — {Count} øvelser";

    // WPF parity: the list has a "today's progress" summary (minutes + session count). This preview has NO session
    // persistence, so these are a clearly-labelled display-only placeholder (0) — no analytics/DB read, no invented
    // numbers. The ProgressNote states the preview does not track progress.
    public string TodaysProgressText => "0 min · 0 økter";
    public string ProgressNote => "Visning — fremgang lagres ikke i denne forhåndsvisningen (ingen lagring).";

    [RelayCommand]
    private void OpenExercise(ExerciseCardViewModel? card)
    {
        if (card is not null) _openDetail(card.Exercise);
    }
}
