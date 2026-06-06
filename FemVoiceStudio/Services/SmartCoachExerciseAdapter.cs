using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Data;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Bindeledd mellom ExerciseFeedbackEngine og eksisterende SmartCoachSubsystem.
    /// 
    /// Ansvar:
    /// - Beregne brukernivå basert på historiske prestasjoner
    /// - Justere ExerciseDefinition målsoner i sanntid
    /// - Prioritere øvelser basert på svakeste parametere
    /// - Generere lokaliserte forklaringer
    /// </summary>
    public class SmartCoachExerciseAdapter
    {
        private readonly IDatabaseService? _databaseService;
        private readonly ILocalizationService? _localization;
        private readonly ExerciseDataService? _exerciseDataService;
        
        // Temporary pitch adjustment from health warnings
        private double _temporaryPitchOffset = 0;
        
        /// <summary>
        /// Default constructor
        /// </summary>
        public SmartCoachExerciseAdapter()
        {
        }
        
        /// <summary>
        /// Constructor with dependency injection
        /// </summary>
        public SmartCoachExerciseAdapter(IDatabaseService? databaseService, ILocalizationService? localization = null)
        {
            _databaseService = databaseService;
            _localization = localization;
            
            // Initialize ExerciseDataService from database connection
            if (databaseService is DatabaseService db)
            {
                _exerciseDataService = new ExerciseDataService(db.ConnectionString);
            }
        }
        
        #region Level Calculation
        
        /// <summary>
        /// Beregn brukernivå basert på historiske prestasjoner for en øvelse
        /// </summary>
        /// <param name="exerciseId">Øvelses-ID</param>
        /// <returns>Brukernivå</returns>
        public UserLevel CalculateUserLevel(int exerciseId)
        {
            try
            {
                // Hent siste 10 økter for øvelsen
                var sessions = GetRecentSessions(exerciseId, 10);
                
                if (sessions.Count == 0)
                    return UserLevel.Nybegynner;
                
                // Beregn gjennomsnittlig score
                var avgScore = sessions.Average(s => s.OverallScore);
                var totalSessions = sessions.Count;
                
                // Bestem nivå basert på antall økter og score
                if (totalSessions <= 10 || avgScore < 60)
                    return UserLevel.Nybegynner;
                if (totalSessions <= 30 || avgScore < 80)
                    return UserLevel.Middels;
                
                return UserLevel.Avansert;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error calculating user level: {ex.Message}");
                return UserLevel.Middels;
            }
        }
        
        /// <summary>
        /// Hent brukernivå basert på overall progresjon (alle øvelser)
        /// </summary>
        public UserLevel CalculateOverallUserLevel()
        {
            try
            {
                var allProgress = _exerciseDataService?.GetAllExerciseProgress();
                if (allProgress == null || allProgress.Count == 0)
                    return UserLevel.Nybegynner;
                
                var totalSessions = allProgress.Sum(p => p.TotalSessions);
                var avgScore = allProgress.Average(p => p.AverageScore);
                
                if (totalSessions <= 10 || avgScore < 60)
                    return UserLevel.Nybegynner;
                if (totalSessions <= 30 || avgScore < 80)
                    return UserLevel.Middels;
                
                return UserLevel.Avansert;
            }
            catch
            {
                return UserLevel.Middels;
            }
        }
        
        #endregion
        
        #region Exercise Definition Adaptation
        
        /// <summary>
        /// Tilpass ExerciseDefinition basert på brukernivå og historikk
        /// </summary>
        /// <param name="definition">Original definisjon</param>
        /// <param name="userLevel">Brukernivå</param>
        /// <returns>Tilpasset definisjon</returns>
        public ExerciseDefinition AdaptDefinition(ExerciseDefinition definition, UserLevel userLevel)
        {
            if (definition == null) return new ExerciseDefinition();
            
            // Kopier definisjonen
            var adapted = new ExerciseDefinition
            {
                ExerciseId = definition.ExerciseId,
                Name = definition.Name,
                TargetPitchRange = new TargetRange(
                    definition.TargetPitchRange.Min + _temporaryPitchOffset,
                    definition.TargetPitchRange.Max + _temporaryPitchOffset
                ),
                TargetF1Range = definition.TargetF1Range,
                TargetF2Range = definition.TargetF2Range,
                TargetF3Min = definition.TargetF3Min,
                StabilityThresholdPercent = definition.StabilityThresholdPercent,
                StrainLimitShimmerPercent = definition.StrainLimitShimmerPercent,
                StrainLimitAmplitudeSpikePercent = definition.StrainLimitAmplitudeSpikePercent,
                NybegynnerFactors = definition.NybegynnerFactors,
                MiddelsFactors = definition.MiddelsFactors,
                AvansertFactors = definition.AvansertFactors,
                FeedbackRules = definition.FeedbackRules,
                HealthThresholds = definition.HealthThresholds,
                RequiresIntonation = definition.RequiresIntonation,
                GoalCategory = definition.GoalCategory
            };
            
            // Juster pitch-mål basert på midlertidig helse-justering
            if (_temporaryPitchOffset != 0)
            {
                adapted.TargetPitchRange = new TargetRange(
                    definition.TargetPitchRange.Min + _temporaryPitchOffset,
                    definition.TargetPitchRange.Max + _temporaryPitchOffset
                );
            }
            
            return adapted;
        }
        
        /// <summary>
        /// Midlertidig senk pitch-mål etter helse-advarsel
        /// </summary>
        /// <param name="hz">Antall Hz å senke med</param>
        public void TemporarilyLowerPitchTarget(double hz)
        {
            _temporaryPitchOffset = -Math.Abs(hz);
        }
        
        /// <summary>
        /// Nullstill midlertidig pitch-justering
        /// </summary>
        public void ResetTemporaryPitchOffset()
        {
            _temporaryPitchOffset = 0;
        }
        
        #endregion
        
        #region Exercise Prioritization
        
        /// <summary>
        /// Anbefal øvelser basert på svakeste parametere
        /// </summary>
        /// <param name="exercises">Tilgjengelige øvelser</param>
        /// <returns>Sortert liste av anbefalte øvelser</returns>
        public List<Exercise> PrioritizeExercises(List<Exercise> exercises)
        {
            try
            {
                // Hent alle progresjoner
                var allProgress = _exerciseDataService?.GetAllExerciseProgress();
                if (allProgress == null || allProgress.Count == 0)
                    return exercises;
                
                // Beregn gjennomsnittlig score per parameter-kategori
                var categoryScores = new Dictionary<GoalCategory, double>();
                
                foreach (var ex in exercises)
                {
                    var progress = allProgress.FirstOrDefault(p => p.ExerciseId == ex.ExerciseId);
                    if (progress != null && !categoryScores.ContainsKey(ex.Goal))
                    {
                        categoryScores[ex.Goal] = progress.AverageScore;
                    }
                }
                
                // Prioriter kategorier med lavest score
                var weakestCategory = categoryScores.OrderBy(x => x.Value).FirstOrDefault().Key;
                
                // Sorter øvelser slik at svakeste kategori kommer først
                return exercises
                    .OrderByDescending(e => e.Goal == weakestCategory)
                    .ThenBy(e => e.SortOrder)
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error prioritizing exercises: {ex.Message}");
                return exercises;
            }
        }
        
        /// <summary>
        /// Anbefal øvelse ved helse-stopp
        /// </summary>
        /// <param name="reason">Stopp-årsak (jitter, shimmer, pitchPress)</param>
        /// <returns>Anbefalt øvelses-ID eller null</returns>
        public int? RecommendExerciseOnHealthStop(string reason)
        {
            // Ved jitter/shimmer: foreslå pusteøvelser
            if (reason == "jitter" || reason == "shimmer")
            {
                // Kategori 4 = Breathing
                var breathingExercises = _exerciseDataService?.GetExercisesByCategory("Pust");
                return breathingExercises?.FirstOrDefault()?.ExerciseId;
            }
            
            // Ved pitch press: foreslå resonans uten pitch
            if (reason == "pitchPress")
            {
                var resonanceExercises = _exerciseDataService?.GetExercisesByCategory("Resonans");
                return resonanceExercises?.FirstOrDefault()?.ExerciseId;
            }
            
            return null;
        }
        
        #endregion
        
        #region Coach Feedback Generation
        
        /// <summary>
        /// Generer coach-hint basert på evalueringsresultat og brukernivå
        /// </summary>
        /// <param name="result">Evalueringsresultat</param>
        /// <param name="userLevel">Brukernivå</param>
        /// <returns>Lokalisert hint-tekst</returns>
        public string GenerateCoachHint(ExerciseEvaluationResult result, UserLevel userLevel)
        {
            var loc = LocalizationService.Instance;
            
            // Håndter helse-advarsler
            if (result.HealthIndicator == HealthIndicator.Critical)
            {
                return loc[result.CoachHintKey];
            }
            
            if (result.HealthIndicator == HealthIndicator.Warning)
            {
                return loc[result.CoachHintKey];
            }
            
            // Generer hint basert på justeringsbehov
            if (result.Status == EvaluationStatus.Adjust)
            {
                var hintKey = result.CoachHintKey;
                
                // For nybegynnere: mer detaljerte hints
                if (userLevel == UserLevel.Nybegynner)
                {
                    hintKey = hintKey switch
                    {
                        "ExerciseFeedback_AdjustResonance" => "ExerciseFeedback_AdjustResonance_Beginner",
                        "ExerciseFeedback_AdjustPitch" => "ExerciseFeedback_AdjustPitch_Beginner",
                        "ExerciseFeedback_AdjustStability" => "ExerciseFeedback_AdjustStability_Beginner",
                        "ExerciseFeedback_AdjustIntonation" => "ExerciseFeedback_AdjustIntonation_Beginner",
                        _ => hintKey
                    };
                }
                
                return loc[hintKey];
            }
            
            // Alt korrekt
            if (userLevel == UserLevel.Avansert)
                return loc["ExerciseFeedback_Correct_Advanced"];
            
            return loc["ExerciseFeedback_Correct"];
        }
        
        /// <summary>
        /// Generer økt-sammendrag basert på resultater og brukernivå
        /// </summary>
        /// <param name="summary">Økt-sammendrag</param>
        /// <param name="userLevel">Brukernivå</param>
        /// <returns>Lokalisert coach-anbefaling</returns>
        public string GenerateSessionSummaryText(SessionEvaluationSummary summary, UserLevel userLevel)
        {
            var loc = LocalizationService.Instance;
            var lines = new List<string>();
            
            // Generell intro
            if (summary.OverallScore >= 80)
                lines.Add(loc["ExerciseSummary_Excellent"]);
            else if (summary.OverallScore >= 60)
                lines.Add(loc["ExerciseSummary_Good"]);
            else if (summary.OverallScore >= 40)
                lines.Add(loc["ExerciseSummary_NeedsWork"]);
            else
                lines.Add(loc["ExerciseSummary_KeepTrying"]);
            
            // Resonans-råd
            if (summary.ResonanceCorrectPercent < 70)
            {
                lines.Add(loc["ExerciseSummary_ResonanceAdvice"]);
            }
            
            // Pitch-råd
            if (summary.PitchCorrectPercent < 70)
            {
                lines.Add(loc["ExerciseSummary_PitchAdvice"]);
            }
            
            // Stabilitets-råd
            if (summary.StabilityCorrectPercent < 70)
            {
                lines.Add(loc["ExerciseSummary_StabilityAdvice"]);
            }
            
            // Strain-nivå
            if (summary.StrainLevel == "High")
            {
                lines.Add(loc["ExerciseSummary_StrainHigh"]);
            }
            else if (summary.StrainLevel == "Moderate")
            {
                lines.Add(loc["ExerciseSummary_StrainModerate"]);
            }
            
            // Forbedrings-råd
            var improvement = GetImprovementFromLastSession(summary.ExerciseId);
            if (improvement.HasValue)
            {
                if (improvement.Value > 5)
                    lines.Add(loc["ExerciseSummary_Improved"]);
                else if (improvement.Value < -5)
                    lines.Add(loc["ExerciseSummary_Declined"]);
            }
            
            return string.Join("\n\n", lines);
        }
        
        #endregion
        
        #region Database Helpers
        
        /// <summary>
        /// Hent nylige økter for en øvelse
        /// </summary>
        private List<TrainingSession> GetRecentSessions(int exerciseId, int count)
        {
            var sessions = new List<TrainingSession>();
            
            try
            {
                // Use reflection or direct SQL query
                // This is simplified - in production, use proper data service
                if (_databaseService != null)
                {
                    // Query would go here - using OverallScore instead of Score
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting recent sessions: {ex.Message}");
            }
            
            return sessions;
        }
        
        /// <summary>
        /// Beregn forbedring fra forrige økt
        /// </summary>
        private double? GetImprovementFromLastSession(int exerciseId)
        {
            try
            {
                // This would query the database for the previous session
                // and compare scores
                return null;
            }
            catch
            {
                return null;
            }
        }
        
        #endregion
        
        #region Persistence
        
        /// <summary>
        /// Lagre tilpasning til database for progresjonssporing
        /// </summary>
        public void LogAdaptation(int exerciseId, UserLevel level, double pitchOffset)
        {
            try
            {
                // Log to database for analytics
                System.Diagnostics.Debug.WriteLine(
                    $"Exercise adaptation: ExerciseId={exerciseId}, Level={level}, PitchOffset={pitchOffset}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error logging adaptation: {ex.Message}");
            }
        }
        
        #endregion
    }
}
