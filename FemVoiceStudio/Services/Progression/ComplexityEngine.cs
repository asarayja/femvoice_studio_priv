using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FemVoiceStudio.Data;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;

namespace FemVoiceStudio.Services.Progression
{
    /// <summary>
    /// ComplexityEngine: Clinical complexity progression for voice feminization training.
    /// Manages speech complexity levels from isolated sounds to natural conversation.
    /// Uses clinical criteria to determine when users are ready for advancement.
    /// </summary>
    public class ComplexityEngine
    {
        private readonly DatabaseService _database;
        private readonly LiveMetricsService _metricsService;
        private readonly ILocalizationService _localization;
        
        // Clinical thresholds
        private const double MinResonanceForProgression = 60.0;
        private const double MinPitchStabilityForProgression = 60.0;
        private const double MinIntonationForProgression = 50.0;
        private const double MinVoiceHealthForProgression = 70.0;
        private const double MaxStrainForProgression = 50.0;
        
        private const int MinSessionsAtLevel = 5;
        private const double MinSuccessRate = 70.0;
        private const double MinSessionsPerWeek = 2.0;

        // Voice Development Progression-terskler (Agent 10). EKSTRA AND-betingelser i
        // CanAdvanceToNextLevel — de gjør avansement STRENGERE, aldri lettere, og
        // anvendes KUN når et VoiceIntelligenceScores-snapshot er tilgjengelig
        // (additivt + bakoverkompatibelt). Health > Progression: Recovery-terskelen
        // sikrer at en overbelastet stemme aldri avanseres selv om resonans/pitch er
        // høye; Comfort/Consistency speiler det kliniske hierarkiet (Comfort > Resonance).
        private const double MinVoiceComfortForProgression = 60.0;
        private const double MinVoiceConsistencyForProgression = 55.0;
        private const double MinVoiceRecoveryForProgression = 55.0;
        
        // Cached evaluation
        private ComplexityEvaluation? _cachedEvaluation;
        private DateTime _cacheTimestamp = DateTime.MinValue;
        private const int CacheValidityMinutes = 5;
        
        public event EventHandler<SpeechComplexityLevel>? ComplexityLevelChanged;
        
        public ComplexityEngine(DatabaseService database, LiveMetricsService? metricsService = null, ILocalizationService? localization = null)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _metricsService = metricsService ?? new LiveMetricsService();
            _localization = localization ?? LocalizationService.Instance;
            _database.InitializeComplexityProgress(1);
        }
        
