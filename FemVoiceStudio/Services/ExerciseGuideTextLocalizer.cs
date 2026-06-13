using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Applies localized, user-facing copy to exercise guide cards.
    /// </summary>
    public sealed class ExerciseGuideTextLocalizer
    {
        private readonly ExerciseTextService _exerciseTextService;
        private readonly ILocalizationService _localization;

        public ExerciseGuideTextLocalizer(
            ExerciseTextService? exerciseTextService = null,
            ILocalizationService? localization = null)
        {
            _localization = localization ?? LocalizationService.Instance;
            _exerciseTextService = exerciseTextService ?? new ExerciseTextService(_localization);
        }

        public void Apply(Exercise exercise)
        {
            ArgumentNullException.ThrowIfNull(exercise);

            var localizationKey = exercise.SortOrder + 100;

            var titleKey = $"Exercise_{localizationKey}_Title";
            var localizedName = _exerciseTextService.GetLocalizedTitle(localizationKey);
            if (HasLocalizedValue(localizedName, titleKey))
                exercise.Name = localizedName;

            var descriptionKey = $"Exercise_{localizationKey}_Description";
            var localizedDescription = _exerciseTextService.GetLocalizedDescription(localizationKey);
            if (HasLocalizedValue(localizedDescription, descriptionKey))
                exercise.Description = localizedDescription;

            exercise.DisplayDifficulty = exercise.DifficultyLevel switch
            {
                DifficultyLevel.Nybegynner => _localization["Difficulty_Beginner"],
                DifficultyLevel.Middels => _localization["Difficulty_Intermediate"],
                DifficultyLevel.Avansert => _localization["Difficulty_Advanced"],
                _ => _localization["Difficulty_Beginner"]
            };

            exercise.FrequencyText = exercise.Frequency switch
            {
                FrequencyType.Daglig => _localization["Frequency_Daily"],
                FrequencyType.TreGangerUkentlig => _localization["Frequency_3xWeek"],
                FrequencyType.ToGangerUkentlig => _localization["Frequency_2xWeek"],
                FrequencyType.Ukentlig => _localization["Frequency_Weekly"],
                _ => exercise.FrequencyText
            };

            var stepsKey = $"Exercise_{localizationKey}_Steps";
            var localizedSteps = _exerciseTextService.GetLocalizedSteps(localizationKey);
            exercise.DisplaySteps = HasLocalizedValue(localizedSteps, stepsKey)
                ? SplitSteps(localizedSteps)
                : exercise.GetStepsList();
        }

        private static bool HasLocalizedValue(string? value, string key)
            => !string.IsNullOrWhiteSpace(value)
               && !string.Equals(value, key, StringComparison.Ordinal);

        private static List<string> SplitSteps(string steps)
            => steps.Split('|')
                .Where(step => !string.IsNullOrWhiteSpace(step))
                .ToList();
    }
}
