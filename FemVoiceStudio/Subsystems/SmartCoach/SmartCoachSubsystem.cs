using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FemVoiceStudio.Models;
using FemVoiceStudio.Subsystems.Data;
using FemVoiceStudio.Subsystems.Progression;
using FemVoiceStudio.Services;
using VoiceParameter = FemVoiceStudio.Subsystems.Analysis.VoiceParameter;
using ResonanceClassification = FemVoiceStudio.Models.ResonanceClassification;
using AnalysisVoiceMetrics = FemVoiceStudio.Subsystems.Analysis.VoiceMetrics;
using TrainingLevel = FemVoiceStudio.Subsystems.Progression.TrainingLevel;
using TrendDirection = FemVoiceStudio.Subsystems.Progression.TrendDirection;

namespace FemVoiceStudio.Subsystems.SmartCoach
{
    // Alias to resolve ambiguity - this is the Models version
    using VoiceMetrics = FemVoiceStudio.Models.VoiceMetrics;
    /// <summary>
    /// Smart Coach Subsystem - orchestrates all other subsystems for intelligent guidance
    /// Uses Strategy pattern for exercise selection and Observer pattern for real-time updates
    /// </summary>
    public class SmartCoachSubsystem : ISmartCoachSubsystem
    {
        private readonly IDataSubsystem _dataSubsystem;
        private readonly IProgressionSubsystem _progressionSubsystem;
        private readonly LocalizationService _localizationService;
        
        private VoiceProfile _voiceProfile = new();
        private readonly Random _random = new();

        // Threshold constants
        private const double ResonancePriorityThreshold = 70.0;
        private const double PitchPressThreshold = 180.0;
        private const double FatigueScoreDrop = 20.0;
        private const double HealthStrainThreshold = 0.5;

        // Exercise strategies per level
        private readonly Dictionary<TrainingLevel, List<ExerciseStrategy>> _exerciseStrategies;

        public SmartCoachSubsystem(
            IDataSubsystem dataSubsystem,
            IProgressionSubsystem progressionSubsystem,
            LocalizationService localizationService)
        {
            _dataSubsystem = dataSubsystem ?? throw new ArgumentNullException(nameof(dataSubsystem));
            _progressionSubsystem = progressionSubsystem ?? throw new ArgumentNullException(nameof(progressionSubsystem));
            _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
            
            _exerciseStrategies = InitializeExerciseStrategies();
        }

        private Dictionary<TrainingLevel, List<ExerciseStrategy>> InitializeExerciseStrategies()
        {
            return new Dictionary<TrainingLevel, List<ExerciseStrategy>>
            {
                {
                    TrainingLevel.Beginner, new List<ExerciseStrategy>
                    {
                        new() { Goal = GoalCategory.Resonance, Name = "Resonance Awareness", ScientificRationale = "Resonance is the PRIMARY mechanism for voice feminization" },
                        new() { Goal = GoalCategory.Pitch, Name = "Pitch Exploration", ScientificRationale = "Safe pitch exploration after resonance foundation is established" },
                        new() { Goal = GoalCategory.Breathing, Name = "Breath Support", ScientificRationale = "Proper breathing supports healthy voice production" }
                    }
                },
                {
                    TrainingLevel.Intermediate, new List<ExerciseStrategy>
                    {
                        new() { Goal = GoalCategory.Combined, Name = "Resonance + Pitch", ScientificRationale = "Combine resonance with pitch control" },
                        new() { Goal = GoalCategory.Resonance, Name = "Sustained Vowels", ScientificRationale = "Maintain resonance consistency" },
                        new() { Goal = GoalCategory.Intonation, Name = "Pitch Glides", ScientificRationale = "Practice smooth pitch transitions" }
                    }
                },
                {
                    TrainingLevel.Advanced, new List<ExerciseStrategy>
                    {
                        new() { Goal = GoalCategory.Combined, Name = "Conversational Phrases", ScientificRationale = "Natural speech integration" },
                        new() { Goal = GoalCategory.Intonation, Name = "Emotional Prosody", ScientificRationale = "Express emotion through intonation" },
                        new() { Goal = GoalCategory.Combined, Name = "Endurance Training", ScientificRationale = "Maintain feminine voice during extended speech" }
                    }
                }
            };
        }

