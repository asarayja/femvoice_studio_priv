using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Tests for <see cref="ExerciseEffectivenessEngine"/> — the per-exercise effectiveness
    /// intelligence (Sprint C.2, Agent EFF). No mocking: the numeric core is the pure
    /// <see cref="ExerciseEffectivenessEngine.Compute"/> over hand-built series, and the
    /// store path uses the real <see cref="InMemorySessionAnalyticsRepository"/> +
    /// <see cref="SessionAnalyticsStore"/> (the in-repo fake), mirroring
    /// MasteryEvaluatorTests / RecoveryIntelligenceServiceTests.
    ///
    /// HONEST PROVENANCE: every "Gain" is an OLS trend SLOPE (metric-points per session),
    /// not a real before/after. For a perfectly linear series y = a + b·x (x = 0,1,…,n−1)
    /// the slope is exactly b, which is what the expectations below are computed against.
    ///
    /// Thresholds under test (mirror the production XML docs):
    ///   • ResonanceGain/ConsistencyGain = OLS slope of (metric × 100).
    ///   • ComfortGain = OLS slope of ComfortScore100 joined per SessionId.
    ///   • RecoveryCost = clamp(18·avgFatigue + 35·avgSafety + 14·avgStrainPause, 0, 100).
    ///   • UserSuccessRate = share of sessions with hold ≥ 0.70 AND resonance ≥ 0.50.
    ///   • Flags: cost ≥ 60, avgFatigue ≥ 2, or comfort slope ≤ −1.5.
    /// </summary>
    public class ExerciseEffectivenessEngineTests
    {
        private static readonly DateTime Now = new(2026, 6, 7, 12, 0, 0, DateTimeKind.Utc);
        private const int ExerciseId = 3;

        private static ExerciseEffectivenessEngine NewEngine()
            => new(new SessionAnalyticsStore(new InMemorySessionAnalyticsRepository()));

        // Builds an exercise summary series from per-session resonance/stability/etc values.
        // SessionId i+1, one day apart, oldest first.
        private static List<ExercisePerformanceSummary> Series(
            int exerciseId,
            double[] resonance,
            double[]? stability = null,
            double[]? hold = null,
            int[]? fatigue = null,
            int[]? safety = null,
            DateTime? start = null)
        {
            var n = resonance.Length;
            var origin = (start ?? Now.AddDays(-30));
            var list = new List<ExercisePerformanceSummary>(n);
            for (var i = 0; i < n; i++)
            {
                list.Add(new ExercisePerformanceSummary
                {
                    SessionId = i + 1,
                    ExerciseId = exerciseId,
                    StartedAt = origin.AddDays(i),
                    EndedAt = origin.AddDays(i).AddMinutes(5),
                    ResonanceQualityIndex = resonance[i],
                    StabilityConsistency = stability?[i] ?? 0.6,
                    HoldCompletionRate = hold?[i] ?? 0.8,
                    FatigueIndicators = fatigue?[i] ?? 0,
                    SafetyEventsCount = safety?[i] ?? 0
                });
            }
            return list;
        }

        // ── 1. Rising resonance ⇒ positive ResonanceGain (slope == +10/session) ──────
        [Fact]
        public void Compute_RisingResonance_PositiveResonanceGain()
        {
            var engine = NewEngine();
            // 0.40,0.50,0.60,0.70 ×100 = 40,50,60,70 ⇒ slope +10.
            var trend = Series(ExerciseId, new[] { 0.40, 0.50, 0.60, 0.70 });

            var p = engine.Compute(ExerciseId, trend);

            Assert.Equal(10.0, p.ResonanceGain, 3);
            Assert.True(p.ResonanceGain > 0);
            Assert.True(p.HasEnoughData);
            Assert.Equal(4, p.SessionCount);
        }

        // ── 2. Flat resonance ⇒ ResonanceGain ≈ 0 ────────────────────────────────────
        [Fact]
        public void Compute_FlatResonance_ZeroResonanceGain()
        {
            var engine = NewEngine();
            var trend = Series(ExerciseId, new[] { 0.55, 0.55, 0.55, 0.55, 0.55 });

            var p = engine.Compute(ExerciseId, trend);

            Assert.Equal(0.0, p.ResonanceGain, 6);
        }

        // ── 3. Declining resonance ⇒ negative gain ───────────────────────────────────
        [Fact]
        public void Compute_DecliningResonance_NegativeGain()
        {
            var engine = NewEngine();
            // 0.70,0.60,0.50,0.40 ×100 ⇒ slope −10.
            var trend = Series(ExerciseId, new[] { 0.70, 0.60, 0.50, 0.40 });

            var p = engine.Compute(ExerciseId, trend);

            Assert.Equal(-10.0, p.ResonanceGain, 3);
        }

        // ── 4. Rising stability ⇒ positive ConsistencyGain ───────────────────────────
        [Fact]
        public void Compute_RisingStability_PositiveConsistencyGain()
        {
            var engine = NewEngine();
            // stability 0.30,0.40,0.50,0.60 ×100 ⇒ slope +10.
            var trend = Series(ExerciseId,
                resonance: new[] { 0.6, 0.6, 0.6, 0.6 },
                stability: new[] { 0.30, 0.40, 0.50, 0.60 });

            var p = engine.Compute(ExerciseId, trend);

            Assert.Equal(10.0, p.ConsistencyGain, 3);
        }

        // ── 5. <N sessions ⇒ HasEnoughData == false, composite stays neutral ─────────
        [Fact]
        public void Compute_BelowMinSessions_HasEnoughDataFalse()
        {
            var engine = NewEngine();
            // 3 sessions, below MinSessionsForTrust (4) — even though resonance rises.
            var trend = Series(ExerciseId, new[] { 0.40, 0.55, 0.70 });

            var p = engine.Compute(ExerciseId, trend);

            Assert.False(p.HasEnoughData);
            Assert.Equal(3, p.SessionCount);
            Assert.Equal(ExerciseEffectivenessEngine.NeutralMidpoint, p.CompositeEffectiveness, 6);
            Assert.Contains("insufficient evidence", p.Explanation);
        }

        // ── 6. Zero sessions ⇒ empty profile, neutral composite, no comfort data ─────
        [Fact]
        public void Compute_NoSessions_EmptyProfile()
        {
            var engine = NewEngine();

            var p = engine.Compute(ExerciseId, Array.Empty<ExercisePerformanceSummary>());

            Assert.Equal(0, p.SessionCount);
            Assert.False(p.HasEnoughData);
            Assert.False(p.HasComfortData);
            Assert.Equal(0.0, p.ResonanceGain, 6);
            Assert.Equal(ExerciseEffectivenessEngine.NeutralMidpoint, p.CompositeEffectiveness, 6);
        }

        // ── 7. High fatigue + safety events ⇒ high RecoveryCost ──────────────────────
        [Fact]
        public void Compute_HighFatigueAndSafety_HighRecoveryCost()
        {
            var engine = NewEngine();
            // avgFatigue = 3, avgSafety = 1 ⇒ 18·3 + 35·1 = 54 + 35 = 89.
            var trend = Series(ExerciseId,
                resonance: new[] { 0.6, 0.6, 0.6, 0.6 },
                fatigue: new[] { 3, 3, 3, 3 },
                safety: new[] { 1, 1, 1, 1 });

            var p = engine.Compute(ExerciseId, trend);

            Assert.Equal(89.0, p.RecoveryCost, 3);
        }

        // ── 8. Strain/pause health events add to RecoveryCost (joined per SessionId) ─
        [Fact]
        public void Compute_StrainPauseEvents_AddToRecoveryCost()
        {
            var engine = NewEngine();
            var trend = Series(ExerciseId, new[] { 0.6, 0.6, 0.6, 0.6 });
            // Two strain + two pause events across the 4 sessions ⇒ avgStrainPause = 4/4 = 1
            // ⇒ contributes 14·1 = 14. No fatigue/safety ⇒ cost == 14.
            var events = new List<HealthAnalyticsEvent>
            {
                Event(1, HealthAnalyticsEventType.StrainPeriod),
                Event(2, HealthAnalyticsEventType.StrainPeriod),
                Event(3, HealthAnalyticsEventType.PauseRecommended),
                Event(4, HealthAnalyticsEventType.PauseRecommended),
                // A hydration event must NOT count toward strain/pause.
                Event(1, HealthAnalyticsEventType.HydrationSuggested),
                // An event for a session that isn't this exercise must NOT count.
                Event(999, HealthAnalyticsEventType.StrainPeriod)
            };

            var p = engine.Compute(ExerciseId, trend, voiceTrend: null, healthEvents: events);

            Assert.Equal(14.0, p.RecoveryCost, 3);
        }

        // ── 9. Comfort joined per SessionId ⇒ ComfortGain from the join ──────────────
        [Fact]
        public void Compute_ComfortJoinedPerSession_ProducesComfortGain()
        {
            var engine = NewEngine();
            var trend = Series(ExerciseId, new[] { 0.6, 0.6, 0.6, 0.6 });
            // Comfort 40,50,60,70 over sessions 1..4 ⇒ slope +10.
            var voice = new[]
            {
                VoicePoint(1, comfort: 40),
                VoicePoint(2, comfort: 50),
                VoicePoint(3, comfort: 60),
                VoicePoint(4, comfort: 70),
                // A session not in this exercise must be ignored by the join.
                VoicePoint(999, comfort: 0)
            };

            var p = engine.Compute(ExerciseId, trend, voice);

            Assert.True(p.HasComfortData);
            Assert.Equal(10.0, p.ComfortGain, 3);
        }

        // ── 10. No joinable comfort ⇒ ComfortGain 0 + flagged unavailable ────────────
        [Fact]
        public void Compute_NoComfortJoin_MarksComfortUnavailable()
        {
            var engine = NewEngine();
            var trend = Series(ExerciseId, new[] { 0.6, 0.6, 0.6, 0.6 });
            // Voice trend only has sessions that aren't this exercise's.
            var voice = new[] { VoicePoint(900, 80), VoicePoint(901, 80) };

            var p = engine.Compute(ExerciseId, trend, voice);

            Assert.False(p.HasComfortData);
            Assert.Equal(0.0, p.ComfortGain, 6);
            Assert.Contains("comfort data unavailable", p.Explanation);
        }

        // ── 11. UserSuccessRate from the hold+resonance threshold gate ───────────────
        [Fact]
        public void Compute_SuccessRate_FromThresholdGate()
        {
            var engine = NewEngine();
            // hold ≥ 0.70 AND resonance ≥ 0.50 ⇒ success.
            //  s1 hold .80 res .60 ⇒ pass
            //  s2 hold .65 res .60 ⇒ fail (hold)
            //  s3 hold .90 res .40 ⇒ fail (resonance)
            //  s4 hold .75 res .55 ⇒ pass
            // 2/4 = 50%.
            var trend = Series(ExerciseId,
                resonance: new[] { 0.60, 0.60, 0.40, 0.55 },
                hold: new[] { 0.80, 0.65, 0.90, 0.75 });

            var p = engine.Compute(ExerciseId, trend);

            Assert.Equal(50.0, p.UserSuccessRate, 3);
        }

        // ── 12. RANKING: most/least effective sort correctly ─────────────────────────
        [Fact]
        public void Ranking_MostAndLeastEffective_SortByComposite()
        {
            var engine = NewEngine();

            // Strong improver (high resonance/comfort/consistency gain, low cost).
            var strong = engine.Compute(1,
                Series(1, new[] { 0.30, 0.45, 0.60, 0.75 },
                    stability: new[] { 0.30, 0.45, 0.60, 0.75 }),
                new[] { VoicePoint(1, 40), VoicePoint(2, 55), VoicePoint(3, 70), VoicePoint(4, 85) });

            // Flat/declining, high cost.
            var weak = engine.Compute(2,
                Series(2, new[] { 0.70, 0.60, 0.50, 0.40 },
                    stability: new[] { 0.70, 0.60, 0.50, 0.40 },
                    fatigue: new[] { 3, 3, 3, 3 }, safety: new[] { 1, 1, 1, 1 }));

            var profiles = new[] { weak, strong };

            var most = engine.RankMostEffective(profiles);
            var least = engine.RankLeastEffective(profiles);

            Assert.Equal(1, most[0].ExerciseId);   // strong first
            Assert.Equal(2, most[1].ExerciseId);
            Assert.Equal(2, least[0].ExerciseId);   // weak first
            Assert.Equal(1, least[1].ExerciseId);
            Assert.True(strong.CompositeEffectiveness > weak.CompositeEffectiveness);
        }

        // ── 13. RANKING: by recovery cost and by resonance gain ──────────────────────
        [Fact]
        public void Ranking_ByRecoveryCostAndResonanceGain_SortCorrectly()
        {
            var engine = NewEngine();

            var costly = engine.Compute(5,
                Series(5, new[] { 0.6, 0.6, 0.6, 0.6 },
                    fatigue: new[] { 3, 3, 3, 3 }, safety: new[] { 1, 1, 1, 1 }));     // cost 89
            var gentle = engine.Compute(6,
                Series(6, new[] { 0.40, 0.50, 0.60, 0.70 }));                          // cost 0, res gain +10

            var profiles = new[] { gentle, costly };

            var byCost = engine.RankByRecoveryCost(profiles);
            var byRes = engine.RankByResonanceGain(profiles);

            Assert.Equal(5, byCost[0].ExerciseId);  // costly first
            Assert.Equal(6, byRes[0].ExerciseId);   // best resonance gain first
        }

        // ── 14. SAFETY FLAG: high recovery cost + high fatigue raises flags ──────────
        [Fact]
        public void FlagConcerns_HighCostAndFatigue_RaisesFlags()
        {
            var engine = NewEngine();
            // avgFatigue 3, avgSafety 1 ⇒ cost 89 (≥60), avgFatigue 3 (≥2).
            var taxing = engine.Compute(7,
                Series(7, new[] { 0.6, 0.6, 0.6, 0.6 },
                    fatigue: new[] { 3, 3, 3, 3 }, safety: new[] { 1, 1, 1, 1 }));
            // Gentle exercise — no flags.
            var gentle = engine.Compute(8, Series(8, new[] { 0.6, 0.6, 0.6, 0.6 }));

            var flags = engine.FlagConcerns(new[] { taxing, gentle });

            Assert.Contains(flags, f => f.ExerciseId == 7 && f.ReasonCode == "HIGH_RECOVERY_COST");
            Assert.Contains(flags, f => f.ExerciseId == 7 && f.ReasonCode == "HIGH_FATIGUE");
            Assert.DoesNotContain(flags, f => f.ExerciseId == 8);
        }

        // ── 15. SAFETY FLAG: comfort decline raises COMFORT_DECLINE ──────────────────
        [Fact]
        public void FlagConcerns_ComfortDecline_RaisesComfortFlag()
        {
            var engine = NewEngine();
            var trend = Series(9, new[] { 0.6, 0.6, 0.6, 0.6 });
            // Comfort 80,70,60,50 over sessions 1..4 ⇒ slope −10 (≤ −1.5).
            var voice = new[]
            {
                VoicePoint(1, 80), VoicePoint(2, 70), VoicePoint(3, 60), VoicePoint(4, 50)
            };
            var p = engine.Compute(9, trend, voice);

            var flags = engine.FlagConcerns(new[] { p });

            Assert.Contains(flags, f => f.ExerciseId == 9 && f.ReasonCode == "COMFORT_DECLINE");
        }

        // ── 16. SAFETY FLAG: a no-data exercise is never flagged (no false alarms) ───
        [Fact]
        public void FlagConcerns_NoDataExercise_NotFlagged()
        {
            var engine = NewEngine();
            var empty = engine.Compute(10, Array.Empty<ExercisePerformanceSummary>());

            var flags = engine.FlagConcerns(new[] { empty });

            Assert.Empty(flags);
        }

        // ── 17. Ranking excludes low-data profiles by default ────────────────────────
        [Fact]
        public void Ranking_ExcludesLowDataByDefault_ButCanInclude()
        {
            var engine = NewEngine();
            var good = engine.Compute(1, Series(1, new[] { 0.40, 0.50, 0.60, 0.70 }));
            var thin = engine.Compute(2, Series(2, new[] { 0.40, 0.70 })); // 2 sessions < 4

            var profiles = new[] { good, thin };

            var defaultRank = engine.RankMostEffective(profiles);
            var withLowData = engine.RankMostEffective(profiles, includeLowData: true);

            Assert.Single(defaultRank);
            Assert.Equal(1, defaultRank[0].ExerciseId);
            Assert.Equal(2, withLowData.Count);
        }

        // ── 18. Store path: EvaluateAsync reads history and computes the same numbers ─
        [Fact]
        public async Task EvaluateAsync_FromStore_MatchesPureCompute()
        {
            var repo = new InMemorySessionAnalyticsRepository();
            var store = new SessionAnalyticsStore(repo);
            var engine = new ExerciseEffectivenessEngine(store);

            // Seed 4 rising-resonance sessions for the exercise within the look-back window.
            // All four clear the success gate (res ≥ .50, hold .80 ≥ .70) ⇒ UserSuccessRate 100;
            // the even +0.10/session rise keeps ResonanceGain at 10.0 (OLS slope ×100).
            var resonance = new[] { 0.50, 0.60, 0.70, 0.80 };
            for (var i = 0; i < resonance.Length; i++)
            {
                await store.RecordExercisePerformanceAsync(new ExercisePerformanceSummary
                {
                    SessionId = i + 1,
                    ExerciseId = ExerciseId,
                    StartedAt = Now.AddDays(-10 + i),
                    EndedAt = Now.AddDays(-10 + i).AddMinutes(5),
                    ResonanceQualityIndex = resonance[i],
                    StabilityConsistency = 0.6,
                    HoldCompletionRate = 0.8
                });
            }

            var p = await engine.EvaluateAsync(ExerciseId, Now);

            Assert.Equal(ExerciseId, p.ExerciseId);
            Assert.Equal(4, p.SessionCount);
            Assert.Equal(10.0, p.ResonanceGain, 3);
            Assert.True(p.HasEnoughData);
            Assert.Equal(100.0, p.UserSuccessRate, 3); // all 4 clear hold .80 / res ≥ .50
        }

        // ── 19. EvaluateAllAsync covers the REAL catalog 1–15 only (no phantom 16+) ──
        [Fact]
        public async Task EvaluateAllAsync_CoversCatalogOneToFifteenOnly()
        {
            var repo = new InMemorySessionAnalyticsRepository();
            var store = new SessionAnalyticsStore(repo);
            var engine = new ExerciseEffectivenessEngine(store);

            // Seed one real catalog exercise (id 3) and one PHANTOM id (16).
            foreach (var id in new[] { 3, 16 })
            {
                for (var i = 0; i < 4; i++)
                {
                    await store.RecordExercisePerformanceAsync(new ExercisePerformanceSummary
                    {
                        SessionId = id * 100 + i,
                        ExerciseId = id,
                        StartedAt = Now.AddDays(-5 + i),
                        EndedAt = Now.AddDays(-5 + i).AddMinutes(5),
                        ResonanceQualityIndex = 0.6,
                        StabilityConsistency = 0.6,
                        HoldCompletionRate = 0.8
                    });
                }
            }

            var all = await engine.EvaluateAllAsync(Now);

            Assert.Equal(15, all.Count);
            Assert.Equal(
                Enumerable.Range(1, 15),
                all.Select(p => p.ExerciseId).OrderBy(x => x));
            // The phantom id 16 must never appear.
            Assert.DoesNotContain(all, p => p.ExerciseId == 16);
            // The real exercise 3 has its 4 sessions; the rest are empty (no rows).
            Assert.Equal(4, all.Single(p => p.ExerciseId == 3).SessionCount);
            Assert.Equal(0, all.Single(p => p.ExerciseId == 1).SessionCount);
        }

        // ── 20. Composite penalises recovery cost (more taxing ⇒ lower composite) ────
        [Fact]
        public void Compute_HigherRecoveryCost_LowersComposite()
        {
            var engine = NewEngine();
            var clean = engine.Compute(1, Series(1, new[] { 0.6, 0.6, 0.6, 0.6 }));
            var taxing = engine.Compute(2,
                Series(2, new[] { 0.6, 0.6, 0.6, 0.6 },
                    fatigue: new[] { 3, 3, 3, 3 }, safety: new[] { 1, 1, 1, 1 }));

            Assert.True(taxing.RecoveryCost > clean.RecoveryCost);
            Assert.True(taxing.CompositeEffectiveness < clean.CompositeEffectiveness);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────

        private static HealthAnalyticsEvent Event(int sessionId, HealthAnalyticsEventType type)
            => new()
            {
                SessionId = sessionId,
                EventType = type,
                OccurredAt = Now.AddDays(-1),
                Severity = 1,
                ReasonCode = type.ToString()
            };

        private static VoiceIntelligenceTrendPoint VoicePoint(int sessionId, double comfort)
            => new()
            {
                SessionId = sessionId,
                StartedAt = Now.AddDays(-30 + sessionId),
                EndedAt = Now.AddDays(-30 + sessionId).AddMinutes(5),
                ComfortScore100 = comfort
            };
    }
}
