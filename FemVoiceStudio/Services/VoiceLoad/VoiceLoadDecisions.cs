using System;
using System.Collections.Generic;
using FemVoiceStudio.Models.VoiceLoad;

namespace FemVoiceStudio.Services.VoiceLoad
{
    /// <summary>Localization KEYS (nb source in Strings.resx) for Sprint F coach messages.</summary>
    public static class VoiceLoadMessageKeys
    {
        public const string ContinueCalmly  = "VoiceLoad_ContinueCalmly";
        public const string PauseSoon        = "VoiceLoad_PauseSoon";
        public const string PauseNow         = "VoiceLoad_PauseNow";
        public const string LowerIntensity   = "VoiceLoad_LowerIntensity";
        public const string HydrationGentle  = "VoiceLoad_HydrationGentle";
        public const string EndSession       = "VoiceLoad_EndSession";
        public const string RecoveryWait     = "VoiceLoad_RecoveryWait";
        public const string RecoveryReady    = "VoiceLoad_RecoveryReady";
        public const string RecoveryContinueGently = "VoiceLoad_RecoveryContinueGently";
        public const string RecoveryEndForToday    = "VoiceLoad_RecoveryEndForToday";
    }

    /// <summary>
    /// Sprint F (Agent 3) — pure pause-recommendation decision. Reads load state + the
    /// already-computed health signals; NEVER overrides a safety lock (it only recommends),
    /// and never escalates straight to PauseRecommended unless a safety/health rule fired.
    /// </summary>
    public static class PauseIntelligenceEngine
    {
        public static (PauseRecommendationLevel Level, IReadOnlyList<string> Reasons) Decide(
            VoiceLoadBand band,
            VoiceLoadTrendDirection trend,
            bool healthPauseRecommended,
            bool isSafetyLocked,
            bool loadPersistedAfterPause,
            bool dataSufficient)
        {
            var reasons = new List<string>();
            if (!dataSufficient)
                return (PauseRecommendationLevel.None, new[] { "INSUFFICIENT_DATA" });

            // Persisted high load after a pause, or an escalating safety/health state, is the
            // only path to the strongest (conservative) recommendation.
            if ((band == VoiceLoadBand.PauseRecommended && loadPersistedAfterPause)
                || (isSafetyLocked && loadPersistedAfterPause))
            {
                reasons.Add("HIGH_LOAD_PERSISTED_AFTER_PAUSE");
                return (PauseRecommendationLevel.EndSession, reasons);
            }

            if (band == VoiceLoadBand.PauseRecommended || isSafetyLocked || healthPauseRecommended)
            {
                if (isSafetyLocked) reasons.Add("SAFETY_LOCK");
                if (healthPauseRecommended) reasons.Add("HEALTH_PAUSE");
                if (band == VoiceLoadBand.PauseRecommended) reasons.Add("LOAD_BAND_PAUSE");
                return (PauseRecommendationLevel.Now, reasons);
            }

            // NOW when load is high OR stability is clearly declining (brief wording).
            if (band == VoiceLoadBand.High)
            {
                reasons.Add("LOAD_BAND_HIGH");
                return (PauseRecommendationLevel.Now, reasons);
            }

            if (band == VoiceLoadBand.Moderate)
            {
                reasons.Add("LOAD_BAND_MODERATE");
                if (trend == VoiceLoadTrendDirection.Worsening)
                {
                    reasons.Add("TREND_WORSENING");
                    return (PauseRecommendationLevel.Now, reasons);
                }
                return (PauseRecommendationLevel.Soon, reasons);
            }

            // Even at low load, a clearly worsening trend earns a gentle "pause soon".
            if (band == VoiceLoadBand.Low && trend == VoiceLoadTrendDirection.Worsening)
            {
                reasons.Add("TREND_WORSENING");
                return (PauseRecommendationLevel.Soon, reasons);
            }

            return (PauseRecommendationLevel.None, reasons);
        }
    }

    /// <summary>
    /// Sprint F (Agent 4) — pure practice-readiness evaluation from a before/after-pause
    /// comparison. Assesses PRACTICE readiness only; never a medical-recovery claim.
    /// </summary>
    public static class RecoveryReadinessEvaluator
    {
        /// <summary>A minimal snapshot taken just before a pause and again after resuming.</summary>
        public readonly record struct Snapshot(double StabilityEma, double StrainEma, double DropoutRate, bool DataSufficient);