        /// <summary>
        /// Evaluates current complexity level status for a user.
        /// </summary>
        public async Task<ComplexityEvaluation> EvaluateCurrentLevelAsync(int userId = 1)
        {
            if (_cachedEvaluation != null && (DateTime.Now - _cacheTimestamp).TotalMinutes < CacheValidityMinutes)
            {
                return _cachedEvaluation;
            }
            
            var progress = _database.GetComplexityProgress(userId);
            var currentLevel = progress != null 
                ? (SpeechComplexityLevel)progress.CurrentLevel 
                : SpeechComplexityLevel.IsolatedSounds;
            
            var recentSessions = await GetRecentSessionsAsync(userId, 10);
            var sessionsAtCurrentLevel = recentSessions
                .Where(s => GetComplexityForSession(s) == currentLevel)
                .Take(MinSessionsAtLevel)
                .ToList();

            // Øvelses-id-bucketingen (GetComplexityForSession) kjenner bare 3 av 7
            // nivåer — på mellomnivåene (Syllables/Phrases/SpontaneousSpeech/
            // Conversational) ga den alltid 0 treff, så brukeren satt permanent fast
            // (review-funn). Den persisterte SessionsAtLevel-telleren (inkrementert
            // per øktslutt i TryAdvanceLevelAsync, nullstilt ved nivåheving) er
            // autoritativ; id-bucketingen beholdes som sekundær kilde der den finnes.
            if (sessionsAtCurrentLevel.Count == 0 && recentSessions.Any())
            {
                sessionsAtCurrentLevel = recentSessions.Take(MinSessionsAtLevel).ToList();
            }
            var persistedSessionsAtLevel = progress?.SessionsAtLevel ?? 0;

            var evaluation = new ComplexityEvaluation
            {
                CurrentLevel = currentLevel,
                SessionsAtCurrentLevel = Math.Max(sessionsAtCurrentLevel.Count,
                    Math.Min(persistedSessionsAtLevel, MinSessionsAtLevel)),
                // Robust parsing (runtime-feil): legacy-databaser kan ha 0/'' i
                // LastEvaluationDate-kolonnen — naken DateTime.Parse kastet
                // "String '0' was not recognized as a valid DateTime" og veltet
                // hele SmartCoach-dashboardlasten.
                LastLevelChange = DateTime.TryParse(progress?.LastEvaluationDate, out var lastEval)
                    ? lastEval
                    : DateTime.Now
            };
            
            if (recentSessions.Any())
            {
                var last3Resonance = recentSessions.Take(3).Select(s => s.ResonanceScore).ToList();
                evaluation.AverageResonance = last3Resonance.Average();
                evaluation.PitchStability = CalculatePitchStability(recentSessions);
                evaluation.IntonationScore = recentSessions.Average(s => s.IntonationScore);
                evaluation.VoiceHealthScore = recentSessions.Average(s => s.VoiceHealthScore);
                evaluation.StrainLevel = CalculateAverageStrain(recentSessions);
                evaluation.SessionsPerWeek = CalculateSessionsPerWeek(recentSessions);
                evaluation.SuccessRate = CalculateSuccessRate(sessionsAtCurrentLevel);
            }
            
            evaluation.IsReadyForNext = CanAdvanceToNextLevel(evaluation);
            evaluation.HealthAllowsProgression = CheckHealthProgression(evaluation);
            evaluation.Feedback = GenerateEvaluationFeedback(evaluation);
            
            if (!evaluation.IsReadyForNext)
            {
                evaluation.BlockingReasons = GetBlockingReasons(evaluation);
            }
            
            _cachedEvaluation = evaluation;
            _cacheTimestamp = DateTime.Now;
            
            return evaluation;
        }
        
        /// <summary>
        /// Synchronous version for simpler use cases.
        /// </summary>
        public ComplexityEvaluation EvaluateCurrentLevel(int userId = 1)
        {
            return EvaluateCurrentLevelAsync(userId).GetAwaiter().GetResult();
        }
        
        /// <summary>
        /// Determines if user can advance to the next complexity level.
        ///
        /// The optional <paramref name="voice"/> snapshot adds Voice Development
        /// Progression gates (Agent 10): when supplied, Comfort/Consistency/Recovery
        /// must each clear their threshold as EXTRA AND-conditions. These are purely
        /// ADDITIVE — they can only make advancement STRICTER, never easier (every
        /// existing threshold above is untouched). When <paramref name="voice"/> is
        /// null the behaviour is byte-identical to before.
        ///
        /// Health &gt; Progression: the Recovery gate ensures an overloaded voice is
        /// never advanced even when resonance/pitch look strong.
        /// </summary>
        public bool CanAdvanceToNextLevel(ComplexityEvaluation evaluation, VoiceIntelligenceScores? voice = null)
        {
            if (evaluation.SessionsAtCurrentLevel < MinSessionsAtLevel)
                return false;

            if (evaluation.SuccessRate < MinSuccessRate)
                return false;

            if (evaluation.AverageResonance < MinResonanceForProgression)
                return false;

            if (evaluation.PitchStability < MinPitchStabilityForProgression)
                return false;

            if (evaluation.IntonationScore < MinIntonationForProgression)
                return false;

            if (!evaluation.HealthAllowsProgression)
                return false;

            if (evaluation.VoiceHealthScore < MinVoiceHealthForProgression)
                return false;

            if (evaluation.StrainLevel >= MaxStrainForProgression)
                return false;

            if (evaluation.SessionsPerWeek < MinSessionsPerWeek)
                return false;

            // ── Voice Development Progression gates (additive AND-conditions) ──────
            // Only tightened when a multidimensional voice snapshot is available;
            // these never relax any existing gate. Health (Comfort + Recovery) and
            // Consistency must each be consolidated before advancing — a high
            // resonance/pitch can NEVER buy progression past an overloaded or
            // uncomfortable voice (Health/Comfort > Progression).
            if (voice != null)
            {
                if (voice.Comfort.Score < MinVoiceComfortForProgression)
                    return false;

                if (voice.Consistency.Score < MinVoiceConsistencyForProgression)
                    return false;

                if (voice.Recovery.Score < MinVoiceRecoveryForProgression)
                    return false;
            }

            return true;
        }
        
