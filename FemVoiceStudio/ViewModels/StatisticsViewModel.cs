using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FemVoiceStudio.Data;
using FemVoiceStudio.Models;
using FemVoiceStudio.Converters;

namespace FemVoiceStudio.ViewModels
{
    /// <summary>
    /// ViewModel for statistikk og progresjonsvisning.
    /// </summary>
    public partial class StatisticsViewModel : ObservableObject
    {
        private readonly DatabaseService _database;
        
        [ObservableProperty]
        private int _totalSessions;
        
        [ObservableProperty]
        private int _currentStreak;
        
        [ObservableProperty]
        private double _averagePitch;
        
        [ObservableProperty]
        private double _averageScore;
        
        [ObservableProperty]
        private string _currentLevel = "";
        
        [ObservableProperty]
        private double _progressToNextLevel;
        
        [ObservableProperty]
        private ObservableCollection<SessionSummary> _recentSessions = new();
        
        public StatisticsViewModel()
        {
            // DatabaseService er DI-singleton; manuelle new re-kjørte skjema-init (integrasjonsaudit-funn).
            _database = App.Services?.GetService(typeof(DatabaseService)) as DatabaseService
                        ?? new DatabaseService();
            LoadStatistics();
        }
        
        public void LoadStatistics()
        {
            var stats = _database.GetProgressionStats();
            var settings = _database.GetUserSettings();
            
            TotalSessions = settings.TotalSessionsCompleted;
            CurrentStreak = stats.Streak;
            AveragePitch = stats.AvgPitch;
            CurrentLevel = GetDifficultyText(settings.CurrentDifficulty);
            
            // Beregn progress til neste nivå
            double sessionsAtLevel = settings.SessionsAtCurrentLevel;
            ProgressToNextLevel = Math.Min(100, sessionsAtLevel / 5.0 * 100);
            
            // Hent siste økter
            var sessions = _database.GetTrainingSessions(
                DateTime.Now.AddDays(-30),
                DateTime.Now);
                
            RecentSessions.Clear();
            foreach (var s in sessions.Take(10))
            {
                RecentSessions.Add(new SessionSummary
                {
                    Date = s.StartTime,
                    Score = s.OverallScore,
                    Duration = s.DurationSeconds,
                    Difficulty = GetDifficultyText(s.DifficultyLevel)
                });
            }
            
            // Beregn gjennomsnittlig score
            if (sessions.Count > 0)
            {
                double total = 0;
                foreach (var s in sessions)
                {
                    total += s.OverallScore;
                }
                AverageScore = total / sessions.Count;
            }
        }
        
        private static string GetDifficultyText(DifficultyLevel level)
        {
            return level switch
            {
                DifficultyLevel.Nybegynner => Loc.Difficulty_Beginner,
                DifficultyLevel.Middels => Loc.Difficulty_Intermediate,
                DifficultyLevel.Avansert => Loc.Difficulty_Advanced,
                _ => level.ToString()
            };
        }
    }
    
    public class SessionSummary
    {
        public DateTime Date { get; set; }
        public double Score { get; set; }
        public int Duration { get; set; }
        public string Difficulty { get; set; } = "";
        
        public string DateText => Date.ToString("dd.MM.yyyy HH:mm");
        public string DurationText => $"{Duration / 60}:{Duration % 60:D2}";
        public string ScoreText => $"{Score:F0}%";
    }
}
