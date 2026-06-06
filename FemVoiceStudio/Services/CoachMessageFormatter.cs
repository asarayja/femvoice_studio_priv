using System;
using System.Text;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Formats coach messages with What/Why/How structure.
    /// Uses emojis for visual feedback (✅ success, ⚠️ warning, 🛑 stop, etc.)
    /// </summary>
    public class CoachMessageFormatter
    {
        private readonly LocalizationService _localization;

        public CoachMessageFormatter()
        {
            _localization = LocalizationService.Instance;
        }

        /// <summary>
        /// Format a complete coach message with What/Why/How structure.
        /// </summary>
        public string FormatCoachMessage(string hintKey, VoiceMetrics? metrics = null, 
            UserLevel userLevel = UserLevel.Middels, string? customContext = null)
        {
            var loc = _localization;
            string hint = loc[hintKey];

            // Get emoji prefix based on hint type
            string emoji = GetEmojiForHint(hintKey);

            // Format based on user level
            if (userLevel == UserLevel.Nybegynner)
            {
                return FormatForBeginner(hintKey, hint, metrics, emoji);
            }
            else if (userLevel == UserLevel.Avansert)
            {
                return FormatForAdvanced(hintKey, hint, metrics, emoji);
            }
            else
            {
                return FormatForIntermediate(hintKey, hint, metrics, emoji);
            }
        }

        /// <summary>
        /// Format pitch feedback message.
        /// </summary>
        public string FormatPitchMessage(double currentPitch, double targetPitch, 
            UserLevel userLevel, bool isCorrect)
        {
            var loc = _localization;
            
            if (isCorrect)
            {
                return loc.GetFormattedString("CoachFormatter_PitchGoodFormat", currentPitch);
            }

            if (currentPitch < targetPitch)
            {
                if (userLevel == UserLevel.Nybegynner)
                {
                    return loc.GetFormattedString("CoachFormatter_PitchLowBeginnerFormat", currentPitch, targetPitch);
                }
                return loc.GetFormattedString("CoachFormatter_PitchLowFormat", currentPitch, targetPitch);
            }
            else
            {
                return loc.GetFormattedString("CoachFormatter_PitchHighFormat", currentPitch, targetPitch);
            }
        }

        /// <summary>
        /// Format resonance feedback message.
        /// </summary>
        public string FormatResonanceMessage(double f2, UserLevel userLevel, bool isCorrect)
        {
            var loc = _localization;

            if (isCorrect && f2 >= 1800)
            {
                return loc.GetFormattedString("CoachFormatter_ResonanceOptimalFormat", f2);
            }

            if (f2 < 1400)
            {
                if (userLevel == UserLevel.Nybegynner)
                {
                    return loc.GetFormattedString("CoachFormatter_ResonanceLowBeginnerFormat", f2);
                }
                return loc.GetFormattedString("CoachFormatter_ResonanceLowFormat", f2);
            }

            if (f2 < 1800)
            {
                return loc.GetFormattedString("CoachFormatter_ResonanceAlmostFormat", f2);
            }

            return loc.GetFormattedString("CoachFormatter_ResonanceGoodFormat", f2);
        }

        /// <summary>
        /// Format health warning message.
        /// </summary>
        public string FormatHealthWarning(HealthIndicator level, string? customMessage = null)
        {
            var loc = _localization;

            return level switch
            {
                HealthIndicator.Critical => $"🛑 {loc["CoachHint_Health_Critical"]}",
                HealthIndicator.Warning => $"⚠️ {loc["CoachHint_Health_TakeItEasy"]}",
                _ => ""
            };
        }

        /// <summary>
        /// Format session summary message.
        /// </summary>
        public string FormatSessionSummary(SessionEvaluationSummary summary)
        {
            var loc = _localization;
            var sb = new StringBuilder();

            // Overall score
            string scoreEmoji = summary.OverallScore >= 80 ? "⭐" : 
                              summary.OverallScore >= 60 ? "👍" : "💪";
            sb.AppendLine(loc.GetFormattedString("CoachFormatter_SummaryScoreFormat", scoreEmoji, summary.OverallScore));

            // Parameter breakdown
            sb.AppendLine(loc.GetFormattedString("CoachFormatter_SummaryResonanceFormat", summary.ResonanceCorrectPercent));
            sb.AppendLine(loc.GetFormattedString("CoachFormatter_SummaryPitchFormat", summary.PitchCorrectPercent));
            sb.AppendLine(loc.GetFormattedString("CoachFormatter_SummaryStabilityFormat", summary.StabilityCorrectPercent));

            // Strain level
            string strainEmoji = summary.StrainLevel switch
            {
                "High" => "🛑",
                "Moderate" => "⚠️",
                _ => "✅"
            };
            sb.AppendLine(loc.GetFormattedString("CoachFormatter_SummaryStrainFormat", strainEmoji, summary.StrainLevel));

            return sb.ToString();
        }

        /// <summary>
        /// Get emoji for hint type.
        /// </summary>
        private string GetEmojiForHint(string hintKey)
        {
            if (hintKey.Contains("Good") || hintKey.Contains("Correct") || hintKey.Contains("Optimal"))
                return "✅";
            if (hintKey.Contains("Critical") || hintKey.Contains("Stop") || hintKey.Contains("Rest"))
                return "🛑";
            if (hintKey.Contains("Warning") || hintKey.Contains("TakeItEasy"))
                return "⚠️";
            if (hintKey.Contains("Pitch"))
                return "🎵";
            if (hintKey.Contains("Resonance"))
                return "✨";
            if (hintKey.Contains("Intonation"))
                return "🎶";
            if (hintKey.Contains("Breathing"))
                return "🌬️";
            if (hintKey.Contains("Stability"))
                return "📊";

            return "💡";
        }

        /// <summary>
        /// Format message for beginner user (more detailed).
        /// </summary>
        private string FormatForBeginner(string hintKey, string hint, VoiceMetrics? metrics, string emoji)
        {
            var loc = _localization;
            
            // Add specific numeric feedback for beginners
            if (metrics != null && hintKey.Contains("Pitch") && metrics.Pitch > 0)
            {
                return loc.GetFormattedString("CoachFormatter_BeginnerPitchMetricFormat", emoji, hint, metrics.Pitch);
            }
            
            if (metrics != null && hintKey.Contains("Resonance") && metrics.F2 > 0)
            {
                return loc.GetFormattedString("CoachFormatter_BeginnerF2MetricFormat", emoji, hint, metrics.F2);
            }

            return $"{emoji} {hint}";
        }

        /// <summary>
        /// Format message for intermediate user.
        /// </summary>
        private string FormatForIntermediate(string hintKey, string hint, VoiceMetrics? metrics, string emoji)
        {
            if (metrics != null)
            {
                if (hintKey.Contains("Pitch") && metrics.Pitch > 0)
                {
                    return $"{emoji} {hint} ({metrics.Pitch:F0} Hz)";
                }
                if (hintKey.Contains("Resonance") && metrics.F2 > 0)
                {
                    return $"{emoji} {hint} (F2: {metrics.F2:F0} Hz)";
                }
            }

            return $"{emoji} {hint}";
        }

        /// <summary>
        /// Format message for advanced user (more technical, shorter).
        /// </summary>
        private string FormatForAdvanced(string hintKey, string hint, VoiceMetrics? metrics, string emoji)
        {
            if (metrics != null)
            {
                if (hintKey.Contains("Pitch") && metrics.Pitch > 0)
                {
                    return $"{emoji} F0: {metrics.Pitch:F0} Hz";
                }
                if (hintKey.Contains("Resonance") && metrics.F2 > 0)
                {
                    return $"{emoji} F2: {metrics.F2:F0} Hz";
                }
            }

            return $"{emoji} {hint}";
        }

        /// <summary>
        /// Generate encouragement message based on streak.
        /// </summary>
        public string GenerateStreakMessage(int streakDays)
        {
            var loc = _localization;
            
            return streakDays switch
            {
                1 => loc["CoachFormatter_Streak1"],
                3 => loc["CoachFormatter_Streak3"],
                7 => loc["CoachFormatter_Streak7"],
                14 => loc["CoachFormatter_Streak14"],
                30 => loc["CoachFormatter_Streak30"],
                _ => loc.GetFormattedString("CoachFormatter_StreakGenericFormat", streakDays)
            };
        }

        /// <summary>
        /// Generate rest instruction message.
        /// </summary>
        public string GenerateRestInstruction(int secondsRemaining = 30)
        {
            var loc = _localization;
            return loc.GetFormattedString("CoachFormatter_RestInstructionFormat", secondsRemaining);
        }
    }
}
