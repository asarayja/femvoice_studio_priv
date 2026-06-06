using System;
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FemVoiceStudio.Data;
using FemVoiceStudio.Services;
using FemVoiceStudio.Views;

namespace FemVoiceStudio.ViewModels
{
    /// <summary>
    /// ViewModel for treningskalenderen.
    /// Viser fullførte økter per dag med visuelle indikatorer og statistikk.
    /// </summary>
    public partial class CalendarViewModel : ObservableObject
    {
        private readonly DatabaseService _database;
        
        [ObservableProperty]
        private DateTime _currentMonth;
        
        [ObservableProperty]
        private string _monthYearText = "";
        
        [ObservableProperty]
        private ObservableCollection<CalendarDay> _days = new();
        
        // Summary properties for month statistics
        [ObservableProperty]
        private int _totalSessions;
        
        [ObservableProperty]
        private double _averageScore;
        
        [ObservableProperty]
        private string _mostImprovedParameter = "";
        
        [ObservableProperty]
        private int _totalTrainingMinutes;
        
        [ObservableProperty]
        private CalendarDay? _selectedDay;
        
        public CalendarViewModel()
        {
            _database = new DatabaseService();
            _currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            LoadCalendar();
        }
        
        /// <summary>
        /// Laster kalenderdata for inneværende måned.
        /// Beregner også statistikk og treningsanbefalinger.
        /// </summary>
        public void LoadCalendar()
        {
            Days.Clear();
            
            var firstDayOfMonth = new DateTime(CurrentMonth.Year, CurrentMonth.Month, 1);
            var daysInMonth = DateTime.DaysInMonth(CurrentMonth.Year, CurrentMonth.Month);
            
            // Hent data fra database
            var calendarData = _database.GetCalendarData(CurrentMonth.Year, CurrentMonth.Month);
            
            // Hent detaljert data for statistikk
            var detailedData = _database.GetDetailedSessionsForMonth(CurrentMonth.Year, CurrentMonth.Month);
            
            MonthYearText = CurrentMonth.ToString("MMMM yyyy", LocalizationService.Instance.CurrentCulture);
            
            // Fyll tomme dager før første dag i måneden
            int startDayOfWeek = (int)firstDayOfMonth.DayOfWeek;
            for (int i = 0; i < startDayOfWeek; i++)
            {
                Days.Add(new CalendarDay { IsCurrentMonth = false });
            }
            
            // Fyll faktiske dager med ny intensitetsformel
            // Intensity = (AverageScore / 100) × min(1, SessionCount / 2)
            for (int day = 1; day <= daysInMonth; day++)
            {
                var date = new DateTime(CurrentMonth.Year, CurrentMonth.Month, day);
                bool hasData = calendarData.TryGetValue(date, out var data);
                
                double intensity = 0;
                if (hasData && data != null && data.Sessions > 0)
                {
                    intensity = (data.Score / 100.0) * Math.Min(1.0, data.Sessions / 2.0);
                }
                
                Days.Add(new CalendarDay
                {
                    Date = date,
                    DayNumber = day,
                    IsCurrentMonth = true,
                    IsToday = date.Date == DateTime.Today,
                    HasSessions = hasData && data != null && data.Sessions > 0,
                    SessionCount = hasData && data != null ? data.Sessions : 0,
                    TotalMinutes = hasData && data != null ? data.Minutes : 0,
                    AverageScore = hasData && data != null ? data.Score : 0,
                    Intensity = intensity,
                    AveragePitchScore = hasData && data != null ? data.PitchScore : 0,
                    AverageResonanceScore = hasData && data != null ? data.ResonanceScore : 0
                });
            }
            
            // Beregn treningsanbefalinger basert på datoer
            CalculateTrainingRecommendations(firstDayOfMonth, daysInMonth);
            
            // Beregn månedssammendrag
            CalculateMonthlySummary(detailedData);
        }
        
