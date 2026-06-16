using System;
using System.Collections.Generic;
using System.Linq;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Trend-analyse for pitch og progresjon
    /// </summary>
    public class TrendAnalysisService
    {
        /// <summary>
        /// Beregn glidende gjennomsnitt (Moving Average)
        /// </summary>
        public static double[] CalculateMovingAverage(double[] values, int windowSize)
        {
            if (values.Length < windowSize)
                return values;
            
            var result = new double[values.Length];
            
            for (int i = 0; i < values.Length; i++)
            {
                int start = Math.Max(0, i - windowSize + 1);
                int count = i - start + 1;
                result[i] = values.Skip(start).Take(count).Average();
            }
            
            return result;
        }
        
        /// <summary>
        /// Beregn trend-retning (positive = forbedring, negative = forverring)
        /// </summary>
        public static TrendResult AnalyzeTrend(List<double> values, int periods = 7)
        {
            if (values.Count < 2)
                return new TrendResult { Direction = TrendDirection.Flat, Strength = 0 };
            
            var recent = values.TakeLast(periods).ToList();
            var previous = values.Skip(Math.Max(0, values.Count - periods * 2)).Take(periods).ToList();
            
            if (previous.Count == 0)
                return new TrendResult { Direction = TrendDirection.Flat, Strength = 0 };
            
            double recentAvg = recent.Average();
            double previousAvg = previous.Average();
            
            double percentChange = previousAvg != 0 ? ((recentAvg - previousAvg) / previousAvg) * 100 : 0;
            
            return new TrendResult
            {
                Direction = percentChange > 2 ? TrendDirection.Improving : 
                           percentChange < -2 ? TrendDirection.Declining : 
                           TrendDirection.Flat,
                Strength = Math.Abs(percentChange),
                PercentChange = percentChange,
                RecentAverage = recentAvg,
                PreviousAverage = previousAvg
            };
        }
        
        /// <summary>
        /// Beregn pitch-konsistens (standard avvik som prosent av gjennomsnitt)
        /// </summary>
        public static double CalculateConsistency(List<double> pitches)
        {
            if (pitches.Count < 2) return 100;
            
            double avg = pitches.Average();
            double stdDev = CalculateStdDev(pitches, avg);
            
            double cv = avg != 0 ? stdDev / avg : 0;
            return Math.Max(0, Math.Min(100, (1 - cv) * 100));
        }
        
        /// <summary>
        /// Beregn ukentlig fremgang
        /// </summary>
        public static WeeklyProgress AnalyzeWeeklyProgress(
            List<(DateTime Date, double Score)> thisWeek,
            List<(DateTime Date, double Score)> lastWeek)
        {
            if (thisWeek.Count == 0 || lastWeek.Count == 0)
                return new WeeklyProgress { HasData = false };
            
            double thisWeekAvg = thisWeek.Average(s => s.Score);
            double lastWeekAvg = lastWeek.Average(s => s.Score);
            
            return new WeeklyProgress
            {
                HasData = true,
                ThisWeekAverage = thisWeekAvg,
                LastWeekAverage = lastWeekAvg,
                Change = thisWeekAvg - lastWeekAvg,
                ChangePercent = lastWeekAvg != 0 ? ((thisWeekAvg - lastWeekAvg) / lastWeekAvg) * 100 : 0,
                SessionsThisWeek = thisWeek.Count,
                IsImproving = thisWeekAvg > lastWeekAvg
            };
        }
        
        /// <summary>
        /// Analyser pitch-mønster (stigende, synkende, flat)
        /// </summary>
        public static PitchPatternResult AnalyzePitchPattern(List<double> pitches)
        {
            if (pitches.Count < 3)
                return new PitchPatternResult { Pattern = PitchPattern.Flat };
            
            // Sammenlign første og siste halvdel
            int half = pitches.Count / 2;
            var firstHalf = pitches.Take(half).ToList();
            var secondHalf = pitches.Skip(half).ToList();
            
            double firstAvg = firstHalf.Average();
            double secondAvg = secondHalf.Average();
            
            double change = secondAvg - firstAvg;
            double threshold = firstAvg * 0.05; // 5% endring
            
            return new PitchPatternResult
            {
                Pattern = change > threshold ? PitchPattern.Rising : 
                         change < -threshold ? PitchPattern.Falling : 
                         PitchPattern.Flat,
                ChangeAmount = change,
                ChangePercent = firstAvg != 0 ? (change / firstAvg) * 100 : 0
            };
        }
        
        private static double CalculateStdDev(List<double> values, double mean)
        {
            double sumSquares = values.Sum(v => Math.Pow(v - mean, 2));
            return Math.Sqrt(sumSquares / values.Count);
        }
        
        /// <summary>
        /// Beregn kalenderdata for heatmap
        /// </summary>
        public static List<CalendarDay> GenerateCalendarHeatmap(
            List<(DateTime Date, int Sessions, double Score)> data,
            int weeks = 12)
        {
            var result = new List<CalendarDay>();
            var today = DateTime.Today;
            var startDate = today.AddDays(-(weeks * 7));
            
            for (var date = startDate; date <= today; date = date.AddDays(1))
            {
                var dayData = data.FirstOrDefault(d => d.Date.Date == date.Date);
                
                result.Add(new CalendarDay
                {
                    Date = date,
                    Sessions = dayData.Sessions,
                    Score = dayData.Score,
                    Intensity = dayData.Sessions > 0 ? Math.Min(1.0, dayData.Score / 100.0) : 0
                });
            }
            
            return result;
        }
    }
    
    public enum TrendDirection { Improving, Flat, Declining }
    
    public enum PitchPattern { Rising, Falling, Flat }
    
    public class TrendResult
    {
        public TrendDirection Direction { get; set; }
        public double Strength { get; set; }
        public double PercentChange { get; set; }
        public double RecentAverage { get; set; }
        public double PreviousAverage { get; set; }
    }
    
    public class WeeklyProgress
    {
        public bool HasData { get; set; }
        public double ThisWeekAverage { get; set; }
        public double LastWeekAverage { get; set; }
        public double Change { get; set; }
        public double ChangePercent { get; set; }
        public int SessionsThisWeek { get; set; }
        public bool IsImproving { get; set; }
        // Ekstra properties for TrainingFrequencyService
        public int TotalSessionsWeek { get; set; }
        public int TotalMinutesWeek { get; set; }
        public int ExercisesCompleted { get; set; }
    }
    
    public class PitchPatternResult
    {
        public PitchPattern Pattern { get; set; }
        public double ChangeAmount { get; set; }
        public double ChangePercent { get; set; }
    }
    
    public class CalendarDay
    {
        public DateTime Date { get; set; }
        public int Sessions { get; set; }
        public double Score { get; set; }
        public double Intensity { get; set; }
    }
}
