using System;

namespace FemVoiceStudio.Services
{
    public sealed class VocalHealthLegacyBridge
    {
        public VocalHealthDecision FromWarning(HealthAlertEventArgs alert)
        {
            if (alert == null) throw new ArgumentNullException(nameof(alert));

            return new VocalHealthDecision
            {
                State = HealthSafetyState.Caution,
                ReasonCode = "LEGACY_HEALTH_WARNING",
                StrainDetected = true,
                FatigueDetected = false,
                PauseRecommended = false,
                HydrationSuggested = false,
                StrainScore = 0.65,
                FatigueScore = 0,
                HydrationScore = 0,
                Timestamp = alert.Timestamp == default ? DateTime.Now : alert.Timestamp
            };
        }

        public VocalHealthDecision FromCritical(HealthAlertEventArgs alert)
        {
            if (alert == null) throw new ArgumentNullException(nameof(alert));

            return new VocalHealthDecision
            {
                State = HealthSafetyState.Restrict,
                ReasonCode = "LEGACY_HEALTH_CRITICAL",
                StrainDetected = true,
                FatigueDetected = false,
                PauseRecommended = true,
                HydrationSuggested = false,
                StrainScore = 0.85,
                FatigueScore = 0,
                HydrationScore = 0,
                Timestamp = alert.Timestamp == default ? DateTime.Now : alert.Timestamp
            };
        }

        public VocalHealthDecision FromLockout(LockoutEventArgs lockout)
        {
            if (lockout == null) throw new ArgumentNullException(nameof(lockout));

            return new VocalHealthDecision
            {
                State = HealthSafetyState.Lock,
                ReasonCode = "LEGACY_VOICE_LOCKOUT",
                StrainDetected = true,
                FatigueDetected = false,
                PauseRecommended = true,
                HydrationSuggested = false,
                StrainScore = 1,
                FatigueScore = 0,
                HydrationScore = 0,
                Timestamp = lockout.LockoutStartTime == default ? DateTime.Now : lockout.LockoutStartTime
            };
        }

        public double ToHealthScore(VocalHealthDecision decision)
        {
            if (decision == null) throw new ArgumentNullException(nameof(decision));

            return decision.State switch
            {
                HealthSafetyState.Lock => 35,
                HealthSafetyState.Restrict => 45,
                HealthSafetyState.Caution => 65,
                _ => 100
            };
        }
    }
}