        /// <summary>
        /// Determines the next complexity level based on current level.
        /// </summary>
        public SpeechComplexityLevel DetermineNextLevel(SpeechComplexityLevel current)
        {
            return current switch
            {
                SpeechComplexityLevel.IsolatedSounds => SpeechComplexityLevel.Syllables,
                SpeechComplexityLevel.Syllables => SpeechComplexityLevel.Words,
                SpeechComplexityLevel.Words => SpeechComplexityLevel.Phrases,
                SpeechComplexityLevel.Phrases => SpeechComplexityLevel.StructuredSentences,
                SpeechComplexityLevel.StructuredSentences => SpeechComplexityLevel.SpontaneousSpeech,
                SpeechComplexityLevel.SpontaneousSpeech => SpeechComplexityLevel.Conversational,
                SpeechComplexityLevel.Conversational => SpeechComplexityLevel.Conversational,
                _ => SpeechComplexityLevel.IsolatedSounds
            };
        }
        
        /// <summary>
        /// Advances complexity level if criteria are met.
        /// </summary>
        public async Task<bool> TryAdvanceLevelAsync(int userId = 1)
        {
            // Tell den nettopp fullførte økten på gjeldende nivå (persistert teller).
            // Telleren nullstilles ved nivåheving lenger ned, og er det som gjør
            // avansement mulig på nivåer øvelses-id-bucketingen ikke kjenner.
            var countProgress = _database.GetComplexityProgress(userId)
                ?? new ComplexityProgress
                {
                    UserId = userId,
                    LastEvaluationDate = DateTime.Now.ToString("yyyy-MM-dd")   // aldri tom/0 i kolonnen
                };
            if (string.IsNullOrWhiteSpace(countProgress.LastEvaluationDate)
                || !DateTime.TryParse(countProgress.LastEvaluationDate, out _))
            {
                countProgress.LastEvaluationDate = DateTime.Now.ToString("yyyy-MM-dd");   // heal legacy 0/''
            }
            countProgress.SessionsAtLevel++;
            _database.SaveComplexityProgress(countProgress);
            _cachedEvaluation = null;   // evalueringen under skal se den nye telleren

            var evaluation = await EvaluateCurrentLevelAsync(userId);

            if (!CanAdvanceToNextLevel(evaluation))
                return false;
            
            var currentLevel = evaluation.CurrentLevel;
            var nextLevel = DetermineNextLevel(currentLevel);
            
            if (nextLevel == currentLevel)
                return false;
            
            var progress = _database.GetComplexityProgress(userId) ?? new ComplexityProgress { UserId = userId };
            progress.CurrentLevel = (int)nextLevel;
            progress.SessionsAtLevel = 0;
            progress.LastEvaluationDate = DateTime.Now.ToString("yyyy-MM-dd");
            progress.IsReadyForNext = false;
            
            _database.SaveComplexityProgress(progress);
            
            _cachedEvaluation = null;
            ComplexityLevelChanged?.Invoke(this, nextLevel);
            
            return true;
        }
        
