using System;
using System.Text;
using FemVoiceStudio.Services;

namespace FemVoiceStudio.ViewModels
{
    /// <summary>
    /// Represents a single day in the training calendar.
    /// Contains data about sessions, scores, and training recommendations.
    /// </summary>
    /// <remarks>
    /// Intensity is calculated using the formula:
    /// Intensity = (AverageScore / 100) × min(1, SessionCount / 2)
    /// This ensures that quality (AverageScore) is weighted equally with activity (SessionCount).
    /// </remarks>
    public class CalendarDay
    {
        /// <summary>
        /// The date for this calendar day.
        /// </summary>
        public DateTime Date { get; set; }
        
        /// <summary>
        /// The day number (1-31). 0 for empty placeholder days.
        /// </summary>
        public int DayNumber { get; set; }
        
        /// <summary>
        /// True if this day belongs to the current month being displayed.
        /// </summary>
        public bool IsCurrentMonth { get; set; }
        
        /// <summary>
        /// True if this day is today's date.
        /// </summary>
        public bool IsToday { get; set; }
        
        /// <summary>
        /// True if there are training sessions for this day.
        /// </summary>
        public bool HasSessions { get; set; }
        
        /// <summary>
        /// Number of training sessions completed on this day.
        /// </summary>
        public int SessionCount { get; set; }
        
        /// <summary>
        /// Total minutes trained on this day.
        /// </summary>
        public int TotalMinutes { get; set; }
        
        /// <summary>
        /// Average score across all sessions for this day (0-100).
        /// </summary>
        public double AverageScore { get; set; }
        
        /// <summary>
        /// Training intensity based on both quality and activity.
        /// Formula: (AverageScore / 100) × min(1, SessionCount / 2)
        /// </summary>
        /// <remarks>
        /// - Low score (0-50): Low intensity regardless of session count
        /// - Medium score (50-75): Moderate intensity
        /// - High score (75-100): High intensity
        /// </remarks>
        public double Intensity { get; set; }
        
        /// <summary>
        /// True if this is a recommended training day.
        /// Calculated as: last session was 1-2 days ago.
        /// </summary>
        /// <remarks>
        /// Uses simple logic: recommended if not trained today or yesterday,
        /// but trained within the last 7 days.
        /// </remarks>
        public bool IsRecommendedTrainingDay { get; set; }
        
        /// <summary>
        /// True if this is a rest day (trained today or yesterday).
        /// </summary>
        public bool IsRestDay { get; set; }
        
        /// <summary>
        /// Average pitch score for this day (0-100).
        /// </summary>
        public double AveragePitchScore { get; set; }
        
        /// <summary>
        /// Average resonance score for this day (0-100).
        /// </summary>
        public double AverageResonanceScore { get; set; }
        
        /// <summary>
        /// Display text for the day number.
        /// </summary>
        public string DayText => DayNumber > 0 ? DayNumber.ToString() : "";
        
        /// <summary>
        /// Formatted tooltip showing day details.
        /// </summary>
        public string ToolTip
        {
            get
            {
                if (!IsCurrentMonth)
                    return "";
                    
                if (HasSessions)
                {
                    var toolTip = new StringBuilder();
                    toolTip.AppendLine(LocalizationService.Instance.GetFormattedString(
                        "Calendar_TooltipSessionsFormat", Date.ToString("d"), SessionCount, TotalMinutes));
                    toolTip.Append(LocalizationService.Instance.GetFormattedString(
                        "Calendar_TooltipScoreFormat", Math.Round(AverageScore)));
                    
                    if (AveragePitchScore > 0 || AverageResonanceScore > 0)
                    {
                        toolTip.AppendLine();
                        if (AveragePitchScore > 0)
                            toolTip.Append(LocalizationService.Instance.GetFormattedString(
                                "Calendar_TooltipPitchFormat", Math.Round(AveragePitchScore)));
                        if (AveragePitchScore > 0 && AverageResonanceScore > 0)
                            toolTip.Append(" | ");
                        if (AverageResonanceScore > 0)
                            toolTip.Append(LocalizationService.Instance.GetFormattedString(
                                "Calendar_TooltipResonanceFormat", Math.Round(AverageResonanceScore)));
                    }
                    
                    return toolTip.ToString();
                }
                
                if (IsToday)
                    return LocalizationService.Instance["Calendar_Today"];
                if (IsRecommendedTrainingDay)
                    return LocalizationService.Instance.GetFormattedString("Calendar_RecommendedTrainingDayFormat", Date.ToString("d"));
                if (IsRestDay)
                    return LocalizationService.Instance.GetFormattedString("Calendar_RestDayFormat", Date.ToString("d"));
                    
                return Date.ToString("d");
            }
        }
        
        /// <summary>
        /// Gets the CSS-style color for intensity display.
        /// Used for heatmap visualization.
        /// </summary>
        public string IntensityColor
        {
            get
            {
                if (Intensity <= 0 || !HasSessions)
                    return "#F5F5F5"; // Light gray for no activity
                    
                double score = AverageScore;
                
                if (score < 50)
                {
                    // Low score: red spectrum
                    double intensity = Intensity * 0.5;
                    int red = (int)(255 * (0.3 + intensity * 0.7));
                    int green = (int)(50 * intensity);
                    int blue = (int)(50 * intensity);
                    return $"#{red:X2}{green:X2}{blue:X2}";
                }
                else if (score < 75)
                {
                    // Medium score: yellow/orange spectrum
                    double intensity = Intensity;
                    int red = 255;
                    int green = (int)(200 * intensity);
                    int blue = (int)(50 * intensity);
                    return $"#{red:X2}{green:X2}{blue:X2}";
                }
                else
                {
                    // High score: green spectrum
                    double intensity = Intensity;
                    int red = (int)(50 * (1 - intensity));
                    int green = (int)(200 * (0.3 + intensity * 0.7));
                    int blue = (int)(50 + 100 * intensity);
                    return $"#{red:X2}{green:X2}{blue:X2}";
                }
            }
        }
    }
}
