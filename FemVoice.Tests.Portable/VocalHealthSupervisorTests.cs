using System;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    public class VocalHealthSupervisorTests
    {
        [Fact]
        public void TrendEngine_IgnoresSingleSpike()
        {
            var engine = new VocalHealthTrendEngine(new VocalHealthSupervisorOptions { SpikeLimit = 0.20 });
            engine.Update(State(resonance: 0.70, stability: 0.70));

            var trend = engine.Update(State(resonance: 0.05, stability: 0.05));

            Assert.True(trend.MicroStability > 0.55);
            Assert.True(trend.MicroResonance > 0.55);
        }

        [Fact]
        public void TrendEngine_FiltersNoise()
        {
            var engine = new VocalHealthTrendEngine(new VocalHealthSupervisorOptions { NoiseFloor = 0.03 });
            engine.Update(State(resonance: 0.70, stability: 0.70));

            var trend = engine.Update(State(resonance: 0.71, stability: 0.71));

            Assert.Equal(0.70, trend.MicroStability, 3);
            Assert.Equal(0.70, trend.MicroResonance, 3);
        }

        [Fact]
        public void Supervisor_DetectsAcuteStrainBeforeFatigue()
        {
            var supervisor = CreateSupervisor();
            supervisor.Evaluate(State(resonance: 0.70, stability: 0.70, hold: 0.80));

            var decision = supervisor.Evaluate(State(resonance: 0.65, stability: 0.35, hold: 0.20, inComfortZone: false));

            Assert.True(decision.StrainDetected);
            Assert.False(decision.FatigueDetected);
            Assert.Equal("STRAIN_DETECTED", decision.ReasonCode);
        }

        [Fact]
        public void Supervisor_DetectsSlowFatigueFromMesoDrift()
        {
            var supervisor = CreateSupervisor();
            VocalHealthDecision decision = default!;

            for (var i = 0; i < 16; i++)
            {
                decision = supervisor.Evaluate(State(
                    resonance: 0.70 - i * 0.020,
                    stability: 0.70 - i * 0.020,
                    hold: 0.75));
            }

            Assert.True(decision.FatigueDetected);
            Assert.True(decision.PauseRecommended);
        }

        [Fact]
        public void Supervisor_SeparatesHydrationFromPause()
        {
            var supervisor = CreateSupervisor();
            VocalHealthDecision decision = default!;

            for (var i = 0; i < 12; i++)
            {
                decision = supervisor.Evaluate(State(
                    resonance: 0.62 - i * 0.014,
                    stability: i % 2 == 0 ? 0.72 : 0.56,
                    hold: 0.75));
            }

            Assert.True(decision.HydrationSuggested);
            Assert.False(decision.PauseRecommended);
            Assert.NotEqual(HealthSafetyState.Lock, decision.State);
        }

        [Fact]
        public void Supervisor_EscalatesRestrictAndLockFromRepeatedSafetyBreaches()
        {
            var supervisor = CreateSupervisor();

            var first = supervisor.Evaluate(State(resonance: 0.50, stability: 0.30, safetyLocked: true));
            supervisor.Evaluate(State(resonance: 0.50, stability: 0.30, safetyLocked: true));
            var third = supervisor.Evaluate(State(resonance: 0.50, stability: 0.30, safetyLocked: true));

            Assert.Equal(HealthSafetyState.Restrict, first.State);
            Assert.Equal(HealthSafetyState.Lock, third.State);
        }

        [Fact]
        public void Supervisor_RequiresStableWindowForRecovery()
        {
            var supervisor = CreateSupervisor();
            supervisor.Evaluate(State(resonance: 0.50, stability: 0.30, safetyLocked: true));
            supervisor.Evaluate(State(resonance: 0.50, stability: 0.30, safetyLocked: true));
            supervisor.Evaluate(State(resonance: 0.50, stability: 0.30, safetyLocked: true));

            VocalHealthDecision decision = default!;
            for (var i = 0; i < 4; i++)
            {
                decision = supervisor.Evaluate(State(resonance: 0.78, stability: 0.78, hold: 0.90));
            }

            Assert.Equal(HealthSafetyState.Lock, decision.State);

            decision = supervisor.Evaluate(State(resonance: 0.78, stability: 0.78, hold: 0.90));

            Assert.Equal(HealthSafetyState.Restrict, decision.State);
        }

        [Fact]
        public void Supervisor_PublishesPauseAndHydrationEvents()
        {
            var supervisor = CreateSupervisor();
            var pauseCount = 0;
            var hydrationCount = 0;
            supervisor.PauseRecommended += (_, _) => pauseCount++;
            supervisor.HydrationSuggested += (_, _) => hydrationCount++;

            for (var i = 0; i < 16; i++)
            {
                supervisor.Evaluate(State(
                    resonance: 0.70 - i * 0.020,
                    stability: 0.70 - i * 0.020,
                    hold: 0.75));
            }

            Assert.True(pauseCount > 0);
            Assert.True(hydrationCount > 0);
        }

        private static VocalHealthSupervisor CreateSupervisor()
            => new(new VocalHealthSupervisorOptions
            {
                SpikeLimit = 0.25,
                StableSamplesForRecovery = 5,
                StrainSamplesForRestrict = 3,
                RestrictCyclesForLock = 3,
                BaselineResonance = 0.70,
                BaselineStability = 0.70
            });

        private static ExerciseLiveState State(
            double resonance,
            double stability,
            double hold = 0.5,
            bool inComfortZone = true,
            bool safetyLocked = false)
            => new()
            {
                PrimaryMetricScore = resonance,
                SecondaryMetricScore = stability,
                StabilityScore = stability,
                HoldProgress = hold,
                IsInComfortZone = inComfortZone,
                IsHoldingCorrectly = hold >= 0.5,
                IsSafetyLocked = safetyLocked,
                Timestamp = DateTime.Now
            };
    }
}
