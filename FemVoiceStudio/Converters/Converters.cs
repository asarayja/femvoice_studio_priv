using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Converters
{
    internal static class ThemeBrushResolver
    {
        public static Brush Get(string key, Brush fallback)
        {
            return Application.Current?.TryFindResource(key) as Brush ?? fallback;
        }
    }

    /// <summary>Konverterer bool til Visibility</summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool invert = parameter?.ToString() == "Invert";
            // Accept both bool and a count-style int (non-zero == "true") so callers can
            // bind a collection's .Count directly (e.g. show a section when it has items).
            bool truthy = value switch
            {
                bool b => b,
                int i => i != 0,
                _ => false
            };
            return (truthy ^ invert) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility v && v == Visibility.Visible;
        }
    }

    /// <summary>Konverterer pitch til farge basert på om den er i målområdet</summary>
    public class PitchToColorConverter : IValueConverter
    {
        public double MinPitch { get; set; } = 165;
        public double MaxPitch { get; set; } = 255;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double pitch && pitch > 0)
            {
                if (pitch >= MinPitch && pitch <= MaxPitch)
                    return ThemeBrushResolver.Get("SuccessBrush", SystemColors.ControlTextBrush);
                else if (pitch < MinPitch)
                    return ThemeBrushResolver.Get("WarningBrush", SystemColors.ControlTextBrush);
                else
                    return ThemeBrushResolver.Get("ErrorBrush", SystemColors.ControlTextBrush);
            }
            return ThemeBrushResolver.Get("TextDisabledBrush", SystemColors.GrayTextBrush);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    /// <summary>Konverterer difficulty enum til farge</summary>
    public class DifficultyToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Models.DifficultyLevel level)
            {
                return level switch
                {
                    Models.DifficultyLevel.Nybegynner => ThemeBrushResolver.Get("SuccessBrush", SystemColors.ControlTextBrush),
                    Models.DifficultyLevel.Middels    => ThemeBrushResolver.Get("WarningBrush", SystemColors.ControlTextBrush),
                    Models.DifficultyLevel.Avansert   => ThemeBrushResolver.Get("ErrorBrush", SystemColors.ControlTextBrush),
                    _                                 => ThemeBrushResolver.Get("TextDisabledBrush", SystemColors.GrayTextBrush)
                };
            }
            return ThemeBrushResolver.Get("TextDisabledBrush", SystemColors.GrayTextBrush);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    /// <summary>Konverterer score til farge</summary>
    public class ScoreToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double score)
            {
                if (score >= 80) return ThemeBrushResolver.Get("SuccessBrush", SystemColors.ControlTextBrush);
                if (score >= 60) return ThemeBrushResolver.Get("WarningBrush", SystemColors.ControlTextBrush);
                if (score >= 40) return ThemeBrushResolver.Get("WarningHoverBrush", SystemColors.ControlTextBrush);
                return ThemeBrushResolver.Get("ErrorBrush", SystemColors.ControlTextBrush);
            }
            return ThemeBrushResolver.Get("TextDisabledBrush", SystemColors.GrayTextBrush);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    /// <summary>Inverterer bool (true → false, false → true)</summary>
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && !b;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && !b;
    }

    /// <summary>Konverterer string til Visibility — synlig hvis string ikke er tom</summary>
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s)
                return string.IsNullOrEmpty(s) ? Visibility.Collapsed : Visibility.Visible;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    /// <summary>Konverterer bool til advarselsfarge (rød hvis true, grønn hvis false)</summary>
    public class BoolToWarningColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b)
                return ThemeBrushResolver.Get("ErrorBrush", SystemColors.ControlTextBrush);
            return ThemeBrushResolver.Get("SuccessBrush", SystemColors.ControlTextBrush);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    /// <summary>Konverterer null/0 til Visibility</summary>
    public class ZeroToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
                return count == 0 ? Visibility.Visible : Visibility.Collapsed;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    /// <summary>Konverterer bool til knappetekst</summary>
    public class BoolToButtonTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool completed && completed)
                return "Fullført ✓";
            return "Fullfør anbefaling";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    /// <summary>Inverterer bool til Visibility (true → Collapsed, false → Visible)</summary>
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Accept both bool and a count-style int (non-zero == "true") so callers can
            // bind a collection's .Count for an empty-state placeholder (visible when 0).
            bool truthy = value switch
            {
                bool b => b,
                int i => i != 0,
                _ => false
            };
            return truthy ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    /// <summary>Konverterer TrainingLevel til farge for level badge</summary>
    public class LevelToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Services.TrainingLevel level)
            {
                return level switch
                {
                    Services.TrainingLevel.Beginner     => ThemeBrushResolver.Get("SuccessBrush", SystemColors.ControlTextBrush),
                    Services.TrainingLevel.Intermediate => ThemeBrushResolver.Get("WarningBrush", SystemColors.ControlTextBrush),
                    Services.TrainingLevel.Advanced     => ThemeBrushResolver.Get("InfoBrush", SystemColors.ControlTextBrush),
                    _                                   => ThemeBrushResolver.Get("TextDisabledBrush", SystemColors.GrayTextBrush)
                };
            }
            return ThemeBrushResolver.Get("TextDisabledBrush", SystemColors.GrayTextBrush);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    /// <summary>Konverterer Direction enum til pil-symbol</summary>
    public class DirectionToArrowConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string direction)
            {
                return direction switch
                {
                    "⬆" => "⬆",
                    "⬇" => "⬇",
                    "➡" => "➡",
                    "✅" => "✅",
                    _   => "➡"
                };
            }
            return "➡";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    /// <summary>Konverterer prosent til bredde basert på container-bredde</summary>
    public class PercentageToWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is double percentage && values[1] is double maxWidth)
                return Math.Min(maxWidth, percentage / 100.0 * maxWidth);
            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            var result = new object[targetTypes.Length];
            Array.Fill(result, Binding.DoNothing);
            return result;
        }
    }

    // ── Nye converters for Live Feedback-panel ───────────────────────────────

    /// <summary>
    /// Konverterer en double (0–1) til prosentstreng, f.eks. "42%".
    /// Brukes av Hold Progress-indikatoren i Live Feedback-panelet.
    /// Verdier utenfor 0–1 klippes til nærmeste gyldige verdi.
    /// </summary>
    [ValueConversion(typeof(double), typeof(string))]
    public class ProgressToPercentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not double d) return "0%";
            return $"{(int)Math.Round(Math.Clamp(d, 0.0, 1.0) * 100)}%";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException("ProgressToPercentConverter støtter ikke ConvertBack.");
    }

    /// <summary>
    /// Konverterer <see cref="MessageSeverity"/> til bakgrunnspensel for
    /// Inline Coach Hint-panelet i Live Feedback.
    ///   Info       → subtil blå   (rgba 100,149,237, a=40)
    ///   Suggestion → varm oransje (rgba 255,165,0,   a=40)
    ///   Warning    → tydelig rød  (rgba 255,68,68,   a=40)
    /// </summary>
    [ValueConversion(typeof(MessageSeverity), typeof(Brush))]
    public class SeverityToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not MessageSeverity severity) return Brushes.Transparent;
            return severity switch
            {
                MessageSeverity.Warning    => ThemeBrushResolver.Get("HealthWarningBackgroundBrush", Brushes.Transparent),
                MessageSeverity.Suggestion => ThemeBrushResolver.Get("AccentLightBrush", Brushes.Transparent),
                _                          => ThemeBrushResolver.Get("BackgroundTertiaryBrush", Brushes.Transparent)
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException("SeverityToBrushConverter støtter ikke ConvertBack.");
    }
}
