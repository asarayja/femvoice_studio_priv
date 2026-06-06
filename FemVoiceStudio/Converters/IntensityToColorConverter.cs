using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace FemVoiceStudio.Converters
{
    /// <summary>
    /// Converts CalendarDay Intensity and AverageScore to a color for heatmap visualization.
    /// </summary>
    /// <remarks>
    /// Color mapping:
    /// - Low score (0-50): Red spectrum with intensity based on quality
    /// - Medium score (50-75): Yellow/orange spectrum
    /// - High score (75-100): Green spectrum
    /// </remarks>
    public class IntensityToColorConverter : IMultiValueConverter
    {
        /// <summary>
        /// Default colors for different score ranges.
        /// </summary>
        private static readonly SolidColorBrush NoActivityBrush = new(Color.FromRgb(245, 245, 245));
        private static readonly SolidColorBrush LowScoreBrush = new(Color.FromRgb(255, 100, 100));
        private static readonly SolidColorBrush MediumScoreBrush = new(Color.FromRgb(255, 200, 50));
        private static readonly SolidColorBrush HighScoreBrush = new(Color.FromRgb(100, 200, 100));
        
        static IntensityToColorConverter()
        {
            // Freeze brushes for performance
            NoActivityBrush.Freeze();
            LowScoreBrush.Freeze();
            MediumScoreBrush.Freeze();
            HighScoreBrush.Freeze();
        }
        
        /// <summary>
        /// Converts intensity and score to a color brush.
        /// </summary>
        /// <param name="values">[0] = Intensity (double), [1] = AverageScore (double), [2] = HasSessions (bool)</param>
        /// <param name="targetType">Target type (SolidColorBrush)</param>
        /// <param name="parameter">Optional parameter for customization</param>
        /// <param name="culture">Current culture</param>
        /// <returns>SolidColorBrush based on intensity and score</returns>
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // Handle null or invalid input
            if (values == null || values.Length < 3)
                return NoActivityBrush;
            
            // Extract values with null checking
            double intensity = 0;
            double score = 0;
            bool hasSessions = false;
            
            try
            {
                if (values[0] != null && values[0] != DependencyProperty.UnsetValue)
                    intensity = System.Convert.ToDouble(values[0]);
                    
                if (values[1] != null && values[1] != DependencyProperty.UnsetValue)
                    score = System.Convert.ToDouble(values[1]);
                    
                if (values[2] != null && values[2] != DependencyProperty.UnsetValue)
                    hasSessions = System.Convert.ToBoolean(values[2]);
            }
            catch (Exception)
            {
                return NoActivityBrush;
            }
            
            // No activity
            if (!hasSessions || intensity <= 0)
                return NoActivityBrush;
            
            // Clamp values
            intensity = Math.Min(1.0, Math.Max(0, intensity));
            score = Math.Min(100.0, Math.Max(0, score));
            
            // Generate color based on score range
            byte r, g, b;
            
            if (score < 50)
            {
                // Low score: Red spectrum
                // Intensity affects saturation - lower intensity = more faded
                double factor = 0.3 + (intensity * 0.7);
                r = (byte)(255);
                g = (byte)(50 * intensity);
                b = (byte)(50 * intensity);
            }
            else if (score < 75)
            {
                // Medium score: Yellow/orange spectrum
                r = 255;
                g = (byte)(200 * intensity);
                b = (byte)(50 * intensity);
            }
            else
            {
                // High score: Green spectrum
                // Higher score and intensity = more vibrant green
                double greenIntensity = 0.3 + (intensity * 0.7);
                r = (byte)(50 * (1 - intensity));
                g = (byte)(180 + (75 * greenIntensity));
                b = (byte)(50 + (100 * intensity));
            }
            
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
        
        /// <summary>
        /// Converts back (not supported for this converter).
        /// </summary>
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("IntensityToColorConverter does not support ConvertBack.");
        }
    }
    
    /// <summary>
    /// Simple intensity to color converter using single value binding.
    /// </summary>
    public class SimpleIntensityToColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush NoActivityBrush = new(Color.FromRgb(245, 245, 245));
        
        static SimpleIntensityToColorConverter()
        {
            NoActivityBrush.Freeze();
        }
        
        /// <summary>
        /// Converts a CalendarDay to its intensity color.
        /// </summary>
        /// <param name="value">CalendarDay object</param>
        /// <param name="targetType">Target type</param>
        /// <param name="parameter">Optional parameter</param>
        /// <param name="culture">Culture</param>
        /// <returns>SolidColorBrush</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not ViewModels.CalendarDay day)
                return NoActivityBrush;
            
            // Use the intensity color from the day object
            try
            {
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(day.IntensityColor);
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                return brush;
            }
            catch
            {
                return NoActivityBrush;
            }
        }
        
        /// <summary>
        /// Converts back (not supported).
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