        public async Task<DailyTrainingGoal> GetTodaysGoalAsync(CancellationToken ct = default)
        {
            var profile = await _dataSubsystem.GetVoiceProfileAsync(ct);
            _voiceProfile = profile ?? new VoiceProfile();

            var goal = new DailyTrainingGoal
            {
                Date = DateTime.Today,
                TargetMinutes = _voiceProfile.OptimalSessionMinutes > 0 ? _voiceProfile.OptimalSessionMinutes : 15,
                TargetSessions = 3
            };

            // Get recommended exercises based on current level
            var recommendations = GetRecommendedExercises(_voiceProfile.CurrentLevel).Take(3);
            goal.RecommendedExercises = recommendations.ToList();

            // Determine focus area based on progression
            var trend = _progressionSubsystem.GetWeeklyTrend();
            if (trend.Direction == TrendDirection.Declining || trend.Direction == TrendDirection.Flat)
            {
                var plateau = _progressionSubsystem.DetectPlateau();
                if (plateau.IsOnPlateau)
                {
                    goal.FocusArea = plateau.WeakestComponent ?? "General";
                    goal.PersonalizedMessage = plateau.SuggestedIntervention;
                }
                else
                {
                    goal.FocusArea = "Consistency";
                    goal.PersonalizedMessage = "Focus on consistent practice to build the foundation";
                }
            }
            else
            {
                goal.FocusArea = "Progression";
                goal.PersonalizedMessage = "Great progress! Keep up the momentum with your current exercises";
            }

            return goal;
        }

        public IEnumerable<ExerciseRecommendation> GetRecommendedExercises(TrainingLevel level)
        {
            if (!_exerciseStrategies.TryGetValue(level, out var strategies))
            {
                strategies = _exerciseStrategies[TrainingLevel.Beginner];
            }

            foreach (var strategy in strategies)
            {
                yield return new ExerciseRecommendation
                {
                    Exercise = CreateExerciseFromStrategy(strategy),
                    Reason = strategy.Name,
                    ScientificRationale = strategy.ScientificRationale,
                    Priority = strategies.IndexOf(strategy)
                };
            }
        }

        private Exercise CreateExerciseFromStrategy(ExerciseStrategy strategy)
        {
            return new Exercise
            {
                Name = strategy.Name,
                Goal = strategy.Goal,
                ScientificRationale = strategy.ScientificRationale,
                DifficultyLevel = (DifficultyLevel)_voiceProfile.CurrentLevel,
                DurationMinutes = _voiceProfile.OptimalSessionMinutes > 0 ? _voiceProfile.OptimalSessionMinutes : 5
            };
        }

        public DirectionAnalysisResult AnalyzeDirection(FemVoiceStudio.Subsystems.Analysis.VoiceMetrics current, FemVoiceStudio.Subsystems.Analysis.VoiceMetrics target)
        {
            var result = new DirectionAnalysisResult();

            // Always prioritize resonance first
            result.Resonance = AnalyzeParameter(
                VoiceParameter.Resonance,
                current.ResonanceScore,
                target.ResonanceScore,
                current.F2,
                1400 // Target F2 minimum
            );

            // Pitch is secondary - only analyze if resonance is stable
            if (result.Resonance.Direction == Direction.Stabilize || result.Resonance.Direction == Direction.Maintain)
            {
                result.Pitch = AnalyzeParameter(
                    VoiceParameter.Pitch,
                    current.Pitch,
                    target.Pitch,
                    current.PitchVariation,
                    20 // Target variation minimum
                );
            }
            else
            {
                // Pitch is lower priority - suggest maintaining
                result.Pitch = new DirectionRecommendation
                {
                    Parameter = VoiceParameter.Pitch,
                    Direction = Direction.Maintain,
                    CurrentValue = current.Pitch,
                    TargetValue = target.Pitch,
                    Reason = "Focus on resonance first - pitch will follow"
                };
            }

            // Intonation analysis
            result.Intonation = AnalyzeParameter(
                VoiceParameter.Intonation,
                current.IntonationRiseScore,
                target.IntonationRiseScore,
                current.IntonationRange,
                30
            );

            // Health monitoring
            var healthIndicators = new FemVoiceStudio.Subsystems.Analysis.VoiceMetrics { StrainLevel = current.StrainLevel };
            if (current.StrainLevel > HealthStrainThreshold)
            {
                result.VoiceHealth = new DirectionRecommendation
                {
                    Parameter = VoiceParameter.VoiceHealth,
                    Direction = Direction.Decrease,
                    CurrentValue = current.StrainLevel * 100,
                    TargetValue = 0,
                    Reason = "Vocal strain detected - reduce intensity",
                    SafetyNote = "Take a break to prevent injury"
                };
                result.HasSafetyConcern = true;
            }
            else
            {
                result.VoiceHealth = new DirectionRecommendation
                {
                    Parameter = VoiceParameter.VoiceHealth,
                    Direction = Direction.Maintain,
                    CurrentValue = current.StrainLevel * 100,
                    TargetValue = 0,
                    Reason = "Voice health is good"
                };
            }

            // Determine primary focus
            if (result.HasSafetyConcern)
            {
                result.PrimaryFocus = VoiceParameter.VoiceHealth;
                result.Summary = "Priority: Voice health - reduce strain immediately";
            }
            else if (result.Resonance.Direction != Direction.Stabilize && result.Resonance.Direction != Direction.Maintain)
            {
                result.PrimaryFocus = VoiceParameter.Resonance;
                result.Summary = $"Priority: Increase resonance - current score {current.ResonanceScore:F0}%";
            }
            else if (result.Pitch.Direction != Direction.Stabilize && result.Pitch.Direction != Direction.Maintain)
            {
                result.PrimaryFocus = VoiceParameter.Pitch;
                result.Summary = $"Focus: Adjust pitch to target range";
            }
            else
            {
                result.PrimaryFocus = VoiceParameter.Intonation;
                result.Summary = "Focus: Improve intonation patterns";
            }

            return result;
        }

