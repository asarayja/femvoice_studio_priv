using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;

namespace FemVoiceStudio.Services.Progression
{
    /// <summary>
    /// WeeklyPlannerEngine: Generates weekly training schedules.
    /// Supports complexity-based exercise filtering for speech progression.
    /// </summary>
    public class WeeklyPlannerEngine
    {
        private ProgressionConfig _cfg;
        private int _currentWeekNumber;
        private PeriodizationCycle _currentCycle;
        private double _currentLoad;
        private ComplexityEngine? _complexityEngine;
        
        /// <summary>
        /// Constructor with optional ComplexityEngine.
        /// </summary>
        public WeeklyPlannerEngine(ProgressionConfig? cfg = null, ComplexityEngine? complexityEngine = null)
        {
            _cfg = cfg ?? ProgressionConfig.CreateDefault();
            _currentWeekNumber = 1;
            _currentCycle = PeriodizationCycle.Standard;
            _currentLoad = 50;
            _complexityEngine = complexityEngine;
        }
        
        /// <summary>
        /// Sets the ComplexityEngine for complexity-based planning.
        /// </summary>
        public void SetComplexityEngine(ComplexityEngine engine)
        {
            _complexityEngine = engine;
        }
        
        /// <summary>
        /// Generates weekly plan with complexity filtering.
        /// </summary>
        public WeeklySchedule GenerateWeeklyPlan(UserProgressionProfile profile, DateTime weekStart)
        {
            return GenerateWeeklyPlan(profile, weekStart, SpeechComplexityLevel.IsolatedSounds, false);
        }
        
        /// <summary>
        /// Generates weekly plan with complexity filtering.
        /// </summary>
        public WeeklySchedule GenerateWeeklyPlan(
            UserProgressionProfile profile, 
            DateTime weekStart,
            SpeechComplexityLevel currentLevel,
            bool includePreview = false)
        {
            _currentWeekNumber++;
            var sessions = new List<ScheduledSession>();
            
            // Get complexity evaluation if engine is available
            ComplexityEvaluation? complexityEval = null;
            if (_complexityEngine != null)
            {
                complexityEval = _complexityEngine.EvaluateCurrentLevel(1);
            }
            
            int day = 0;
            
            // Recovery week - simplified
            if (_currentCycle == PeriodizationCycle.Recovery)
            {
                for (int i = 0; i < 3; i++)
                {
                    sessions.Add(new ScheduledSession
                    {
                        Date = weekStart.AddDays(day++),
                        DayType = TrainingDayType.Recovery,
                        WeeklyExplanation = "Recovery"
                    });
                }
                sessions.Add(new ScheduledSession
                {
                    Date = weekStart.AddDays(day++),
                    DayType = TrainingDayType.Light,
                    WeeklyExplanation = "Rest"
                });
                return CreateSchedule(weekStart, sessions);
            }
            
            // Generate sessions based on complexity level
            sessions = GenerateComplexityBasedSessions(weekStart, currentLevel, complexityEval, includePreview);
            
            // Check for recovery week
            if (_currentWeekNumber % 4 == 0)
            {
                _currentCycle = PeriodizationCycle.Recovery;
            }
            
            return CreateSchedule(weekStart, sessions);
        }
        
        /// <summary>
        /// Generates sessions based on complexity level.
        /// </summary>
        private List<ScheduledSession> GenerateComplexityBasedSessions(
            DateTime weekStart,
            SpeechComplexityLevel currentLevel,
            ComplexityEvaluation? complexityEval,
            bool includePreview)
        {
            var sessions = new List<ScheduledSession>();
            
            // Get exercise IDs for current complexity level
            var exerciseIds = _complexityEngine?.GetExerciseIdsForComplexity(currentLevel, includePreview) 
                ?? GetDefaultExerciseIds(currentLevel);
            
            // Determine next level for preview
            var nextLevel = GetNextLevel(currentLevel);
            var previewIds = includePreview && _complexityEngine != null 
                ? _complexityEngine.GetExerciseIdsForComplexity(nextLevel, false)
                : new List<int>();
            
            int day = 0;
            
            switch (currentLevel)
            {
                case SpeechComplexityLevel.IsolatedSounds:
                case SpeechComplexityLevel.Syllables:
                    // Focus on technical pitch/resonance training
                    sessions.Add(CreateSession(weekStart, day++, TrainingDayType.Progression, 
                        "Teknisk trening - grunnleggende", exerciseIds.GetRange(0, Math.Min(3, exerciseIds.Count))));
                    sessions.Add(CreateSession(weekStart, day++, TrainingDayType.Progression, 
                        "Resonans-fokus", exerciseIds.GetRange(0, Math.Min(3, exerciseIds.Count))));
                    sessions.Add(CreateSession(weekStart, day++, TrainingDayType.Consolidation, 
                        "Konsolidering", exerciseIds.GetRange(0, Math.Min(2, exerciseIds.Count))));
                    sessions.Add(CreateSession(weekStart, day++, TrainingDayType.Light, 
                        "Vedlikehold", exerciseIds.GetRange(0, Math.Min(2, exerciseIds.Count))));
                    break;
                    
                case SpeechComplexityLevel.Words:
                case SpeechComplexityLevel.Phrases:
                    // Mixed: technical + functional
                    sessions.Add(CreateSession(weekStart, day++, TrainingDayType.Progression, 
                        "Funksjonell tale", exerciseIds.GetRange(0, Math.Min(3, exerciseIds.Count))));
                    sessions.Add(CreateSession(weekStart, day++, TrainingDayType.Progression, 
                        "Teknisk repetisjon", GetDefaultExerciseIds(SpeechComplexityLevel.Syllables).GetRange(0, 2)));
                    sessions.Add(CreateSession(weekStart, day++, TrainingDayType.Progression, 
                        "Progresjon mot fraser", previewIds.GetRange(0, Math.Min(2, previewIds.Count))));
                    sessions.Add(CreateSession(weekStart, day++, TrainingDayType.Consolidation, 
                        "Konsolidering", exerciseIds.GetRange(0, Math.Min(2, exerciseIds.Count))));
                    break;
                    
                case SpeechComplexityLevel.StructuredSentences:
                case SpeechComplexityLevel.SpontaneousSpeech:
                case SpeechComplexityLevel.Conversational:
                    // Focus on natural speech
                    sessions.Add(CreateSession(weekStart, day++, TrainingDayType.Progression, 
                        "Setningslesing", exerciseIds.GetRange(0, Math.Min(3, exerciseIds.Count))));
                    sessions.Add(CreateSession(weekStart, day++, TrainingDayType.Progression, 
                        "Narrativ prosodi", exerciseIds.GetRange(Math.Min(3, exerciseIds.Count), Math.Min(3, exerciseIds.Count - 3))));
                    sessions.Add(CreateSession(weekStart, day++, TrainingDayType.Progression, 
                        "Emosjonell prosodi", exerciseIds.GetRange(0, Math.Min(2, exerciseIds.Count))));
                    sessions.Add(CreateSession(weekStart, day++, TrainingDayType.Consolidation, 
                        "Naturlig flyt", exerciseIds.GetRange(0, Math.Min(2, exerciseIds.Count))));
                    break;
                    
                default:
                    // Default 4-session week
                    sessions.Add(CreateSession(weekStart, day++, TrainingDayType.Progression, "Pitch", exerciseIds.GetRange(0, 2)));
                    sessions.Add(CreateSession(weekStart, day++, TrainingDayType.Progression, "Resonans", exerciseIds.GetRange(0, 2)));
                    sessions.Add(CreateSession(weekStart, day++, TrainingDayType.Progression, "Prosodi", exerciseIds.GetRange(0, 2)));
                    sessions.Add(CreateSession(weekStart, day++, TrainingDayType.Consolidation, "Konsolidering", exerciseIds.GetRange(0, 2)));
                    break;
            }
            
            return sessions;
        }
        