        public static RecoveryReadinessResult Evaluate(Snapshot before, Snapshot after, double pausedSeconds)
        {
            if (!before.DataSufficient || !after.DataSufficient || pausedSeconds < 5)
                return new RecoveryReadinessResult
                {
                    Readiness = RecoveryReadiness.InsufficientData,
                    Reasons = new[] { "INSUFFICIENT_DATA" }
                };

            var stabilityDelta = after.StabilityEma - before.StabilityEma;
            var strainDelta = after.StrainEma - before.StrainEma;
            var reasons = new List<string>();

            // Clearly worse after the pause despite resting → stop for today (conservative).
            if (strainDelta > 0.15 || stabilityDelta < -0.15)
            {
                reasons.Add("STILL_DECLINING_AFTER_PAUSE");
                return new RecoveryReadinessResult
                {
                    Readiness = RecoveryReadiness.EndForToday,
                    MessageKey = VoiceLoadMessageKeys.RecoveryEndForToday,
                    Reasons = reasons
                };
            }

            // Recovered: more stable and less strain than before the pause.
            if (stabilityDelta >= 0.05 && strainDelta <= 0.0 && after.StrainEma < 0.4)
            {
                reasons.Add("STABILITY_IMPROVED_AFTER_PAUSE");
                return new RecoveryReadinessResult
                {
                    Readiness = RecoveryReadiness.Ready,
                    MessageKey = VoiceLoadMessageKeys.RecoveryReady,
                    Reasons = reasons
                };
            }

            // Lingering strain or unclear improvement → wait a little longer.
            if (after.StrainEma >= 0.5 || strainDelta > 0.05)
            {
                reasons.Add("STRAIN_LINGERS_AFTER_PAUSE");
                return new RecoveryReadinessResult
                {
                    Readiness = RecoveryReadiness.WaitLonger,
                    MessageKey = VoiceLoadMessageKeys.RecoveryWait,
                    Reasons = reasons
                };
            }

            // Roughly back to baseline → resume gently at low intensity.
            reasons.Add("NEAR_BASELINE_AFTER_PAUSE");
            return new RecoveryReadinessResult
            {
                Readiness = RecoveryReadiness.ContinueGently,
                MessageKey = VoiceLoadMessageKeys.RecoveryContinueGently,
                Reasons = reasons
            };
        }
    }

    /// <summary>
    /// Sprint F (Agent 7) — pure mapping from the technical decision to ONE gentle-coach
    /// message category + localization key, in priority order (Safety/Health-driven pause
    /// first, then hydration, then trend/continue). Returns None when nothing should be said.
    /// </summary>
    public static class GentleCoachComposer
    {
        public static GentleCoachMessage Compose(
            PauseRecommendationLevel pause,
            HydrationContextLevel hydration,
            VoiceLoadBand band,
            VoiceLoadTrendDirection trend,
            bool dataSufficient)
        {
            if (!dataSufficient)
                return new GentleCoachMessage { Category = GentleCoachCategory.InsufficientData, LocalizationKey = string.Empty };

            switch (pause)
            {
                case PauseRecommendationLevel.EndSession:
                    return Msg(GentleCoachCategory.EndSession, VoiceLoadMessageKeys.EndSession);
                case PauseRecommendationLevel.Now:
                    // A worsening trend warrants the "lower intensity + pause" framing.
                    return trend == VoiceLoadTrendDirection.Worsening
                        ? Msg(GentleCoachCategory.LowerIntensity, VoiceLoadMessageKeys.LowerIntensity)
                        : Msg(GentleCoachCategory.PauseNow, VoiceLoadMessageKeys.PauseNow);
                case PauseRecommendationLevel.Soon:
                    return Msg(GentleCoachCategory.PauseSoon, VoiceLoadMessageKeys.PauseSoon);
            }

            // No pause needed — surface hydration context if present, else a calm continue.
            if (hydration != HydrationContextLevel.None)
                return Msg(GentleCoachCategory.HydrationGentle, VoiceLoadMessageKeys.HydrationGentle);

            return Msg(GentleCoachCategory.ContinueCalmly, VoiceLoadMessageKeys.ContinueCalmly);
        }

        private static GentleCoachMessage Msg(GentleCoachCategory category, string key)
            => new() { Category = category, LocalizationKey = key };
    }
}
