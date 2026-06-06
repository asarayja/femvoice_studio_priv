using System;
using System.Collections.Generic;
using System.Text;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Coach message with What/Why/How structure
    /// </summary>
    public class CoachMessage
    {
        public string What { get; set; } = "";      // Parameter to focus on
        public string Why { get; set; } = "";       // Pedagogical explanation
        public string How { get; set; } = "";       // Specific exercise
        public string Encouragement { get; set; } = ""; // Positive framing
        public string FullMessage { get; set; } = ""; // Combined message
        public string Emoji { get; set; } = "💪";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
    
    /// <summary>
    /// Coach Message Generator
    /// Generates intelligent messages with three mandatory components:
    /// 1. What (concrete parameter)
    /// 2. Why (pedagogical explanation)  
    /// 3. How (specific exercise)
    /// 
    /// Requirements:
    /// - Contextual (adapted to current level)
    /// - Personal (based on VoiceProfile data)
    /// - Encouraging (positive framing)
    /// - Concrete (no vague advice)
    /// </summary>
    public class CoachMessageGenerator
    {
        private readonly Random _random = new();
        private readonly ILocalizationService _localization;
        
        /// <summary>
        /// Constructor with dependency injection (recommended)
        /// </summary>
        public CoachMessageGenerator(ILocalizationService? localization = null)
        {
            _localization = localization ?? LocalizationService.Instance;
        }
        
        /// <summary>
        /// Generate a coach message based on direction analysis
        /// </summary>
        public CoachMessage GenerateMessage(
            DirectionAnalysisResult direction,
            TrainingLevel level,
            double recentScore,
            string? previousBestParameter = null)
        {
            var message = new CoachMessage();
            
            // Determine focus based on direction analysis
            string focusArea = direction.PrimaryFocus;
            
            if (IsFocus(focusArea, "Dashboard_Resonance", "Resonans"))
                GenerateResonanceMessage(message, direction.Resonance, level);
            else if (IsFocus(focusArea, "Dashboard_Pitch", "Pitch"))
                GeneratePitchMessage(message, direction.Pitch, level);
            else if (IsFocus(focusArea, "Dashboard_Intonation", "Intonasjon"))
                GenerateIntonationMessage(message, direction.Intonation, level);
            else if (IsFocus(focusArea, "Dashboard_VoiceHealth", "Stemmehelse"))
                GenerateHealthMessage(message, direction.VoiceHealth);
            else
                GenerateMaintenanceMessage(message, level, recentScore);
            
            // Add encouragement
            message.Encouragement = GetEncouragement(recentScore);
            
            // Build full message
            message.FullMessage = BuildFullMessage(message);
            
            return message;
        }
        
        private void GenerateResonanceMessage(CoachMessage message, DirectionRecommendation resonance, TrainingLevel level)
        {
            message.What = _localization.GetString("Dashboard_Resonance");
            message.Emoji = "🔊";
            
            if (resonance.Direction == Direction.Increase)
            {
                message.How = resonance.SafetyNote ?? _localization.GetString("CoachGenerator_ResonanceHowDefault");
                message.Why = _localization.GetString("CoachGenerator_ResonanceWhy");
                
                if (level == TrainingLevel.Beginner)
                    message.How = _localization.GetString("CoachGenerator_ResonanceHowBeginner");
                else if (level == TrainingLevel.Intermediate)
                    message.How = _localization.GetString("CoachGenerator_ResonanceHowIntermediate");
                else
                    message.How = _localization.GetString("CoachGenerator_ResonanceHowAdvanced");
            }
            else if (resonance.Direction == Direction.Stabilize)
            {
                message.What = _localization.GetString("CoachGenerator_ResonanceStability");
                message.How = _localization.GetString("CoachGenerator_ResonanceStabilityHow");
                message.Why = _localization.GetString("CoachGenerator_ResonanceStabilityWhy");
            }
            else
            {
                message.How = _localization.GetString("CoachGenerator_ResonanceMaintainHow");
                message.Why = _localization.GetString("CoachGenerator_ResonanceMaintainWhy");
            }
        }
        
        private void GeneratePitchMessage(CoachMessage message, DirectionRecommendation pitch, TrainingLevel level)
        {
            message.What = _localization.GetString("Dashboard_Pitch");
            message.Emoji = "🎵";
            
            if (pitch.Direction == Direction.Increase)
            {
                message.How = pitch.SafetyNote ?? _localization.GetString("CoachGenerator_PitchIncreaseHow");
                message.Why = _localization.GetString("CoachGenerator_PitchIncreaseWhy");
                
                if (level == TrainingLevel.Beginner)
                    message.How = _localization.GetString("CoachGenerator_PitchHowBeginner");
                else if (level == TrainingLevel.Intermediate)
                    message.How = _localization.GetString("CoachGenerator_PitchHowIntermediate");
                else
                    message.How = _localization.GetString("CoachGenerator_PitchHowAdvanced");
            }
            else if (pitch.Direction == Direction.Decrease)
            {
                message.What = _localization.GetString("CoachGenerator_PitchReduction");
                message.How = _localization.GetString("CoachGenerator_PitchReductionHow");
                message.Why = _localization.GetString("CoachGenerator_PitchReductionWhy");
                
                if (!string.IsNullOrEmpty(pitch.SafetyNote))
                    message.How = pitch.SafetyNote;
            }
            else
            {
                message.How = _localization.GetString("CoachGenerator_PitchMaintainHow");
                message.Why = _localization.GetString("CoachGenerator_PitchMaintainWhy");
            }
        }
        
        private void GenerateIntonationMessage(CoachMessage message, DirectionRecommendation intonation, TrainingLevel level)
        {
            message.What = _localization.GetString("Dashboard_Intonation");
            message.Emoji = "📈";
            
            if (intonation.Direction == Direction.Increase)
            {
                message.How = intonation.SafetyNote ?? _localization.GetString("CoachGenerator_IntonationHow");
                message.Why = _localization.GetString("CoachGenerator_IntonationWhy");
                
                if (level == TrainingLevel.Beginner)
                    message.How = _localization.GetString("CoachGenerator_IntonationHowBeginner");
                else
                    message.How = _localization.GetString("CoachGenerator_IntonationHowAdvanced");
            }
            else
            {
                message.How = _localization.GetString("CoachGenerator_IntonationGoodHow");
                message.Why = _localization.GetString("CoachGenerator_IntonationGoodWhy");
            }
        }
        
        private void GenerateHealthMessage(CoachMessage message, DirectionRecommendation health)
        {
            message.What = _localization.GetString("Dashboard_VoiceHealth");
            message.Emoji = "🛡️";
            
            if (health.Direction == Direction.Decrease)
            {
                message.How = _localization.GetString("CoachGenerator_HealthRestHow");
                message.Why = _localization.GetString("CoachGenerator_HealthRestWhy");
                message.Encouragement = _localization.GetString("CoachGenerator_HealthRestEncouragement");
            }
            else
            {
                message.How = _localization.GetString("CoachGenerator_HealthGoodHow");
                message.Why = _localization.GetString("CoachGenerator_HealthGoodWhy");
            }
        }
        
        private void GenerateMaintenanceMessage(CoachMessage message, TrainingLevel level, double recentScore)
        {
            message.What = _localization.GetString("Direction_Maintenance");
            message.Emoji = "✨";
            
            if (recentScore >= 70)
            {
                message.How = _localization.GetString("CoachGenerator_MaintenanceGoodHow");
                message.Why = _localization.GetString("CoachGenerator_MaintenanceGoodWhy");
            }
            else
            {
                message.How = _localization.GetString("CoachGenerator_MaintenanceBasicHow");
                message.Why = _localization.GetString("CoachGenerator_MaintenanceBasicWhy");
            }
        }
        
        private string GetEncouragement(double recentScore)
        {
            var encouragements = new List<string>();
            
            if (recentScore >= 80)
            {
                encouragements.AddRange(new[]{
                    _localization.GetString("CoachGenerator_EncouragementHigh1"),
                    _localization.GetString("CoachGenerator_EncouragementHigh2"),
                    _localization.GetString("CoachGenerator_EncouragementHigh3")
                });
            }
            else if (recentScore >= 60)
            {
                encouragements.AddRange(new[]{
                    _localization.GetString("Coach_Encourage_1"),
                    _localization.GetString("CoachGenerator_EncouragementMedium2"),
                    _localization.GetString("CoachGenerator_EncouragementMedium3")
                });
            }
            else
            {
                encouragements.AddRange(new[]{
                    _localization.GetString("Coach_Encourage_2"),
                    _localization.GetString("CoachGenerator_EncouragementLow2"),
                    _localization.GetString("CoachGenerator_EncouragementLow3")
                });
            }
            
            return encouragements[_random.Next(encouragements.Count)];
        }
        
        private string BuildFullMessage(CoachMessage msg)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine($"**{msg.What}**");
            sb.AppendLine();
            sb.AppendLine(msg.How);
            sb.AppendLine();
            sb.AppendLine($"💡 {msg.Why}");
            sb.AppendLine();
            sb.AppendLine(msg.Encouragement);
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Generate a short status message for dashboard
        /// </summary>
        public string GenerateShortStatus(DirectionAnalysisResult direction)
        {
            if (direction.HasSafetyConcern)
                return _localization.GetString("CoachGenerator_ShortSafety");
            
            if (IsFocus(direction.PrimaryFocus, "Dashboard_Resonance", "Resonans"))
                return $"🔊 {direction.Resonance.Reason}";
            if (IsFocus(direction.PrimaryFocus, "Dashboard_Pitch", "Pitch"))
                return $"🎵 {direction.Pitch.Reason}";
            if (IsFocus(direction.PrimaryFocus, "Dashboard_Intonation", "Intonasjon"))
                return $"📈 {direction.Intonation.Reason}";
            if (IsFocus(direction.PrimaryFocus, "Dashboard_VoiceHealth", "Stemmehelse"))
                return $"🛡️ {direction.VoiceHealth.Reason}";
            return _localization.GetString("CoachGenerator_ShortAllGood");
        }
        
        /// <summary>
        /// Generates complexity-specific feedback message.
        /// </summary>
        public string GenerateComplexityFeedback(
            ComplexityEvaluation evaluation, 
            VoiceMetrics? metrics = null,
            TrainingLevel level = TrainingLevel.Intermediate)
        {
            var message = new CoachMessage();
            var levelName = ComplexityLevelStep.GetDisplayName(evaluation.CurrentLevel);
            var emoji = ComplexityLevelStep.GetIcon(evaluation.CurrentLevel);
            
            message.Emoji = emoji;
            
            if (evaluation.IsReadyForNext)
            {
                var nextLevel = ComplexityLevelStep.GetDisplayName(
                    GetNextComplexityLevel(evaluation.CurrentLevel));
                
                message.What = _localization.GetString("CoachGenerator_ComplexityProgression");
                message.How = _localization.GetFormattedString("CoachGenerator_ComplexityReadyFormat", nextLevel);
                message.Why = _localization.GetFormattedString("CoachGenerator_ComplexityReadyWhyFormat", evaluation.AverageResonance, evaluation.PitchStability);
                message.Encouragement = _localization.GetString("CoachGenerator_ComplexityExcellent");
            }
            else
            {
                message.What = _localization.GetString("CoachGenerator_ComplexityLevel");
                
                // Generate specific feedback based on blocking reasons
                var reasons = evaluation.BlockingReasons;
                
                if (reasons.Any(r => r.Contains("resonans") || r.Contains("Resonans")))
                {
                    message.How = _localization.GetString("CoachGenerator_ComplexityResonanceHow");
                    message.Why = _localization.GetFormattedString("CoachGenerator_ComplexityResonanceWhyFormat", evaluation.AverageResonance);
                }
                else if (reasons.Any(r => r.Contains("stemmehelse") || r.Contains("helse")))
                {
                    message.How = _localization.GetString("CoachGenerator_ComplexityHealthHow");
                    message.Why = _localization.GetFormattedString("CoachGenerator_ComplexityHealthWhyFormat", evaluation.VoiceHealthScore);
                }
                else if (reasons.Any(r => r.Contains("økter") || r.Contains("Suksess")))
                {
                    var needed = 5 - evaluation.SessionsAtCurrentLevel;
                    message.How = _localization.GetFormattedString("CoachGenerator_ComplexityContinueFormat", levelName);
                    message.Why = _localization.GetFormattedString("CoachGenerator_ComplexitySessionsWhyFormat", needed, evaluation.SuccessRate);
                }
                else
                {
                    message.How = _localization.GetFormattedString("CoachGenerator_ComplexityLevelHowFormat", levelName);
                    message.Why = _localization.GetString("CoachGenerator_ComplexityThresholdWhy");
                }
                
                message.Encouragement = _localization.GetFormattedString("CoachGenerator_ComplexityEncouragementFormat", evaluation.SessionsAtCurrentLevel);
            }
            
            // Build full message
            message.FullMessage = BuildFullMessage(message);
            
            return message.FullMessage;
        }
        
        /// <summary>
        /// Gets next complexity level.
        /// </summary>
        private SpeechComplexityLevel GetNextComplexityLevel(SpeechComplexityLevel current)
        {
            return current switch
            {
                SpeechComplexityLevel.IsolatedSounds => SpeechComplexityLevel.Syllables,
                SpeechComplexityLevel.Syllables => SpeechComplexityLevel.Words,
                SpeechComplexityLevel.Words => SpeechComplexityLevel.Phrases,
                SpeechComplexityLevel.Phrases => SpeechComplexityLevel.StructuredSentences,
                SpeechComplexityLevel.StructuredSentences => SpeechComplexityLevel.SpontaneousSpeech,
                SpeechComplexityLevel.SpontaneousSpeech => SpeechComplexityLevel.Conversational,
                SpeechComplexityLevel.Conversational => SpeechComplexityLevel.Conversational,
                _ => SpeechComplexityLevel.IsolatedSounds
            };
        }

        private bool IsFocus(string value, string localizationKey, string legacyNorwegian)
        {
            return value == _localization.GetString(localizationKey) ||
                   value.Equals(legacyNorwegian, StringComparison.OrdinalIgnoreCase);
        }
    }
}
