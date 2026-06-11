using System;
using FemVoiceStudio.Models.VoiceLoad;
using FemVoiceStudio.Services.VoiceLoad;
using Xunit;
using Assert = Xunit.Assert;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Sprint F (Agent 11/12) — Predictive Voice Intelligence engine tests.
    ///
    /// These exercise the PURE Sprint F core (LiveVoiceLoadMonitor + score/pause/recovery/
    /// trend/messaging engines). They were verified against the real source via a standalone
    /// net10.0 harness on the Linux dev box; they were NOT executed through xUnit here because
    /// the test project targets net10.0-windows (no WindowsDesktop runtime). No mocking
    /// frameworks — real classes, deterministic timestamps (no wall clock).
    /// </summary>
    public class VoiceLoadTests
    {
        private static readonly DateTime Start = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        private static VoiceLoadInputs Tick(
            int i, double stability, double strain = 0, double fatigue = 0, bool voiced = true,
            bool comfort = true, bool usesResonance = false, double resonance = 0.7, double intensity = 0.3,
            bool pauseHealth = false, bool hydrationHealth = false, bool hydrationAdvisor = false,
            int healthRank = 0, bool locked = false)
        {
            var ts = Start.AddMilliseconds(i * 100);
            return new VoiceLoadInputs
            {
                Timestamp = ts,
                SessionElapsedSeconds = (int)(ts - Start).TotalSeconds,
                StabilityScore = stability,
                ResonanceScore = resonance,
                UsesResonanceSignal = usesResonance,
                PitchHz = voiced ? 180 : 0,
                Intensity = intensity,
                IsHoldingCorrectly = voiced,
                IsInComfortZone = comfort,
                IsSafetyLocked = locked,
                FatigueScore = fatigue,
                FatigueDetected = fatigue >= 0.45,
                StrainScore = strain,
                StrainDetected = strain >= 0.5,
                PauseRecommendedByHealth = pauseHealth,
                HydrationSuggestedByHealth = hydrationHealth,
                HydrationSuggestedByAdvisor = hydrationAdvisor,
                HealthStateRank = healthRank
            };
        }

        // Scenario 1 — short stable session → LOW band, no pause.
        [Fact]
        public void Scenario1_ShortStableSession_LowBand_NoPause()
        {
            var m = new LiveVoiceLoadMonitor(); m.Reset(Start);
            VoiceLoadObservation o = default!;
            for (var i = 0; i < 80; i++) o = m.Observe(Tick(i, stability: 0.85));

            Assert.True(o.State.IsDataSufficient);
            Assert.Equal(VoiceLoadBand.Low, o.State.VoiceLoadBand);
            Assert.Equal(PauseRecommendationLevel.None, o.Recommendation.Pause);
        }

        // Scenario 2 — long session without a pause → at least Moderate band, pause Soon+.
        [Fact]
        public void Scenario2_LongSessionNoPause_ModerateOrHigher_PauseSoon()
        {
            var m = new LiveVoiceLoadMonitor(); m.Reset(Start);
            VoiceLoadObservation o = default!;
            for (var i = 0; i < 7000; i++) o = m.Observe(Tick(i, stability: 0.62));

            Assert.True(o.State.VoiceLoadBand >= VoiceLoadBand.Moderate);
            Assert.True(o.Recommendation.Pause >= PauseRecommendationLevel.Soon);
        }

        // Scenario 3 — increasing instability → declining trend, pause Now+.
        [Fact]
        public void Scenario3_IncreasingInstability_DeclineTrend_PauseNow()
        {
            var m = new LiveVoiceLoadMonitor(); m.Reset(Start);
            VoiceLoadObservation o = default!;
            for (var i = 0; i < 120; i++) o = m.Observe(Tick(i, stability: 0.85));
            for (var i = 120; i < 1200; i++)
                o = m.Observe(Tick(i, stability: 0.30, strain: 0.5, fatigue: 0.5, comfort: false, healthRank: 1));

            var trend = m.BuildSessionTrendSummary();
            Assert.True(trend.TrendCategory is SessionTrendCategory.MildDecline or SessionTrendCategory.ClearDecline);
            Assert.Equal(VoiceLoadTrendDirection.Worsening, o.State.TrendDirection);
            Assert.True(o.Recommendation.Pause >= PauseRecommendationLevel.Now);
        }

        // Scenario 4 — pause taken → time-since-pause resets, score does not rise, recovery evaluates.
        [Fact]
        public void Scenario4_PauseTaken_ResetsAndRecovers()
        {
            var m = new LiveVoiceLoadMonitor(); m.Reset(Start);
            VoiceLoadObservation pre = default!;
            for (var i = 0; i < 800; i++) pre = m.Observe(Tick(i, stability: 0.5, strain: 0.4, fatigue: 0.4, healthRank: 1));
            var beforeScore = pre.State.VoiceLoadScore;
            var beforeSince = pre.State.TimeSinceLastPauseSeconds;

            m.NotePauseTaken(Start.AddMilliseconds(800 * 100));
            VoiceLoadObservation o = default!;
            for (var i = 900; i < 1500; i++) o = m.Observe(Tick(i, stability: 0.85, strain: 0.05, fatigue: 0.05));
            var recovery = m.EvaluateRecoveryReadiness(Start.AddMilliseconds(1500 * 100));

            Assert.True(o.State.TimeSinceLastPauseSeconds < beforeSince);
            Assert.True(o.State.VoiceLoadScore <= beforeScore);
            Assert.NotEqual(RecoveryReadiness.InsufficientData, recovery.Readiness);
        }

        // Scenario 5 — hydration context surfaces gently and is anti-spammed (no dehydration claim).
        [Fact]
        public void Scenario5_HydrationContext_GentleAndAntiSpam()
        {
            var m = new LiveVoiceLoadMonitor(); m.Reset(Start);
            var gentle = 0;
            for (var i = 0; i < 400; i++)
            {
                var o = m.Observe(Tick(i, stability: 0.75, hydrationAdvisor: true));
                if (o.Recommendation.Hydration == HydrationContextLevel.GentleReminder) gentle++;
            }
            Assert.True(gentle >= 1);
            Assert.True(gentle <= 2);   // cooldown prevents per-exercise spam
        }

        // Scenario 6 — high load persists after a pause → EndSession (safe wording).
        [Fact]
        public void Scenario6_HighLoadPersistsAfterPause_EndSession()
        {
            var m = new LiveVoiceLoadMonitor(); m.Reset(Start);
            for (var i = 0; i < 1500; i++)
                m.Observe(Tick(i, stability: 0.25, strain: 0.7, fatigue: 0.7, comfort: false, pauseHealth: true, healthRank: 2));
            m.NotePauseTaken(Start.AddMilliseconds(1500 * 100));
            VoiceLoadObservation o = default!;
            for (var i = 1600; i < 2300; i++)
                o = m.Observe(Tick(i, stability: 0.25, strain: 0.7, fatigue: 0.7, comfort: false, pauseHealth: true, healthRank: 2));

            Assert.Equal(PauseRecommendationLevel.EndSession, o.Recommendation.Pause);
        }

        // Scenario 7 — insufficient data → InsufficientData band, no recommendation, no message.
        [Fact]
        public void Scenario7_InsufficientData_NoStrongRecommendation()
        {
            var m = new LiveVoiceLoadMonitor(); m.Reset(Start);
            VoiceLoadObservation o = default!;
            for (var i = 0; i < 10; i++) o = m.Observe(Tick(i, stability: 0.8));

            Assert.Equal(VoiceLoadBand.InsufficientData, o.State.VoiceLoadBand);
            Assert.Equal(PauseRecommendationLevel.None, o.Recommendation.Pause);
            Assert.Null(o.Recommendation.Message);
        }

        // Score bands map as specified.
        [Theory]
        [InlineData(15, VoiceLoadBand.Low)]
        [InlineData(45, VoiceLoadBand.Moderate)]
        [InlineData(70, VoiceLoadBand.High)]
        [InlineData(90, VoiceLoadBand.PauseRecommended)]
        public void ResolveBand_MapsScoreToExpectedBand(double score, VoiceLoadBand expected)
            => Assert.Equal(expected, VoiceLoadScoreEngine.ResolveBand(score, false, false, 0, dataSufficient: true));

        // Band jumps straight to PauseRecommended only when a safety/health rule has fired.
        [Fact]
        public void ResolveBand_SafetyOrHealth_JumpsToPauseRecommended()
        {
            Assert.Equal(VoiceLoadBand.PauseRecommended, VoiceLoadScoreEngine.ResolveBand(10, isSafetyLocked: true, false, 0, true));
            Assert.Equal(VoiceLoadBand.PauseRecommended, VoiceLoadScoreEngine.ResolveBand(10, false, healthPauseRecommended: true, 0, true));
            Assert.Equal(VoiceLoadBand.PauseRecommended, VoiceLoadScoreEngine.ResolveBand(10, false, false, healthStateRank: 2, true));
        }

        // Anti-spam: a steady moderate session does not emit a stream of messages.
        [Fact]
        public void Messaging_AntiSpam_RateLimitsRepeats()
        {
            var m = new LiveVoiceLoadMonitor(); m.Reset(Start);
            var messages = 0;
            for (var i = 0; i < 600; i++)
                if (m.Observe(Tick(i, stability: 0.62)).Recommendation.Message is not null) messages++;
            Assert.True(messages <= 3);
        }

        // No-medical-wording guarantee at the engine boundary: only known VoiceLoad_ keys are emitted.
        [Fact]
        public void Messaging_EmitsOnlyLocalizationKeys_NeverFreeText()
        {
            var m = new LiveVoiceLoadMonitor(); m.Reset(Start);
            for (var i = 0; i < 2000; i++)
            {
                var msg = m.Observe(Tick(i, stability: 0.4, strain: 0.3)).Recommendation.Message;
                if (msg is not null)
                    Assert.StartsWith("VoiceLoad_", msg.LocalizationKey);
            }
        }

        [Fact]
        public void MessageKeys_AllUseVoiceLoadPrefix()
        {
            foreach (var key in new[]
            {
                VoiceLoadMessageKeys.ContinueCalmly, VoiceLoadMessageKeys.PauseSoon, VoiceLoadMessageKeys.PauseNow,
                VoiceLoadMessageKeys.LowerIntensity, VoiceLoadMessageKeys.HydrationGentle, VoiceLoadMessageKeys.EndSession,
                VoiceLoadMessageKeys.RecoveryWait, VoiceLoadMessageKeys.RecoveryReady,
                VoiceLoadMessageKeys.RecoveryContinueGently, VoiceLoadMessageKeys.RecoveryEndForToday
            })
                Assert.StartsWith("VoiceLoad_", key);
        }

        // Evidence is populated with enum-name/code fields only (no free-text, no medical claims).
        [Fact]
        public void Evidence_IsPopulated_WithoutFreeText()
        {
            var m = new LiveVoiceLoadMonitor(); m.Reset(Start);
            VoiceLoadObservation o = default!;
            for (var i = 0; i < 200; i++) o = m.Observe(Tick(i, stability: 0.5, strain: 0.3));
            var trend = m.BuildSessionTrendSummary();
            var ev = m.BuildEvidence(o, RecoveryReadiness.InsufficientData, trend);

            Assert.False(string.IsNullOrEmpty(ev.VoiceLoadBand));
            Assert.False(string.IsNullOrEmpty(ev.PauseRecommendationLevel));
            Assert.False(string.IsNullOrEmpty(ev.HydrationContextLevel));
            Assert.Equal(o.State.VoiceLoadScore, ev.VoiceLoadScore);
        }
    }
}
