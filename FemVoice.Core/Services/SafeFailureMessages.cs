using System;

namespace FemVoiceStudio.Services
{
    public enum SafeFailureKind
    {
        General,
        MicrophoneUnavailable,
        ReportExport,
        DiagnosticsExport,
        EmptyAnalytics,
        PersistenceReadback,
        BackupRestore,
        SettingsRecovery
    }

    /// <summary>
    /// Centralized user-facing recovery copy. Technical exception details belong in
    /// logs/evidence, not in normal UI.
    /// </summary>
    public static class SafeFailureMessages
    {
        public static string For(SafeFailureKind kind)
        {
            var key = kind switch
            {
                SafeFailureKind.MicrophoneUnavailable => "SafeFailure_MicrophoneUnavailable",
                SafeFailureKind.ReportExport => "SafeFailure_ReportExport",
                SafeFailureKind.DiagnosticsExport => "SafeFailure_DiagnosticsExport",
                SafeFailureKind.EmptyAnalytics => "SafeFailure_EmptyAnalytics",
                SafeFailureKind.PersistenceReadback => "SafeFailure_PersistenceReadback",
                SafeFailureKind.BackupRestore => "SafeFailure_BackupRestore",
                SafeFailureKind.SettingsRecovery => "SafeFailure_SettingsRecovery",
                _ => "SafeFailure_General"
            };

            var message = LocalizationService.Instance.GetString(key);
            return string.IsNullOrWhiteSpace(message) || string.Equals(message, key, StringComparison.Ordinal)
                ? NorwegianFallback(kind)
                : message;
        }

        public static string TechnicalReason(Exception ex)
        {
            ArgumentNullException.ThrowIfNull(ex);
            var name = ex.GetType().Name;
            if (name.EndsWith("Exception", StringComparison.Ordinal))
                name = name[..^"Exception".Length];

            return string.IsNullOrWhiteSpace(name)
                ? "ERROR"
                : ToScreamingSnakeCase(name);
        }

        public static bool LooksLikeRawExceptionText(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;

            return message.Contains("Exception", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("StackTrace", StringComparison.OrdinalIgnoreCase)
                   || message.Contains(" at ", StringComparison.Ordinal)
                   || message.Contains("C:\\", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("System.", StringComparison.Ordinal)
                   || message.Contains("\n   at ", StringComparison.Ordinal);
        }

        private static string NorwegianFallback(SafeFailureKind kind) => kind switch
        {
            SafeFailureKind.MicrophoneUnavailable => "Mikrofonen er ikke tilgjengelig akkurat nå.",
            SafeFailureKind.ReportExport => "Rapporten kunne ikke eksporteres. Prøv igjen senere.",
            SafeFailureKind.DiagnosticsExport => "Diagnostikken kunne ikke eksporteres akkurat nå.",
            SafeFailureKind.EmptyAnalytics => "Ingen øktdata er tilgjengelig ennå.",
            SafeFailureKind.PersistenceReadback => "Noe gikk galt, men dataene dine ser ut til å være trygge.",
            SafeFailureKind.BackupRestore => "Noe gikk galt med gjenoppretting, men eksisterende data ser ut til å være trygge.",
            SafeFailureKind.SettingsRecovery => "Innstillingene kunne ikke leses, men appen kan fortsette med trygge standardvalg.",
            _ => "Noe gikk galt, men dataene dine ser ut til å være trygge."
        };

        private static string ToScreamingSnakeCase(string value)
        {
            var chars = new System.Text.StringBuilder(value.Length + 8);
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(value[i - 1]))
                    chars.Append('_');
                chars.Append(char.ToUpperInvariant(c));
            }

            return chars.ToString();
        }
    }
}
