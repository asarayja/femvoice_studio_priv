using System;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Result from FemVoiceScore calculation
    /// </summary>
    public class FemVoiceScoreResult
    {
        public int Id { get; set; }
        public double OverallScore { get; set; }
        public double ResonanceScore { get; set; }
        public double PitchScore { get; set; }
        public double IntonationScore { get; set; }
        public double VoiceHealthScore { get; set; }
        public DateTime CalculatedAt { get; set; } = DateTime.Now;
        public int? SessionId { get; set; }
        public int UserId { get; set; } = 1;
        public string? WarningFlags { get; set; }
    }
    
    /// <summary>
    /// Input parameters for FemVoiceScore calculation
    /// </summary>
    public class FemVoiceScoreInput
    {
        public double AveragePitch { get; set; }
        public double MinPitch { get; set; }
        public double MaxPitch { get; set; }
        public double PitchVariation { get; set; }
        public double AverageF1 { get; set; }
        public double AverageF2 { get; set; }
        public double AverageF3 { get; set; }
        public double SpectralCentroid { get; set; }
        public double IntonationRange { get; set; }
        public double IntonationRiseScore { get; set; }
        public double StrainLevel { get; set; }
        public double IntensityRms { get; set; }
        public double TargetMinPitch { get; set; } = 165;
        public double TargetMaxPitch { get; set; } = 255;
        public double TargetMinF1 { get; set; } = 400;
        public double TargetMaxF1 { get; set; } = 700;
        public double TargetMinF2 { get; set; } = 1400;
        public double TargetMaxF2 { get; set; } = 2000;
        public Models.DifficultyLevel DifficultyLevel { get; set; } = Models.DifficultyLevel.Nybegynner;
        
        // Adaptive pitch increase parameters - clinical principle: resonance must support pitch
        public double ResonanceScore { get; set; }      // For checking if resonance supports pitch increase
        public double VoiceHealthScore { get; set; }    // For checking if health allows pitch increase
        public bool IsMaintenanceSession { get; set; }  // Reduce pitch requirements for maintenance
        public int ConsecutiveStableSessions { get; set; }  // Track stability for progression
    }

    /// <summary>
    /// FemVoiceScore Algorithm
    /// Calculates comprehensive voice feminization score based on evidence-based principles:
    /// - Resonance: 45% (highest priority - primary voice feminization factor)
    /// - Pitch: 30% (secondary, never at expense of resonance)
    /// - Intonation: 15% (natural variation patterns)
    /// - Voice Health: 10% (safety monitoring)
    /// Clinical Principle: Resonance before Pitch
    /// Safety: Blocks progression if strain detected
    /// </summary>
    public class FemVoiceScore
    {
        #region Constants
        private const double ResonanceWeight = 0.45;
        private const double PitchWeight = 0.30;
        private const double IntonationWeight = 0.15;
        private const double VoiceHealthWeight = 0.10;
        
        private const double MaxSafePitch = 280.0;
        private const double StrainThreshold = 50.0;
        private const double CriticalStrainThreshold = 75.0;
        
        // SAFETY-CERT-01: These pitch/formant "ideal" constants are NO LONGER consumed by
        // the scoring math (CalculatePitchScore / CalculateResonanceScore now read the
        // per-user input.TargetMin/Max* fields). They are retained only as the documented
        // canonical default values mirrored by FemVoiceScoreInput's field initializers, so
        // callers that supply no personalized targets fall back to the same neutral defaults.
        // They must never be reintroduced into the scoring path (no-universal-target invariant).
        private const double IdealMinPitch = 165.0;
        private const double IdealMaxPitch = 255.0;
        private const double PitchVariationIdealMax = 25.0;

        private const double F1IdealMin = 400.0;
        private const double F1IdealMax = 700.0;
        private const double F2IdealMin = 1400.0;
        private const double F2IdealMax = 2000.0; // aligned to FemVoiceScoreInput.TargetMaxF2 default
        private const double SpectralCentroidIdealMin = 2000.0;
        
        private const double IntonationRangeIdealMin = 30.0;
        private const double IntonationRangeIdealMax = 120.0;
        #endregion
        
        /// <summary>
        /// Calculate FemVoiceScore from audio analysis data
        /// </summary>
        public FemVoiceScoreResult Calculate(FemVoiceScoreInput input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));
            
            var result = new FemVoiceScoreResult
            {
                CalculatedAt = DateTime.Now,
                UserId = input.DifficultyLevel == Models.DifficultyLevel.Nybegynner ? 1 : 
                        input.DifficultyLevel == Models.DifficultyLevel.Middels ? 2 : 3
            };
            
            var strainWarning = CheckStrainSafety(input);
            if (!string.IsNullOrEmpty(strainWarning))
            {
                result.WarningFlags = strainWarning;
            }
            
            result.ResonanceScore = CalculateResonanceScore(input);
            result.PitchScore = CalculatePitchScore(input);
            result.IntonationScore = CalculateIntonationScore(input);
            result.VoiceHealthScore = CalculateVoiceHealthScore(input);
            result.OverallScore = CalculateOverallScore(result, input);
            
            result.OverallScore = Math.Clamp(result.OverallScore, 0, 100);
            result.ResonanceScore = Math.Clamp(result.ResonanceScore, 0, 100);
            result.PitchScore = Math.Clamp(result.PitchScore, 0, 100);
            result.IntonationScore = Math.Clamp(result.IntonationScore, 0, 100);
            result.VoiceHealthScore = Math.Clamp(result.VoiceHealthScore, 0, 100);
            
            return result;
        }
        
        /// <summary>
        /// Calculate score for a training session
        /// </summary>
        public FemVoiceScoreResult CalculateFromSession(Models.TrainingSession session)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            
            var input = new FemVoiceScoreInput
            {
                AveragePitch = session.AveragePitch,
                MinPitch = session.MinPitch,
                MaxPitch = session.MaxPitch,
                PitchVariation = session.PitchVariation,
                AverageF1 = session.AverageF1,
                AverageF2 = session.AverageF2,
                AverageF3 = session.AverageF3,
                SpectralCentroid = session.SpectralCentroid,
                IntonationRange = session.IntonationScore,
                IntonationRiseScore = session.IntonationScore,
                StrainLevel = session.ResonanceScore > 0 ? (100 - session.ResonanceScore) / 2 : 0,
                DifficultyLevel = session.DifficultyLevel
            };
            
            var result = Calculate(input);
            result.SessionId = session.Id;
            result.UserId = 1;
            
            return result;
        }

        private string? CheckStrainSafety(FemVoiceScoreInput input)
        {
            if (input.StrainLevel >= CriticalStrainThreshold)
                return "CRITICAL_STRAIN";
            if (input.AveragePitch > MaxSafePitch)
                return "HIGH_PITCH_STRAIN";
            if (input.StrainLevel >= StrainThreshold)
                return "MODERATE_STRAIN";
            return null;
        }

        private double CalculateResonanceScore(FemVoiceScoreInput input)
        {
            // If a precomputed resonance score is supplied by caller, prefer that
            if (input.ResonanceScore > 0)
                return ApplyDifficultyTolerance(input.ResonanceScore, input.DifficultyLevel);

            double f1Score = 0, f2Score = 0, brightnessScore = 0;

            // SAFETY-CERT-01: Drive the resonance curve from the user's personalized
            // formant targets (input.TargetMin/MaxF1, input.TargetMin/MaxF2) rather than
            // the universal feminine formant constants. This removes the no-universal-target
            // invariant breach. The F1/F2 constants now serve only as the defaults for the
            // FemVoiceScoreInput fields, so callers that do not override them are unaffected.
            double f1IdealMin = input.TargetMinF1;
            double f1IdealMax = input.TargetMaxF1;
            double f2IdealMin = input.TargetMinF2;
            double f2IdealMax = input.TargetMaxF2;
            // Guard against zero/negative formant spans (personalized values may collapse).
            double f1Span = f1IdealMax - f1IdealMin; if (f1Span <= 0) f1Span = 1;
            double f2Span = f2IdealMax - f2IdealMin; if (f2Span <= 0) f2Span = 1;

            if (input.AverageF1 > 0)
            {
                if (input.AverageF1 >= f1IdealMin)
                    f1Score = Math.Min(100, 50 + (input.AverageF1 - f1IdealMin) / f1Span * 50);
                else
                    f1Score = Math.Max(0, 50 - (f1IdealMin - input.AverageF1) / f1IdealMin * 50);
            }

            if (input.AverageF2 > 0)
            {
                if (input.AverageF2 >= f2IdealMin)
                    f2Score = Math.Min(100, 50 + (input.AverageF2 - f2IdealMin) / f2Span * 50);
                else
                    f2Score = Math.Max(0, 50 - (f2IdealMin - input.AverageF2) / f2IdealMin * 50);
            }
            
            if (input.SpectralCentroid > 0)
                brightnessScore = Math.Min(100, input.SpectralCentroid / SpectralCentroidIdealMin * 100);
            
            double resonanceScore = f1Score * 0.30 + f2Score * 0.40 + brightnessScore * 0.30;
            return ApplyDifficultyTolerance(resonanceScore, input.DifficultyLevel);
        }
        
        private double CalculatePitchScore(FemVoiceScoreInput input)
        {
            if (input.AveragePitch <= 0) return 0;
            
            double pitchScore = 0;
            
            // Adaptive target logic: Clinical principle - resonance must support pitch
            // Only allow pitch increase when ResonanceScore > 60 and VoiceHealthScore > 70
            double effectiveMaxPitch = input.TargetMaxPitch;
            bool canIncreasePitch = input.ResonanceScore > 60 && input.VoiceHealthScore > 70;
            
            // For maintenance sessions, reduce pitch requirements to protect against overtraining
            if (input.IsMaintenanceSession)
            {
                effectiveMaxPitch = Math.Min(effectiveMaxPitch, 240);  // Cap at 240 for maintenance
            }
            
            // Check if pitch is in target range (using adaptive max)
            if (input.AveragePitch >= input.TargetMinPitch && input.AveragePitch <= effectiveMaxPitch)
            {
                // SAFETY-CERT-01: Personalize the ideal-pitch curve to the user's own
                // comfort zone instead of the universal feminine 210 Hz centre. The peak
                // (top pitchScore) sits at the centre of [TargetMinPitch, effectiveMaxPitch],
                // so a user with a down-adjusted zone (e.g. 150-190 Hz) is rewarded for
                // staying in their zone, not penalised toward a universal 210 Hz target.
                // No-universal-target invariant: this method must never bias toward a
                // hardcoded feminine centre. Constants remain only as input-field defaults.
                double idealCenter = (input.TargetMinPitch + effectiveMaxPitch) / 2;
                double distanceFromIdeal = Math.Abs(input.AveragePitch - idealCenter);
                double idealRange = (effectiveMaxPitch - input.TargetMinPitch) / 2;
                if (idealRange <= 0) idealRange = 1; // guard against zero/negative span
                pitchScore = 100 - (distanceFromIdeal / idealRange * 30);
                
                // If pitch is high but resonance doesn't support it, apply penalty
                // Clinical rule: pitch chasing is dangerous - must have resonance support
                // Apply penalty when resonance is below clinical threshold (60)
                if (input.AveragePitch > 200 && input.ResonanceScore < 60)
                {
                    pitchScore -= 45;  // Strong penalty for high pitch without resonance support
                }
            }
            else if (input.AveragePitch < input.TargetMinPitch)
            {
                double belowAmount = input.TargetMinPitch - input.AveragePitch;
                pitchScore = Math.Max(0, 70 - belowAmount * 2);
            }
            else
            {
                // Pitch is above effective max
                double aboveAmount = input.AveragePitch - effectiveMaxPitch;
                
                // Apply stricter penalty if resonance doesn't support high pitch
                double penalty = canIncreasePitch ? 3 : 5;
                pitchScore = Math.Max(0, 70 - aboveAmount * penalty);
                
                // Additional penalty for exceeding safe pitch limits
                if (input.AveragePitch > MaxSafePitch)
                    pitchScore -= 20;
            }
            
            // Penalize excessive variation
            if (input.PitchVariation > PitchVariationIdealMax)
            {
                double variationPenalty = (input.PitchVariation - PitchVariationIdealMax) / 5 * 10;
                pitchScore = Math.Max(0, pitchScore - variationPenalty);
            }
            
            return ApplyDifficultyTolerance(pitchScore, input.DifficultyLevel);
        }
        
        private double CalculateIntonationScore(FemVoiceScoreInput input)
        {
            if (input.IntonationRange <= 0) return 50;
            
            double intonationScore = 0;
            
            if (input.IntonationRange >= IntonationRangeIdealMin && input.IntonationRange <= IntonationRangeIdealMax)
            {
                double idealCenter = (IntonationRangeIdealMin + IntonationRangeIdealMax) / 2;
                double distanceFromIdeal = Math.Abs(input.IntonationRange - idealCenter);
                double idealRange = (IntonationRangeIdealMax - IntonationRangeIdealMin) / 2;
                intonationScore = 100 - (distanceFromIdeal / idealRange * 20);
            }
            else if (input.IntonationRange < IntonationRangeIdealMin)
            {
                intonationScore = Math.Max(0, 60 - (IntonationRangeIdealMin - input.IntonationRange) * 2);
            }
            else
            {
                intonationScore = Math.Max(0, 80 - (input.IntonationRange - IntonationRangeIdealMax) / 10 * 10);
            }
            
            if (input.IntonationRiseScore > 30)
                intonationScore = Math.Min(100, intonationScore + 10);
            
            return ApplyDifficultyTolerance(intonationScore, input.DifficultyLevel);
        }
        
        private double CalculateVoiceHealthScore(FemVoiceScoreInput input)
        {
            // Start with the input voice health score if provided, otherwise default to 100
            double healthScore = input.VoiceHealthScore > 0 ? input.VoiceHealthScore : 100;
            
            if (input.StrainLevel > 0)
            {
                if (input.StrainLevel >= CriticalStrainThreshold)
                    healthScore = Math.Max(0, healthScore - (input.StrainLevel - CriticalStrainThreshold) * 4);
                else if (input.StrainLevel >= StrainThreshold)
                    healthScore = healthScore - input.StrainLevel;
                else
                    healthScore = healthScore - input.StrainLevel * 0.5;
            }
            
            if (input.IntensityRms > 0.8)
                healthScore -= 10;
            else if (input.IntensityRms < 0.01)
                healthScore -= 15;
            
            if (input.AveragePitch > MaxSafePitch)
                healthScore = Math.Max(0, healthScore - 30);
            else if (input.AveragePitch > 260)
                healthScore -= 10;
            
            return Math.Clamp(healthScore, 0, 100);
        }
        
        private double CalculateOverallScore(FemVoiceScoreResult result, FemVoiceScoreInput input)
        {
            double overall = 
                result.ResonanceScore * ResonanceWeight +
                result.PitchScore * PitchWeight +
                result.IntonationScore * IntonationWeight +
                result.VoiceHealthScore * VoiceHealthWeight;
            
            // Critical: If strain detected, apply significant penalty
            if (input.StrainLevel >= StrainThreshold)
            {
                overall = Math.Min(overall, 60);
                if (input.StrainLevel >= CriticalStrainThreshold)
                    overall = Math.Min(overall, 40);
            }
            
            // Clinical rule: if pitch in target but resonance poor, cap pitch contribution
            // This prevents "pitch chasing" - getting high score without proper resonance
            if (result.PitchScore > 60 && result.ResonanceScore < 40)
            {
                overall = overall * 0.7; // Cap at 70% when resonance is poor but pitch is good
            }
            
            // Additional adaptive rule: If pitch is high but health doesn't support it, penalize
            // This prevents " Hz hunting" behavior
            if (input.VoiceHealthScore < 60 && input.AveragePitch > 220)
            {
                overall = overall * 0.8; // Reduce score when high pitch + poor health
            }
            
            // For maintenance sessions, ensure score reflects consolidation focus
            // Don't penalize for lower pitch in maintenance mode
            if (input.IsMaintenanceSession && overall < 50 && result.VoiceHealthScore > 70)
            {
                // Boost score slightly for maintenance sessions with good health
                overall = Math.Min(70, overall + 10);
            }
            
            return overall;
        }
        
        private double ApplyDifficultyTolerance(double score, Models.DifficultyLevel level)
        {
            // Beginner: +10 bonus (wider tolerances)
            // Intermediate: 0 (standard)
            // Advanced: -5 (stricter)
            return level switch
            {
                Models.DifficultyLevel.Nybegynner => Math.Min(100, score + 10),
                Models.DifficultyLevel.Avansert => Math.Max(0, score - 5),
                _ => score
            };
        }
    }
}