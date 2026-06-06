using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Models;
using FemVoiceStudio.Data;
using FemVoiceStudio.Services;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// In-memory database implementation for unit testing.
    /// Implements IDatabaseService for testability without database dependencies.
    /// </summary>
    public class TestDatabaseService : IDatabaseService
    {
        private readonly List<TrainingSession> _sessions = new();
        private readonly List<SmartCoachHealthMonitoring> _healthMonitoring = new();
        private readonly List<SmartCoachBaseline> _baselines = new();
        private readonly List<SmartCoachGoal> _goals = new();
        private readonly List<SmartCoachDailyRecommendation> _dailyRecommendations = new();
        private readonly List<SmartCoachMessage> _messages = new();
        private readonly List<SmartCoachWeeklyProgress> _weeklyProgress = new();
        private readonly List<FemVoiceScoreResult> _femVoiceScores = new();
        private readonly List<AchievementData> _achievements = new();
        private readonly List<Milestone> _milestones = new();
        private readonly List<DailyProgressEntry> _dailyProgress = new();
        
        private UserSettings _userSettings = new();
        private UserProgressData _userProgress = new();
        private ComplexityProgress? _complexityProgress;
        
        private int _sessionIdCounter = 1;
        
        public TestDatabaseService() 
        {
            // Initialize with default achievements
            InitializeDefaultAchievements();
        }
        
        private void InitializeDefaultAchievements()
        {
            _achievements.AddRange(new[]
            {
                new AchievementData { Code = "FIRST_SESSION", Name = "Første økt", Description = "Fullfør din første treningsøkt", Icon = "🎯", Category = "progress", XPReward = 10, IsUnlocked = false },
                new AchievementData { Code = "SESSION_10", Name = "Ti-er", Description = "Fullfør 10 økter", Icon = "⭐", Category = "progress", XPReward = 25, IsUnlocked = false },
                new AchievementData { Code = "WEEK_STREAK", Name = "Ukeholder", Description = "7 dager på rad", Icon = "🔥", Category = "streak", XPReward = 50, IsUnlocked = false },
                new AchievementData { Code = "MONTH_STREAK", Name = "Månedsmester", Description = "30 dager på rad", Icon = "💎", Category = "streak", XPReward = 200, IsUnlocked = false },
            });
        }
        
        #region Training Sessions
        
        public int SaveTrainingSession(TrainingSession session)
        {
            session.Id = _sessionIdCounter++;
            _sessions.Add(session);
            return session.Id;
        }
        
        public void UpdateTrainingSession(TrainingSession session)
        {
            var existing = _sessions.FirstOrDefault(s => s.Id == session.Id);
            if (existing != null)
            {
                _sessions.Remove(existing);
            }
            _sessions.Add(session);
        }
        
        public void DeleteTrainingSession(int sessionId)
        {
            var session = _sessions.FirstOrDefault(s => s.Id == sessionId);
            if (session != null)
            {
                _sessions.Remove(session);
            }
        }
        
        public List<TrainingSession> GetRecentSessions(int count = 10, int userId = 1)
        {
            return _sessions
                .Where(s => s.UserId == userId || s.UserId == 0)
                .OrderByDescending(s => s.StartTime)
                .Take(count)
                .ToList();
        }
        
        public List<TrainingSession> GetTrainingSessions(DateTime from, DateTime to)
        {
            return _sessions
                .Where(s => s.StartTime >= from && s.StartTime <= to)
                .OrderByDescending(s => s.StartTime)
                .ToList();
        }
        
        public int GetTrainingDaysCount(DateTime from, DateTime to, int userId = 1)
        {
            return _sessions
                .Where(s => s.StartTime >= from && s.StartTime <= to)
                .Select(s => s.StartTime.Date)
                .Distinct()
                .Count();
        }
        
        public (int Sessions, int Minutes, double AvgScore, double AvgPitch, double AvgResonance, double AvgIntonation) GetTrainingStats(DateTime from, DateTime to, int userId = 1)
        {
            var sessions = _sessions
                .Where(s => s.StartTime >= from && s.StartTime <= to && (s.UserId == userId || s.UserId == 0))
                .ToList();
            
            if (!sessions.Any())
                return (0, 0, 0, 0, 0, 0);

            var performanceSessions = sessions.Where(s => !s.IsRecoveryPractice).ToList();
            
            return (
                Sessions: sessions.Count,
                Minutes: sessions.Sum(s => s.DurationSeconds / 60),
                AvgScore: performanceSessions.Any() ? performanceSessions.Average(s => s.OverallScore) : 0,
                AvgPitch: performanceSessions.Any() ? performanceSessions.Average(s => s.AveragePitch) : 0,
                AvgResonance: performanceSessions.Any() ? performanceSessions.Average(s => s.ResonanceScore) : 0,
                AvgIntonation: performanceSessions.Any() ? performanceSessions.Average(s => s.IntonationScore) : 0
            );
        }
        
        public void AddTrainingSession(TrainingSession session)
        {
            SaveTrainingSession(session);
        }
        
        public List<TrainingSession> GetAllSessions()
        {
            return _sessions.ToList();
        }
        
        public void ClearSessions()
        {
            _sessions.Clear();
        }
        
        #endregion
        
        #region User Settings
        
        public UserSettings GetUserSettings()
        {
            return _userSettings;
        }
        
        public void UpdateUserSettings(UserSettings settings)
        {
            _userSettings = settings;
        }
        
        #endregion
        
        #region Progression Stats
        
        public (double AvgPitch, double Consistency, int Streak) GetProgressionStats()
        {
            var recentSessions = _sessions
                .Where(s => s.StartTime >= DateTime.Now.AddDays(-30))
                .ToList();
            
            if (!recentSessions.Any())
            {
                return (0, 0, 0);
            }
            
            var avgPitch = recentSessions.Average(s => s.AveragePitch);
            var consistency = recentSessions.Average(s => s.PitchVariation);
            
            int streak = 0;
            var checkDate = DateTime.Today;
            while (true)
            {
                var hasSession = _sessions.Any(s => s.StartTime.Date == checkDate.Date);
                if (hasSession)
                {
                    streak++;
                    checkDate = checkDate.AddDays(-1);
                }
                else if (checkDate == DateTime.Today)
                {
                    checkDate = checkDate.AddDays(-1);
                }
                else
                {
                    break;
                }
            }
            
            return (avgPitch, consistency, streak);
        }
        
        #endregion
        
        #region Health Monitoring
        
        public List<SmartCoachHealthMonitoring> GetRecentHealthIssues(int userId = 1, int days = 7)
        {
            var cutoff = DateTime.Now.AddDays(-days);
            return _healthMonitoring
                .Where(h => h.Date >= cutoff)
                .ToList();
        }
        
        public void SaveHealthMonitoring(SmartCoachHealthMonitoring health)
        {
            _healthMonitoring.Add(health);
        }
        
        #endregion
        
        #region SmartCoach - Baseline
        
        public void SaveSmartCoachBaseline(SmartCoachBaseline baseline)
        {
            var existing = _baselines.FirstOrDefault(b => b.UserId == baseline.UserId);
            if (existing != null)
            {
                _baselines.Remove(existing);
            }
            _baselines.Add(baseline);
        }
        
        public SmartCoachBaseline? GetSmartCoachBaseline(int userId = 1)
        {
            return _baselines.FirstOrDefault(b => b.UserId == userId);
        }
        
        #endregion
        
        #region SmartCoach - Goals
        
        public void SaveSmartCoachGoal(SmartCoachGoal goal)
        {
            var existing = _goals.FirstOrDefault(g => g.UserId == goal.UserId && g.GoalType == goal.GoalType);
            if (existing != null)
            {
                _goals.Remove(existing);
            }
            _goals.Add(goal);
        }
        
        public List<SmartCoachGoal> GetSmartCoachGoals(int userId = 1, bool activeOnly = true)
        {
            return _goals.Where(g => g.UserId == userId).ToList();
        }
        
        #endregion
        
        #region SmartCoach - Daily Recommendations
        
        public void SaveDailyRecommendation(SmartCoachDailyRecommendation recommendation)
        {
            var existing = _dailyRecommendations.FirstOrDefault(r => 
                r.Date.Date == recommendation.Date.Date && r.UserId == recommendation.UserId);
            
            if (existing != null)
            {
                _dailyRecommendations.Remove(existing);
            }
            _dailyRecommendations.Add(recommendation);
        }
        
        public SmartCoachDailyRecommendation? GetDailyRecommendation(DateTime date, int userId = 1)
        {
            return _dailyRecommendations.FirstOrDefault(r => 
                r.Date.Date == date.Date && r.UserId == userId);
        }
        
        #endregion
        
        #region SmartCoach - Weekly Progress
        
        public void SaveWeeklyProgress(SmartCoachWeeklyProgress progress)
        {
            var existing = _weeklyProgress.FirstOrDefault(w => 
                w.UserId == progress.UserId && w.WeekStart == progress.WeekStart);
            
            if (existing != null)
            {
                _weeklyProgress.Remove(existing);
            }
            _weeklyProgress.Add(progress);
        }
        
        public SmartCoachWeeklyProgress? GetWeeklyProgress(DateTime weekStart, int userId = 1)
        {
            return _weeklyProgress.FirstOrDefault(w => w.WeekStart == weekStart && w.UserId == userId);
        }
        
        public List<SmartCoachWeeklyProgress> GetRecentWeeklyProgress(int weeks = 4, int userId = 1)
        {
            return _weeklyProgress
                .Where(w => w.UserId == userId)
                .OrderByDescending(w => w.WeekStart)
                .Take(weeks)
                .ToList();
        }
        
        #endregion
        
        #region SmartCoach - Messages
        
        public void SaveCoachMessage(SmartCoachMessage message)
        {
            _messages.Add(message);
        }

        public List<SmartCoachMessage> GetUnreadMessages(int userId = 1)
        {
            return _messages.Where(m => m.UserId == userId && !m.IsRead).ToList();
        }
        
        public int GetUnreadMessageCount(int userId = 1)
        {
            return _messages.Count(m => m.UserId == userId && !m.IsRead);
        }
        
        public List<SmartCoachMessage> GetMessages(int userId = 1, int count = 10)
        {
            return _messages
                .Where(m => m.UserId == userId)
                .OrderByDescending(m => m.CreatedAt)
                .Take(count)
                .ToList();
        }
        
        public void MarkMessageAsRead(int messageId)
        {
            var message = _messages.FirstOrDefault(m => m.Id == messageId);
            if (message != null)
            {
                message.IsRead = true;
            }
        }
        
        #endregion
        
        #region FemVoiceScore
        
        public void SaveFemVoiceScore(FemVoiceScoreResult score)
        {
            _femVoiceScores.Add(score);
        }
        
        public List<FemVoiceScoreResult> GetRecentFemVoiceScores(int count = 10, int userId = 1)
        {
            return _femVoiceScores
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.CalculatedAt)
                .Take(count)
                .ToList();
        }
        
        public List<FemVoiceScoreResult> GetFemVoiceScores(DateTime from, DateTime to, int userId = 1)
        {
            return _femVoiceScores
                .Where(s => s.UserId == userId && s.CalculatedAt >= from && s.CalculatedAt <= to)
                .OrderByDescending(s => s.CalculatedAt)
                .ToList();
        }
        
        #endregion
        
        #region Complexity Progress
        
        public ComplexityProgress? GetComplexityProgress(int userId = 1)
        {
            return _complexityProgress;
        }
        
        public void SaveComplexityProgress(ComplexityProgress progress)
        {
            _complexityProgress = progress;
        }
        
        public void InitializeComplexityProgress(int userId = 1)
        {
            _complexityProgress = new ComplexityProgress
            {
                UserId = userId,
                CurrentLevel = 0,
                SessionsAtLevel = 0,
                SuccessRate = 0,
                LastEvaluationDate = DateTime.Now.ToString("yyyy-MM-dd"),
                IsReadyForNext = false
            };
        }
        
        #endregion
        
        #region User Progress (Gamification)
        
        public UserProgressData GetUserProgress()
        {
            return _userProgress;
        }
        
        public void UpdateUserProgress(UserProgressData progress)
        {
            _userProgress = progress;
        }
        
        public void UnlockAchievement(string code)
        {
            var achievement = _achievements.FirstOrDefault(a => a.Code == code && !a.IsUnlocked);
            if (achievement != null)
            {
                achievement.IsUnlocked = true;
                achievement.UnlockedAt = DateTime.Now;
            }
        }
        
        public List<AchievementData> GetAllAchievements(int userId = 1)
        {
            return _achievements.ToList();
        }
        
        #endregion
        
        #region Milestones and Daily Progress
        
        public void SaveMilestone(Milestone milestone)
        {
            var existing = _milestones.FirstOrDefault(m => m.UserId == milestone.UserId && m.MilestoneType == milestone.MilestoneType);
            if (existing != null)
            {
                _milestones.Remove(existing);
            }
            _milestones.Add(milestone);
        }
        
        public List<Milestone> GetMilestones(int userId = 1)
        {
            return _milestones.Where(m => m.UserId == userId).ToList();
        }
        
        public void SaveDailyProgress(DailyProgressEntry progress)
        {
            var existing = _dailyProgress.FirstOrDefault(d => 
                d.UserId == progress.UserId && d.Date.Date == progress.Date.Date);
            
            if (existing != null)
            {
                _dailyProgress.Remove(existing);
            }
            _dailyProgress.Add(progress);
        }
        
        public List<DailyProgressEntry> GetDailyProgress(DateTime from, DateTime to, int userId = 1)
        {
            return _dailyProgress
                .Where(d => d.UserId == userId && d.Date >= from && d.Date <= to)
                .OrderByDescending(d => d.Date)
                .ToList();
        }
        
        #endregion
        
        #region Calendar
        
        public void SaveCalendarData(DateTime date, int sessionsCompleted, int totalMinutes, double averageScore)
        {
            // Calendar data stored in daily progress
            var progress = new DailyProgressEntry
            {
                UserId = 1,
                Date = date,
                SessionsCompleted = sessionsCompleted,
                SessionMinutes = totalMinutes,
                FemVoiceScore = averageScore
            };
            SaveDailyProgress(progress);
        }
        
        #endregion
        
        #region Test Helpers
        
        public void SetSmartCoachBaseline(SmartCoachBaseline baseline)
        {
            var existing = _baselines.FirstOrDefault(b => b.UserId == baseline.UserId);
            if (existing != null)
            {
                _baselines.Remove(existing);
            }
            _baselines.Add(baseline);
        }
        
        public void AddHealthMonitoring(SmartCoachHealthMonitoring health)
        {
            _healthMonitoring.Add(health);
        }
        
        public void Clear()
        {
            _sessions.Clear();
            _healthMonitoring.Clear();
            _baselines.Clear();
            _goals.Clear();
            _dailyRecommendations.Clear();
            _messages.Clear();
            _weeklyProgress.Clear();
            _femVoiceScores.Clear();
            _milestones.Clear();
            _dailyProgress.Clear();
            _complexityProgress = null;
            _userSettings = new UserSettings();
            _userProgress = new UserProgressData();
            _sessionIdCounter = 1;
        }
        
        #endregion
    }
}