        /// <summary>
        /// Gets exercises filtered by complexity level.
        /// </summary>
        public List<int> GetExerciseIdsForComplexity(SpeechComplexityLevel level, bool includePreview = false)
        {
            // FANTOM-ID-FIKS: katalogen har KUN 15 reelle øvelser (ExerciseId 1–15).
            // Tidligere returnerte denne metoden id 16–50, som ikke finnes som rader —
            // GetExerciseById(16+) gir null, så enhver anbefaling/planlegging på de id-ene
            // pekte på en ikke-eksisterende øvelse. De 15 øvelsene partisjoneres nå over de
            // 7 kompleksitetsnivåene (lettere/foundational øvelser på lavere nivå).
            // SYNK-KILDE: dette id-kartet er duplisert (pure, ingen DB-avhengighet) i
            // ExerciseRecommendationEngine.ExerciseIdsForComplexity og
            // LearningPathProfileBuilder.ExerciseIdsForLevel — endres ALLE tre samtidig.
            var ids = new List<int>(CatalogIdsForLevel(level));

            if (includePreview)
            {
                var nextLevel = DetermineNextLevel(level);
                if (nextLevel != level)
                    ids.AddRange(CatalogIdsForLevel(nextLevel));
            }

            return ids.Distinct().ToList();
        }

        /// <summary>
        /// Maps a complexity level to its slice of the REAL catalog id space (1–15).
        /// Every level returns a non-empty sub-range that exists as a seeded exercise row;
        /// no phantom id is ever emitted. Kept in SYNC with the duplicated mirrors in
        /// <see cref="ExerciseRecommendationEngine"/> and <see cref="LearningPathProfileBuilder"/>.
        /// Partition (monotonic by SortOrder — lighter exercises on lower levels):
        ///   IsolatedSounds 1–3 · Syllables 4–6 · Words 7–8 · Phrases 9–10 ·
        ///   StructuredSentences 11–12 · SpontaneousSpeech 13–14 · Conversational 15.
        /// </summary>
        private static IReadOnlyList<int> CatalogIdsForLevel(SpeechComplexityLevel level) => level switch
        {
            SpeechComplexityLevel.IsolatedSounds      => new[] { 1, 2, 3 },
            SpeechComplexityLevel.Syllables           => new[] { 4, 5, 6 },
            SpeechComplexityLevel.Words               => new[] { 7, 8 },
            SpeechComplexityLevel.Phrases             => new[] { 9, 10 },
            SpeechComplexityLevel.StructuredSentences => new[] { 11, 12 },
            SpeechComplexityLevel.SpontaneousSpeech   => new[] { 13, 14 },
            SpeechComplexityLevel.Conversational      => new[] { 15 },
            _                                         => new[] { 1, 2, 3 }
        };
        
        /// <summary>
        /// Gets progression steps for UI visualization.
        /// </summary>
        public List<ComplexityLevelStep> GetProgressionSteps(int userId = 1)
        {
            var evaluation = EvaluateCurrentLevel(userId);
            var currentLevel = evaluation.CurrentLevel;
            var steps = new List<ComplexityLevelStep>();
            
            foreach (SpeechComplexityLevel level in Enum.GetValues(typeof(SpeechComplexityLevel)))
            {
                var step = new ComplexityLevelStep
                {
                    Level = level,
                    IsCompleted = level < currentLevel,
                    IsCurrent = level == currentLevel,
                    DisplayName = ComplexityLevelStep.GetDisplayName(level),
                    Icon = ComplexityLevelStep.GetIcon(level)
                };
                
                if (level == currentLevel && evaluation.SessionsAtCurrentLevel > 0)
                {
                    step.ProgressToNext = Math.Min(100, (int)(evaluation.SessionsAtCurrentLevel * 100.0 / MinSessionsAtLevel));
                }
                
                steps.Add(step);
            }
            
            return steps;
        }
        
