using System.Globalization;
using System.Text;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Conservative cleanup for report/PDF text. It removes broken control artifacts
    /// without stripping valid localized characters.
    /// </summary>
    public static class ReportTextSanitizer
    {
        public static string Clean(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var normalized = value
                .Replace("\uFFFE", string.Empty)
                .Replace("\uFEFF", string.Empty)
                .Replace("\uFFFD", string.Empty)
                .Replace("\u00AD", string.Empty)
                .Normalize(NormalizationForm.FormC);

            var sb = new StringBuilder(normalized.Length);
            foreach (var ch in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (category == UnicodeCategory.Control && ch != '\r' && ch != '\n' && ch != '\t')
                    continue;
                if (category == UnicodeCategory.Format && ch != '\u200C' && ch != '\u200D')
                    continue;

                sb.Append(ch);
            }

            return sb.ToString();
        }
    }
}
