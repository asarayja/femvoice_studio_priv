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
            // Common mojibake (double-encoded) legacy variants seen in older data files
            { "\u00f0\u0178\u017d\u00b5", IconKey.Microphone },
            { "\u00f0\u0178\u201c\u0160", IconKey.MusicNote },
            { "\u00f0\u0178\u201c\u02c6", IconKey.TrendingUp },
            { "\u00f0\u0178\u201c\u2030", IconKey.TrendingDown },
        };

        public static IconKey Map(string value)
        {
            if (string.IsNullOrEmpty(value)) return IconKey.Unknown;
            // Minimal explicit compatibility checks for common mojibake variants
            if (value.Equals("\u00f0\u0178\u017d\u00b5", StringComparison.Ordinal)) return IconKey.Microphone;
            if (value.Equals("\u00f0\u0178\u201c\u0160", StringComparison.Ordinal)) return IconKey.MusicNote;
            if (value.Equals("\u00f0\u0178\u201c\u02c6", StringComparison.Ordinal)) return IconKey.TrendingUp;
            if (value.Equals("\u00f0\u0178\u201c\u2030", StringComparison.Ordinal)) return IconKey.TrendingDown;
            if (LegacyToKey.TryGetValue(value, out var key)) return key;

            // Log unmapped incoming value for diagnostic purposes (RC-0)
            try
            {
                var codepoints = string.Join(" ", value.Select(c => ((int)c).ToString("X4")));
                Rc0RuntimeLog.Write("IconMapping", $"UnmappedValue: '{value}' CodePoints={codepoints}");
            }
            catch { }

            // Attempt to repair common mojibake double-encoding: try 1252 <-> UTF8 conversions
            try
            {
                var cp1252 = System.Text.Encoding.GetEncoding(1252);
                var utf8 = System.Text.Encoding.UTF8;

                var bytes1252 = cp1252.GetBytes(value);
                var repairedUtf8 = utf8.GetString(bytes1252);
                if (LegacyToKey.TryGetValue(repairedUtf8, out key)) return key;

                var bytesUtf8 = utf8.GetBytes(value);
                var repaired1252 = cp1252.GetString(bytesUtf8);
                if (LegacyToKey.TryGetValue(repaired1252, out key)) return key;
            }
            catch { }
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
