using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using FemVoiceStudio.Data;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;

namespace FemVoiceStudio.ViewModels
{
    /// <summary>
    /// ViewModel for øvelseslisten - viser alle tilgjengelige øvelser
    /// </summary>
    public class ExerciseListViewModel
    {
        private readonly ExerciseDataService _exerciseService;
        
        public ObservableCollection<Exercise> Exercises { get; } = new();
        public ObservableCollection<Exercise> FilteredExercises { get; } = new();
        
        private Exercise? _selectedExercise;
        public Exercise? SelectedExercise
        {
            get => _selectedExercise;
            set
            {
                _selectedExercise = value;
                SelectExerciseCommand.Execute(value);
            }
        }
        
        private string _selectedCategory = LocalizationService.Instance["Exercise_All"];
        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                _selectedCategory = value;
                FilterExercises();
            }
        }
        
        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                FilterExercises();
            }
        }
        
        private int _totalExercisesToday;
        public int TotalExercisesToday
        {
            get => _totalExercisesToday;
            set { _totalExercisesToday = value; OnPropertyChanged(nameof(TotalExercisesToday)); }
        }
        
        private int _totalMinutesToday;
        public int TotalMinutesToday
        {
            get => _totalMinutesToday;
            set { _totalMinutesToday = value; OnPropertyChanged(nameof(TotalMinutesToday)); }
        }
        
        public string[] Categories { get; } =
        {
            LocalizationService.Instance["Exercise_All"],
            LocalizationService.Instance["Exercise_Pitch"],
            LocalizationService.Instance["Exercise_Resonance"],
            LocalizationService.Instance["Exercise_Intonation"],
            LocalizationService.Instance["Exercise_Breathing"],
            LocalizationService.Instance["Exercise_Practice"]
        };
        
        public ICommand RefreshCommand           { get; }
        public ICommand SelectExerciseCommand    { get; }
        public ICommand FilterByCategoryCommand  { get; }
        
        public event Action<Exercise>? NavigateToExerciseDetail;
        
        public ExerciseListViewModel(ExerciseDataService exerciseService)
        {
            _exerciseService = exerciseService;
            
            RefreshCommand = new RelayCommand(_ => LoadExercises());
            SelectExerciseCommand = new RelayCommand(obj =>
            {
                if (obj is Exercise exercise)
                    NavigateToExerciseDetail?.Invoke(exercise);
            });
            FilterByCategoryCommand = new RelayCommand(obj =>
            {
                if (obj is string category)
                    SelectedCategory = category;
            });
            
            LoadExercises();
        }
        
        public void LoadExercises()
        {
            var exercises = _exerciseService.GetAllExercises();
            Exercises.Clear();
            foreach (var ex in exercises)
                Exercises.Add(ex);
            FilterExercises();
            LoadTodaysStats();
        }
        
        private void LoadTodaysStats()
        {
            TotalExercisesToday = _exerciseService.GetCompletedSessionsToday();
            TotalMinutesToday   = _exerciseService.GetTotalMinutesToday();
        }
        
        private void FilterExercises()
        {
            FilteredExercises.Clear();
            foreach (var ex in Exercises)
            {
                bool matchesCategory = IsAllCategory(SelectedCategory) || CategoryMatches(ex.Category, SelectedCategory);
                bool matchesSearch = string.IsNullOrEmpty(SearchText) ||
                    ex.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    ex.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
                if (matchesCategory && matchesSearch)
                    FilteredExercises.Add(ex);
            }
        }
        
        public void GetTodaysRecommendations()
        {
            var recommended = _exerciseService.GetTodaysRecommendedExercises();
            foreach (var ex in recommended)
                ex.IsCompletedToday = _exerciseService.IsExerciseCompletedToday(ex.ExerciseId);
        }
        
        protected void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));

        private static bool IsAllCategory(string category)
            => category.Equals(LocalizationService.Instance["Exercise_All"], StringComparison.OrdinalIgnoreCase);

        private static bool CategoryMatches(string exerciseCategory, string selectedCategory)
        {
            if (exerciseCategory.Equals(selectedCategory, StringComparison.OrdinalIgnoreCase))
                return true;

            var normalized = exerciseCategory.ToLowerInvariant();
            return normalized switch
            {
                "pitch" => selectedCategory.Equals(LocalizationService.Instance["Exercise_Pitch"], StringComparison.OrdinalIgnoreCase),
                "resonans" => selectedCategory.Equals(LocalizationService.Instance["Exercise_Resonance"], StringComparison.OrdinalIgnoreCase),
                "intonasjon" => selectedCategory.Equals(LocalizationService.Instance["Exercise_Intonation"], StringComparison.OrdinalIgnoreCase),
                "pust" => selectedCategory.Equals(LocalizationService.Instance["Exercise_Breathing"], StringComparison.OrdinalIgnoreCase),
                "praksis" => selectedCategory.Equals(LocalizationService.Instance["Exercise_Practice"], StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }
        
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
    
    // `RelayCommand` is provided centrally in `ViewModels/RelayCommand.cs`.
}
