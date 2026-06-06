using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FemVoiceStudio.Models;
using FemVoiceStudio.Subsystems.Analysis;
using FemVoiceStudio.Subsystems.Data;

namespace FemVoiceStudio.Subsystems.Progression
{
    /// <summary>
    /// Progression Subsystem - Central source of truth for all coaching logic
    /// Handles FemVoiceScore calculation, trend analysis, plateau detection, and level transitions
    /// </summary>
    public class ProgressionSubsystem : IProgressionSubsystem
    {
        private readonly IDataSubsystem _dataSubsystem;
        private readonly ObservableCollection<TrainingSession> _sessions = new();
        private readonly List<FemVoiceScoreResult> _scoreHistory = new();
        private FemVoiceScoreResult? _currentScore;
        
        // Threshold constants
        private const int MinSessionsForTrend = 3;
        private const int PlateauThresholdDays = 14;
        private const double MinImprovementRate = 0.5; // percent per week
        
        // Health thresholds
        private const double StrainThresholdHigh = 0.7;
        private const double StrainThresholdMedium = 0.5;
        
        // Score weights
        private const double ResonanceWeight = 0.50;
        private const double PitchWeight = 0.30;
        private const double IntonationWeight = 0.15;
        private const double ConsistencyWeight = 0.05;

        public ProgressionSubsystem(IDataSubsystem dataSubsystem)
        {
            _dataSubsystem = dataSubsystem ?? throw new ArgumentNullException(nameof(dataSubsystem));
        }

        public ObservableCollection<TrainingSession> Sessions => _sessions;

        public FemVoiceScoreResult? CurrentScore => _currentScore;

        /// <summary>
        /// Calculate FemVoiceScore based on evidence-based principles:
        /// - Resonance: 50% (primary feminization factor)
        /// - Pitch: 30% (secondary)
        /// - Intonation: 15% (prosody patterns)
        /// - Consistency: 5% (stability over time)
        /// </summary>
        public Task<FemVoiceScoreResult> CalculateScoreAsync(FemVoiceScoreInput input, CancellationToken ct = default)
        {
            return Task.Run(() =>
            {
                // 1. Calculate Resonance Score (50%)
                double resonanceScore = CalculateResonanceScore(input);

                // 2. Calculate Pitch Score (30%)
                double pitchScore = CalculatePitchScore(input);

                // 3. Calculate Intonation Score (15%)
                double intonationScore = CalculateIntonationScore(input);

                // 4. Calculate Consistency Score (5%)
                double consistencyScore = CalculateConsistencyScore(input);

                // Calculate overall score
                double overallScore = (resonanceScore * ResonanceWeight) +
                                     (pitchScore * PitchWeight) +
                                     (intonationScore * IntonationWeight) +
                                     (consistencyScore * ConsistencyWeight);

                // Health override mechanism
                bool healthOverride = false;
                string? warningFlags = null;

                if (input.StrainLevel > StrainThresholdHigh)
                {
                    overallScore = Math.Min(overallScore, 50);
                    healthOverride = true;
                    warningFlags = "HIGH_STRAIN";
                }
                else if (input.StrainLevel > StrainThresholdMedium)
                {
                    overallScore = Math.Min(overallScore, 70);
                    warningFlags = "MEDIUM_STRAIN";
                }

                var result = new FemVoiceScoreResult
                {
                    OverallScore = overallScore,
                    ResonanceScore = resonanceScore,
                    PitchScore = pitchScore,
                    IntonationScore = intonationScore,
                    ConsistencyScore = consistencyScore,
                    VoiceHealthScore = 100 - (input.StrainLevel * 100),
                    CalculatedAt = DateTime.Now,
                    HealthOverride = healthOverride,
                    WarningFlags = warningFlags
                };

                _currentScore = result;
                _scoreHistory.Add(result);

                return result;
            }, ct);
        }

        private double CalculateResonanceScore(FemVoiceScoreInput input)
        {
            double score = 0;

            // F1 scoring: lower is more forward (feminine), target 280-450 Hz
            if (input.AverageF1 >= 280 && input.AverageF1 <= 450)
            {
                double optimalDistance = Math.Abs(input.AverageF1 - 330); // optimal ~330 Hz
                score += 40 * (1 - optimalDistance / 170);
            }
            else if (input.AverageF1 > 450)
            {
                score += Math.Max(0, 40 - (input.AverageF1 - 450) * 0.1);
            }
            else // < 280
            {
                score += Math.Max(0, 40 - (280 - input.AverageF1) * 0.1);
            }

            // F2 scoring: higher is more forward (feminine), target 1800-2600 Hz
            if (input.AverageF2 >= 1800 && input.AverageF2 <= 2600)
            {
                double optimalDistance = Math.Abs(input.AverageF2 - 2200);
                score += 40 * (1 - optimalDistance / 400);
            }
            else if (input.AverageF2 < 1800)
            {
                score += Math.Max(0, 40 - (1800 - input.AverageF2) * 0.05);
            }
            else // > 2600
            {
                score += Math.Max(0, 40 - (input.AverageF2 - 2600) * 0.05);
            }

            // Spectral centroid bonus
            if (input.SpectralCentroid > 2000)
            {
                score += 20 * Math.Min(1, (input.SpectralCentroid - 2000) / 1500);
            }

            return Math.Min(100, Math.Max(0, score));
        }