        /// <summary>
        /// Beregner treningsanbefalinger for hver dag i måneden.
        /// </summary>
        private void CalculateTrainingRecommendations(DateTime firstDayOfMonth, int daysInMonth)
        {
            // Finn forrige treningsdag (før inneværende måned)
            DateTime? lastTrainingDate = null;
            var previousMonthData = _database.GetCalendarData(
                firstDayOfMonth.AddMonths(-1).Year, 
                firstDayOfMonth.AddMonths(-1).Month);
            
            foreach (var kvp in previousMonthData)
            {
                if (kvp.Value.Sessions > 0)
                {
                    if (lastTrainingDate == null || kvp.Key > lastTrainingDate)
                        lastTrainingDate = kvp.Key;
                }
            }
            
            // Sjekk også tidligere i inneværende måned
            foreach (var day in Days)
            {
                if (day.HasSessions)
                {
                    if (lastTrainingDate == null || day.Date > lastTrainingDate)
                        lastTrainingDate = day.Date;
                }
            }
            
            // Marker anbefalte dager og hviledager
            foreach (var day in Days)
            {
                if (!day.IsCurrentMonth || day.HasSessions)
                    continue;
                    
                if (lastTrainingDate.HasValue)
                {
                    var daysSinceLastSession = (day.Date.Date - lastTrainingDate.Value.Date).Days;
                    
                    // Anbefalt treningsdag: 1-2 dager siden forrige økt
                    day.IsRecommendedTrainingDay = daysSinceLastSession >= 1 && daysSinceLastSession <= 2;
                    
                    // Hviledag: samme dag eller dagen før
                    day.IsRestDay = daysSinceLastSession <= 1;
                }
            }
        }
        
        /// <summary>
        /// Beregner månedssammendrag basert på detaljerte data.
        /// </summary>
        private void CalculateMonthlySummary(List<Models.TrainingSession> sessions)
        {
            if (sessions == null || sessions.Count == 0)
            {
                TotalSessions = 0;
                AverageScore = 0;
                MostImprovedParameter = LocalizationService.Instance["Calendar_NoData"];
                TotalTrainingMinutes = 0;
                return;
            }
            
            // Beregn totaler
            TotalSessions = sessions.Count;
            TotalTrainingMinutes = sessions.Sum(s => s.EndTime.HasValue ? (int)(s.EndTime.Value - s.StartTime).TotalMinutes : 0);
            
            // Gjennomsnittlig score
            AverageScore = sessions.Average(s => s.OverallScore);
            
            // Finn mest forbedret parameter
            double avgPitch = sessions.Average(s => s.AveragePitch);
            double avgResonance = sessions.Average(s => s.ResonanceScore);
            double avgConsistency = sessions.Average(s => s.PitchVariation);
            
            // Bestem mest forbedret basert på relative verdier
            // (Dette er en forenkling - i virkeligheten ville man sammenlignet med forrige måned)
            if (avgResonance > 0 && avgResonance > avgConsistency && avgResonance > avgPitch)
            {
                MostImprovedParameter = LocalizationService.Instance["Dashboard_Resonance"];
            }
            else if (avgConsistency > 70)
            {
                MostImprovedParameter = LocalizationService.Instance["Calendar_Consistency"];
            }
            else if (avgPitch > 0)
            {
                MostImprovedParameter = LocalizationService.Instance["Dashboard_Pitch"];
            }
            else
            {
                MostImprovedParameter = LocalizationService.Instance["Calendar_NoData"];
            }
        }
        
        /// <summary>
        /// Går til forrige måned.
        /// </summary>
        [RelayCommand]
        public void PreviousMonth()
        {
            CurrentMonth = CurrentMonth.AddMonths(-1);
            LoadCalendar();
        }
        
        /// <summary>
        /// Går til neste måned.
        /// </summary>
        [RelayCommand]
        public void NextMonth()
        {
            CurrentMonth = CurrentMonth.AddMonths(1);
            LoadCalendar();
        }
        
        /// <summary>
        /// Går til inneværende måned.
        /// </summary>
        [RelayCommand]
        public void GoToToday()
        {
            CurrentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            LoadCalendar();
        }
        
        /// <summary>
        /// Viser detaljer for en valgt dag.
        /// </summary>
        [RelayCommand]
        public void ShowDayDetails(CalendarDay? day)
        {
            if (day == null || !day.IsCurrentMonth || !day.HasSessions)
                return;
            
            SelectedDay = day;
            
            // Hent detaljerte økt-data for denne dagen
            var sessions = _database.GetDetailedSessionsForDay(day.Date);
            
            // Opprett og vis vinduet
            var viewModel = new DayDetailsViewModel(day.Date, sessions);
            var window = new DayDetailsWindow
            {
                DataContext = viewModel,
                Owner = Application.Current.MainWindow
            };
            
            window.ShowDialog();
            
            SelectedDay = null;
        }
        
        /// <summary>
        /// Kaldes fra XAML når en dag klikkes.
        /// </summary>
        public void OnDayClicked(CalendarDay day)
        {
            if (day?.IsCurrentMonth == true)
            {
                ShowDayDetailsCommand.Execute(day);
            }
        }
    }
}
