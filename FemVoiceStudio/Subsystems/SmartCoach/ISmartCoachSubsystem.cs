using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FemVoiceStudio.Models;
using FemVoiceStudio.Subsystems.Analysis;
using FemVoiceStudio.Subsystems.Progression;

namespace FemVoiceStudio.Subsystems.SmartCoach
{
    /// <summary>
    /// Direction of change recommendation
    /// </summary>
    public enum Direction
    {
        Increase,
        Decrease,
        Stabilize,
        Maintain
    }

    /// <summary>
    /// Direction recommendation for a voice parameter
    /// </summary>
    public class DirectionRecommendation
    {
        public VoiceParameter Parameter { get; set; }
        public Direction Direction { get; set; }
        public double CurrentValue { get; set; }
        public double TargetValue { get; set; }
        public double ChangeAmount { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string ScientificRationale { get; set; } = string.Empty;
        public string SafetyNote { get; set; } = string.Empty;
    }

    /// <summary>
    /// Complete direction analysis result
    /// </summary>
    public class DirectionAnalysisResult
    {
        public DirectionRecommendation Pitch { get; set; } = new();
        public DirectionRecommendation Resonance { get; set; } = new();
        public DirectionRecommendation Intonation { get; set; } = new();
        public DirectionRecommendation VoiceHealth { get; set; } = new();
        public VoiceParameter PrimaryFocus { get; set; }
        public string Summary { get; set; } = string.Empty;
        public bool HasSafetyConcern { get; set; }
    }

    /// <summary>
    /// Exercise recommendation
    /// </summary>
    public class ExerciseRecommendation
    {
        public Exercise Exercise { get; set; } = null!;
        public string Reason { get; set; } = string.Empty;
        public string ScientificRationale { get; set; } = string.Empty;
        public int Priority { get; set; }
    }

    /// <summary>
    /// Daily training goal
    /// </summary>
    public class DailyTrainingGoal
    {
        public DateTime Date { get; set; }
        public int TargetMinutes { get; set; }
        public int TargetSessions { get; set; }
        public List<ExerciseRecommendation> RecommendedExercises { get; set; } = new();
        public string FocusArea { get; set; } = string.Empty;
        public string? PersonalizedMessage { get; set; }
    }

    /// <summary>
    /// Health override from coach
    /// </summary>
    public class HealthOverride
    {
        public bool ShouldPause { get; set; }
        public bool ShouldReduceIntensity { get; set; }
        public string Reason { get; set; } = string.Empty;
        public TimeSpan RecommendedBreakDuration { get; set; }
    }

    /// <summary>
    /// Coach message with What/Why/How structure
    /// </summary>
    public class CoachMessage
    {
        public VoiceParameter What { get; set; }
        public string Why { get; set; } = string.Empty;
        public string How { get; set; } = string.Empty;
        public string Encouragement { get; set; } = string.Empty;
        public string FullMessage { get; set; } = string.Empty;
        public string Emoji { get; set; } = "💪";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Smart Coach subsystem interface - orchestrates all other subsystems for intelligent guidance
    /// </summary>
    public interface ISmartCoachSubsystem
    {
        /// <summary>
        /// Get daily training goal
        /// </summary>
        Task<DailyTrainingGoal> GetTodaysGoalAsync(CancellationToken ct = default);

        /// <summary>
        /// Get recommended exercises for current level
        /// </summary>
        IEnumerable<ExerciseRecommendation> GetRecommendedExercises(TrainingLevel level);

        /// <summary>
        /// Analyze direction for each parameter
        /// </summary>
        DirectionAnalysisResult AnalyzeDirection(FemVoiceStudio.Subsystems.Analysis.VoiceMetrics current, FemVoiceStudio.Subsystems.Analysis.VoiceMetrics target);

        /// <summary>
        /// Check if it's safe to continue training
        /// </summary>
        bool IsSafeToContinue(FemVoiceStudio.Subsystems.Analysis.VoiceMetrics metrics);

        /// <summary>
        /// Get health override if needed
        /// </summary>
        HealthOverride? GetHealthOverride(FemVoiceStudio.Subsystems.Analysis.VoiceMetrics metrics);

        /// <summary>
        /// Generate explanation for a recommendation
        /// </summary>
        string ExplainRecommendation(DirectionRecommendation recommendation);

        /// <summary>
        /// Generate coach message based on analysis
        /// </summary>
        CoachMessage GenerateMessage(DirectionAnalysisResult analysis, TrainingLevel level, double recentScore);

        /// <summary>
        /// Update user's baseline from recent session
        /// </summary>
        void UpdateBaseline(FemVoiceStudio.Subsystems.Analysis.VoiceMetrics baseline);

        /// <summary>
        /// Get user's voice profile
        /// </summary>
        VoiceProfile GetVoiceProfile();

        /// <summary>
        /// Save user's voice profile
        /// </summary>
        Task SaveVoiceProfileAsync(VoiceProfile profile, CancellationToken ct = default);
    }

    /// <summary>
    /// Voice Profile - stores user learning data and personalizes training
    /// </summary>
    public class VoiceProfile
    {
        public int UserId { get; set; } = 1;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        // Baseline measurements
        public double BaselinePitch { get; set; }
        public double BaselineF1 { get; set; }
        public double BaselineF2 { get; set; }

        // Current targets
        public double TargetMinPitch { get; set; } = 165;
        public double TargetMaxPitch { get; set; } = 255;
        public double TargetMinF1 { get; set; } = 400;
        public double TargetMinF2 { get; set; } = 1400;

        // Current level
        public TrainingLevel CurrentLevel { get; set; } = TrainingLevel.Beginner;
        public DateTime? LastLevelChange { get; set; }

        // Optimal session length (minutes)
        public int OptimalSessionMinutes { get; set; } = 5;

        // Weekly progression rate (safe change per week)
        public double WeeklyProgressionRate { get; set; } = 2.0;

        // Strengths and weaknesses (relative scores)
        public double ResonanceStrength { get; set; } = 50;
        public double PitchStrength { get; set; } = 50;
        public double IntonationStrength { get; set; } = 50;

        // Best improvement parameter
        public string MostImprovedParameter { get; set; } = "";
    }
}
