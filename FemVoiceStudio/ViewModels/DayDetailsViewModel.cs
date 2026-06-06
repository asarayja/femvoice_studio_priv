using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;

namespace FemVoiceStudio.ViewModels
{
    /// <summary>
    /// ViewModel for displaying detailed training session data for a specific day.
    /// Shows individual sessions with pitch and resonance scores.
    /// </summary>
    public partial class DayDetailsViewModel : ObservableObject
    {
        [ObservableProperty]
        private DateTime _selectedDate;

        [ObservableProperty]
        private string _dateDisplayText = "";

        [ObservableProperty]
        private int _totalSessions;

        [ObservableProperty]
        private int _totalMinutes;

        [ObservableProperty]
        private double _averageScore;

        [ObservableProperty]
        private double _averagePitch;

        [ObservableProperty]
        private double _averageResonance;

        [ObservableProperty]
        private double _bestScore;

        [ObservableProperty]
        private ObservableCollection<SessionDetailItem> _sessions = new();

        [ObservableProperty]
        private SessionDetailItem? _selectedSession;

        public DayDetailsViewModel(DateTime date, List<TrainingSession> sessions)
        {
            SelectedDate = date;
            LoadSessions(sessions);
        }

        private void LoadSessions(List<TrainingSession> sessions)
        {
            DateDisplayText = SelectedDate.ToString("dddd, d. MMMM yyyy", LocalizationService.Instance.CurrentCulture);

            if (sessions == null || sessions.Count == 0)
            {
                TotalSessions = 0;
                TotalMinutes = 0;
                AverageScore = 0;
                AveragePitch = 0;
                AverageResonance = 0;
                BestScore = 0;
                return;
            }

            // Calculate totals
            TotalSessions = sessions.Count;
            TotalMinutes = (int)sessions.Sum(s => s.DurationSeconds / 60.0);
            AverageScore = sessions.Average(s => s.OverallScore);
            AveragePitch = sessions.Average(s => s.AveragePitch);
            AverageResonance = sessions.Average(s => s.ResonanceScore);
            BestScore = sessions.Max(s => s.OverallScore);

            // Create session items
            Sessions.Clear();
            foreach (var session in sessions.OrderByDescending(s => s.StartTime))
            {
                Sessions.Add(new SessionDetailItem
                {
                    SessionId = session.Id,
                    StartTime = session.StartTime,
                    EndTime = session.EndTime,
                    DurationMinutes = session.DurationSeconds / 60,
                    Score = session.OverallScore,
                    Pitch = session.AveragePitch,
                    MinPitch = session.MinPitch,
                    MaxPitch = session.MaxPitch,
                    ResonanceScore = session.ResonanceScore,
                    F1 = session.AverageF1,
                    F2 = session.AverageF2,
                    F3 = session.AverageF3,
                    IntonationScore = session.IntonationScore,
                    Feedback = session.Feedback,
                    DifficultyLevel = session.DifficultyLevel
                });
            }
        }

        [RelayCommand]
        private void Close()
        {
            // This will be handled by the window's Close method
        }
    }

    /// <summary>
    /// Represents a single training session detail for display in the day details view.
    /// </summary>
    public class SessionDetailItem
    {
        public int SessionId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int DurationMinutes { get; set; }
        public double Score { get; set; }
        public double Pitch { get; set; }
        public double MinPitch { get; set; }
        public double MaxPitch { get; set; }
        public double ResonanceScore { get; set; }
        public double F1 { get; set; }
        public double F2 { get; set; }
        public double F3 { get; set; }
        public double IntonationScore { get; set; }
        public string Feedback { get; set; } = "";
        public DifficultyLevel DifficultyLevel { get; set; }

        public string TimeDisplay => StartTime.ToString("HH:mm");
        
        public string DurationDisplay => DurationMinutes > 0 
            ? LocalizationService.Instance.GetFormattedString("DayDetails_DurationMinutesFormat", DurationMinutes)
            : "-";

        public string ScoreDisplay => $"{Math.Round(Score)}%";

        public string PitchDisplay => Pitch > 0 
            ? $"{Math.Round(Pitch)} Hz" 
            : "-";

        public string ResonanceDisplay => ResonanceScore > 0 
            ? $"{Math.Round(ResonanceScore)}%" 
            : "-";

        public string FormantDisplay
        {
            get
            {
                if (F1 > 0 || F2 > 0 || F3 > 0)
                {
                    return LocalizationService.Instance.GetFormattedString("DayDetails_FormantsFormat", Math.Round(F1), Math.Round(F2), Math.Round(F3));
                }
                return "-";
            }
        }

        public string DifficultyDisplay => DifficultyLevel switch
        {
            DifficultyLevel.Nybegynner => FemVoiceStudio.Services.LocalizationService.Instance.GetString("Difficulty_Beginner"),
            DifficultyLevel.Middels => FemVoiceStudio.Services.LocalizationService.Instance.GetString("Difficulty_Intermediate"),
            DifficultyLevel.Avansert => FemVoiceStudio.Services.LocalizationService.Instance.GetString("Difficulty_Advanced"),
            _ => "-"
        };
    }
}
