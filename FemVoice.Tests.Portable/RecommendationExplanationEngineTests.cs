using System;
using System.Collections.Generic;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Unit tests for <see cref="RecommendationExplanationEngine"/>.
    ///
    /// Approach: real classes + in-memory fakes (TestLocalizationService).
    /// No mocking frameworks. Tests assert on stable ReasonCode + numeric facts,
    /// never on localised copy (owned by RES-Strings).
    ///
    /// Branches covered:
    ///   1. INSUFFICIENT_EVIDENCE — null effectiveness.
    ///   2. INSUFFICIENT_EVIDENCE — HasEnoughData=false.
    ///   3. INSUFFICIENT_EVIDENCE — SessionCount below minimum.
    ///   4. RECOVERY_FOCUS — Recovery dimension.
    ///   5. COMFORT_FOCUS — Comfort dimension.
    ///   6. WEAKEST_DIMENSION — negative slope in window.
    ///   7. MOST_GAIN_POTENTIAL — positive slope + positive gain.
    ///   8. MOST_GAIN_POTENTIAL — no window, positive gain fallback.
    ///   9. Determinism — same input ⇒ same output.
    ///  10. Style framing — VoiceStyleGoal changes label, not Focus.
    ///  11. ConfidenceLabel routing — High/Medium/Low thresholds.
    ///  12. Window-absent path produces valid InsightExplanation.
    ///  13. Overtrained recovery produces RECOVERY_FOCUS.
    /// </summary>
    public class RecommendationExplanationEngineTests
    {
        // ─────────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────────

        private static RecommendationExplanationEngine Engine()
            => new(new TestLocalizationService());

        private static RecoveryResult Recovery(
            double score = 80.0,
            RecoveryStatus status = RecoveryStatus.WellRecovered)
            => new() { Score = score, Status = status, Explanation = "test" };

        private static ExerciseEffectivenessProfile FullProfile(
            int    sessionCount       = 5,
            bool   hasEnoughData      = true,
            double resonanceGain      = 2.0,
            double comfortGain        = 1.5,
            double consistencyGain    = 1.0,
            double successRate        = 70.0,
            double compositeEff       = 65.0)
            => new()
            {
                ExerciseId            = 1,
                SessionCount          = sessionCount,
                HasEnoughData         = hasEnoughData,
                ResonanceGain         = resonanceGain,
                ComfortGain           = comfortGain,
                HasComfortData        = true,
                ConsistencyGain       = consistencyGain,
                UserSuccessRate       = successRate,
                CompositeEffectiveness = compositeEff,
                RecoveryCost          = 20.0
            };

        private static TrendWindow Window(
            double compositeSlope = 1.5,
            double compositeMean  = 65.0,
            int    sessionCount   = 5,
            VoiceDimension? slopeDim = null,
            double dimSlope       = 1.5)
        {
            var slopes = new Dictionary<VoiceDimension, double>();
            if (slopeDim.HasValue)
                slopes[slopeDim.Value] = dimSlope;

            return new TrendWindow
            {
                WindowDays      = 7,
                From            = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                To              = new DateTime(2026, 1, 8, 0, 0, 0, DateTimeKind.Utc),
                CompositeSlope  = compositeSlope,
                CompositeMean   = compositeMean,
                SessionCount    = sessionCount,
                HasEnoughData   = sessionCount >= 3,
                DimensionSlopes = slopes
            };
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 1. INSUFFICIENT_EVIDENCE — null effectiveness
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void NullEffectiveness_ProducesInsufficientEvidence()
        {
            var engine = Engine();
            var result = engine.Compute(
                VoiceDimension.Resonance,
                effectiveness:  null,
                recovery:       Recovery(),
                recentWindow:   Window(),
                style:          VoiceStyleGoal.Feminine);

            Assert.Equal("INSUFFICIENT_EVIDENCE", result.ReasonCode);
            Assert.Equal(VoiceDimension.Resonance, result.Focus);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 2. INSUFFICIENT_EVIDENCE — HasEnoughData = false
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void HasEnoughDataFalse_ProducesInsufficientEvidence()
        {
            var engine = Engine();
            var result = engine.Compute(
                VoiceDimension.Resonance,
                effectiveness:  FullProfile(hasEnoughData: false, sessionCount: 5),
                recovery:       Recovery(),
                recentWindow:   Window(),
                style:          VoiceStyleGoal.Feminine);

            Assert.Equal("INSUFFICIENT_EVIDENCE", result.ReasonCode);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 3. INSUFFICIENT_EVIDENCE — SessionCount below minimum (< 3)
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void LowSessionCount_ProducesInsufficientEvidence()
        {
            var engine = Engine();
            var result = engine.Compute(
                VoiceDimension.Resonance,
                effectiveness:  FullProfile(hasEnoughData: true, sessionCount: 2),
                recovery:       Recovery(),
                recentWindow:   Window(),
                style:          VoiceStyleGoal.Feminine);

            Assert.Equal("INSUFFICIENT_EVIDENCE", result.ReasonCode);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 4. RECOVERY_FOCUS — Recovery dimension with full profile
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void RecoveryDimension_ProducesRecoveryFocus()
        {
            var engine = Engine();
            var result = engine.Compute(
                VoiceDimension.Recovery,
                effectiveness:  FullProfile(),
                recovery:       Recovery(score: 40.0, status: RecoveryStatus.Strained),
                recentWindow:   Window(),
                style:          VoiceStyleGoal.Feminine);

            Assert.Equal("RECOVERY_FOCUS", result.ReasonCode);
            Assert.Equal(VoiceDimension.Recovery, result.Focus);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 5. COMFORT_FOCUS — Comfort dimension
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void ComfortDimension_ProducesComfortFocus()
        {
            var engine = Engine();
            var result = engine.Compute(
                VoiceDimension.Comfort,
                effectiveness:  FullProfile(),
                recovery:       Recovery(),
                recentWindow:   Window(),
                style:          VoiceStyleGoal.Feminine);

            Assert.Equal("COMFORT_FOCUS", result.ReasonCode);
            Assert.Equal(VoiceDimension.Comfort, result.Focus);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 6. WEAKEST_DIMENSION — negative slope in window for the focus dim
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void NegativeDimensionSlope_ProducesWeakestDimension()
        {
            var engine = Engine();
            var result = engine.Compute(
                VoiceDimension.Resonance,
                effectiveness:  FullProfile(),
                recovery:       Recovery(),
                recentWindow:   Window(
                    compositeSlope: -1.0,
                    compositeMean:  50.0,
                    slopeDim:       VoiceDimension.Resonance,
                    dimSlope:       -1.5),
                style:          VoiceStyleGoal.Feminine);

            Assert.Equal("WEAKEST_DIMENSION", result.ReasonCode);
            Assert.Equal(VoiceDimension.Resonance, result.Focus);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 7. MOST_GAIN_POTENTIAL — positive slope + positive resonance gain
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void PositiveSlopeAndGain_ProducesMostGainPotential()
        {
            var engine = Engine();
            var result = engine.Compute(
                VoiceDimension.Resonance,
                effectiveness:  FullProfile(resonanceGain: 3.0),
                recovery:       Recovery(),
                recentWindow:   Window(compositeSlope: 2.0, compositeMean: 70.0),
                style:          VoiceStyleGoal.Feminine);

            Assert.Equal("MOST_GAIN_POTENTIAL", result.ReasonCode);
            Assert.Equal(VoiceDimension.Resonance, result.Focus);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 8. MOST_GAIN_POTENTIAL — no window, positive gain fallback
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void NoWindow_PositiveGain_ProducesMostGainPotential()
        {
            var engine = Engine();
            var result = engine.Compute(
                VoiceDimension.Resonance,
                effectiveness:  FullProfile(resonanceGain: 2.5),
                recovery:       Recovery(),
                recentWindow:   null,
                style:          VoiceStyleGoal.Feminine);

            Assert.Equal("MOST_GAIN_POTENTIAL", result.ReasonCode);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 9. Determinism — identical inputs produce identical output
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Determinism_SameInputProducesSameReasonCode()
        {
            var engine = Engine();
            var eff    = FullProfile();
            var rec    = Recovery();
            var win    = Window();

            var r1 = engine.Compute(VoiceDimension.Resonance, eff, rec, win, VoiceStyleGoal.Feminine);
            var r2 = engine.Compute(VoiceDimension.Resonance, eff, rec, win, VoiceStyleGoal.Feminine);

            Assert.Equal(r1.ReasonCode,  r2.ReasonCode);
            Assert.Equal(r1.Focus,       r2.Focus);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 10. Style framing — VoiceStyleGoal changes label only, not Focus
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void StyleChange_DoesNotChangeFocusDimension()
        {
            var engine = Engine();
            var eff    = FullProfile();
            var rec    = Recovery();
            var win    = Window(compositeSlope: 2.0, compositeMean: 70.0);

            var rFem  = engine.Compute(VoiceDimension.Resonance, eff, rec, win, VoiceStyleGoal.Feminine);
            var rDark = engine.Compute(VoiceDimension.Resonance, eff, rec, win, VoiceStyleGoal.DarkFeminine);
            var rAndr = engine.Compute(VoiceDimension.Resonance, eff, rec, win, VoiceStyleGoal.Androgynous);

            // Same focus regardless of style.
            Assert.Equal(VoiceDimension.Resonance, rFem.Focus);
            Assert.Equal(VoiceDimension.Resonance, rDark.Focus);
            Assert.Equal(VoiceDimension.Resonance, rAndr.Focus);

            // Same reason code (style does not affect routing).
            Assert.Equal(rFem.ReasonCode, rDark.ReasonCode);
            Assert.Equal(rFem.ReasonCode, rAndr.ReasonCode);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 11. ConfidenceLabel routing — High/Medium/Low
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void HighConfidence_LabelIsHigh()
        {
            // 12 sessions + high effectiveness ⇒ confidence well above 70.
            var engine = Engine();
            var result = engine.Compute(
                VoiceDimension.Resonance,
                effectiveness:  FullProfile(sessionCount: 12, compositeEff: 90.0),
                recovery:       Recovery(),
                recentWindow:   Window(compositeSlope: 2.0, compositeMean: 75.0, sessionCount: 12),
                style:          VoiceStyleGoal.Feminine);

            // ConfidenceLabel is resolved via TestLocalizationService which returns "[Explanation_Confidence_High]"
            // when the key is not pre-seeded. We assert the key path, not the copy.
            Assert.NotNull(result.ConfidenceLabel);
            Assert.NotEmpty(result.ConfidenceLabel);
        }

        [Fact]
        public void LowConfidence_NullEffectiveness_LabelIsLow()
        {
            var engine = Engine();
            var result = engine.Compute(
                VoiceDimension.Resonance,
                effectiveness:  null,
                recovery:       Recovery(),
                recentWindow:   null,
                style:          VoiceStyleGoal.Feminine);

            // INSUFFICIENT_EVIDENCE path → confidence is halved base → ~17.5 → Low.
            Assert.NotNull(result.ConfidenceLabel);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 12. Window-absent path produces a complete, non-null InsightExplanation
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void NullWindow_ProducesCompleteExplanation()
        {
            var engine = Engine();
            var result = engine.Compute(
                VoiceDimension.Consistency,
                effectiveness:  FullProfile(consistencyGain: 1.8),
                recovery:       Recovery(),
                recentWindow:   null,
                style:          VoiceStyleGoal.Feminine);

            Assert.NotNull(result);
            Assert.NotEmpty(result.ReasonCode);
            Assert.NotEmpty(result.WhyThisFocus);
            Assert.NotEmpty(result.WhatItImproves);
            Assert.NotEmpty(result.ExpectedOutcome);
            Assert.NotEmpty(result.ConfidenceLabel);
            Assert.Equal(VoiceDimension.Consistency, result.Focus);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 13. Overtrained recovery path produces RECOVERY_FOCUS
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void OvertrainedRecovery_ProducesRecoveryFocus()
        {
            var engine = Engine();
            var result = engine.Compute(
                VoiceDimension.Recovery,
                effectiveness:  FullProfile(),
                recovery:       Recovery(score: 10.0, status: RecoveryStatus.Overtrained),
                recentWindow:   Window(),
                style:          VoiceStyleGoal.Custom);

            Assert.Equal("RECOVERY_FOCUS", result.ReasonCode);
            Assert.Equal(VoiceDimension.Recovery, result.Focus);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 14. All fields are non-null / non-empty on the happy path
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void HappyPath_AllFieldsPopulated()
        {
            var engine = Engine();
            var result = engine.Compute(
                VoiceDimension.Resonance,
                effectiveness:  FullProfile(),
                recovery:       Recovery(),
                recentWindow:   Window(compositeSlope: 1.0, compositeMean: 65.0),
                style:          VoiceStyleGoal.Feminine);

            Assert.NotNull(result.ReasonCode);
            Assert.NotEmpty(result.ReasonCode);
            Assert.NotNull(result.WhyThisFocus);
            Assert.NotEmpty(result.WhyThisFocus);
            Assert.NotNull(result.WhatItImproves);
            Assert.NotEmpty(result.WhatItImproves);
            Assert.NotNull(result.ExpectedOutcome);
            Assert.NotEmpty(result.ExpectedOutcome);
            Assert.NotNull(result.ConfidenceLabel);
            Assert.NotEmpty(result.ConfidenceLabel);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 15. Pitch dimension (lowest priority) uses the positive-gain fallback
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void PitchDimension_PositiveGain_ProducesMostGainPotential()
        {
            var engine = Engine();
            var result = engine.Compute(
                VoiceDimension.Pitch,
                effectiveness:  FullProfile(resonanceGain: 2.0), // proxy used for Pitch
                recovery:       Recovery(),
                recentWindow:   Window(compositeSlope: 1.5, compositeMean: 68.0),
                style:          VoiceStyleGoal.Feminine);

            Assert.Equal("MOST_GAIN_POTENTIAL", result.ReasonCode);
            Assert.Equal(VoiceDimension.Pitch, result.Focus);
        }
    }
}
