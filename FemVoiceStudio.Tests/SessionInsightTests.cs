using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Unit tests for <see cref="SessionInsightBuilder"/> and the
    /// <see cref="SessionInsight"/> aggregate. No mocking: the builder is a pure function
    /// over real, hand-built records, so every test exercises the real types with
    /// hand-computed expected values.
    ///
    /// Coverage:
    ///   • improvements captured when a dimension rose vs the previous session,
    ///   • sub-threshold/regressing dimensions excluded,
    ///   • improvements ordered strongest-delta-first with hierarchy tie-break,
    ///   • risks capture strain / comfort breach / safety-lock from the outcome,
    ///   • recovery needs reflected (including NeedsAttention),
    ///   • suggested focus = weakest dimension with hierarchy tie-break,
    ///   • empty history (first session) ⇒ no improvements but a valid insight,
    ///   • the summary is encouraging and non-shaming (ClinicalLanguagePolicy-clean).
    /// </summary>
    public class SessionInsightTests
    {
        private static readonly SessionInsightBuilder Builder = new();

        // ── Builders for the real records under test ──────────────────────────────

        private static DimensionScore Dim(double score) => new(score, $"score {score:0.0}");

        /// <summary>Builds a full VoiceIntelligenceScores with each dimension's 0–100 value.</summary>
        private static VoiceIntelligenceScores Scores(
            double resonance = 50,
            double comfort = 50,
            double consistency = 50,
            double intonation = 50,
            double vocalWeight = 50,
            double recovery = 50,
            double pitch = 50,
            double composite = 50) => new()
            {
                Resonance = Dim(resonance),
                Comfort = Dim(comfort),
                Consistency = Dim(consistency),
                Intonation = Dim(intonation),
                VocalWeight = Dim(vocalWeight),
                Recovery = Dim(recovery),
                Pitch = Dim(pitch),
                CompositeVoiceScore = composite,
            };

        private static VoiceIntelligenceTrendPoint TrendPoint(
            DateTime startedAt,
            double resonance = 50,
            double comfort = 50,
            double consistency = 50,
            double intonation = 50,
            double vocalWeight = 50,
            double recovery = 50,
            double pitch = 50,
            int sessionId = 1) => new()
            {
                SessionId = sessionId,
                StartedAt = startedAt,
                EndedAt = startedAt.AddMinutes(10),
                ResonanceScore100 = resonance,
                ComfortScore100 = comfort,
                ConsistencyScore100 = consistency,
                IntonationScore100 = intonation,
                VocalWeightScore100 = vocalWeight,
                RecoveryScore100 = recovery,
                PitchScore100 = pitch,
                CompositeVoiceScore = 50,
            };

        private static ExerciseSessionOutcome Outcome(
            int safetyLockEpisodes = 0,
            int strainDetections = 0,
            int comfortBreachEpisodes = 0,
            int fatigueIndicators = 0) => new()
            {
                SafetyLockEpisodes = safetyLockEpisodes,
                StrainDetections = strainDetections,
                ComfortBreachEpisodes = comfortBreachEpisodes,
                FatigueIndicators = fatigueIndicators,
            };

        private static RecoveryResult Recovery(
            double score = 100,
            RecoveryStatus status = RecoveryStatus.WellRecovered,
            string explanation = "well rested") => new()
            {
                Score = score,
                Status = status,
                Explanation = explanation,
            };

        // ──────────────────────────────────────────────────────────────────────
        // 1. Improvement captured when a dimension rose vs the previous session.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Build_ResonanceRose_CapturesImprovement()
        {
            // Resonance 60 now vs 54 last session ⇒ +6 improvement. Everything else flat.
            var current = Scores(resonance: 60);
            var prior = new[] { TrendPoint(DateTime.Now.AddDays(-1), resonance: 54) };

            var insight = Builder.Build(current, prior, Outcome(), Recovery());

            var resonance = insight.Improvements.SingleOrDefault(i => i.Dimension == VoiceDimension.Resonance);
            Assert.NotNull(resonance);
            Assert.Equal(6.0, resonance!.Delta, 6);
            Assert.Equal(60.0, resonance.CurrentScore, 6);
            Assert.Equal(54.0, resonance.PreviousScore, 6);
            Assert.Contains("Reson", resonance.Explanation, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("+6", resonance.Explanation, StringComparison.Ordinal);
            Assert.False(insight.IsFirstSession);
        }

        [Fact]
        public void Build_NorwegianSessionReflection_UsesLocalizedCopyAndRecoveryDrivers()
        {
            LocalizationService.Instance.SetLanguage("nb");
            try
            {
                var builder = new SessionInsightBuilder(LocalizationService.Instance);
                var scorer = new RecoveryScorer(LocalizationService.Instance);
                var current = Scores(intonation: 90, consistency: 54, pitch: 53, recovery: 0);
                var prior = new[] { TrendPoint(DateTime.Now.AddDays(-1), intonation: 50, consistency: 50, pitch: 50, recovery: 50) };
                var recovery = scorer.Score(new RecoveryScoreInput
                {
                    RecentSafetyLocks = 5,
                    RecentStrainEpisodes = 1,
                    RecentPauseRecommendations = 2,
                    RecentFatigueIndicators = 3,
                    PriorFatigueIndicators = 1,
                    SessionsLast7Days = 8,
                    HoursSinceLastSession = 1,
                    HydrationSuggestionsRecent = 2
                });

                var insight = builder.Build(current, prior, Outcome(), recovery);
                var reflectionText = string.Join("\n",
                    new[] { LocalizationService.Instance["SessionInsight_Title"], insight.Summary }
                        .Concat(insight.Improvements.Select(i => i.Explanation))
                        .Append(insight.RecoveryNeeds.Explanation));

                Assert.Equal("Øktrefleksjon", LocalizationService.Instance["SessionInsight_Title"]);
                Assert.Contains("Stemmen din kan ha godt av litt hvile", insight.Summary);
                Assert.Contains("Fint jobbet", insight.Summary);
                Assert.Contains("intonasjon økte med 40", insight.Summary);
                Assert.Contains("Neste gang kan du forsiktig utforske restitusjon", insight.Summary);
                Assert.Contains("Intonasjon +40 siden forrige økt", reflectionText);
                Assert.Contains("Konsistens +4 siden forrige økt", reflectionText);
                Assert.Contains("Pitch +3 siden forrige økt", reflectionText);
                Assert.Contains("Restitusjon 0 (overtrent).", reflectionText);
                Assert.Contains("Senket av:", reflectionText);
                Assert.Contains("belastningsepisode", reflectionText);
                Assert.Contains("pauseanbefalinger", reflectionText);
                Assert.Contains("tretthetsindikatorer", reflectionText);
                Assert.Contains("stigende tretthetstrend", reflectionText);
                Assert.Contains("høy treningsmengde med lite hvile", reflectionText);
                Assert.Contains("hydreringspåminnelser", reflectionText);
                Assert.Contains("ø", reflectionText);
                Assert.Contains("å", reflectionText);
                Assert.DoesNotContain("Session reflection", reflectionText, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Your voice could use", reflectionText, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Nice work", reflectionText, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("since last session", reflectionText, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Next, you might", reflectionText, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Lowered by", reflectionText, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Ã", reflectionText, StringComparison.Ordinal);
                Assert.DoesNotContain("�", reflectionText, StringComparison.Ordinal);
            }
            finally
            {
                LocalizationService.Instance.SetLanguage("en");
            }
        }

        [Fact]
        public void Build_EnglishSessionReflection_UsesEnglishCopy()
        {
            LocalizationService.Instance.SetLanguage("en");
            var builder = new SessionInsightBuilder(LocalizationService.Instance);
            var scorer = new RecoveryScorer(LocalizationService.Instance);
            var current = Scores(intonation: 90, recovery: 0);
            var prior = new[] { TrendPoint(DateTime.Now.AddDays(-1), intonation: 50, recovery: 50) };
            var recovery = scorer.Score(new RecoveryScoreInput
            {
                RecentSafetyLocks = 5,
                RecentStrainEpisodes = 1,
                RecentPauseRecommendations = 1,
                RecentFatigueIndicators = 1,
                PriorFatigueIndicators = 0,
                SessionsLast7Days = 8,
                HoursSinceLastSession = 1,
                HydrationSuggestionsRecent = 1
            });

            var insight = builder.Build(current, prior, Outcome(), recovery);
            var reflectionText = string.Join("\n",
                new[] { LocalizationService.Instance["SessionInsight_Title"], insight.Summary }
                    .Concat(insight.Improvements.Select(i => i.Explanation))
                    .Append(insight.RecoveryNeeds.Explanation));

            Assert.Equal("Session reflection", LocalizationService.Instance["SessionInsight_Title"]);
            Assert.Contains("Your voice could use some rest", reflectionText);
            Assert.Contains("Nice work", reflectionText);
            Assert.Contains("Intonation +40 since last session", reflectionText);
            Assert.Contains("Lowered by:", reflectionText);
        }

        // ──────────────────────────────────────────────────────────────────────
        // 2. A dimension that fell is NOT reported as an improvement.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Build_DimensionFell_NotReportedAsImprovement()
        {
            // Resonance dropped 60 → 50; nothing else changed ⇒ no improvements.
            var current = Scores(resonance: 50);
            var prior = new[] { TrendPoint(DateTime.Now.AddDays(-1), resonance: 60) };

            var insight = Builder.Build(current, prior, Outcome(), Recovery());

            Assert.Empty(insight.Improvements);
        }

        // ──────────────────────────────────────────────────────────────────────
        // 3. A sub-threshold rise (< 1.0) is filtered out as noise.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Build_SubThresholdRise_Filtered()
        {
            // +0.5 is below ImprovementThreshold (1.0) ⇒ not reported.
            var current = Scores(comfort: 50.5);
            var prior = new[] { TrendPoint(DateTime.Now.AddDays(-1), comfort: 50.0) };

            var insight = Builder.Build(current, prior, Outcome(), Recovery());

            Assert.DoesNotContain(insight.Improvements, i => i.Dimension == VoiceDimension.Comfort);
        }

        // ──────────────────────────────────────────────────────────────────────
        // 4. Multiple improvements are ordered strongest-delta-first.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Build_MultipleImprovements_OrderedByDeltaDescending()
        {
            // Resonance +4, Pitch +10, Comfort +2. Expect Pitch, Resonance, Comfort.
            var current = Scores(resonance: 54, pitch: 60, comfort: 52);
            var prior = new[]
            {
                TrendPoint(DateTime.Now.AddDays(-1), resonance: 50, pitch: 50, comfort: 50)
            };

            var insight = Builder.Build(current, prior, Outcome(), Recovery());

            Assert.Equal(
                new[] { VoiceDimension.Pitch, VoiceDimension.Resonance, VoiceDimension.Comfort },
                insight.Improvements.Select(i => i.Dimension).ToArray());
        }

        // ──────────────────────────────────────────────────────────────────────
        // 5. Equal-delta improvements break ties by clinical hierarchy (Health-first).
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Build_EqualDeltaImprovements_TieBreakByHierarchy()
        {
            // Comfort (Health, enum 0) and Pitch (enum 6) both +5. Comfort must come first.
            var current = Scores(comfort: 55, pitch: 55);
            var prior = new[] { TrendPoint(DateTime.Now.AddDays(-1), comfort: 50, pitch: 50) };

            var insight = Builder.Build(current, prior, Outcome(), Recovery());

            var dims = insight.Improvements.Select(i => i.Dimension).ToArray();
            Assert.Equal(new[] { VoiceDimension.Comfort, VoiceDimension.Pitch }, dims);
        }

        // ──────────────────────────────────────────────────────────────────────
        // 6. Risks: safety lock is flagged.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Build_SafetyLockEpisodes_FlaggedAsRisk()
        {
            var insight = Builder.Build(Scores(), Array.Empty<VoiceIntelligenceTrendPoint>(),
                Outcome(safetyLockEpisodes: 2), Recovery());

            var risk = insight.Risks.SingleOrDefault(r => r.ReasonCode == "SAFETY_LOCK");
            Assert.NotNull(risk);
            Assert.Equal(2, risk!.Count);
            Assert.False(string.IsNullOrWhiteSpace(risk.Description));
        }

        // ──────────────────────────────────────────────────────────────────────
        // 7. Risks: strain is flagged.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Build_StrainDetections_FlaggedAsRisk()
        {
            var insight = Builder.Build(Scores(), null,
                Outcome(strainDetections: 4), Recovery());

            var risk = insight.Risks.SingleOrDefault(r => r.ReasonCode == "STRAIN_DETECTED");
            Assert.NotNull(risk);
            Assert.Equal(4, risk!.Count);
        }

        // ──────────────────────────────────────────────────────────────────────
        // 8. Risks: comfort breaches flag only at/above the ≥3 threshold (matches the
        //    recorder's ComfortZoneBreach journaling threshold).
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Build_ComfortBreaches_FlaggedOnlyAtThreshold()
        {
            var below = Builder.Build(Scores(), null, Outcome(comfortBreachEpisodes: 2), Recovery());
            Assert.DoesNotContain(below.Risks, r => r.ReasonCode == "COMFORT_BREACH");

            var atThreshold = Builder.Build(Scores(), null, Outcome(comfortBreachEpisodes: 3), Recovery());
            var risk = atThreshold.Risks.SingleOrDefault(r => r.ReasonCode == "COMFORT_BREACH");
            Assert.NotNull(risk);
            Assert.Equal(3, risk!.Count);
        }

        // ──────────────────────────────────────────────────────────────────────
        // 9. A clean session has no risks.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Build_CleanSession_NoRisks()
        {
            var insight = Builder.Build(Scores(), null, Outcome(), Recovery());
            Assert.Empty(insight.Risks);
        }

        // ──────────────────────────────────────────────────────────────────────
        // 10. Risk ordering: safety lock before strain before comfort breach.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Build_MultipleRisks_OrderedBySeverity()
        {
            var insight = Builder.Build(Scores(), null,
                Outcome(safetyLockEpisodes: 1, strainDetections: 2, comfortBreachEpisodes: 5),
                Recovery());

            Assert.Equal(
                new[] { "SAFETY_LOCK", "STRAIN_DETECTED", "COMFORT_BREACH" },
                insight.Risks.Select(r => r.ReasonCode).ToArray());
        }

        // ──────────────────────────────────────────────────────────────────────
        // 11. Recovery needs are reflected, including the NeedsAttention hint.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Build_StrainedRecovery_ReflectedAndNeedsAttention()
        {
            var recovery = Recovery(score: 40, status: RecoveryStatus.Strained,
                explanation: "Lowered by: 3 strain episodes.");

            var insight = Builder.Build(Scores(), null, Outcome(), recovery);

            Assert.Equal(40.0, insight.RecoveryNeeds.Score, 6);
            Assert.Equal(RecoveryStatus.Strained, insight.RecoveryNeeds.Status);
            Assert.Contains("strain", insight.RecoveryNeeds.Explanation, StringComparison.Ordinal);
            Assert.True(insight.RecoveryNeeds.NeedsAttention);
        }

        [Fact]
        public void Build_WellRecovered_DoesNotNeedAttention()
        {
            var insight = Builder.Build(Scores(), null, Outcome(),
                Recovery(score: 90, status: RecoveryStatus.WellRecovered));

            Assert.False(insight.RecoveryNeeds.NeedsAttention);
        }

        // ──────────────────────────────────────────────────────────────────────
        // 12. Suggested focus = weakest dimension.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Build_SuggestedFocus_IsWeakestDimension()
        {
            // Pitch is the clear lowest at 20 ⇒ suggested focus.
            var current = Scores(
                resonance: 70, comfort: 80, consistency: 65,
                intonation: 60, vocalWeight: 55, recovery: 75, pitch: 20);

            var insight = Builder.Build(current, null, Outcome(), Recovery());

            Assert.Equal(VoiceDimension.Pitch, insight.SuggestedFocus);
            Assert.NotEmpty(insight.SuggestedExercises);
        }

        // ──────────────────────────────────────────────────────────────────────
        // 13. Suggested focus tie-break: equal-lowest dimensions resolve Health-first.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Build_WeakestTie_ResolvesByHierarchy()
        {
            // Recovery (Health, enum 1) and Pitch (enum 6) both lowest at 30.
            // Recovery wins the tie.
            var current = Scores(
                resonance: 70, comfort: 70, consistency: 70,
                intonation: 70, vocalWeight: 70, recovery: 30, pitch: 30);

            var insight = Builder.Build(current, null, Outcome(), Recovery());

            Assert.Equal(VoiceDimension.Recovery, insight.SuggestedFocus);
        }

        // ──────────────────────────────────────────────────────────────────────
        // 14. Empty history (first session) ⇒ no improvements but a valid insight.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Build_FirstSession_NoImprovementsButValid()
        {
            var current = Scores(resonance: 80, pitch: 30, composite: 62);

            var insight = Builder.Build(current, Array.Empty<VoiceIntelligenceTrendPoint>(),
                Outcome(), Recovery());

            Assert.True(insight.IsFirstSession);
            Assert.Empty(insight.Improvements);
            Assert.Equal(62.0, insight.CompositeVoiceScore, 6);
            Assert.Equal(VoiceDimension.Pitch, insight.SuggestedFocus); // weakest still computed
            Assert.NotNull(insight.RecoveryNeeds);
            Assert.False(string.IsNullOrWhiteSpace(insight.Summary));
        }

        [Fact]
        public void Build_NullTrend_TreatedAsFirstSession()
        {
            var insight = Builder.Build(Scores(), null, Outcome(), Recovery());
            Assert.True(insight.IsFirstSession);
            Assert.Empty(insight.Improvements);
        }

        // ──────────────────────────────────────────────────────────────────────
        // 15. The most recent prior point is used as the improvement reference, even
        //     when the trend list is supplied out of chronological order.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Build_UsesMostRecentPriorPoint_EvenWhenOutOfOrder()
        {
            // Two prior points; the newer (yesterday) has comfort 50, the older (a week
            // ago) has comfort 10. Current comfort 56. Improvement must be +6 (vs the
            // newer point), not +46 (vs the older).
            var newer = TrendPoint(DateTime.Now.AddDays(-1), comfort: 50, sessionId: 2);
            var older = TrendPoint(DateTime.Now.AddDays(-7), comfort: 10, sessionId: 1);
            var priorOutOfOrder = new[] { newer, older }; // deliberately newest-first

            var insight = Builder.Build(Scores(comfort: 56), priorOutOfOrder, Outcome(), Recovery());

            var comfort = insight.Improvements.Single(i => i.Dimension == VoiceDimension.Comfort);
            Assert.Equal(6.0, comfort.Delta, 6);
        }

        // ──────────────────────────────────────────────────────────────────────
        // 16. The summary is encouraging and passes the clinical-language policy.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Build_Summary_IsEncouragingAndClinicallyClean()
        {
            // A session with an improvement and a gentle focus.
            var current = Scores(resonance: 60, pitch: 40);
            var prior = new[] { TrendPoint(DateTime.Now.AddDays(-1), resonance: 52) };

            var insight = Builder.Build(current, prior, Outcome(), Recovery());

            Assert.False(string.IsNullOrWhiteSpace(insight.Summary));
            AssertClinicallyClean(insight.Summary);
        }

        [Fact]
        public void Build_FirstSessionSummary_IsEncouragingAndClinicallyClean()
        {
            var insight = Builder.Build(Scores(), null, Outcome(), Recovery());
            Assert.Contains("first session", insight.Summary, StringComparison.OrdinalIgnoreCase);
            AssertClinicallyClean(insight.Summary);
        }

        [Fact]
        public void Build_SummaryAndRiskCopy_AllClinicallyClean()
        {
            // A worst-case session: strained recovery + every risk + a focus. All the
            // generated user-visible copy must still pass the clinical-language policy.
            var recovery = Recovery(score: 20, status: RecoveryStatus.Overtrained,
                explanation: "Lowered by: 2 safety locks (dominant).");
            var insight = Builder.Build(Scores(pitch: 15), null,
                Outcome(safetyLockEpisodes: 2, strainDetections: 3, comfortBreachEpisodes: 4),
                recovery);

            AssertClinicallyClean(insight.Summary);
            foreach (var risk in insight.Risks)
                AssertClinicallyClean(risk.Description);
        }

        // ──────────────────────────────────────────────────────────────────────
        // 17. BuildBreakdown is deterministic and lists focus / improvements / recovery.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void BuildBreakdown_ListsFocusImprovementsAndRecovery()
        {
            var current = Scores(resonance: 60, pitch: 30, composite: 55);
            var prior = new[] { TrendPoint(DateTime.Now.AddDays(-1), resonance: 50) };

            var insight = Builder.Build(current, prior, Outcome(strainDetections: 2),
                Recovery(score: 70, status: RecoveryStatus.Adequate));

            var breakdown = insight.BuildBreakdown();

            Assert.Contains("Composite:", breakdown, StringComparison.Ordinal);
            Assert.Contains("focus: Pitch", breakdown, StringComparison.Ordinal);
            Assert.Contains("Improvement:", breakdown, StringComparison.Ordinal);
            Assert.Contains("Reson", breakdown, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Risk: STRAIN_DETECTED", breakdown, StringComparison.Ordinal);
            Assert.Contains("Recovery: 70/100", breakdown, StringComparison.Ordinal);
        }

        // ── helper: assert generated copy is clinically clean ─────────────────────

        private static void AssertClinicallyClean(string text)
        {
            var violations = ClinicalLanguagePolicy.Scan(
                new[] { new KeyValuePair<string, string>("Insight.Generated", text) });
            Assert.True(violations.Count == 0,
                "Clinical-language policy violation in generated copy: " +
                string.Join("; ", violations.Select(v => v.ToString())) + $" — text was: \"{text}\"");
        }
    }
}
