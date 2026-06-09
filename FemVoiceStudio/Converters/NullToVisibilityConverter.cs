using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using FemVoiceStudio.Services;

namespace FemVoiceStudio.Converters
{
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // If value is an icon key or legacy glyph, show Image only when provider returns an image
            try
            {
                var str = value as string;
                if (string.IsNullOrEmpty(str)) return Visibility.Collapsed;
                var img = IconProvider.GetImageForValue(str);
                return img == null ? Visibility.Collapsed : Visibility.Visible;
            }
            catch
            {
                return Visibility.Collapsed;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
