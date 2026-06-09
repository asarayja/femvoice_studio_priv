using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    public static class IconProvider
    {
        // Optional ResourceDictionary for tests or overrides. When set, lookup will consult
        // this dictionary before checking Application resources.
        public static System.Windows.ResourceDictionary? TestResources { get; set; }
        // Returns a DrawingImage resource for the given IconKey if available
        public static ImageSource? GetImageForKey(IconKey key)
        {
            try
            {
                var resourceKey = key switch
                {
                    IconKey.Microphone => "Icon.Microphone",
                    IconKey.MusicNote => "Icon.MusicNote",
                    IconKey.TrendingUp => "Icon.TrendingUp",
                    IconKey.TrendingDown => "Icon.TrendingDown",
                    IconKey.Volume => "Icon.Volume",
                    IconKey.Breath => "Icon.Breath",
                    IconKey.Chat => "Icon.Chat",
                    IconKey.Book => "Icon.Book",
                    IconKey.Star => "Icon.Star",
                    IconKey.Goal => "Icon.Goal",
                    _ => null
                };

                if (resourceKey == null) return null;
                // Check test resources first (unit tests can register icons here)
                if (TestResources != null && TestResources.Contains(resourceKey))
                {
                    return TestResources[resourceKey] as ImageSource;
                }
                if (Application.Current == null) return null;
                var res = Application.Current.TryFindResource(resourceKey);
                return res as ImageSource;
            }
            catch
            {
                return null;
            }
        }

        // Accepts either a legacy glyph string or canonical IconKey string
        public static ImageSource? GetImageForValue(string? value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            var key = IconMapping.Map(value);
            var img = GetImageForKey(key);
            return img;
        }
    }
}
