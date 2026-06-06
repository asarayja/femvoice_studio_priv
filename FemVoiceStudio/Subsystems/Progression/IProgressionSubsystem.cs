using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Subsystems.Progression
{
    /// <summary>
    /// Result from FemVoiceScore calculation
    /// </summary>
    public class FemVoiceScoreResult
    {
        public int Id { get; set; }
        public double OverallScore { get; set; }
        public double ResonanceScore { get; set; }
        public double PitchScore { get; set; }
        public double IntonationScore { get; set; }
        public double ConsistencyScore { get; set; }
        public double VoiceHealthScore { get; set; }
        public DateTime CalculatedAt { get; set; } = DateTime.Now;
        public int? SessionId { get; set; }
        public int UserId { get; set; } = 1;
        public string? WarningFlags { get; set; }
        public bool HealthOverride { get; set; }
    }

    /// <summary>
    /// Input parameters for FemVoiceScore calculation
    /// </summary>
    public class FemVoiceScoreInput
    {
        public double AveragePitch { get; set; }
        public double MinPitch { get; set; }
        public double MaxPitch { get; set; }
        public double PitchVariation { get; set; }
        public double AverageF1 { get; set; }
        public double AverageF2 { get; set; }
        public double AverageF3 { get; set; }
        public double SpectralCentroid { get; set; }
        public double IntonationRange { get; set; }
        public double IntonationRiseScore { get; set; }
        public double StrainLevel { get; set; }
        public double IntensityRms { get; set; }
        public double TargetMinPitch { get; set; } = 165;
        public double TargetMaxPitch { get; set; } = 255;
        public double TargetMinF1 { get; set; } = 400;
        public double TargetMaxF1 { get; set; } = 700;
        public double TargetMinF2 { get; set; } = 1400;
        public double TargetMaxF2 { get; set; } = 2000;
        public DifficultyLevel DifficultyLevel { get; set; } = DifficultyLevel.Nybegynner;
    }

    /// <summary>
    /// Trend analysis result
    /// </summary>
    public class TrendResult
    {
        public TrendDirection Direction { get; set; }
        public double Strength { get; set; }
        public double ChangePercent { get; set; }
        public DateTime CalculatedAt { get; set; } = DateTime.Now;
        public int DataPoints { get; set; }
    }

    public enum TrendDirection
    {
        Improving,
        Declining,
        Stable,
        Flat
    }

    /// <summary>
    /// Plateau detection result
    /// </summary>
    public class PlateauDetectionResult
    {
        public bool IsOnPlateau { get; set; }
        public int PlateauDurationDays { get; set; }
        public double ImprovementRate { get; set; }
        public string? SuggestedIntervention { get; set; }
        public string? WeakestComponent { get; set; }
    }

    /// <summary>
    /// Health warning from voice analysis
    /// </summary>
    public class HealthWarning
    {
        public HealthWarningLevel Level { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
        public DateTime IssuedAt { get; set; } = DateTime.Now;
    }

    public enum HealthWarningLevel
    {
        None,
        Caution,
        Warning,
        Critical
    }

    /// <summary>
    /// Level classification result with transition info
    /// </summary>
    public class LevelClassificationResult
    {
        public TrainingLevel CurrentLevel { get; set; }
        public TrainingLevel? SuggestedLevel { get; set; }
        public bool ShouldUpgrade { get; set; }
        public bool ShouldDowngrade { get; set; }
        public string Reason { get; set; } = "";
        public double UpgradeProgress { get; set; }
        public double DowngradeRisk { get; set; }
        public DateTime? LastTransitionDate { get; set; }
        public int DaysSinceLastTransition { get; set; }
    }

    /// <summary>
    /// Training level enumeration
    /// </summary>
    public enum TrainingLevel
    {
        Beginner = 1,
        Intermediate = 2,
        Advanced = 3
    }

    /// <summary>
    /// Progression subsystem interface - central source of truth for all coaching logic
    /// </summary>
    public interface IProgressionSubsystem
    {
        /// <summary>
        /// Observable collection of training sessions for WPF data binding
        /// </summary>
        ObservableCollection<TrainingSession> Sessions { get; }

        /// <summary>
        /// Current FemVoiceScore
        /// </summary>
        FemVoiceScoreResult? CurrentScore { get; }

        /// <summary>
        /// Calculate FemVoiceScore from input metrics
        /// </summary>
        Task<FemVoiceScoreResult> CalculateScoreAsync(FemVoiceScoreInput input, CancellationToken ct = default);

        /// <summary>
        /// Get weekly trend analysis
        /// </summary>
        TrendResult GetWeeklyTrend();

        /// <summary>
        /// Get monthly trend analysis
        /// </summary>
        TrendResult GetMonthlyTrend();

        /// <summary>
        /// Get overall trend since start
        /// </summary>
        TrendResult GetOverallTrend();

        /// <summary>
        /// Detect if user is on a plateau
        /// </summary>
        PlateauDetectionResult DetectPlateau();

        /// <summary>
        /// Evaluate if user should transition to different level
        /// </summary>
        LevelClassificationResult EvaluateLevelTransition();

        /// <summary>
        /// Get current health warning if any
        /// </summary>
        HealthWarning? GetCurrentHealthWarning();

        /// <summary>
        /// Get the most effective exercise for this user
        /// </summary>
        string? GetMostEffectiveExercise();

        /// <summary>
        /// Load session history
        /// </summary>
        Task LoadHistoryAsync(DateTime from, DateTime to, CancellationToken ct = default);

        /// <summary>
        /// Add a new session to history
        /// </summary>
        Task AddSessionAsync(TrainingSession session, CancellationToken ct = default);

        /// <summary>
        /// Get user's percentile rank within their own history
        /// </summary>
        double GetPercentileRank(double score);
    }
}
