using System;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    public class VocalHealthLegacyBridgeTests
    {
        [Fact]
        public void FromWarning_MapsLegacyWarningToCautionDecision()
        {
            var bridge = new VocalHealthLegacyBridge();

            var decision = bridge.FromWarning(new HealthAlertEventArgs
            {
                Timestamp = new DateTime(2026, 5, 29, 12, 0, 0),
                AlertType = IncidentType.Warning
            });

            Assert.Equal(HealthSafetyState.Caution, decision.State);
            Assert.Equal("LEGACY_HEALTH_WARNING", decision.ReasonCode);
            Assert.True(decision.StrainDetected);
            Assert.False(decision.PauseRecommended);
            Assert.Equal(65, bridge.ToHealthScore(decision));
        }

        [Fact]
        public void FromCritical_MapsLegacyCriticalToRestrictDecision()
        {
            var bridge = new VocalHealthLegacyBridge();

            var decision = bridge.FromCritical(new HealthAlertEventArgs
            {
                Timestamp = new DateTime(2026, 5, 29, 12, 0, 0),
                AlertType = IncidentType.Critical
            });

            Assert.Equal(HealthSafetyState.Restrict, decision.State);
            Assert.Equal("LEGACY_HEALTH_CRITICAL", decision.ReasonCode);
            Assert.True(decision.StrainDetected);
            Assert.True(decision.PauseRecommended);
            Assert.Equal(45, bridge.ToHealthScore(decision));
        }

        [Fact]
        public void FromLockout_MapsLegacyLockoutToLockDecision()
        {
            var bridge = new VocalHealthLegacyBridge();

            var decision = bridge.FromLockout(new LockoutEventArgs
            {
                LockoutStartTime = new DateTime(2026, 5, 29, 12, 0, 0),
                Reason = "critical strain"
            });

            Assert.Equal(HealthSafetyState.Lock, decision.State);
            Assert.Equal("LEGACY_VOICE_LOCKOUT", decision.ReasonCode);
            Assert.True(decision.PauseRecommended);
            Assert.Equal(1, decision.StrainScore);
            Assert.Equal(35, bridge.ToHealthScore(decision));
        }
    }
}