        /// <summary>
        /// Generates complexity-specific feedback message.
        /// </summary>
        public string GenerateComplexityFeedback(ComplexityEvaluation evaluation, VoiceMetrics? metrics = null)
        {
            var sb = new System.Text.StringBuilder();
            var levelName = ComplexityLevelStep.GetDisplayName(evaluation.CurrentLevel);
            
            sb.AppendLine(_localization.GetFormattedString("Complexity_CurrentLevelLine", levelName));
            sb.AppendLine();
            sb.AppendLine(_localization.GetFormattedString("Complexity_SessionsAtLevelLine", evaluation.SessionsAtCurrentLevel, MinSessionsAtLevel));
            sb.AppendLine();
            sb.AppendLine(_localization.GetString("Complexity_TechnicalValuesHeader"));
            sb.AppendLine(_localization.GetFormattedString("Complexity_ResonanceRequirementLine", evaluation.AverageResonance, MinResonanceForProgression));
            sb.AppendLine(_localization.GetFormattedString("Complexity_PitchStabilityRequirementLine", evaluation.PitchStability, MinPitchStabilityForProgression));
            sb.AppendLine(_localization.GetFormattedString("Complexity_IntonationRequirementLine", evaluation.IntonationScore, MinIntonationForProgression));
            sb.AppendLine();
            sb.AppendLine(_localization.GetString("Complexity_HealthStatusHeader"));
            sb.AppendLine(_localization.GetFormattedString("Complexity_VoiceHealthRequirementLine", evaluation.VoiceHealthScore, MinVoiceHealthForProgression));
            sb.AppendLine(_localization.GetFormattedString("Complexity_StrainRequirementLine", evaluation.StrainLevel, MaxStrainForProgression));
            sb.AppendLine();
            sb.AppendLine(_localization.GetFormattedString("Complexity_SuccessRateLine", evaluation.SuccessRate, MinSuccessRate));
            sb.AppendLine();
            sb.AppendLine(_localization.GetFormattedString("Complexity_SessionsPerWeekLine", evaluation.SessionsPerWeek, MinSessionsPerWeek));
            
            if (evaluation.IsReadyForNext)
            {
                var nextLevel = ComplexityLevelStep.GetDisplayName(DetermineNextLevel(evaluation.CurrentLevel));
                sb.AppendLine();
                sb.AppendLine(_localization.GetFormattedString("Complexity_ReadyForNextLine", nextLevel));
            }
            else
            {
                sb.AppendLine();
                sb.AppendLine(_localization.GetString("Complexity_NotReadyYet"));
                if (evaluation.BlockingReasons.Any())
                {
                    sb.AppendLine(_localization.GetString("Complexity_ReasonsHeader"));
                    foreach (var reason in evaluation.BlockingReasons)
                    {
                        sb.AppendLine($"   - {reason}");
                    }
                }
            }
            
            return sb.ToString();
        }
        
        #region Private Helpers
        
        private async Task<List<TrainingSession>> GetRecentSessionsAsync(int userId, int count)
        {
            return await Task.Run(() =>
            {
                try
                {
                    return _database.GetRecentSessions(count)
                        .Where(s => s.UserId == userId || s.UserId == 1)
                        .ToList();
                }
                catch
                {
                    return new List<TrainingSession>();
                }
            });
        }
        
        private SpeechComplexityLevel GetComplexityForSession(TrainingSession session)
        {
            var exerciseId = session.ExerciseTextId > 0 ? session.ExerciseTextId : 1;
            if (exerciseId <= 15)
                return SpeechComplexityLevel.IsolatedSounds;
            if (exerciseId <= 35)
                return SpeechComplexityLevel.Words;
            return SpeechComplexityLevel.StructuredSentences;
        }
        
        private double CalculatePitchStability(List<TrainingSession> sessions)
        {
            if (!sessions.Any()) return 50;
            var variations = sessions.Where(s => s.PitchVariation > 0).Select(s => s.PitchVariation).ToList();
            if (!variations.Any()) return 50;
            var avgVariation = variations.Average();
            return Math.Min(100, Math.Max(0, 100 - avgVariation * 3));
        }
        
        private double CalculateAverageStrain(List<TrainingSession> sessions)
        {
            if (!sessions.Any()) return 0;
            var strains = sessions.Select(s =>
            {
                var strain = 0.0;
                if (s.AveragePitch > 260) strain += 20;
                if (s.ResonanceScore < 40) strain += (40 - s.ResonanceScore) / 2;
                return strain;
            }).ToList();
            return strains.Average();
        }
        
        private double CalculateSessionsPerWeek(List<TrainingSession> sessions)
        {
            if (sessions.Count < 2) return sessions.Count;
            var orderedSessions = sessions.OrderByDescending(s => s.StartTime).ToList();
            var daysSpan = (orderedSessions.First().StartTime - orderedSessions.Last().StartTime).TotalDays;
            if (daysSpan < 1) daysSpan = 1;
            var weeks = daysSpan / 7.0;
            return weeks > 0 ? sessions.Count / weeks : sessions.Count;
        }
        
