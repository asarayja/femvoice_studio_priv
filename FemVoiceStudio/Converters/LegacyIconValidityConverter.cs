using System;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Data;

namespace FemVoiceStudio.Converters
{
    /// <summary>
    /// Validates legacy icon glyph strings (e.g. corrupted single orphan glyphs like , , , ).
    /// Returns Visibility.Visible only when the legacy glyph is considered valid.
    /// </summary>
    public sealed class LegacyIconValidityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var str = value as string;
            return IsValidLegacyIcon(str) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();

        public static bool IsValidLegacyIcon(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            // Treat the Unicode replacement character as invalid.
            // Also reject control characters / non-printable.
            foreach (var ch in value)
            {
                var code = (int)ch;

                // Reject replacement char (U+FFFD) even if mixed.
                if (ch == '\uFFFD')
                    return false;

                // Control characters and other non-printables.
                if (char.IsControl(ch))
                    return false;

                // Reject common bidi/control/format ranges.
                // (This catches most mojibake / corrupted non-printable remnants.)
                if ((code >= 0x0000 && code <= 0x001F) || (code >= 0x007F && code <= 0x009F))
                    return false;

                // Reject surrogate halves (invalid unicode scalar values)
                if (char.IsSurrogate(ch))
                    return false;
            }

            // Reject known mojibake patterns that often show up as partial emoji bytes.
            // Examples given: ðŸ..., Ã..., ðŸ… etc.
            // These are ASCII-ish sequences; if we see them, consider invalid.
            var s = value.Trim();
            if (s.Contains('Ã') || s.Contains('ð') || s.Contains('Ÿ') || s.Contains("â"))
                return false;

            // Reject "?" placeholders and multiple-question sequences.
            if (s.Contains("??"))
                return false;

            // If it's only 1 "orphan" glyph, it's only valid when it's a printable non-control scalar.
            // WPF will render the glyph; corrupted ones in the examples are printable but should be
            // treated as invalid legacy artifacts. We conservatively reject the specific known ones.
            // (Keep this list small and explicit; extend if more glyphs are observed.)
            if (s.Length == 1)
            {
                // Known broken glyphs from RC-0
                if (s == "\u0090" || s == "\u0091" || s == "\u0092" || s == "\u009D")
                    return false;

                // Explicitly listed in the task (these render as single-character placeholders).
                if (s == "\u00AC" || s == "\u0090" || s == "\u0091" || s == "\u0092" || s == "\u009D")
                    return false;
            }

            // For safety: ensure it's normalized and doesn’t produce an empty when encoded.
            // (Prevents weird whitespace/control combos.)
            try
            {
                var form = s.Normalize(NormalizationForm.FormKC);
                if (string.IsNullOrWhiteSpace(form))
                    return false;
            }
            catch
            {
                return false;
            }

            return true;
        }
    }
}

