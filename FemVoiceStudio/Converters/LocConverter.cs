using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;
using FemVoiceStudio.Services;

namespace FemVoiceStudio.Converters
{
    public class LocConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;
            var key = value.ToString() ?? string.Empty;
            try
            {
                return LocalizationService.Instance[key];
            }
            catch
            {
                return key;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();

        public override object ProvideValue(IServiceProvider serviceProvider) => this;
    }
}