        private double CalculatePitchScore(FemVoiceScoreInput input)
        {
            double score = 0;
            double pitchRange = input.MaxPitch - input.MinPitch;

            // Pitch within target range
            if (input.AveragePitch >= input.TargetMinPitch && input.AveragePitch <= input.TargetMaxPitch)
            {
                double optimalDistance = Math.Abs(input.AveragePitch - 200);
                score += 50 * (1 - optimalDistance / 45);
            }
            else if (input.AveragePitch < input.TargetMinPitch)
            {
                double diff = input.TargetMinPitch - input.AveragePitch;
                score += Math.Max(0, 50 - diff * 1.5);
            }
            else // above target
            {
                double diff = input.AveragePitch - input.TargetMaxPitch;
                score += Math.Max(0, 50 - diff);
            }

            // Pitch variation bonus (healthy variation)
            if (pitchRange > 15 && pitchRange < 100)
            {
                score += 30 * (pitchRange / 100);
            }
            else if (pitchRange <= 15)
            {
                score += 30 * (pitchRange / 15) * 0.5;
            }

            // Pitch stability bonus
            double pitchStability = 100 - (input.PitchVariation / 2);
            score += 20 * (pitchStability / 100);

            return Math.Min(100, Math.Max(0, score));
        }

        private double CalculateIntonationScore(FemVoiceScoreInput input)
        {
            double score = 0;

            // Intonation range scoring
            if (input.IntonationRange > 0)
            {
                // Good intonation variation
                if (input.IntonationRange >= 30 && input.IntonationRange <= 100)
                {
                    score += 50;
                }
                else if (input.IntonationRange < 30)
                {
                    score += 50 * (input.IntonationRange / 30);
                }
                else // > 100
                {
                    score += Math.Max(0, 50 - (input.IntonationRange - 100) * 0.3);
                }
            }

            // Rising intonation bonus (feminine pattern)
            if (input.IntonationRiseScore > 0.2)
            {
                score += 50 * Math.Min(1, input.IntonationRiseScore);
            }

            return Math.Min(100, score);
        }

        private double CalculateConsistencyScore(FemVoiceScoreInput input)
        {
            // Based on standard deviation - lower is more consistent
            double variation = input.PitchVariation;
            
            if (variation <= 10)
                return 100;
            if (variation >= 50)
                return 0;
            
            return 100 - ((variation - 10) * 2);
        }

        public TrendResult GetWeeklyTrend()
        {
            return CalculateTrend(7);
        }

        public TrendResult GetMonthlyTrend()
        {
            return CalculateTrend(30);
        }

        public TrendResult GetOverallTrend()
        {
            return CalculateTrend(null);
        }

        private TrendResult CalculateTrend(int? days)
        {
            var scores = days.HasValue
                ? _scoreHistory.Where(s => s.CalculatedAt >= DateTime.Now.AddDays(-days.Value)).ToList()
                : _scoreHistory.ToList();

            if (scores.Count < MinSessionsForTrend)
            {
                return new TrendResult
                {
                    Direction = TrendDirection.Flat,
                    Strength = 0,
                    DataPoints = scores.Count
                };
            }

            // Compare recent half to older half
            int midPoint = scores.Count / 2;
            var recent = scores.Skip(midPoint).ToList();
            var older = scores.Take(midPoint).ToList();

            double recentAvg = recent.Average(s => s.OverallScore);
            double olderAvg = older.Average(s => s.OverallScore);

            double changePercent = olderAvg != 0 ? ((recentAvg - olderAvg) / olderAvg) * 100 : 0;

            TrendDirection direction;
            if (changePercent > 2)
                direction = TrendDirection.Improving;
            else if (changePercent < -2)
                direction = TrendDirection.Declining;
            else
                direction = TrendDirection.Stable;

            return new TrendResult
            {
                Direction = direction,
                Strength = Math.Abs(changePercent),
                ChangePercent = changePercent,
                DataPoints = scores.Count,
                CalculatedAt = DateTime.Now
            };
        }

