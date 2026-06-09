using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using FemVoiceStudio.Services;

namespace FemVoiceStudio.Converters
{
    public class IconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var str = value as string;
            var img = IconProvider.GetImageForValue(str);
            if (img != null) return img;
            // Fallback: return the original string so TextBlock can show it
            return str ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
