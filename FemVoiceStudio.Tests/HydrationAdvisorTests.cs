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
            DateTime? timestamp = null)
            => new()
            {
                PrimaryMetricScore = resonance,
                SecondaryMetricScore = stability,
                StabilityScore = stability,
                IsInComfortZone = true,
                IsHoldingCorrectly = holding,
                HoldProgress = holding ? 0.7 : 0.2,
                IsSafetyLocked = safetyLocked,
                Timestamp = timestamp ?? DateTime.Now
            };
    }
}
