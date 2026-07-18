using FemVoice.Avalonia.Localization;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FemVoiceStudio.Services;   // VoiceFeminizationExerciseService, EnhancedExercise

namespace FemVoice.Avalonia.ViewModels;

/// <summary>
/// Exercise Guide list. Reads the shared, UI-free catalog (VoiceFeminizationExerciseService — pure,
/// no DB, no WPF) and exposes read-only cards. Opening a card asks the shell to show the detail.
/// The exercise catalog itself is not modified.
///
/// WPF parity (ExerciseWindow + ExerciseListViewModel): the list has category-filter chips ("Alle" + categories)
/// and a name/description search; the two combine (matchesCategory AND matchesSearch), all over the in-memory
/// list. This is replicated here as a DISPLAY-ONLY filter over the loaded cards — no persistence, no analytics,
/// no DB, no session writes, no saved search. The chip axis uses the exercise Goal (the clean WPF category axis;
/// the catalog's freeform Category string is not the WPF filter axis).
/// </summary>
public partial class ExerciseGuideViewModel : ObservableObject
{
    private readonly Action<EnhancedExercise> _openDetail;
    private readonly List<ExerciseCardViewModel> _all;
    public const string AllCategory = "Alle";

    public ExerciseGuideViewModel(VoiceFeminizationExerciseService service, Action<EnhancedExercise> openDetail,
        FemVoiceStudio.Data.IDatabaseService? database = null)
    {
        _openDetail = openDetail;
        _all = service.GetAllEnhancedExercises()
            .Select(e => new ExerciseCardViewModel(e))
            .ToList();

        ComputeTodaysProgress(database);

        // Freeform catalog categories (unchanged; surfaced for diagnostics/back-compat).
        Categories = _all
            .Select(c => c.Category)
            .Distinct()
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // WPF-parity filter chips: "Alle" + the distinct exercise goals present (the clean WPF category axis,
        // e.g. Tonehøyde / Resonans / Intonasjon / Pust / Kombinert).
        var goals = _all.Select(c => c.GoalText)
            .Distinct()
            .OrderBy(g => g, StringComparer.OrdinalIgnoreCase);
        CategoryChips = new[] { AllCategory }.Concat(goals)
            .Select(label => new CategoryChipViewModel(label, string.Equals(label, AllCategory, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        FilteredExercises = new ObservableCollection<ExerciseCardViewModel>(_all);
    }

    /// <summary>Full, unfiltered catalog (used by navigation/smokes). Order/content unchanged.</summary>
    public IReadOnlyList<ExerciseCardViewModel> Exercises => _all;
    /// <summary>Distinct freeform catalog categories (diagnostics/back-compat — not the filter chip axis).</summary>
    public IReadOnlyList<string> Categories { get; }
    /// <summary>WPF-parity category-filter chips ("Alle" + exercise goals).</summary>
    public IReadOnlyList<CategoryChipViewModel> CategoryChips { get; }
    /// <summary>The currently visible (filtered) cards — what the list binds to.</summary>
    public ObservableCollection<ExerciseCardViewModel> FilteredExercises { get; }

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _selectedCategory = AllCategory;

    public int Count => _all.Count;
    public int FilteredCount => FilteredExercises.Count;
    public bool HasResults => FilteredExercises.Count > 0;
    public bool IsEmpty => FilteredExercises.Count == 0;
    // Heading uses the shared WPF keys (Exercise_Title + Exercise_Subtitle) with the live count appended.
    public string Heading => $"{Localized.Get("Exercise_Title", "Øvelsesguide")} — {Count} {Localized.Get("Exercise_ItemsSuffix", "øvelser")}";
    public string Subtitle => Localized.Get("Exercise_Subtitle", "steg for steg");
    public string SearchPlaceholder => "Søk i øvelser …";
    public string EmptyText => "Ingen øvelser matcher søket.";

    // WPF parity: the list has a "today's progress" summary (minutes + session count) — now REAL, read from the
    // database (the sessions completed today), or a neutral placeholder when no DB is present (headless/tests).
    [ObservableProperty] private string _todaysProgressText = "— · 0 økter";
    [ObservableProperty] private string _progressNote =
        "Fullførte øvelser lagres og teller mot progresjonen din.";

    /// <summary>Real today's minutes + completed-session count from the database (local day). Null-safe: with no DB
    /// the note stays neutral and the counts show a placeholder. Never throws.</summary>
    private void ComputeTodaysProgress(FemVoiceStudio.Data.IDatabaseService? database)
    {
        if (database is null)
        {
            TodaysProgressText = "— · 0 økter";
            ProgressNote = "Fullførte øvelser lagres og teller mot progresjonen din.";
            return;
        }
        try
        {
            var today = DateTime.Now.Date;
            var todays = database.GetRecentSessions(1000)
                .Where(s => s.StartTime.ToLocalTime().Date == today)
                .ToList();
            int minutes = (int)Math.Round(todays.Sum(s => Math.Max(0, ((s.EndTime ?? s.StartTime) - s.StartTime).TotalMinutes)));
            TodaysProgressText = $"{minutes} min · {todays.Count} økter";
            ProgressNote = todays.Count > 0
                ? "Dagens fullførte øvelser er lagret og teller mot progresjonen din."
                : "Fullførte øvelser lagres og teller mot progresjonen din.";
        }
        catch
        {
            TodaysProgressText = "— · 0 økter";
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedCategoryChanged(string value)
    {
        foreach (var chip in CategoryChips)
            chip.IsSelected = string.Equals(chip.Label, value, StringComparison.OrdinalIgnoreCase);
        ApplyFilter();
    }

    // Display-only filter over the in-memory cards — mirrors WPF FilterExercises (matchesCategory && matchesSearch).
    // Category "Alle" matches everything; otherwise the chip label is compared to the card's goal. Search matches
    // the name OR the description, case-insensitive (exactly WPF's Name/Description contains). No DB, no analytics.
    private void ApplyFilter()
    {
        var search = (SearchText ?? string.Empty).Trim();
        bool allCategories = string.Equals(SelectedCategory, AllCategory, StringComparison.OrdinalIgnoreCase);

        FilteredExercises.Clear();
        foreach (var card in _all)
        {
            bool matchesCategory = allCategories
                || string.Equals(card.GoalText, SelectedCategory, StringComparison.OrdinalIgnoreCase);
            bool matchesSearch = search.Length == 0
                || card.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || card.ShortDescription.Contains(search, StringComparison.OrdinalIgnoreCase);
            if (matchesCategory && matchesSearch)
                FilteredExercises.Add(card);
        }

        OnPropertyChanged(nameof(FilteredCount));
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private void SelectCategory(string? category)
    {
        if (!string.IsNullOrEmpty(category))
            SelectedCategory = category;
    }

    [RelayCommand]
    private void OpenExercise(ExerciseCardViewModel? card)
    {
        if (card is not null) _openDetail(card.Exercise);
    }
}
