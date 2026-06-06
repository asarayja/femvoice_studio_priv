using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Data;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Service for å beregne treningsfrekvens og anbefalinger
    /// </summary>
    public class TrainingFrequencyService
    {
        private readonly ExerciseDataService _exerciseService;
        
        // Anbefalt daglig trening
        private const int RecommendedDailyMinutes = 25;
        private const int MaxDailyMinutes = 30;
        
        public TrainingFrequencyService(ExerciseDataService exerciseService)
        {
            _exerciseService = exerciseService;
        }
        
        /// <summary>
        /// Hent anbefalte øvelser for i dag
        /// </summary>
        public List<TrainingRecommendation> GetTodaysRecommendations()
        {
            var recommendations = new List<TrainingRecommendation>();
            var allExercises = _exerciseService.GetAllExercises();
            var today = DateTime.Today;
            var dayOfWeek = (int)today.DayOfWeek;
            
            // Beregn hvor mye tid brukeren har trent i dag
            var minutesToday = _exerciseService.GetTotalMinutesToday();
            var sessionsToday = _exerciseService.GetCompletedSessionsToday();
            var remainingMinutes = Math.Max(0, RecommendedDailyMinutes - minutesToday);
            
            // Prioriter øvelser basert på frekvens og progresjon
            var dailyExercises = allExercises.Where(e => 
                e.Frequency == FrequencyType.Daglig && 
                !_exerciseService.IsExerciseCompletedToday(e.ExerciseId))
                .OrderBy(e => e.TotalSessions)
                .ToList();
            
            foreach (var exercise in dailyExercises.Take(2))
            {
                if (remainingMinutes <= 0) break;
                
                recommendations.Add(new TrainingRecommendation
                {
                    RecommendedExercise = exercise,
                    Reason = GetReasonForRecommendation(exercise, dayOfWeek),
                    RecommendedDurationMinutes = Math.Min(exercise.DurationMinutes, remainingMinutes),
                    Frequency = exercise.Frequency,
                    IsCompletedToday = _exerciseService.IsExerciseCompletedToday(exercise.ExerciseId)
                });
                
                remainingMinutes -= exercise.DurationMinutes;
            }
            
            // Legg til 3x ukentlig øvelser hvis det er riktig dag
            if (dayOfWeek == 0 || dayOfWeek == 2 || dayOfWeek == 4)
            {
                var threeTimesWeek = allExercises.Where(e => 
                    e.Frequency == FrequencyType.TreGangerUkentlig && 
                    !_exerciseService.IsExerciseCompletedToday(e.ExerciseId))
                    .OrderBy(e => e.TotalSessions)
                    .Take(1);
                
                foreach (var exercise in threeTimesWeek)
                {
                    if (remainingMinutes <= 0) break;
                    
                    recommendations.Add(new TrainingRecommendation
                    {
                        RecommendedExercise = exercise,
                        Reason = LocalizationService.Instance.GetFormattedString("TrainingFrequency_TodaysRecommendationFormat", exercise.Name),
                        RecommendedDurationMinutes = Math.Min(exercise.DurationMinutes, remainingMinutes),
                        Frequency = exercise.Frequency,
                        IsCompletedToday = _exerciseService.IsExerciseCompletedToday(exercise.ExerciseId)
                    });
                }
            }
            
            // Hvis ingen spesifikke anbefalinger, gi en generell
            if (recommendations.Count == 0 && sessionsToday == 0)
            {
                var randomExercise = allExercises
                    .Where(e => !_exerciseService.IsExerciseCompletedToday(e.ExerciseId))
                    .OrderBy(e => e.DifficultyLevel)
                    .FirstOrDefault();
                
                if (randomExercise != null)
                {
                    recommendations.Add(new TrainingRecommendation
                    {
                        RecommendedExercise = randomExercise,
                        Reason = LocalizationService.Instance["TrainingFrequency_TryExerciseToday"],
                        RecommendedDurationMinutes = randomExercise.DurationMinutes,
                        Frequency = randomExercise.Frequency,
                        IsCompletedToday = false
                    });
                }
            }
            
            return recommendations;
        }
        
        /// <summary>
        /// Beregn treningsstatus for i dag
        /// </summary>
        public TrainingStatus GetTodaysStatus()
        {
            var minutesToday = _exerciseService.GetTotalMinutesToday();
            var sessionsToday = _exerciseService.GetCompletedSessionsToday();
            
            return new TrainingStatus
            {
                MinutesToday = minutesToday,
                RecommendedMinutes = RecommendedDailyMinutes,
                SessionsToday = sessionsToday,
                IsGoalMet = minutesToday >= RecommendedDailyMinutes,
                ProgressPercent = Math.Min(100, (minutesToday * 100 / RecommendedDailyMinutes)),
                RemainingMinutes = Math.Max(0, RecommendedDailyMinutes - minutesToday)
            };
        }
        
        /// <summary>
        /// Sjekk om brukeren skal ta en pause
        /// </summary>
        public bool ShouldTakeBreak(int consecutiveMinutes)
        {
            return consecutiveMinutes >= 20;
        }
        
        /// <summary>
        /// Beregn ukentlig progresjon
        /// </summary>
        public WeeklyProgress GetWeeklyProgress()
        {
            var progress = new WeeklyProgress();
            var today = DateTime.Today;
            var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
            
            var allProgress = _exerciseService.GetAllExerciseProgress();
            
            progress.TotalSessionsWeek = allProgress.Sum(p => 
                p.LastSessionDate?.Date >= startOfWeek ? 1 : 0);
            progress.TotalMinutesWeek = allProgress.Sum(p => 
                p.LastSessionDate?.Date >= startOfWeek ? p.TotalMinutes : 0);
            progress.ExercisesCompleted = allProgress.Count(p => 
                p.LastSessionDate?.Date >= startOfWeek);
            
            // Sammenlign med forrige uke
            var lastWeekStart = startOfWeek.AddDays(-7);
            var lastWeekEnd = startOfWeek.AddDays(-1);
            
            progress.IsImproving = progress.TotalSessionsWeek > 0; // Forenklet
            
            return progress;
        }
        
        /// <summary>
        /// Generer motivasjonsmelding
        /// </summary>
        public string GetMotivationalMessage()
        {
            var status = GetTodaysStatus();
            
            if (status.IsGoalMet)
            {
                return LocalizationService.Instance["TrainingFrequency_GoalMet"];
            }
            
            if (status.RemainingMinutes > 15)
            {
                return LocalizationService.Instance.GetFormattedString("TrainingFrequency_RemainingMinutesFormat", status.RemainingMinutes);
            }
            
            if (status.RemainingMinutes > 0)
            {
                return LocalizationService.Instance.GetFormattedString("TrainingFrequency_AlmostDoneFormat", status.RemainingMinutes);
            }
            
            return LocalizationService.Instance["TrainingFrequency_StartExercise"];
        }
        
        /// <summary>
        /// Få grunn for anbefaling
        /// </summary>
        private string GetReasonForRecommendation(Exercise exercise, int dayOfWeek)
        {
            return exercise.Frequency switch
            {
                FrequencyType.Daglig => LocalizationService.Instance["TrainingFrequency_DailyReason"],
                FrequencyType.TreGangerUkentlig => LocalizationService.Instance.GetFormattedString("TrainingFrequency_ThreeTimesReasonFormat", exercise.Name),
                FrequencyType.ToGangerUkentlig => LocalizationService.Instance.GetFormattedString("TrainingFrequency_TwoTimesReasonFormat", exercise.Name),
                FrequencyType.Ukentlig => LocalizationService.Instance["TrainingFrequency_WeeklyReason"],
                _ => LocalizationService.Instance["TrainingFrequency_DefaultReason"]
            };
        }
    }
    
    /// <summary>
    /// Status for dagens trening
    /// </summary>
    public class TrainingStatus
    {
        public int MinutesToday { get; set; }
        public int RecommendedMinutes { get; set; }
        public int SessionsToday { get; set; }
        public bool IsGoalMet { get; set; }
        public int ProgressPercent { get; set; }
        public int RemainingMinutes { get; set; }
    }
}
