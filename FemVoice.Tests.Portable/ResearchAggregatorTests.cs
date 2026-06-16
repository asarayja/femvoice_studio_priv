using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;
using Assert = Xunit.Assert;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Agent 9 (Research Analytics) — verifies the PURE group-level
    /// <see cref="ResearchAggregator"/>.
    ///
    /// Core contracts: the shape is multi-participant even at N=1; the single-participant
    /// longitudinal series is STILL emitted at N=1 while <see cref="ResearchDataset.HasSufficientCohort"/>
    /// is false; group aggregates collapse to means/shares without exposing per-session detail;
    /// no PII enters or leaves.
    ///
    /// House style: no mocking frameworks — real classes, fixed inputs, hand-computed expectations.
    /// </summary>
    public class ResearchAggregatorTests
    {
        private static readonly DateTime Day0 = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        // ── 1. N=1: HasSufficientCohort false + caveat, but series still emitted ─────
        [Fact]
        public void BuildDataset_SingleParticipant_FlagsInsufficientCohortButKeepsSeries()
        {
            var agg = new ResearchAggregator();
            var p1 = new[]
            {
                Row("tok-1", Day0,           composite: 60, recovery: 70, exerciseId: 1, eff: 50, plateau: false),
                Row("tok-1", Day0.AddDays(1), composite: 65, recovery: 72, exerciseId: 1, eff: 55, plateau: true),
            };

            var dataset = agg.BuildDataset(new[] { (IReadOnlyList<ResearchParticipantRow>)p1 }, "dataset-tok");

            // Cohort generalisation is withheld at N=1.
            Assert.Equal(1, dataset.ParticipantCount);
            Assert.False(dataset.HasSufficientCohort);
            Assert.False(string.IsNullOrWhiteSpace(dataset.VolumeCaveatNote));
            Assert.Contains("N=1", dataset.VolumeCaveatNote);

            // ...yet the valid single-participant longitudinal series is STILL present.
            Assert.Equal(2, dataset.DayBucketedRows.Count);
            Assert.All(dataset.DayBucketedRows, r => Assert.Equal("tok-1", r.ParticipantToken));

            // Aggregates are still computed at group shape.
            var ex = Assert.Single(dataset.AggregateMetrics.ExerciseEffectiveness);
            Assert.Equal(1, ex.ExerciseId);
            Assert.Equal(52.5, ex.MeanEffectiveness, 6); // (50+55)/2
            Assert.Equal(1, ex.ContributingParticipantCount);
            Assert.Equal(2, ex.SessionCount);
        }

        // ── 2. Empty cohort: empty dataset, caveat present, no throw ─────────────────
        [Fact]
        public void BuildDataset_EmptyCohort_ProducesEmptyButValidDataset()
        {
            var agg = new ResearchAggregator();
            var dataset = agg.BuildDataset(
                Array.Empty<IReadOnlyList<ResearchParticipantRow>>(), "tok");

            Assert.Equal(0, dataset.ParticipantCount);
            Assert.False(dataset.HasSufficientCohort);
            Assert.Empty(dataset.DayBucketedRows);
            Assert.Empty(dataset.AggregateMetrics.ExerciseEffectiveness);
            Assert.Equal(0, dataset.AggregateMetrics.PlateauFrequency.ParticipantCount);
            Assert.Equal(0.0, dataset.AggregateMetrics.RecoveryDistribution.Mean, 6);
        }

        // ── 3. Sufficient cohort: HasSufficientCohort true, no caveat ────────────────
        [Fact]
        public void BuildDataset_AtOrAboveThreshold_HasSufficientCohort()
        {
            var agg = new ResearchAggregator();
            // MinimumCohortSize participants, each with one row.
            var series = Enumerable.Range(0, ResearchAggregator.MinimumCohortSize)
                .Select(i => (IReadOnlyList<ResearchParticipantRow>)new[]
                {
                    Row($"tok-{i}", Day0, composite: 50 + i, recovery: 60 + i)
                })
                .ToList();

            var dataset = agg.BuildDataset(series, "tok");

            Assert.Equal(ResearchAggregator.MinimumCohortSize, dataset.ParticipantCount);
            Assert.True(dataset.HasSufficientCohort);
            Assert.Equal(string.Empty, dataset.VolumeCaveatNote);
            Assert.Equal(ResearchAggregator.MinimumCohortSize, dataset.DayBucketedRows.Count);
        }

        // ── 4. One participant below threshold (N>1) ⇒ still insufficient + caveat ──
        [Fact]
        public void BuildDataset_BelowThresholdButMultiple_StillInsufficient()
        {
            var agg = new ResearchAggregator();
            var series = new List<IReadOnlyList<ResearchParticipantRow>>
            {
                new[] { Row("a", Day0, recovery: 40) },
                new[] { Row("b", Day0, recovery: 80) },
            };

            var dataset = agg.BuildDataset(series, "tok");
            Assert.Equal(2, dataset.ParticipantCount);
            Assert.False(dataset.HasSufficientCohort);
            Assert.Contains("N=2", dataset.VolumeCaveatNote);
            Assert.Contains("minimum", dataset.VolumeCaveatNote);
        }

        // ── 5. GroupExerciseEffectiveness: ranked, multi-participant means ───────────
        [Fact]
        public void GroupExerciseEffectiveness_AggregatesAndRanksAcrossParticipants()
        {
            var agg = new ResearchAggregator();
            var p1 = new[]
            {
                Row("a", Day0, exerciseId: 1, eff: 40),
                Row("a", Day0, exerciseId: 2, eff: 90),
            };
            var p2 = new[]
            {
                Row("b", Day0, exerciseId: 1, eff: 60), // exercise 1 mean = (40+60)/2 = 50
                Row("b", Day0, exerciseId: 2, eff: 70), // exercise 2 mean = (90+70)/2 = 80
            };

            var ranked = agg.GroupExerciseEffectivenessOf(
                new[] { (IReadOnlyList<ResearchParticipantRow>)p1, p2 });

            Assert.Equal(2, ranked.Count);
            // Ranked by mean effectiveness descending: exercise 2 (80) before exercise 1 (50).
            Assert.Equal(2, ranked[0].ExerciseId);
            Assert.Equal(80.0, ranked[0].MeanEffectiveness, 6);
            Assert.Equal(2, ranked[0].ContributingParticipantCount);
            Assert.Equal(2, ranked[0].SessionCount);

            Assert.Equal(1, ranked[1].ExerciseId);
            Assert.Equal(50.0, ranked[1].MeanEffectiveness, 6);
        }

        // ── 6. Effectiveness ignores rows with no measured effectiveness ────────────
        [Fact]
        public void GroupExerciseEffectiveness_IgnoresRowsWithoutMeasuredEffectiveness()
        {
            var agg = new ResearchAggregator();
            var p1 = new[]
            {
                Row("a", Day0, exerciseId: 5, eff: 70),
                Row("a", Day0, exerciseId: 5, eff: null), // no measurement ⇒ excluded
                Row("a", Day0, exerciseId: null, eff: 99), // no exercise ⇒ excluded
            };

            var ranked = agg.GroupExerciseEffectivenessOf(
                new[] { (IReadOnlyList<ResearchParticipantRow>)p1 });
            var ex = Assert.Single(ranked);
            Assert.Equal(5, ex.ExerciseId);
            Assert.Equal(70.0, ex.MeanEffectiveness, 6);
            Assert.Equal(1, ex.SessionCount); // only the measured one
        }

        // ── 7. CohortPlateauFrequency: share of participants with any plateau ────────
        [Fact]
        public void CohortPlateauFrequency_CountsParticipantsWithAnyPlateau()
        {
            var agg = new ResearchAggregator();
            var withPlateau = new[]
            {
                Row("a", Day0, plateau: false),
                Row("a", Day0.AddDays(1), plateau: true), // any plateau ⇒ counts
            };
            var withoutPlateau = new[]
            {
                Row("b", Day0, plateau: false),
            };
            var alsoPlateau = new[]
            {
                Row("c", Day0, plateau: true),
            };

            var freq = agg.CohortPlateauFrequencyOf(new[]
            {
                (IReadOnlyList<ResearchParticipantRow>)withPlateau, withoutPlateau, alsoPlateau
            });

            Assert.Equal(3, freq.ParticipantCount);
            Assert.Equal(2, freq.ParticipantsWithPlateau);
            Assert.Equal(2.0 / 3 * 100.0, freq.PlateauFrequencyPercent, 6);
        }

        // ── 8. CohortRecoveryDistribution: per-participant means only ────────────────
        [Fact]
        public void CohortRecoveryDistribution_UsesPerParticipantMeans()
        {
            var agg = new ResearchAggregator();
            var p1 = new[]
            {
                Row("a", Day0, recovery: 40),
                Row("a", Day0.AddDays(1), recovery: 60), // p1 mean = 50
            };
            var p2 = new[]
            {
                Row("b", Day0, recovery: 90), // p2 mean = 90
            };

            var dist = agg.CohortRecoveryDistributionOf(
                new[] { (IReadOnlyList<ResearchParticipantRow>)p1, p2 });

            Assert.Equal(2, dist.ParticipantCount);
            Assert.Equal((50.0 + 90.0) / 2, dist.Mean, 6); // 70
            Assert.Equal(50.0, dist.Min, 6);
            Assert.Equal(90.0, dist.Max, 6);
        }

        // ── 9. Recovery distribution excludes empty participants ─────────────────────
        [Fact]
        public void CohortRecoveryDistribution_ExcludesParticipantsWithNoRows()
        {
            var agg = new ResearchAggregator();
            var dist = agg.CohortRecoveryDistributionOf(new[]
            {
                (IReadOnlyList<ResearchParticipantRow>)new[] { Row("a", Day0, recovery: 55) },
                Array.Empty<ResearchParticipantRow>(), // contributes nothing
            });

            Assert.Equal(1, dist.ParticipantCount);
            Assert.Equal(55.0, dist.Mean, 6);
            Assert.Equal(55.0, dist.Min, 6);
            Assert.Equal(55.0, dist.Max, 6);
        }

        // ── 10. Null tolerance: null cohort and null inner series handled ────────────
        [Fact]
        public void Aggregator_ToleratesNullCohortAndNullSeries()
        {
            var agg = new ResearchAggregator();

            Assert.Empty(agg.GroupExerciseEffectivenessOf(null));
            Assert.Equal(0, agg.CohortPlateauFrequencyOf(null).ParticipantCount);
            Assert.Equal(0.0, agg.CohortRecoveryDistributionOf(null).Mean, 6);

            var withNullInner = new IReadOnlyList<ResearchParticipantRow>?[] { null }
                .Cast<IReadOnlyList<ResearchParticipantRow>>().ToList();
            var freq = agg.CohortPlateauFrequencyOf(withNullInner!);
            Assert.Equal(1, freq.ParticipantCount);
            Assert.Equal(0, freq.ParticipantsWithPlateau);
        }

        // ── 11. Full dataset is PII-free when serialized end-to-end ──────────────────
        [Fact]
        public void BuildDataset_OutputSerializesWithoutPii()
        {
            var agg = new ResearchAggregator();
            var anonymizer = new ResearchAnonymizer();

            // Build a raw PII-bearing row, anonymize, then aggregate — full pipeline.
            var raw = new RawResearchRow
            {
                UserId = 77,
                Timestamp = new DateTime(2026, 6, 7, 8, 9, 10, DateTimeKind.Utc),
                CompositeVoiceScore = 50,
                RecoveryScore0to100 = 50,
                SubjectiveNote = "secret note about Alice",
                ClinicalNoteBody = "diagnosis text"
            };
            var anonymized = anonymizer.Anonymize(new[] { raw }, "tok-77");
            var dataset = agg.BuildDataset(
                new[] { anonymized }, "dataset-tok-77");

            var json = JsonSerializer.Serialize(dataset);
            Assert.DoesNotContain("secret note", json);
            Assert.DoesNotContain("Alice", json);
            Assert.DoesNotContain("diagnosis", json);
            Assert.DoesNotContain(":77", json);
            Assert.DoesNotContain("\"UserId\"", json);
        }

        // ── 12. N=1 ranked single-participant series identical to its own means ──────
        [Fact]
        public void GroupExerciseEffectiveness_N1_EqualsThatParticipantsMeans()
        {
            var agg = new ResearchAggregator();
            var p1 = new[]
            {
                Row("solo", Day0,           exerciseId: 3, eff: 30),
                Row("solo", Day0.AddDays(1), exerciseId: 3, eff: 50),
            };

            var ranked = agg.GroupExerciseEffectivenessOf(
                new[] { (IReadOnlyList<ResearchParticipantRow>)p1 });
            var ex = Assert.Single(ranked);
            Assert.Equal(40.0, ex.MeanEffectiveness, 6); // (30+50)/2
            Assert.Equal(1, ex.ContributingParticipantCount);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static ResearchParticipantRow Row(
            string token,
            DateTime dayBucket,
            double composite = 50,
            double recovery = 50,
            int? exerciseId = null,
            double? eff = null,
            bool plateau = false) => new()
        {
            ParticipantToken = token,
            DayBucket = dayBucket,
            CompositeVoiceScore = composite,
            RecoveryScore0to100 = recovery,
            ExerciseId = exerciseId,
            ExerciseEffectiveness = eff,
            PlateauActive = plateau
        };
    }
}