        public PlateauDetectionResult DetectPlateau()
        {
            var result = new PlateauDetectionResult();

            if (_scoreHistory.Count < MinSessionsForTrend)
            {
                result.IsOnPlateau = false;
                return result;
            }

            // Check last 14 days
            var recentScores = _scoreHistory
                .Where(s => s.CalculatedAt >= DateTime.Now.AddDays(-PlateauThresholdDays))
                .ToList();

            if (recentScores.Count < MinSessionsForTrend)
            {
                result.IsOnPlateau = false;
                return result;
            }

            // Calculate improvement rate
            double improvement = recentScores.Last().OverallScore - recentScores.First().OverallScore;
            int days = (int)(recentScores.Last().CalculatedAt - recentScores.First().CalculatedAt).TotalDays;
            double weeklyRate = days > 0 ? (improvement / days) * 7 : 0;

            result.ImprovementRate = weeklyRate;
            result.PlateauDurationDays = days;
            result.IsOnPlateau = weeklyRate < MinImprovementRate;

            if (result.IsOnPlateau)
            {
                // Identify weakest component
                var lastScore = recentScores.Last();
                var minComponent = Math.Min(Math.Min(lastScore.ResonanceScore, lastScore.PitchScore),
                                           Math.Min(lastScore.IntonationScore, lastScore.ConsistencyScore));

                result.WeakestComponent = minComponent == lastScore.ResonanceScore ? "Resonance" :
                                         minComponent == lastScore.PitchScore ? "Pitch" :
                                         minComponent == lastScore.IntonationScore ? "Intonation" : "Consistency";

                result.SuggestedIntervention = result.WeakestComponent switch
                {
                    "Resonance" => "Focus on resonance exercises to break plateau",
                    "Pitch" => "Try pitch variation exercises to improve pitch control",
                    "Intonation" => "Practice intonation patterns with varied phrases",
                    _ => "Focus on consistency with longer sustained vowels"
                };
            }

            return result;
        }

        public LevelClassificationResult EvaluateLevelTransition()
        {
            var result = new LevelClassificationResult
            {
                CurrentLevel = TrainingLevel.Beginner
            };

            if (_scoreHistory.Count < 5)
            {
                result.Reason = "Not enough data to evaluate level transition";
                return result;
            }

            // Get recent sessions
            var recentScores = _scoreHistory.TakeLast(10).ToList();
            double avgScore = recentScores.Average(s => s.OverallScore);
            double avgResonance = recentScores.Average(s => s.ResonanceScore);
            double consistency = recentScores.Average(s => s.ConsistencyScore);

            result.UpgradeProgress = (avgScore - 50) * 2; // 0-100 scale

            // Check for upgrade (Intermediate -> Advanced)
            if (avgScore >= 75 && avgResonance >= 70 && consistency >= 70)
            {
                result.SuggestedLevel = TrainingLevel.Advanced;
                result.ShouldUpgrade = true;
                result.Reason = $"Strong performance: Score {avgScore:F0}%, Resonance {avgResonance:F0}%";
            }
            // Check for upgrade (Beginner -> Intermediate)
            else if (avgScore >= 65 && avgResonance >= 50)
            {
                result.SuggestedLevel = TrainingLevel.Intermediate;
                result.ShouldUpgrade = true;
                result.Reason = $"Good progress: Score {avgScore:F0}%, Resonance {avgResonance:F0}%";
            }
            // Check for downgrade
            else if (avgScore < 40 || recentScores.Any(s => s.HealthOverride))
            {
                result.ShouldDowngrade = true;
                result.DowngradeRisk = 100 - avgScore;
                result.Reason = $"Performance below threshold or health concerns detected";
            }
            else
            {
                result.Reason = $"Continue at current level. Progress: {result.UpgradeProgress:F0}%";
            }

            return result;
        }

        public HealthWarning? GetCurrentHealthWarning()
        {
            if (_currentScore == null)
                return null;

            // Check recent strain levels
            var recentStrains = _scoreHistory.TakeLast(5).ToList();
            if (recentStrains.Any(s => s.WarningFlags?.Contains("HIGH") == true))
            {
                return new HealthWarning
                {
                    Level = HealthWarningLevel.Critical,
                    Message = "High vocal strain detected",
                    Recommendation = "Take a break and rest your voice for at least 24 hours"
                };
            }

            if (recentStrains.Any(s => s.WarningFlags?.Contains("MEDIUM") == true))
            {
                return new HealthWarning
                {
                    Level = HealthWarningLevel.Warning,
                    Message = "Moderate vocal strain detected",
                    Recommendation = "Consider reducing training intensity today"
                };
            }

            return null;
        }

        public string? GetMostEffectiveExercise()
        {
            // This would ideally be calculated from actual exercise effectiveness data
            // For now, return null to indicate no data yet
            return null;
        }

        public async Task LoadHistoryAsync(DateTime from, DateTime to, CancellationToken ct = default)
        {
            var sessions = await _dataSubsystem.GetSessionsAsync(from, to, ct);
            
            _sessions.Clear();
            foreach (var session in sessions)
            {
                _sessions.Add(session);
            }
        }

        public async Task AddSessionAsync(TrainingSession session, CancellationToken ct = default)
        {
            var id = await _dataSubsystem.SaveSessionAsync(session, ct);
            session.Id = id;
            _sessions.Add(session);
        }

        public double GetPercentileRank(double score)
        {
            if (_scoreHistory.Count == 0)
                return 50;

            int belowCount = _scoreHistory.Count(s => s.OverallScore < score);
            return (belowCount / (double)_scoreHistory.Count) * 100;
        }
    }
}
