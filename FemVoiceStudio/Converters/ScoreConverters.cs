using System;
using System.Globalization;
using System.Windows.Data;

namespace FemVoiceStudio.Converters
{
    /// <summary>
    /// Multi-value converter for score bar width calculation
    /// Takes score (0-100) and container width, returns proportional width
    /// </summary>
    public class ScoreToWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2)
                return 0.0;
            
            if (values[0] is double score && values[1] is double containerWidth)
            {
                // Calculate width as percentage of container
                return (score / 100.0) * containerWidth;
            }
            
            return 0.0;
        }
        
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            var result = new object[targetTypes.Length];
            Array.Fill(result, Binding.DoNothing);
            return result;
        }
    }
    
    /// <summary>
    /// Converts stability state to color brush
    /// </summary>
    public class StabilityToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Models.StabilityState stability)
            {
                return stability switch
                {
                    Models.StabilityState.VeryStable => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80)),   // Green
                    Models.StabilityState.Stable => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 195, 74)),   // Light Green
                    Models.StabilityState.Developing => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 193, 7)), // Yellow
                    Models.StabilityState.Unstable => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 152, 0)),   // Orange
                    _ => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(158, 158, 158))                              // Gray - NoVoice
                };
            }
            
            return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
    
    /// <summary>
    /// Converts health state to color brush
    /// </summary>
    public class HealthToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Models.HealthState health)
            {
                return health switch
                {
                    Models.HealthState.Safe => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80)),    // Green
                    Models.HealthState.Monitor => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 193, 7)), // Yellow
                    Models.HealthState.Warning => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 152, 0)),  // Orange
                    Models.HealthState.Danger => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 67, 54)),   // Red
                    _ => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(158, 158, 158))                         // Gray - NoVoice
                };
            }
            
            return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
    
    /// <summary>
    /// Converts stability state to text
    /// </summary>
    public class StabilityToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Models.StabilityState stability)
            {
                return stability switch
                {
                    Models.StabilityState.VeryStable => "Veldig stabil",
                    Models.StabilityState.Stable => "Stabil",
                    Models.StabilityState.Developing => "Utvikler seg",
                    Models.StabilityState.Unstable => "Ustabil",
                    _ => "Ingen stemme"
                };
            }
            
            return "Ukjent";
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
    
    /// <summary>
    /// Converts health state to text
    /// </summary>
    public class HealthToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Models.HealthState health)
            {
                return health switch
                {
                    Models.HealthState.Safe => "Trygt",
                    Models.HealthState.Monitor => "Observer",
                    Models.HealthState.Warning => "Advarsel",
                    Models.HealthState.Danger => "Fare",
                    _ => "Ingen stemme"
                };
            }
            
            return "Ukjent";
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
