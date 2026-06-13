using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Tests for <see cref="RecoveryIntelligenceService"/> — the PREDICTIVE recovery
    /// layer. No mocking: the predictive core is a pure function over
    /// <see cref="RecoveryHistorySnapshot"/>, exercised with hand-computed expectations,
    /// and the store path uses the real <see cref="InMemorySessionAnalyticsRepository"/>
    /// + <see cref="SessionAnalyticsStore"/> (the in-repo fake), following the
    /// RecoveryScorerTests / RecoveryFirstOrderingTests patterns.
    ///
    /// Formulas/thresholds under test (mirror the production XML docs):
    ///   • TrainingLoad/session = clamp(composite/100,0,1)·durationMinutes (0.5 if unscored).
    ///   • ACWR = acute / (chronic / 4); &gt;1.3 ⇒ overload, &gt;1.5 ⇒ urgent, (1.15,1.3] ⇒ watch.
    ///   • RecoveryDebt = clamp(max(0, sessions − hours/24)/7·100, 0, 100); ≥60 ⇒ high.
    ///   • ComfortSlope = least-squares slope (need ≥3 pts); ≤−1.5 ⇒ declining, ≤−0.5 ⇒ watch.
    ///   • OvertrainingPredicted reuses the scorer's branch: sessions&gt;5 AND hours&lt;12.
    /// </summary>
    public class RecoveryIntelligenceServiceTests
    {
        private static readonly RecoveryIntelligenceService Service = new();
        private static readonly DateTime Now = new(2026, 6, 7, 12, 0, 0, DateTimeKind.Utc);

        private static SessionAnalyticsStore NewStore() =>
            new(new InMemorySessionAnalyticsRepository());

        private static RecoveryHistorySnapshot EmptySnapshot() => new()
        {
            HoursSinceLastSession = double.PositiveInfinity,
            RecentComfortScores = Array.Empty<double>()
        };

        // ──────────────────────────────────────────────────────────────────────
        // 1. Empty / new user ⇒ no flags, WellRecovered, severity None.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Forecast_EmptyHistory_NoFlagsWellRecovered()
        {
            var forecast = Service.Forecast(EmptySnapshot());

            Assert.False(forecast.OvertrainingPredicted);
            Assert.False(forecast.RecoveryDebtHigh);
            Assert.False(forecast.TrainingOverload);
            Assert.False(forecast.ComfortDeclining);
            Assert.Equal(RecoverySeverity.None, forecast.Severity);
            Assert.Equal(RecoveryStatus.WellRecovered, forecast.Current.Status);
            Assert.Equal(0.0, forecast.AcuteChronicWorkloadRatio, 6);
            Assert.Equal(0.0, forecast.RecoveryDebt, 6);
        }

        [Fact]
        public void Forecast_LightHistory_StaysNoneAndRested()
        {
            // 2 sessions, 48h rest (debt fully paid), no strain, flat comfort.
            var snapshot = EmptySnapshot() with
            {
                SessionsLast7Days = 2,
                SessionsLast28Days = 6,
                HoursSinceLastSession = 48.0,
                AcuteTrainingLoad = 80.0,   // ~ chronic/4 ⇒ ACWR ≈ 1.0
                ChronicTrainingLoad = 320.0,
                RecentComfortScores = new[] { 70.0, 71.0, 70.0 }
            };

            var forecast = Service.Forecast(snapshot);

            Assert.Equal(RecoverySeverity.None, forecast.Severity);
            Assert.False(forecast.TrainingOverload);
            Assert.False(forecast.RecoveryDebtHigh);
            Assert.Equal(0.0, forecast.RecoveryDebt, 6); // 2 - 48/24 = 0
            Assert.Equal(RecoveryStatus.WellRecovered, forecast.Current.Status);
        }

        // ──────────────────────────────────────────────────────────────────────
        // 2. High density + short rest ⇒ OvertrainingPredicted FOR THE FORECAST even
        //    with NO strain/safety event logged (the predictive point).
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Forecast_DenseAndUnrested_PredictsOvertrainingBeforeAnyStrain()
        {
            var snapshot = EmptySnapshot() with
            {
                SessionsLast7Days = 8,
                SessionsLast28Days = 20,
                HoursSinceLastSession = 2.0,
                // No strain, no safety locks, no fatigue — purely a density/rest signal.
                RecentComfortScores = new[] { 70.0, 70.0, 70.0 }
            };

            var forecast = Service.Forecast(snapshot);

            Assert.True(forecast.OvertrainingPredicted);
            Assert.InRange(forecast.Current.Score, 0.0, 100.0); // score finite & clamped
            Assert.True(forecast.Severity >= RecoverySeverity.Recommend);
            Assert.Contains("training density", forecast.Recommendation);
        }

        // ──────────────────────────────────────────────────────────────────────
        // 3. CONTRAST: the same density makes the scorer's overtraining branch FIRE
        //    once fed a full input, whereas today's live 0-input leaves it dormant.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void BuildScoreInput_FullDensity_FiresScorerOvertrainingBranch_UnlikeZeroInput()
        {
            var scorer = new RecoveryScorer();

            // Today's live path: density/rest/prior-fatigue all left at 0.
            var legacyZeroInput = new RecoveryScoreInput
            {
                SessionsLast7Days = 0,
                HoursSinceLastSession = 0.0
            };
            var legacy = scorer.Score(legacyZeroInput);

            // New path: the service fills the previously-empty fields from history.
            var snapshot = EmptySnapshot() with
            {
                SessionsLast7Days = 8,
                HoursSinceLastSession = 2.0
            };
            var fullInput = RecoveryIntelligenceService.BuildScoreInput(snapshot);
            var full = scorer.Score(fullInput);

            // Legacy 0-input: branch dormant ⇒ perfect 100 / WellRecovered, no density note.
            Assert.Equal(100.0, legacy.Score, 6);
            Assert.DoesNotContain("training density", legacy.Explanation, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("treningsmengde", legacy.Explanation, StringComparison.OrdinalIgnoreCase);

            // Full input: overtraining branch fires (−10) ⇒ 90 and an explicit density note.
            Assert.Equal(90.0, full.Score, 5);
            Assert.True(
                full.Explanation.Contains("training density", StringComparison.OrdinalIgnoreCase)
                || full.Explanation.Contains("treningsmengde", StringComparison.OrdinalIgnoreCase),
                full.Explanation);
            Assert.True(full.Score < legacy.Score);
        }

        // ──────────────────────────────────────────────────────────────────────
        // 4. ACWR > 1.3 ⇒ TrainingOverload (load ramping faster than adapted to).
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Forecast_AcwrAboveThreshold_FlagsTrainingOverload()
        {
            // acute 140, chronic 400 ⇒ weekly 100 ⇒ ACWR 1.4 (>1.3, ≤1.5).
            var snapshot = EmptySnapshot() with
            {
                SessionsLast7Days = 4,
                SessionsLast28Days = 12,
                HoursSinceLastSession = 30.0,
                AcuteTrainingLoad = 140.0,
                ChronicTrainingLoad = 400.0,
                RecentComfortScores = new[] { 70.0, 70.0, 70.0 }
            };

            var forecast = Service.Forecast(snapshot);

            Assert.Equal(1.4, forecast.AcuteChronicWorkloadRatio, 6);
            Assert.True(forecast.TrainingOverload);
            Assert.Equal(RecoverySeverity.Recommend, forecast.Severity);
            Assert.Contains("climbing faster", forecast.Recommendation);
        }

        [Fact]
        public void Forecast_AcwrSeverelyHigh_EscalatesToUrgent()
        {
            // acute 200, chronic 400 ⇒ weekly 100 ⇒ ACWR 2.0 (>1.5 ⇒ Urgent).
            var snapshot = EmptySnapshot() with
            {
                SessionsLast7Days = 4,
                SessionsLast28Days = 10,
                HoursSinceLastSession = 30.0,
                AcuteTrainingLoad = 200.0,
                ChronicTrainingLoad = 400.0,
                RecentComfortScores = new[] { 70.0, 70.0, 70.0 }
            };

            var forecast = Service.Forecast(snapshot);

            Assert.Equal(2.0, forecast.AcuteChronicWorkloadRatio, 6);
            Assert.True(forecast.TrainingOverload);
            Assert.Equal(RecoverySeverity.Urgent, forecast.Severity);
        }

        [Fact]
        public void Forecast_AcwrInWatchBand_OnlyWatch()
        {
            // acute 125, chronic 400 ⇒ weekly 100 ⇒ ACWR 1.25 (in (1.15,1.3]).
            var snapshot = EmptySnapshot() with
            {
                SessionsLast7Days = 3,
                SessionsLast28Days = 12,
                HoursSinceLastSession = 30.0,
                AcuteTrainingLoad = 125.0,
                ChronicTrainingLoad = 400.0,
                RecentComfortScores = new[] { 70.0, 70.0, 70.0 }
            };

            var forecast = Service.Forecast(snapshot);

            Assert.Equal(1.25, forecast.AcuteChronicWorkloadRatio, 6);
            Assert.False(forecast.TrainingOverload);
            Assert.Equal(RecoverySeverity.Watch, forecast.Severity);
        }

        [Fact]
        public void Forecast_NoChronicBase_NeverOverloads()
        {
            // Acute load but zero chronic history ⇒ ACWR 0 ⇒ no overload flag.
            var snapshot = EmptySnapshot() with
            {
                SessionsLast7Days = 3,
                HoursSinceLastSession = 30.0,
                AcuteTrainingLoad = 500.0,
                ChronicTrainingLoad = 0.0,
                RecentComfortScores = new[] { 70.0, 70.0, 70.0 }
            };

            var forecast = Service.Forecast(snapshot);

            Assert.Equal(0.0, forecast.AcuteChronicWorkloadRatio, 6);
            Assert.False(forecast.TrainingOverload);
        }

        // ──────────────────────────────────────────────────────────────────────
        // 5. Comfort decline — a falling ComfortScore trend flags ComfortDeclining
        //    even with NO breach / strain / safety event logged.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Forecast_FallingComfortTrend_FlagsComfortDecliningWithoutAnyBreach()
        {
            // Comfort 80→70→60→50: slope −10 (≤ −1.5). No strain/safety at all.
            var snapshot = EmptySnapshot() with
            {
                SessionsLast7Days = 4,
                SessionsLast28Days = 8,
                HoursSinceLastSession = 30.0,
                RecentComfortScores = new[] { 80.0, 70.0, 60.0, 50.0 }
            };

            var forecast = Service.Forecast(snapshot);

            Assert.Equal(-10.0, forecast.ComfortDeclineSlope, 6);
            Assert.True(forecast.ComfortDeclining);
            // Reactive scorer is untouched by comfort (no strain/safety) ⇒ stays high.
            Assert.Equal(RecoveryStatus.WellRecovered, forecast.Current.Status);
            Assert.True(forecast.Severity >= RecoverySeverity.Recommend);
            Assert.Contains("comfort", forecast.Recommendation);
        }

        [Fact]
        public void Forecast_FlatComfortTrend_DoesNotFlagDeclining()
        {
            var snapshot = EmptySnapshot() with
            {
                SessionsLast7Days = 3,
                SessionsLast28Days = 6,
                HoursSinceLastSession = 30.0,
                RecentComfortScores = new[] { 70.0, 70.0, 70.0 }
            };

            var forecast = Service.Forecast(snapshot);

            Assert.Equal(0.0, forecast.ComfortDeclineSlope, 6);
            Assert.False(forecast.ComfortDeclining);
        }

        [Fact]
        public void ComputeComfortSlope_TooFewPoints_ReturnsZero()
        {
            // Two points is below the minimum-of-3 trust threshold ⇒ slope 0 (no trend).
            Assert.Equal(0.0, RecoveryIntelligenceService.ComputeComfortSlope(new[] { 90.0, 10.0 }), 6);
            Assert.Equal(0.0, RecoveryIntelligenceService.ComputeComfortSlope(Array.Empty<double>()), 6);
            Assert.Equal(0.0, RecoveryIntelligenceService.ComputeComfortSlope(null), 6);
        }

        // ──────────────────────────────────────────────────────────────────────
        // 6. Recovery debt — accrues with sessions, is paid down by rest.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void ComputeRecoveryDebt_DenseNoRest_IsHigh()
        {
            // 7 sessions, 0h rest ⇒ net 7 units ⇒ 100.
            Assert.Equal(100.0, RecoveryIntelligenceService.ComputeRecoveryDebt(7, 0.0), 6);
            // 6 sessions, 0h ⇒ net 6 ⇒ 85.71 (≥60).
            Assert.True(RecoveryIntelligenceService.ComputeRecoveryDebt(6, 0.0) >= 60.0);
        }

        [Fact]
        public void ComputeRecoveryDebt_RestPaysItDown()
        {
            // 4 sessions but 96h rest ⇒ paid 4 ⇒ net 0 ⇒ debt 0.
            Assert.Equal(0.0, RecoveryIntelligenceService.ComputeRecoveryDebt(4, 96.0), 6);
            // 4 sessions, 24h rest ⇒ paid 1 ⇒ net 3 ⇒ 42.857 (< 60, not high).
            var partial = RecoveryIntelligenceService.ComputeRecoveryDebt(4, 24.0);
            Assert.True(partial < 60.0 && partial > 0.0);
        }

        [Fact]
        public void Forecast_HighDebtNoOtherSignal_FlagsRecoveryDebtHighAndRecommends()
        {
            // 6 sessions, 0h rest ⇒ debt 85.7 (≥60). Density 6 is NOT >5? 6 > 5 ⇒ also
            // overtraining; isolate debt by keeping it the dominant message but assert debt.
            var snapshot = EmptySnapshot() with
            {
                SessionsLast7Days = 6,
                SessionsLast28Days = 18,
                HoursSinceLastSession = 0.0,
                AcuteTrainingLoad = 90.0,
                ChronicTrainingLoad = 360.0, // weekly 90 ⇒ ACWR 1.0
                RecentComfortScores = new[] { 70.0, 70.0, 70.0 }
            };

            var forecast = Service.Forecast(snapshot);

            Assert.True(forecast.RecoveryDebt >= 60.0);
            Assert.True(forecast.RecoveryDebtHigh);
            Assert.True(forecast.Severity >= RecoverySeverity.Recommend);
            Assert.Contains("rest is running behind", forecast.Recommendation);
        }

        // ──────────────────────────────────────────────────────────────────────
        // 7. Reactive scorer drives severity — real logged load (safety locks) keeps the
        //    forecast Urgent regardless of predictive flags (Health/Recovery present).
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Forecast_RealSafetyLocksLogged_ReactiveStatusDrivesUrgent()
        {
            // 4 safety locks ⇒ scorer 100−72 = 28 ⇒ Strained ⇒ Urgent severity.
            var snapshot = EmptySnapshot() with
            {
                SessionsLast7Days = 3,
                SessionsLast28Days = 9,
                HoursSinceLastSession = 30.0,
                RecentSafetyLocks = 4,
                RecentComfortScores = new[] { 70.0, 70.0, 70.0 }
            };

            var forecast = Service.Forecast(snapshot);

            Assert.Equal(RecoveryStatus.Strained, forecast.Current.Status);
            Assert.Equal(RecoverySeverity.Urgent, forecast.Severity);
        }

        // ──────────────────────────────────────────────────────────────────────
        // 8. CLINICAL INVARIANT — the recommendation never pressures the user; it only
        //    advises rest / a lighter session and clears the clinical-language policy.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Forecast_Recommendation_IsCalmAndPassesClinicalLanguagePolicy()
        {
            // Drive every flag at once so the worst-case copy is exercised.
            var snapshot = EmptySnapshot() with
            {
                SessionsLast7Days = 8,
                SessionsLast28Days = 12,
                HoursSinceLastSession = 1.0,
                AcuteTrainingLoad = 300.0,
                ChronicTrainingLoad = 400.0, // weekly 100 ⇒ ACWR 3.0
                RecentComfortScores = new[] { 90.0, 75.0, 60.0, 45.0 }
            };

            var forecast = Service.Forecast(snapshot);

            Assert.Equal(RecoverySeverity.Urgent, forecast.Severity);
            Assert.False(string.IsNullOrWhiteSpace(forecast.Recommendation));

            // Must not contain any pressure / shame language.
            var violations = ClinicalLanguagePolicy.Scan(new[]
            {
                new KeyValuePair<string, string>("recovery.forecast", forecast.Recommendation)
            });
            Assert.Empty(violations);

            // And it must read as a gentle suggestion, never a hard limit.
            Assert.Contains("gentle suggestion", forecast.Recommendation);
        }

        [Theory]
        [InlineData(RecoverySeverity.None)]
        [InlineData(RecoverySeverity.Watch)]
        [InlineData(RecoverySeverity.Recommend)]
        [InlineData(RecoverySeverity.Urgent)]
        public void Forecast_EverySeverityRecommendation_PassesClinicalLanguagePolicy(RecoverySeverity target)
        {
            var forecast = target switch
            {
                RecoverySeverity.None => Service.Forecast(EmptySnapshot()),
                RecoverySeverity.Watch => Service.Forecast(EmptySnapshot() with
                {
                    RecentComfortScores = new[] { 72.0, 71.0, 70.0 } // slope −1 ⇒ watch
                }),
                RecoverySeverity.Recommend => Service.Forecast(EmptySnapshot() with
                {
                    SessionsLast7Days = 6,
                    HoursSinceLastSession = 0.0,
                    RecentComfortScores = new[] { 70.0, 70.0, 70.0 }
                }),
                _ => Service.Forecast(EmptySnapshot() with
                {
                    RecentSafetyLocks = 4,
                    RecentComfortScores = new[] { 70.0, 70.0, 70.0 }
                })
            };

            Assert.Equal(target, forecast.Severity);
            var violations = ClinicalLanguagePolicy.Scan(new[]
            {
                new KeyValuePair<string, string>("recovery.forecast", forecast.Recommendation)
            });
            Assert.Empty(violations);
        }

        // ──────────────────────────────────────────────────────────────────────
        // 9. Robustness — non-finite / negative inputs never produce NaN or throw.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Forecast_NonFiniteAndNegativeInputs_AreSanitised()
        {
            var snapshot = new RecoveryHistorySnapshot
            {
                SessionsLast7Days = -5,
                SessionsLast28Days = -3,
                HoursSinceLastSession = double.NaN,
                RecentFatigueIndicators = -2,
                PriorFatigueIndicators = -7,
                RecentStrainEpisodes = -1,
                RecentSafetyLocks = -4,
                RecentPauseRecommendations = -2,
                RecentHydrationSuggestions = -9,
                AcuteTrainingLoad = double.NegativeInfinity,
                ChronicTrainingLoad = double.NaN,
                RecentComfortScores = new[] { double.NaN, double.PositiveInfinity, 70.0 }
            };

            var forecast = Service.Forecast(snapshot);

            Assert.InRange(forecast.Current.Score, 0.0, 100.0);
            Assert.False(double.IsNaN(forecast.AcuteChronicWorkloadRatio));
            Assert.False(double.IsNaN(forecast.RecoveryDebt));
            Assert.False(double.IsNaN(forecast.ComfortDeclineSlope));
            Assert.Equal(0.0, forecast.AcuteChronicWorkloadRatio, 6);
            Assert.Equal(0.0, forecast.RecoveryDebt, 6);
            Assert.Equal(RecoverySeverity.None, forecast.Severity);
        }

        // ──────────────────────────────────────────────────────────────────────
        // 10. TrainingLoad helper — intensity × duration, neutral 0.5 when unscored.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void TrainingLoadOf_UsesIntensityTimesDuration()
        {
            var start = Now;
            // 20-minute session, composite 80 ⇒ intensity 0.8 ⇒ load 16.
            var scored = new VoiceIntelligenceTrendPoint
            {
                StartedAt = start,
                EndedAt = start.AddMinutes(20),
                CompositeVoiceScore = 80.0
            };
            Assert.Equal(16.0, RecoveryIntelligenceService.TrainingLoadOf(scored), 6);

            // Same duration, unscored ⇒ neutral 0.5 ⇒ load 10.
            var unscored = scored with { CompositeVoiceScore = 0.0 };
            Assert.Equal(10.0, RecoveryIntelligenceService.TrainingLoadOf(unscored), 6);

            // No valid duration ⇒ 0 load.
            var noDuration = scored with { EndedAt = null };
            Assert.Equal(0.0, RecoveryIntelligenceService.TrainingLoadOf(noDuration), 6);
        }

        // ──────────────────────────────────────────────────────────────────────
        // 11. STORE PATH — full integration over the real in-memory store: persist a
        //    dense, unrested week with falling comfort and assert the forecast predicts
        //    overload BEFORE any strain/safety event is recorded.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task ForecastFromHistory_DenseUnrestedFallingComfort_PredictsBeforeAnyStrain()
        {
            var store = NewStore();

            // Seven sessions over the last ~6 days, each 20 min, comfort falling 85→55,
            // composite high (heavy load). The MOST RECENT session is only ~4h old so the
            // density-without-rest (overtraining) condition is met. NO health events.
            var comforts = new[] { 85.0, 80.0, 75.0, 70.0, 65.0, 60.0, 55.0 };
            // Hours-before-Now for each session, oldest → newest. Newest is 4h ago (<12).
            var hoursAgo = new[] { 150.0, 126.0, 100.0, 76.0, 52.0, 28.0, 4.0 };
            for (var i = 0; i < comforts.Length; i++)
            {
                var startedAt = Now.AddHours(-hoursAgo[i]);
                await store.RecordSessionCompletedAsync(new SessionAnalyticsRecord
                {
                    SessionId = 100 + i,
                    StartedAt = startedAt,
                    EndedAt = startedAt.AddMinutes(20),
                    ComfortScore100 = comforts[i],
                    CompositeVoiceScore = 80.0,
                    ExerciseCount = 1
                });
            }

            var forecast = await Service.ForecastFromHistoryAsync(store, Now);

            // Dense (7 in 7d) + a recent session (4h) ⇒ overtraining predicted; comfort
            // clearly falling ⇒ comfort declining. All BEFORE a single strain/safety event.
            Assert.True(forecast.OvertrainingPredicted);
            Assert.True(forecast.ComfortDeclining);
            Assert.True(forecast.Severity >= RecoverySeverity.Recommend);
        }

        [Fact]
        public async Task ForecastFromHistory_QuietHistory_NoFlags()
        {
            var store = NewStore();

            // One short, well-rested session a week ago, flat-ish comfort.
            var startedAt = Now.AddDays(-7).AddHours(1);
            await store.RecordSessionCompletedAsync(new SessionAnalyticsRecord
            {
                SessionId = 1,
                StartedAt = startedAt,
                EndedAt = startedAt.AddMinutes(10),
                ComfortScore100 = 72.0,
                CompositeVoiceScore = 70.0,
                ExerciseCount = 1
            });

            var forecast = await Service.ForecastFromHistoryAsync(store, Now);

            Assert.False(forecast.OvertrainingPredicted);
            Assert.False(forecast.TrainingOverload);
            Assert.False(forecast.RecoveryDebtHigh);
            Assert.Equal(RecoverySeverity.None, forecast.Severity);
        }

        [Fact]
        public async Task BuildSnapshot_SeparatesAcutePriorAndChronicWindows()
        {
            var store = NewStore();

            // Acute window (last 7d): 2 sessions, fatigue 3 total.
            for (var i = 0; i < 2; i++)
            {
                var startedAt = Now.AddDays(-(i + 1)).AddHours(2);
                await store.RecordSessionCompletedAsync(new SessionAnalyticsRecord
                {
                    SessionId = 10 + i,
                    StartedAt = startedAt,
                    EndedAt = startedAt.AddMinutes(15),
                    ComfortScore100 = 70.0,
                    CompositeVoiceScore = 60.0
                });
                await store.RecordExercisePerformanceAsync(new ExercisePerformanceSummary
                {
                    SessionId = 10 + i,
                    ExerciseId = 1,
                    StartedAt = startedAt,
                    EndedAt = startedAt.AddMinutes(15),
                    FatigueIndicators = i == 0 ? 2 : 1
                });
            }

            // Prior week (days 7–14 ago): 1 session, fatigue 5 ⇒ feeds PriorFatigueIndicators.
            var priorStart = Now.AddDays(-10).AddHours(2);
            await store.RecordSessionCompletedAsync(new SessionAnalyticsRecord
            {
                SessionId = 20,
                StartedAt = priorStart,
                EndedAt = priorStart.AddMinutes(15),
                ComfortScore100 = 70.0,
                CompositeVoiceScore = 60.0
            });
            await store.RecordExercisePerformanceAsync(new ExercisePerformanceSummary
            {
                SessionId = 20,
                ExerciseId = 1,
                StartedAt = priorStart,
                EndedAt = priorStart.AddMinutes(15),
                FatigueIndicators = 5
            });

            var snapshot = await Service.BuildSnapshotAsync(store, Now);

            Assert.Equal(2, snapshot.SessionsLast7Days);
            Assert.Equal(3, snapshot.SessionsLast28Days);
            Assert.Equal(3, snapshot.RecentFatigueIndicators);  // 2 + 1
            Assert.Equal(5, snapshot.PriorFatigueIndicators);   // the day-10 session only
            Assert.Equal(2, snapshot.RecentComfortScores.Count);
            // Most recent session started Now − 1d + 2h ⇒ 22h ago.
            Assert.Equal(22.0, snapshot.HoursSinceLastSession, 3);
        }

        [Fact]
        public async Task BuildSnapshot_CountsHealthEventsInAcuteWindowOnly()
        {
            var store = NewStore();

            // Two strain periods + one safety freeze + one hydration nudge in the acute window.
            await store.RecordHealthEventAsync(StrainEvent(1, Now.AddDays(-1)));
            await store.RecordHealthEventAsync(StrainEvent(1, Now.AddDays(-2)));
            await store.RecordHealthEventAsync(SafetyEvent(1, Now.AddDays(-3)));
            await store.RecordHealthEventAsync(HydrationEvent(1, Now.AddHours(-5)));
            // One strain OUTSIDE the acute window (10 days ago) must NOT be counted.
            await store.RecordHealthEventAsync(StrainEvent(1, Now.AddDays(-10)));

            var snapshot = await Service.BuildSnapshotAsync(store, Now);

            Assert.Equal(2, snapshot.RecentStrainEpisodes);
            Assert.Equal(1, snapshot.RecentSafetyLocks);
            Assert.Equal(1, snapshot.RecentHydrationSuggestions);
            Assert.Equal(0, snapshot.RecentPauseRecommendations);
        }

        private static HealthAnalyticsEvent StrainEvent(int sessionId, DateTime at) => new()
        {
            SessionId = sessionId,
            EventType = HealthAnalyticsEventType.StrainPeriod,
            OccurredAt = at,
            Severity = 1,
            ReasonCode = "STRAIN_PERIOD"
        };

        private static HealthAnalyticsEvent SafetyEvent(int sessionId, DateTime at) => new()
        {
            SessionId = sessionId,
            EventType = HealthAnalyticsEventType.SafetyFreeze,
            OccurredAt = at,
            Severity = 1,
            ReasonCode = "SAFETY_FREEZE"
        };

        private static HealthAnalyticsEvent HydrationEvent(int sessionId, DateTime at) => new()
        {
            SessionId = sessionId,
            EventType = HealthAnalyticsEventType.HydrationSuggested,
            OccurredAt = at,
            Severity = 0.5,
            ReasonCode = "HYDRATION"
        };
    }
}