        private DirectionRecommendation AnalyzeParameter(
            VoiceParameter parameter,
            double currentValue,
            double targetValue,
            double currentVariation,
            double variationTarget)
        {
            var recommendation = new DirectionRecommendation
            {
                Parameter = parameter,
                CurrentValue = currentValue,
                TargetValue = targetValue
            };

            // Determine direction based on difference from target
            double difference = targetValue - currentValue;

            if (parameter == VoiceParameter.Pitch)
            {
                // Special handling for pitch - check for strain
                if (currentValue > PitchPressThreshold)
                {
                    recommendation.Direction = Direction.Decrease;
                    recommendation.ChangeAmount = currentValue - PitchPressThreshold;
                    recommendation.Reason = "Pitch too high - risk of vocal strain";
                    recommendation.SafetyNote = "Reduce pitch to safe level";
                }
                else if (difference > 10)
                {
                    recommendation.Direction = Direction.Increase;
                    recommendation.ChangeAmount = difference;
                    recommendation.Reason = "Increase pitch toward feminine range";
                }
                else if (difference < -10)
                {
                    recommendation.Direction = Direction.Decrease;
                    recommendation.ChangeAmount = Math.Abs(difference);
                    recommendation.Reason = "Slightly above target - adjust down";
                }
                else
                {
                    recommendation.Direction = Direction.Stabilize;
                    recommendation.Reason = "Pitch in target range - focus on consistency";
                }
            }
            else if (parameter == VoiceParameter.Resonance)
            {
                if (currentValue < targetValue - 20)
                {
                    recommendation.Direction = Direction.Increase;
                    recommendation.ChangeAmount = targetValue - currentValue;
                    recommendation.Reason = "Resonance is primary feminization factor - prioritize this";
                    recommendation.ScientificRationale = "Resonance (formant modification) is more important than pitch for perceived femininity";
                }
                else if (currentValue < targetValue)
                {
                    recommendation.Direction = Direction.Increase;
                    recommendation.ChangeAmount = targetValue - currentValue;
                    recommendation.Reason = "Slight improvement needed in resonance";
                }
                else
                {
                    recommendation.Direction = Direction.Stabilize;
                    recommendation.Reason = "Resonance is good - maintain current position";
                }
            }
            else // Intonation
            {
                if (currentValue < variationTarget)
                {
                    recommendation.Direction = Direction.Increase;
                    recommendation.ChangeAmount = variationTarget - currentValue;
                    recommendation.Reason = "More intonation variation needed";
                }
                else
                {
                    recommendation.Direction = Direction.Stabilize;
                    recommendation.Reason = "Good intonation variety";
                }
            }

            return recommendation;
        }

        public bool IsSafeToContinue(FemVoiceStudio.Subsystems.Analysis.VoiceMetrics metrics)
        {
            // Safety checks
            if (metrics.StrainLevel >= 0.7)
                return false;
                
            if (metrics.Pitch > 280 && metrics.StrainLevel > 0.5)
                return false;
                
            return true;
        }

        public HealthOverride? GetHealthOverride(FemVoiceStudio.Subsystems.Analysis.VoiceMetrics metrics)
        {
            if (metrics.StrainLevel >= 0.7)
            {
                return new HealthOverride
                {
                    ShouldPause = true,
                    ShouldReduceIntensity = true,
                    Reason = "High vocal strain detected",
                    RecommendedBreakDuration = TimeSpan.FromHours(24)
                };
            }

            if (metrics.StrainLevel >= 0.5)
            {
                return new HealthOverride
                {
                    ShouldPause = false,
                    ShouldReduceIntensity = true,
                    Reason = "Moderate strain - reduce intensity",
                    RecommendedBreakDuration = TimeSpan.FromMinutes(15)
                };
            }

            return null;
        }

