using System;
using System.Collections.Generic;
using System.Linq;

namespace FemVoiceStudio.Services
{
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
        
        // Learning data
        public Dictionary<string, ExerciseEffectiveness> ExerciseEffectiveness { get; set; } = new();
        public List<DailyProgress> DailyProgress { get; set; } = new();
        
        // Current level
        public TrainingLevel CurrentLevel { get; set; } = TrainingLevel.Beginner;
        public DateTime? LastLevelChange { get; set; }
        
        // Optimal session length (minutes)
        public int OptimalSessionMinutes { get; set; } = 5;
        
        // Weekly progression rate (safe change per week)
        public double WeeklyProgressionRate { get; set; } = 2.0; // Hz per week
        
        // Strengths and weaknesses (relative scores)
        public double ResonanceStrength { get; set; } = 50;
        public double PitchStrength { get; set; } = 50;
        public double IntonationStrength { get; set; } = 50;
        
        // Best improvement parameter
        public string MostImprovedParameter { get; set; } = "";
    }
    
    /// <summary>
    /// Exercise effectiveness tracking
    /// </summary>
    public class ExerciseEffectiveness
    {
        public string ExerciseType { get; set; } = "";
        public int TimesCompleted { get; set; }
        public double AverageScoreDelta { get; set; }
        public DateTime LastUsed { get; set; }
        public double WinRate { get; set; } // Percentage of times it improved score
    }
    
    /// <summary>
    /// Daily progress tracking
    /// </summary>
    public class DailyProgress
    {
        public DateTime Date { get; set; }
        public double FemVoiceScore { get; set; }
        public double ResonanceScore { get; set; }
        public double PitchScore { get; set; }
        public double IntonationScore { get; set; }
        public double VoiceHealthScore { get; set; }
        public int SessionMinutes { get; set; }
        public string? ExerciseType { get; set; }
    }
    
    /// <summary>
    /// Voice Profile Extensions - personal learning with bandit algorithm
    /// 80% focus on what works, 20% exploration of new exercises
    /// </summary>
    public class VoiceProfileExtensions
    {
        private const double ExploitRate = 0.80;  // 80% exploit
        private const double ExploreRate = 0.20;   // 20% explore
        private const int MinSessionsForRecommendation = 3;
        
        private readonly Random _random = new();
        
        /// <summary>
        /// Update voice profile with new session data
        /// </summary>
        public void UpdateProfile(VoiceProfile profile, FemVoiceScoreResult score, string? exerciseType = null)
        {
            if (profile == null) return;
            
            profile.LastUpdated = DateTime.Now;
            
            // Update daily progress
            var progress = new DailyProgress
            {
                Date = DateTime.Today,
                FemVoiceScore = score.OverallScore,
                ResonanceScore = score.ResonanceScore,
                PitchScore = score.PitchScore,
                IntonationScore = score.IntonationScore,
                VoiceHealthScore = score.VoiceHealthScore,
                ExerciseType = exerciseType
            };
            
            profile.DailyProgress.Add(progress);
            
            // Keep only last 30 days
            profile.DailyProgress = profile.DailyProgress
                .Where(p => p.Date >= DateTime.Today.AddDays(-30))
                .ToList();
            
            // Update exercise effectiveness if exercise type provided
            if (!string.IsNullOrEmpty(exerciseType))
            {
                UpdateExerciseEffectiveness(profile, exerciseType, score.OverallScore);
            }
            
            // Update strength/weakness analysis
            UpdateStrengthsAndWeaknesses(profile);
            
            // Update optimal session length
            UpdateOptimalSessionLength(profile);
        }
        
        private void UpdateExerciseEffectiveness(VoiceProfile profile, string exerciseType, double score)
        {
            if (!profile.ExerciseEffectiveness.ContainsKey(exerciseType))
            {
                profile.ExerciseEffectiveness[exerciseType] = new ExerciseEffectiveness
                {
                    ExerciseType = exerciseType,
                    LastUsed = DateTime.Now
                };
            }
            
            var effectiveness = profile.ExerciseEffectiveness[exerciseType];
            
            // Calculate running average
            effectiveness.TimesCompleted++;
            effectiveness.AverageScoreDelta = ((effectiveness.AverageScoreDelta * (effectiveness.TimesCompleted - 1)) + score) / effectiveness.TimesCompleted;
            effectiveness.LastUsed = DateTime.Now;
            
            // Update win rate (score above 60 = success)
            if (score >= 60)
            {
                // Simple incremental update
                effectiveness.WinRate = ((effectiveness.WinRate * (effectiveness.TimesCompleted - 1)) + 100) / effectiveness.TimesCompleted;
            }
        }
        
        private void UpdateStrengthsAndWeaknesses(VoiceProfile profile)
        {
            if (!profile.DailyProgress.Any()) return;
            
            var recent = profile.DailyProgress
                .OrderByDescending(p => p.Date)
                .Take(7)
                .ToList();
            
            if (!recent.Any()) return;
            
            // Calculate average scores
            profile.ResonanceStrength = recent.Average(p => p.ResonanceScore);
            profile.PitchStrength = recent.Average(p => p.PitchScore);
            profile.IntonationStrength = recent.Average(p => p.IntonationScore);
            
            // Find most improved (compare first 3 to last 3 sessions)
            if (recent.Count >= 6)
            {
                var firstHalf = recent.Skip(recent.Count / 2).ToList();
                var secondHalf = recent.Take(recent.Count / 2).ToList();
                
                var resonanceDelta = secondHalf.Average(p => p.ResonanceScore) - firstHalf.Average(p => p.ResonanceScore);
                var pitchDelta = secondHalf.Average(p => p.PitchScore) - firstHalf.Average(p => p.PitchScore);
                var intonationDelta = secondHalf.Average(p => p.IntonationScore) - firstHalf.Average(p => p.IntonationScore);
                
                var maxDelta = Math.Max(resonanceDelta, Math.Max(pitchDelta, intonationDelta));
                
                if (maxDelta == resonanceDelta)
                    profile.MostImprovedParameter = LocalizationService.Instance["Dashboard_Resonance"];
                else if (maxDelta == pitchDelta)
                    profile.MostImprovedParameter = LocalizationService.Instance["Dashboard_Pitch"];
                else
                    profile.MostImprovedParameter = LocalizationService.Instance["Dashboard_Intonation"];
            }
        }
        
        private void UpdateOptimalSessionLength(VoiceProfile profile)
        {
            if (!profile.DailyProgress.Any()) return;
            
            // Find session length where score is best
            var grouped = profile.DailyProgress
                .GroupBy(p => p.SessionMinutes)
                .Select(g => new { Minutes = g.Key, AvgScore = g.Average(p => p.FemVoiceScore), Count = g.Count() })
                .Where(x => x.Count >= 2)
                .OrderByDescending(x => x.AvgScore)
                .FirstOrDefault();
            
            if (grouped != null)
            {
                profile.OptimalSessionMinutes = grouped.Minutes;
            }
        }
        
        /// <summary>
        /// Recommend next exercise using bandit algorithm (80/20)
        /// </summary>
        public string? RecommendExercise(VoiceProfile profile, List<string> availableExercises)
        {
            if (profile == null || availableExercises == null || !availableExercises.Any())
                return availableExercises?.FirstOrDefault();
            
            // Check if we have enough data for recommendations
            var experiencedExercises = profile.ExerciseEffectiveness
                .Where(e => e.Value.TimesCompleted >= MinSessionsForRecommendation)
                .OrderByDescending(e => e.Value.AverageScoreDelta)
                .ToList();
            
            // If not enough data, explore
            if (experiencedExercises.Count < 2)
            {
                // Pick random unexplored exercise
                var unexplored = availableExercises
                    .Where(e => !profile.ExerciseEffectiveness.ContainsKey(e) || profile.ExerciseEffectiveness[e].TimesCompleted < MinSessionsForRecommendation)
                    .ToList();
                
                return unexplored.Any() 
                    ? unexplored[_random.Next(unexplored.Count)] 
                    : availableExercises[_random.Next(availableExercises.Count)];
            }
            
            // 80% exploit: use best exercise
            // If there's a clear leader (significant advantage), deterministically choose it
            if (experiencedExercises.Count >= 2)
            {
                var top = experiencedExercises[0].Value.AverageScoreDelta;
                var second = experiencedExercises[1].Value.AverageScoreDelta;
                if (top - second >= 5.0)
                    return experiencedExercises.First().Key;
            }

            if (_random.NextDouble() < ExploitRate)
            {
                return experiencedExercises.First().Key;
            }
            
            // 20% explore: try something new or less used
            var lessUsed = availableExercises
                .Where(e => !experiencedExercises.Any(ee => ee.Key == e) || 
                           experiencedExercises.First(ee => ee.Key == e).Value.TimesCompleted < 5)
                .ToList();
            
            if (lessUsed.Any())
            {
                return lessUsed[_random.Next(lessUsed.Count)];
            }
            
            return experiencedExercises.First().Key;
        }
        
        /// <summary>
        /// Get personalized daily goals based on profile
        /// </summary>
        public (double targetPitch, double targetF2, string focusArea) GetPersonalizedGoals(VoiceProfile profile)
        {
            if (profile == null)
                return (170, 1400, LocalizationService.Instance["Dashboard_Resonance"]);
            
            // Determine weakest area
            var weakest = LocalizationService.Instance["Dashboard_Resonance"];
            var lowest = profile.ResonanceStrength;
            
            if (profile.PitchStrength < lowest)
            {
                lowest = profile.PitchStrength;
                weakest = LocalizationService.Instance["Dashboard_Pitch"];
            }
            
            if (profile.IntonationStrength < lowest)
            {
                lowest = profile.IntonationStrength;
                weakest = LocalizationService.Instance["Dashboard_Intonation"];
            }
            
            // Adjust targets based on weekly progression rate
            var pitchTarget = profile.TargetMinPitch + profile.WeeklyProgressionRate;
            var f2Target = profile.TargetMinF2 + (profile.WeeklyProgressionRate * 20); // F2 scales faster
            
            return (pitchTarget, f2Target, weakest);
        }
        
        /// <summary>
        /// Calculate progression rate (safe change per week)
        /// </summary>
        public double CalculateProgressionRate(VoiceProfile profile)
        {
            if (profile == null || profile.DailyProgress.Count < 7)
                return 2.0; // Default safe rate
            
            // Compare weekly averages
            var thisWeek = profile.DailyProgress
                .Where(p => p.Date >= DateTime.Today.AddDays(-7))
                .ToList();
            
            var lastWeek = profile.DailyProgress
                .Where(p => p.Date >= DateTime.Today.AddDays(-14) && p.Date < DateTime.Today.AddDays(-7))
                .ToList();
            
            if (!thisWeek.Any() || !lastWeek.Any())
                return 2.0;
            
            var thisWeekAvgPitch = thisWeek.Average(p => p.PitchScore);
            var lastWeekAvgPitch = lastWeek.Average(p => p.PitchScore);
            
            var pitchChange = thisWeekAvgPitch - lastWeekAvgPitch;
            
            // If improving fast, increase rate; if stagnating, decrease
            if (pitchChange > 10)
                return 3.0;
            else if (pitchChange < 0)
                return 1.0;
            
            return 2.0;
        }
        
        /// <summary>
        /// Get focus recommendation based on profile
        /// </summary>
        public string GetFocusRecommendation(VoiceProfile profile)
        {
            if (profile == null)
                return LocalizationService.Instance["VoiceProfile_FocusResonance"];
            
            // Find weakest area
            if (profile.ResonanceStrength < profile.PitchStrength && 
                profile.ResonanceStrength < profile.IntonationStrength)
            {
                return LocalizationService.Instance["VoiceProfile_ResonanceForwardFocus"];
            }
            
            if (profile.PitchStrength < profile.IntonationStrength)
            {
                return LocalizationService.Instance["VoiceProfile_PitchControlledIncrease"];
            }
            
            return LocalizationService.Instance["VoiceProfile_IntonationVariation"];
        }
    }
}
