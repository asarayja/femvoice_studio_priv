using System.Collections.Generic;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    public static class IconMapping
    {
        // Legacy emoji (or mojibake) -> IconKey mapping
        public static readonly Dictionary<string, IconKey> LegacyToKey = new()
        {
            { "🎤", IconKey.Microphone },
            { "ðŸŽµ", IconKey.Microphone }, // mojibake variants
            { "🎵", IconKey.MusicNote },
            { "ðŸ“Š", IconKey.MusicNote },
            { "📈", IconKey.TrendingUp },
            { "ðŸ“ˆ", IconKey.TrendingUp },
            { "📉", IconKey.TrendingDown },
            { "ðŸ“‰", IconKey.TrendingDown },
            { "🔊", IconKey.Volume },
            { "ðŸ”Š", IconKey.Volume },
            { "💨", IconKey.Breath },
            { "ðŸ’¨", IconKey.Breath },
            { "💬", IconKey.Chat },
            { "ðŸ’¬", IconKey.Chat },
            { "📖", IconKey.Book },
            { "ðŸ“–", IconKey.Book },
            { "⭐", IconKey.Star },
            { "â­", IconKey.Goal },
            { "â", IconKey.Goal }
        };

        public static IconKey Map(string value)
        {
            if (string.IsNullOrEmpty(value)) return IconKey.Unknown;
            if (LegacyToKey.TryGetValue(value, out var key)) return key;
            // Also allow storing canonical keys directly like "icon:microphone"
            return value.ToLowerInvariant() switch
            {
                "icon:microphone" => IconKey.Microphone,
                "icon:music" => IconKey.MusicNote,
                "icon:trendingup" => IconKey.TrendingUp,
                "icon:trendingdown" => IconKey.TrendingDown,
                "icon:volume" => IconKey.Volume,
                "icon:breath" => IconKey.Breath,
                "icon:chat" => IconKey.Chat,
                "icon:book" => IconKey.Book,
                "icon:star" => IconKey.Star,
                _ => IconKey.Unknown
            };
        }
    }
}