        /// <summary>
        /// Creates a scheduled session.
        /// </summary>
        private ScheduledSession CreateSession(DateTime weekStart, int dayOffset, TrainingDayType type, string focus, List<int> exerciseIds)
        {
            return new ScheduledSession
            {
                Date = weekStart.AddDays(dayOffset),
                DayType = type,
                WeeklyExplanation = focus,
                RecommendedExerciseIds = exerciseIds
            };
        }
        
        /// <summary>
        /// Gets default exercise IDs for a complexity level.
        /// </summary>
        private List<int> GetDefaultExerciseIds(SpeechComplexityLevel level)
        {
            return level switch
            {
                SpeechComplexityLevel.IsolatedSounds => Enumerable.Range(1, 5).ToList(),
                SpeechComplexityLevel.Syllables => Enumerable.Range(6, 10).ToList(),
                SpeechComplexityLevel.Words => Enumerable.Range(16, 10).ToList(),
                SpeechComplexityLevel.Phrases => Enumerable.Range(26, 10).ToList(),
                SpeechComplexityLevel.StructuredSentences => Enumerable.Range(36, 10).ToList(),
                SpeechComplexityLevel.SpontaneousSpeech => Enumerable.Range(40, 5).ToList(),
                SpeechComplexityLevel.Conversational => Enumerable.Range(45, 5).ToList(),
                _ => Enumerable.Range(1, 10).ToList()
            };
        }
        
        /// <summary>
        /// Gets the next complexity level.
        /// </summary>
        private SpeechComplexityLevel GetNextLevel(SpeechComplexityLevel current)
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
        /// Adjusts weekly plan for strain detection.
        /// </summary>
        public WeeklySchedule AdjustForStrain(WeeklySchedule current, double strainLevel)
        {
            if (strainLevel < 30)
                return current;
            
            // Reduce load if strain detected
            var adjusted = new WeeklySchedule
            {
                WeekStartDate = current.WeekStartDate,
                WeekEndDate = current.WeekEndDate,
                TotalWeeklyLoad = Math.Max(20, current.TotalWeeklyLoad - 20),
                IsRecoveryWeek = strainLevel >= 50
            };
            
            adjusted.Sessions = current.Sessions.Select(s =>
            {
                if (s.DayType == TrainingDayType.Progression)
                {
                    return new ScheduledSession
                    {
                        Date = s.Date,
                        DayType = TrainingDayType.Light,
                        WeeklyExplanation = $"Redusert: {s.WeeklyExplanation}"
                    };
                }
                return s;
            }).ToList();
            
            // Update cycle
            if (strainLevel >= 50)
            {
                _currentCycle = PeriodizationCycle.Recovery;
            }
            
            return adjusted;
        }
        
        /// <summary>
        /// Filters exercises by complexity level.
        /// </summary>
        public List<int> FilterExercisesByComplexity(List<int> allExerciseIds, SpeechComplexityLevel level)
        {
            return _complexityEngine?.GetExerciseIdsForComplexity(level, false) 
                ?? GetDefaultExerciseIds(level);
        }
        
        private WeeklySchedule CreateSchedule(DateTime start, List<ScheduledSession> sessions)
        {
            return new WeeklySchedule
            {
                WeekStartDate = start,
                WeekEndDate = start.AddDays(6),
                Sessions = sessions,
                IsRecoveryWeek = _currentCycle == PeriodizationCycle.Recovery,
                TotalWeeklyLoad = _currentLoad
            };
        }
    }
}
