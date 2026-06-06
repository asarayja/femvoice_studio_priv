using System;
using System.Collections.Generic;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// Treningsmål-kategorier for øvelser
    /// </summary>
    public enum GoalCategory
    {
        Pitch,
        Resonance,
        Intonation,
        Breathing,
        Combined
    }
    
    /// <summary>
    /// Modell for en øvelse i treningsguiden
    /// </summary>
    public class Exercise
    {
        public int ExerciseId { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public List<string> Steps { get; set; } = new();
        public string StepsJson { get; set; } = "[]";
        public int DurationMinutes { get; set; } = 5;
        public FrequencyType Frequency { get; set; } = FrequencyType.Daglig;
        public DifficultyLevel DifficultyLevel { get; set; } = DifficultyLevel.Nybegynner;
        public List<MetricType> MetricsToTrack { get; set; } = new();
        public string MetricsJson { get; set; } = "[]";
        public string Category { get; set; } = ""; // pitch, resonance, intonation, breathing, etc.
        public string Icon { get; set; } = "🎤";
        public int SortOrder { get; set; } = 0;
        
        // Nye felt for utvidet øvelsesmodul
        public GoalCategory Goal { get; set; } = GoalCategory.Pitch;
        public string GoalIcon { get; set; } = "🎵";
        public string ScientificRationale { get; set; } = "";
        public string FrequencyText { get; set; } = "Daglig";
        public bool HasStreak { get; set; }
        public string CategoryColor { get; set; } = "#E91E63";
        
        // Target pitch range for voice feminization
        public double TargetPitchMin { get; set; } = 140;
        public double TargetPitchMax { get; set; } = 220;
        
        /// <summary>
        /// Identifies which <see cref="ExerciseTargetProfile"/> factory method applies to this exercise.
        /// Stored as INTEGER in the Exercises table. Defaults to ResonanceHumming (0) — the safest
        /// clinical baseline for any row that predates the ProfileType column.
        /// </summary>
        public ExerciseProfileType ProfileType { get; set; } = ExerciseProfileType.ResonanceHumming;

        // Progresjonsdata
        public int TotalSessions { get; set; }
        public DateTime? LastSessionDate { get; set; }
        public double AverageScore { get; set; }
        public bool IsCompletedToday { get; set; }
        
        // Localized display properties
        public string DisplayDifficulty { get; set; } = "";
        public List<string> DisplaySteps { get; set; } = new();

        // ── Guidance localisation keys (copied to ExerciseDefinition/ExerciseTargetProfile)
        public string? ClinicalPurposeKey { get; set; }
        public string? PhysicalFocusKey { get; set; }
        public string? CommonMistakesKey { get; set; }
        public string? SafetyInfoKey { get; set; }
        
        /// <summary>
        /// Hent steg som liste
        /// </summary>
        public List<string> GetStepsList()
        {
            if (Steps.Count > 0)
                return Steps;
                
            if (string.IsNullOrEmpty(StepsJson) || StepsJson == "[]")
                return new List<string>();
                
            try
            {
                var steps = System.Text.Json.JsonSerializer.Deserialize<List<string>>(StepsJson);
                return steps ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }
        
        /// <summary>
        /// Hent metrikker som liste
        /// </summary>
        public List<MetricType> GetMetricsList()
        {
            if (MetricsToTrack.Count > 0)
                return MetricsToTrack;
                
            if (string.IsNullOrEmpty(MetricsJson) || MetricsJson == "[]")
                return new List<MetricType>();
                
            try
            {
                var metrics = System.Text.Json.JsonSerializer.Deserialize<List<MetricType>>(MetricsJson);
                return metrics ?? new List<MetricType>();
            }
            catch
            {
                return new List<MetricType>();
            }
        }
        
        /// <summary>
        /// Get display-friendly frequency text
        /// </summary>
        public string GetFrequencyDisplayText()
        {
            return Frequency switch
            {
                FrequencyType.Daglig => "Daglig",
                FrequencyType.TreGangerUkentlig => "3×/uke",
                FrequencyType.ToGangerUkentlig => "2×/uke",
                FrequencyType.Ukentlig => "Ukentlig",
                _ => "Daglig"
            };
        }
        
        /// <summary>
        /// Get color for goal category
        /// </summary>
        public string GetGoalColor()
        {
            return Goal switch
            {
                GoalCategory.Pitch => "#E91E63",      // Pink - pitch focus
                GoalCategory.Resonance => "#9C27B0",  // Purple - resonance
                GoalCategory.Intonation => "#2196F3", // Blue - intonation
                GoalCategory.Breathing => "#4CAF50",  // Green - breathing
                GoalCategory.Combined => "#FF9800",    // Orange - combined
                _ => "#E91E63"
            };
        }
        
        /// <summary>
        /// Get icon for goal category
        /// </summary>
        public string GetGoalIcon()
        {
            return Goal switch
            {
                GoalCategory.Pitch => "🎵",
                GoalCategory.Resonance => "🔊",
                GoalCategory.Intonation => "📈",
                GoalCategory.Breathing => "💨",
                GoalCategory.Combined => "⭐",
                _ => "🎵"
            };
        }
    }
    
    /// <summary>
    /// Treningsfrekvens-type
    /// </summary>
    public enum FrequencyType
    {
        Daglig = 1,
        TreGangerUkentlig = 2,
        ToGangerUkentlig = 3,
        Ukentlig = 4
    }
    
    /// <summary>
    /// Metrikk-typer som appen kan analysere
    /// </summary>
    public enum MetricType
    {
        Pitch,
        PitchVariability,
        Intonation,
        Resonance,
        Intensity,
        Consistency,
        Duration,
        Smoothness
    }
    
    /// <summary>
    /// Modell for en treningsøkt for en øvelse
    /// </summary>
    public class ExerciseSession
    {
        public int SessionId { get; set; }
        public int ExerciseId { get; set; }
        public int UserId { get; set; } = 1;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int DurationSeconds { get; set; }
        public bool Completed { get; set; }
        public double Score { get; set; }
        public string Notes { get; set; } = "";
        
        // Navigasjonsproperty
        public Exercise? Exercise { get; set; }
    }
    
    /// <summary>
    /// Modell for progresjon på en øvelse
    /// </summary>
    public class ExerciseProgress
    {
        public int ProgressId { get; set; }
        public int ExerciseId { get; set; }
        public int UserId { get; set; } = 1;
        public int TotalSessions { get; set; }
        public DateTime? LastSessionDate { get; set; }
        public int TotalMinutes { get; set; }
        public double BestScore { get; set; }
        public double AverageScore { get; set; }
        public int CurrentStreak { get; set; }
        public int LongestStreak { get; set; }
        
        // Navigasjonsproperty
        public Exercise? Exercise { get; set; }
    }
    
    /// <summary>
    /// Anbefaling for dagens trening
    /// </summary>
    public class TrainingRecommendation
    {
        public Exercise? RecommendedExercise { get; set; }
        public string Reason { get; set; } = "";
        public int RecommendedDurationMinutes { get; set; }
        public FrequencyType Frequency { get; set; }
        public bool IsCompletedToday { get; set; }
    }
}