        public string ExplainRecommendation(DirectionRecommendation recommendation)
        {
            var explanation = new List<string>();

            explanation.Add($"Focus: {recommendation.Parameter}");
            explanation.Add($"Current: {recommendation.CurrentValue:F1} → Target: {recommendation.TargetValue:F1}");
            explanation.Add($"Direction: {recommendation.Direction}");

            if (!string.IsNullOrEmpty(recommendation.Reason))
            {
                explanation.Add($"Reason: {recommendation.Reason}");
            }

            if (!string.IsNullOrEmpty(recommendation.ScientificRationale))
            {
                explanation.Add($"Science: {recommendation.ScientificRationale}");
            }

            if (!string.IsNullOrEmpty(recommendation.SafetyNote))
            {
                explanation.Add($"⚠️ Safety: {recommendation.SafetyNote}");
            }

            return string.Join("\n", explanation);
        }

        public CoachMessage GenerateMessage(DirectionAnalysisResult analysis, TrainingLevel level, double recentScore)
        {
            var message = new CoachMessage
            {
                CreatedAt = DateTime.Now
            };

            // What to focus on
            message.What = analysis.PrimaryFocus;

            // Generate Why based on the principle
            message.Why = analysis.PrimaryFocus switch
            {
                VoiceParameter.Resonance => "Resonance (formant frequencies) is the PRIMARY voice feminization mechanism. A feminine resonance creates a more convincing feminine voice than pitch alone.",
                VoiceParameter.Pitch => "Pitch is secondary to resonance. After establishing good resonance, pitch adds to the feminine impression.",
                VoiceParameter.Intonation => "Intonation patterns (prosody) significantly affect perceived gender. Female speech typically has more varied intonation.",
                VoiceParameter.VoiceHealth => "Voice health must always come first. A healthy voice is essential for sustainable progress.",
                _ => "Consistent practice builds lasting results."
            };

            // Generate How based on direction
            message.How = analysis.PrimaryFocus switch
            {
                VoiceParameter.Resonance => analysis.Resonance.Direction switch
                {
                    Direction.Increase => "Practice forward resonance by imagining sound placement in your face/nose. Focus on brighter vowels (ee, ay).",
                    Direction.Decrease => "Reduce strain and focus on relaxed, forward placement.",
                    _ => "Maintain your current resonance position."
                },
                VoiceParameter.Pitch => analysis.Pitch.Direction switch
                {
                    Direction.Increase => "Gently lift your pitch. Think of speaking from a higher position in your throat.",
                    Direction.Decrease => "Reduce to a comfortable pitch level to avoid strain.",
                    _ => "Keep your pitch consistent."
                },
                VoiceParameter.Intonation => "Practice questions and statements with varied intonation patterns.",
                VoiceParameter.VoiceHealth => "Take a break, stay hydrated, and rest your voice.",
                _ => "Continue your current practice routine."
            };

            // Encouragement
            message.Encouragement = recentScore switch
            {
                > 75 => "Excellent progress! You're approaching advanced levels.",
                > 60 => "Great improvement! Keep up the consistent practice.",
                > 40 => "Good effort! Regular practice will bring results.",
                _ => "Every practice session builds your foundation."
            };

            // Emoji based on focus
            message.Emoji = analysis.PrimaryFocus switch
            {
                VoiceParameter.Resonance => "🎭",
                VoiceParameter.Pitch => "🎵",
                VoiceParameter.Intonation => "📈",
                VoiceParameter.VoiceHealth => "🛡️",
                _ => "💪"
            };

            // Full message
            message.FullMessage = $"{message.Emoji} {message.What}\n\n{message.How}\n\n💡 {message.Encouragement}";

            return message;
        }

        public void UpdateBaseline(FemVoiceStudio.Subsystems.Analysis.VoiceMetrics baseline)
        {
            _voiceProfile.BaselinePitch = baseline.Pitch;
            _voiceProfile.BaselineF1 = baseline.F1;
            _voiceProfile.BaselineF2 = baseline.F2;
            _voiceProfile.LastUpdated = DateTime.Now;
        }

        public VoiceProfile GetVoiceProfile()
        {
            return _voiceProfile;
        }

        public async Task SaveVoiceProfileAsync(VoiceProfile profile, CancellationToken ct = default)
        {
            _voiceProfile = profile;
            await _dataSubsystem.SaveVoiceProfileAsync(profile, ct);
        }

        private class ExerciseStrategy
        {
            public GoalCategory Goal { get; set; }
            public string Name { get; set; } = string.Empty;
            public string ScientificRationale { get; set; } = string.Empty;
        }
    }
}
