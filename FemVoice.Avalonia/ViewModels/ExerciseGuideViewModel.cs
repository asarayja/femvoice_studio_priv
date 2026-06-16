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

    [RelayCommand]
    private void OpenExercise(ExerciseCardViewModel? card)
    {
        if (card is not null) _openDetail(card.Exercise);
    }
}