        private double CalculateSuccessRate(List<TrainingSession> sessions)
        {
            if (!sessions.Any()) return 0;
            var successfulSessions = sessions.Count(s => s.OverallScore >= 70);
            return (successfulSessions * 100.0) / sessions.Count;
        }
        
        private bool CheckHealthProgression(ComplexityEvaluation evaluation)
        {
            var recentHealthStatus = _metricsService.CurrentHealth;
            if (recentHealthStatus == HealthState.Warning || recentHealthStatus == HealthState.Danger)
                return false;
            if (evaluation.StrainLevel >= MaxStrainForProgression)
                return false;
            return true;
        }
        
        private string GenerateEvaluationFeedback(ComplexityEvaluation evaluation)
        {
            var levelName = ComplexityLevelStep.GetDisplayName(evaluation.CurrentLevel);
            
            if (evaluation.IsReadyForNext)
            {
                var nextLevel = ComplexityLevelStep.GetDisplayName(DetermineNextLevel(evaluation.CurrentLevel));
                return _localization.GetFormattedString("Complexity_FeedbackReadyForNext", nextLevel);
            }
            
            var reasons = GetBlockingReasons(evaluation);
            
            if (evaluation.SessionsAtCurrentLevel < MinSessionsAtLevel || reasons.Count == 0)
            {
                return _localization.GetFormattedString("Complexity_FeedbackNeedMoreSessions", levelName, MinSessionsAtLevel - evaluation.SessionsAtCurrentLevel);
            }
            
            return _localization.GetFormattedString("Complexity_FeedbackStayAtLevel", levelName);
        }
        
        private List<string> GetBlockingReasons(ComplexityEvaluation evaluation)
        {
            var reasons = new List<string>();
            
            if (evaluation.SessionsAtCurrentLevel < MinSessionsAtLevel)
                reasons.Add(_localization.GetFormattedString("Complexity_BlockingFewSessions", evaluation.SessionsAtCurrentLevel, MinSessionsAtLevel));
            
            if (evaluation.SuccessRate < MinSuccessRate)
                reasons.Add(_localization.GetFormattedString("Complexity_BlockingLowSuccessRate", evaluation.SuccessRate, MinSuccessRate));
            
            if (evaluation.AverageResonance < MinResonanceForProgression)
                reasons.Add(_localization.GetFormattedString("Complexity_BlockingLowResonance", evaluation.AverageResonance, MinResonanceForProgression));
            
            if (evaluation.PitchStability < MinPitchStabilityForProgression)
                reasons.Add(_localization.GetFormattedString("Complexity_BlockingLowPitchStability", evaluation.PitchStability, MinPitchStabilityForProgression));
            
            if (evaluation.IntonationScore < MinIntonationForProgression)
                reasons.Add(_localization.GetFormattedString("Complexity_BlockingLowIntonation", evaluation.IntonationScore, MinIntonationForProgression));
            
            if (evaluation.VoiceHealthScore < MinVoiceHealthForProgression)
                reasons.Add(_localization.GetFormattedString("Complexity_BlockingLowVoiceHealth", evaluation.VoiceHealthScore, MinVoiceHealthForProgression));
            
            if (evaluation.StrainLevel >= MaxStrainForProgression)
                reasons.Add(_localization.GetFormattedString("Complexity_BlockingHighStrain", evaluation.StrainLevel, MaxStrainForProgression));
            
            if (evaluation.SessionsPerWeek < MinSessionsPerWeek)
                reasons.Add(_localization.GetFormattedString("Complexity_BlockingFewWeeklySessions", evaluation.SessionsPerWeek, MinSessionsPerWeek));
            
            if (!evaluation.HealthAllowsProgression)
                reasons.Add(_localization.GetString("Complexity_BlockingHealthIssues"));
            
            return reasons;
        }
        
        #endregion
    }
}
