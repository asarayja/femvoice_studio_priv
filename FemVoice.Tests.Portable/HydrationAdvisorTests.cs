using System;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    public class HydrationAdvisorTests
    {
        [Fact]
        public void Advisor_IgnoresSingleSpike()
        {
            var advisor = CreateAdvisor();
            advisor.Evaluate(State(resonance: 0.70, stability: 0.70));

            var advice = advisor.Evaluate(State(resonance: 0.05, stability: 0.05));

            Assert.False(advice.Suggested);
            Assert.True(advice.ResonanceDrift < 0.04);
        }

        [Fact]
        public void Advisor_SuggestsHydrationFromResonanceDriftAndVariance()
        {
            var advisor = CreateAdvisor();
            HydrationAdvice? suggestedAdvice = null;
            HydrationAdvice advice = default!;

            for (var i = 0; i < 18; i++)
            {
                advice = advisor.Evaluate(State(
                    resonance: 0.68 - i * 0.012,
                    stability: i % 2 == 0 ? 0.72 : 0.56,
                    holding: true));
                suggestedAdvice ??= advice.Suggested ? advice : null;
            }

            Assert.NotNull(suggestedAdvice);
            Assert.Equal("HYDRATION_RESONANCE_DRIFT", suggestedAdvice!.ReasonCode);
            Assert.True(advice.AccumulatedLoad > 0);
        }

        [Fact]
        public void Advisor_DoesNotSuggestDuringSafetyLock()
        {
            var advisor = CreateAdvisor(new HydrationAdvisorOptions
            {
                SuggestionThreshold = 0.20,
                AccumulatedLoadThreshold = 0.10,
                BaselineResonance = 0.70,
                BaselineStability = 0.70
            });

            var advice = advisor.Evaluate(State(
                resonance: 0.45,
                stability: 0.45,
                safetyLocked: true,
                holding: true));

            Assert.False(advice.Suggested);
        }

        [Fact]
        public void Advisor_PublishesHydrationSuggestedEvent()
        {
            var advisor = CreateAdvisor();
            var count = 0;
            advisor.HydrationSuggested += (_, _) => count++;

            for (var i = 0; i < 18; i++)
            {
                advisor.Evaluate(State(
                    resonance: 0.68 - i * 0.012,
                    stability: i % 2 == 0 ? 0.72 : 0.56,
                    holding: true));
            }

            Assert.True(count > 0);
        }

        [Fact]
        public void Advisor_RateLimitsRepeatedSuggestions()
        {
            var advisor = CreateAdvisor(new HydrationAdvisorOptions
            {
                SpikeLimit = 0.25,
                BaselineResonance = 0.70,
                BaselineStability = 0.70,
                ResonanceDriftThreshold = 0.04,
                StabilityVarianceThreshold = 0.025,
                AccumulatedLoadThreshold = 0.20,
                SuggestionThreshold = 0.60,
                MinimumSuggestionInterval = TimeSpan.FromMinutes(5)
            });
            var eventCount = 0;
            advisor.HydrationSuggested += (_, _) => eventCount++;
            var start = new DateTime(2026, 6, 1, 12, 0, 0);
            HydrationAdvice? first = null;

            for (var i = 0; i < 18; i++)
            {
                var advice = advisor.Evaluate(State(
                    resonance: 0.68 - i * 0.012,
                    stability: i % 2 == 0 ? 0.72 : 0.56,
                    holding: true,
                    timestamp: start.AddSeconds(i)));
                first ??= advice.Suggested ? advice : null;
            }

            var suppressed = advisor.Evaluate(State(
                resonance: 0.45,
                stability: 0.50,
                holding: true,
                timestamp: start.AddMinutes(1)));
            var afterCooldown = advisor.Evaluate(State(
                resonance: 0.44,
                stability: 0.49,
                holding: true,
                timestamp: start.AddMinutes(6)));

            Assert.NotNull(first);
            Assert.False(suppressed.Suggested);
            Assert.True(suppressed.Score >= 0.60);
            Assert.True(afterCooldown.Suggested);
            Assert.Equal(2, eventCount);
        }

        [Fact]
        public void Advisor_DoesNotChangeSafetyState()
        {
            var advisor = CreateAdvisor(new HydrationAdvisorOptions
            {
                SuggestionThreshold = 0.20,
                AccumulatedLoadThreshold = 0.10,
                BaselineResonance = 0.70,
                BaselineStability = 0.70
            });
            var lockedState = State(
                resonance: 0.45,
                stability: 0.45,
                safetyLocked: true,
                holding: true);

            var advice = advisor.Evaluate(lockedState);

            Assert.False(advice.Suggested);
            Assert.True(lockedState.IsSafetyLocked);
        }

        [Fact]
        public void HydrationFeedbackMapper_MapsSuggestionToHydrationPriority()
        {
            var mapper = new HydrationFeedbackMapper();
            var advice = new HydrationAdvice
            {
                Suggested = true,
                ReasonCode = "HYDRATION_RESONANCE_DRIFT",
                Score = 0.80
            };

            var candidate = mapper.Map(advice);
            var context = mapper.BuildContext(advice, State(resonance: 0.60, stability: 0.60));

            Assert.NotNull(candidate);
            Assert.Equal("VoiceHealthFeedback_Hydration", candidate!.Message);
            Assert.Equal(FeedbackPriority.HydrationSuggestion, candidate.Priority);
            Assert.Equal("HydrationAdvisor", candidate.Source);
            Assert.False(context.IsSafetyFreezeActive);
        }

        private static HydrationAdvisor CreateAdvisor(HydrationAdvisorOptions? options = null)
            => new(options ?? new HydrationAdvisorOptions
            {
                SpikeLimit = 0.25,
                BaselineResonance = 0.70,
                BaselineStability = 0.70,
                ResonanceDriftThreshold = 0.04,
                StabilityVarianceThreshold = 0.025,
                AccumulatedLoadThreshold = 0.35,
                SuggestionThreshold = 0.60
            });

        private static ExerciseLiveState State(
            double resonance,
            double stability,
            bool safetyLocked = false,
            bool holding = false,
            DateTime? timestamp = null,
            int elapsedSeconds = 0)
            => new()
            {
                PrimaryMetricScore = resonance,
                SecondaryMetricScore = stability,
                StabilityScore = stability,
                IsInComfortZone = true,
                IsHoldingCorrectly = holding,
                HoldProgress = holding ? 0.7 : 0.2,
                IsSafetyLocked = safetyLocked,
                Timestamp = timestamp ?? DateTime.Now,
                SessionElapsedSeconds = elapsedSeconds
            };

        // Mirrors the proven Advisor_RateLimitsRepeatedSuggestions ramp options so the
        // RC-0 context-gating tests below drive real suggestions deterministically.
        private static HydrationAdvisorOptions RampOptions(TimeSpan interval, int budget = 3)
            => new()
            {
                SpikeLimit = 0.25,
                BaselineResonance = 0.70,
                BaselineStability = 0.70,
                ResonanceDriftThreshold = 0.04,
                StabilityVarianceThreshold = 0.025,
                AccumulatedLoadThreshold = 0.20,
                SuggestionThreshold = 0.60,
                MinimumSuggestionInterval = interval,
                SuggestionBudget = budget
            };

        // ── RC-0 context-aware hydration tests ──────────────────────────────────────
        // NOTE: these were verified against the real HydrationAdvisor via a standalone
        // net10.0 harness on the Linux dev box; they were NOT run through xUnit here
        // because the test project targets net10.0-windows (no WindowsDesktop runtime).

        [Fact]
        public void Advisor_ZeroElapsed_PreservesExistingBehavior()
        {
            // SessionElapsedSeconds == 0 (the existing helper default) is the escape hatch:
            // the minimum-practice gate must NOT block suggestions when elapsed is unknown.
            var advisor = CreateAdvisor();
            HydrationAdvice? first = null;

            for (var i = 0; i < 18; i++)
            {
                var advice = advisor.Evaluate(State(
                    resonance: 0.68 - i * 0.012,
                    stability: i % 2 == 0 ? 0.72 : 0.56,
                    holding: true,
                    elapsedSeconds: 0));
                first ??= advice.Suggested ? advice : null;
            }

            Assert.NotNull(first);
            Assert.Equal("HYDRATION_RESONANCE_DRIFT", first!.ReasonCode);
        }

        [Fact]
        public void Advisor_ShortSession_SuppressesBeforeMinimumPractice()
        {
            var advisor = CreateAdvisor();
            var anyBefore = false;

            // Loaded ticks, but elapsed 5..22s (< default 120s) → never suggest.
            for (var i = 0; i < 18; i++)
            {
                var advice = advisor.Evaluate(State(
                    resonance: 0.68 - i * 0.012,
                    stability: i % 2 == 0 ? 0.72 : 0.56,
                    holding: true,
                    elapsedSeconds: 5 + i));
                anyBefore |= advice.Suggested;
            }

            Assert.False(anyBefore);

            // Once real practice time passes the gate, a suggestion can fire.
            var afterGate = advisor.Evaluate(State(
                resonance: 0.40,
                stability: 0.55,
                holding: true,
                elapsedSeconds: 130));

            Assert.True(afterGate.Suggested);
        }

        [Fact]
        public void Advisor_SuggestionBudget_CapsAndResetReArms()
        {
            var advisor = CreateAdvisor(RampOptions(TimeSpan.FromSeconds(1), budget: 3));
            var start = new DateTime(2026, 6, 1, 12, 0, 0);

            var count = 0;
            for (var i = 0; i < 40; i++)
            {
                var advice = advisor.Evaluate(State(
                    resonance: 0.60 - (i % 6) * 0.03,
                    stability: i % 2 == 0 ? 0.72 : 0.52,
                    holding: true,
                    timestamp: start.AddSeconds(i)));
                if (advice.Suggested) count++;
            }

            Assert.Equal(3, count);   // capped at the per-session budget

            advisor.Reset();
            var countAfterReset = 0;
            for (var i = 0; i < 40; i++)
            {
                var advice = advisor.Evaluate(State(
                    resonance: 0.60 - (i % 6) * 0.03,
                    stability: i % 2 == 0 ? 0.72 : 0.52,
                    holding: true,
                    timestamp: start.AddMinutes(5).AddSeconds(i)));
                if (advice.Suggested) countAfterReset++;
            }

            Assert.Equal(3, countAfterReset);   // budget re-armed by Reset()
        }

        [Fact]
        public void Advisor_Reset_ClearsCooldownLeakBetweenSessions()
        {
            var advisor = CreateAdvisor(RampOptions(TimeSpan.FromMinutes(5)));
            var start = new DateTime(2026, 6, 1, 12, 0, 0);
            HydrationAdvice? first = null;

            for (var i = 0; i < 18; i++)
            {
                var advice = advisor.Evaluate(State(
                    resonance: 0.68 - i * 0.012,
                    stability: i % 2 == 0 ? 0.72 : 0.56,
                    holding: true,
                    timestamp: start.AddSeconds(i)));
                first ??= advice.Suggested ? advice : null;
            }

            // Within the 5-minute cooldown a high-score tick is still suppressed.
            var suppressed = advisor.Evaluate(State(
                resonance: 0.45, stability: 0.50, holding: true, timestamp: start.AddMinutes(1)));

            Assert.NotNull(first);
            Assert.False(suppressed.Suggested);
            Assert.True(suppressed.Score >= 0.60);

            // Reset (called per session in ExerciseSessionRecorder.BeginSession) clears
            // _lastSuggestionAt, so a new session is NOT suppressed by the prior session's
            // cooldown — the leak this RC-0 fix targets.
            advisor.Reset();
            HydrationAdvice? afterReset = null;
            for (var i = 0; i < 18; i++)
            {
                var advice = advisor.Evaluate(State(
                    resonance: 0.68 - i * 0.012,
                    stability: i % 2 == 0 ? 0.72 : 0.56,
                    holding: true,
                    timestamp: start.AddMinutes(2).AddSeconds(i)));
                afterReset ??= advice.Suggested ? advice : null;
            }

            Assert.NotNull(afterReset);
            Assert.Equal("HYDRATION_RESONANCE_DRIFT", afterReset!.ReasonCode);
        }

        [Fact]
        public void Advisor_FatigueContext_SelectsWithRestReasonCode()
        {
            var advisor = CreateAdvisor();
            HydrationAdvice? first = null;

            for (var i = 0; i < 18; i++)
            {
                var advice = advisor.Evaluate(
                    State(resonance: 0.68 - i * 0.012, stability: i % 2 == 0 ? 0.72 : 0.56, holding: true),
                    fatigueScore: 0.6,
                    fatigueDetected: true);
                first ??= advice.Suggested ? advice : null;
            }

            Assert.NotNull(first);
            Assert.Equal("HYDRATION_WITH_REST", first!.ReasonCode);
        }

        [Fact]
        public void Advisor_SecondSuggestion_UsesSustainedReasonCode()
        {
            var advisor = CreateAdvisor(RampOptions(TimeSpan.FromMinutes(1)));
            var start = new DateTime(2026, 6, 1, 12, 0, 0);
            HydrationAdvice? first = null;

            for (var i = 0; i < 18; i++)
            {
                var advice = advisor.Evaluate(State(
                    resonance: 0.68 - i * 0.012,
                    stability: i % 2 == 0 ? 0.72 : 0.56,
                    holding: true,
                    timestamp: start.AddSeconds(i)));
                first ??= advice.Suggested ? advice : null;
            }

            var second = advisor.Evaluate(State(
                resonance: 0.44, stability: 0.49, holding: true, timestamp: start.AddMinutes(2)));

            Assert.NotNull(first);
            Assert.Equal("HYDRATION_RESONANCE_DRIFT", first!.ReasonCode);
            Assert.True(second.Suggested);
            Assert.Equal("HYDRATION_SUSTAINED", second.ReasonCode);
        }

        [Fact]
        public void HydrationProducers_ShareBasicReasonCode_ForCoalescing()
        {
            // The standalone advisor and the supervisor path must emit the SAME candidate
            // ReasonCode for the everyday hydration nudge so FeedbackConsistencyGuard's
            // per-ReasonCode rate limit coalesces the duplicate visible message.
            var advisorCandidate = new HydrationFeedbackMapper().Map(new HydrationAdvice
            {
                Suggested = true,
                ReasonCode = "HYDRATION_RESONANCE_DRIFT",
                Score = 0.80
            });
            var supervisorCandidate = new VocalHealthFeedbackMapper().Map(new VocalHealthDecision
            {
                State = HealthSafetyState.Normal,
                HydrationSuggested = true,
                ReasonCode = "NORMAL"
            });

            Assert.NotNull(advisorCandidate);
            Assert.NotNull(supervisorCandidate);
            Assert.Equal("HYDRATION_SUGGESTED", advisorCandidate!.ReasonCode);
            Assert.Equal("HYDRATION_SUGGESTED", supervisorCandidate!.ReasonCode);
            Assert.Equal(advisorCandidate.ReasonCode, supervisorCandidate.ReasonCode);
            Assert.Equal(FeedbackPriority.HydrationSuggestion, advisorCandidate.Priority);
            Assert.Equal(FeedbackPriority.HydrationSuggestion, supervisorCandidate.Priority);
        }

        [Fact]
        public void HydrationFeedbackMapper_SustainedAndWithRest_MapToNewKeys_WithoutRaisingPriority()
        {
            var sustained = new HydrationFeedbackMapper().Map(new HydrationAdvice
            {
                Suggested = true, ReasonCode = "HYDRATION_SUSTAINED", Score = 0.80
            });
            var withRest = new HydrationFeedbackMapper().Map(new HydrationAdvice
            {
                Suggested = true, ReasonCode = "HYDRATION_WITH_REST", Score = 0.80
            });

            Assert.NotNull(sustained);
            Assert.Equal("VoiceHealthFeedback_HydrationSustained", sustained!.Message);
            Assert.Equal(FeedbackPriority.HydrationSuggestion, sustained.Priority);

            Assert.NotNull(withRest);
            Assert.Equal("VoiceHealthFeedback_HydrationWithRest", withRest!.Message);
            Assert.Equal(FeedbackPriority.HydrationSuggestion, withRest.Priority);
        }
    }
}
