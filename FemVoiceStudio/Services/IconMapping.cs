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
            { "🎵", IconKey.MusicNote },
            { "📈", IconKey.TrendingUp },
            { "📉", IconKey.TrendingDown },
            { "🔊", IconKey.Volume },
            { "💨", IconKey.Breath },
            { "💬", IconKey.Chat },
            { "📖", IconKey.Book },
            { "⭐", IconKey.Star },
            { "â­", IconKey.Goal },
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
