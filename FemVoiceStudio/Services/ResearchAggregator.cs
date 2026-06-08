using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// PURE (no I/O) group-level aggregator for Research Mode analytics (Sprint E, Agent 9).
    ///
    /// <para><b>SHAPE IS MULTI-PARTICIPANT, ALWAYS.</b> Every method takes a
    /// <c>IReadOnlyList</c> of per-participant inputs, so the cohort form is identical whether
    /// there are 50 participants or just 1. A single local install (N=1) is the normal case;
    /// the aggregator still produces a valid single-participant longitudinal series and valid
    /// group statistics, but flags <see cref="ResearchDataset.HasSufficientCohort"/> = false
    /// and attaches a <see cref="ResearchDataset.VolumeCaveatNote"/> so the reader knows the
    /// "cohort" is really one case.</para>
    ///
    /// <para><b>PRIVACY.</b> Group aggregates collapse per-session detail to means/shares, and
    /// each participant contributes a single pre-anonymized series keyed only by an opaque
    /// token. No PII enters or leaves this aggregator.</para>
    ///
    /// <para>This is REPORTING-tier. It never gates or influences any Safety/Health/Recovery
    /// decision.</para>
    /// </summary>
    public sealed class ResearchAggregator
    {
        /// <summary>
        /// Documented minimum cohort size for a group finding to be treated as
        /// generalisable rather than a single case study. Below this, the dataset reports
        /// <see cref="ResearchDataset.HasSufficientCohort"/> = false and carries a caveat.
        ///
        /// <para>Set to 5: a conventional small-cohort floor below which group means are too
        /// volatile and individual re-identification risk in a shared dataset is too high to
        /// claim a generalisable result. This is a reporting threshold, not a clinical gate.</para>
        /// </summary>
        public const int MinimumCohortSize = 5;

        /// <summary>
        /// Builds a complete PII-free <see cref="ResearchDataset"/> from one anonymized series
        /// per participant.
        /// </summary>
        /// <param name="participantSeries">
        /// One anonymized day-bucketed series per participant. Each inner list is the output of
        /// <see cref="ResearchAnonymizer.Anonymize"/> for one participant. Null is treated as
        /// an empty cohort.
        /// </param>
        /// <param name="datasetToken">
        /// The opaque token to stamp on the dataset itself (typically the producing install's
        /// token). Individual rows keep their own per-participant tokens.
        /// </param>
        public ResearchDataset BuildDataset(
            IReadOnlyList<IReadOnlyList<ResearchParticipantRow>>? participantSeries,
            string datasetToken)
        {
            var series = NormalizeSeries(participantSeries);
            var participantCount = series.Count;
            var hasSufficientCohort = participantCount >= MinimumCohortSize;

            // Flatten for the day-bucketed export — emitted even at N=1.
            var allRows = series.SelectMany(s => s).ToList();

            var metrics = new ResearchAggregateMetrics
            {
                ExerciseEffectiveness = GroupExerciseEffectivenessOf(series),
                PlateauFrequency = CohortPlateauFrequencyOf(series),
                RecoveryDistribution = CohortRecoveryDistributionOf(series)
            };

            return new ResearchDataset
            {
                ParticipantToken = datasetToken ?? string.Empty,
                DayBucketedRows = allRows,
                AggregateMetrics = metrics,
                ParticipantCount = participantCount,
                HasSufficientCohort = hasSufficientCohort,
                VolumeCaveatNote = hasSufficientCohort
                    ? string.Empty
                    : BuildVolumeCaveat(participantCount)
            };
        }

        /// <summary>
        /// Cohort mean effectiveness per catalog exercise, ranked by mean effectiveness
        /// descending then exercise id ascending. Only sessions with a measured
        /// <see cref="ResearchParticipantRow.ExerciseEffectiveness"/> contribute. Multi-
        /// participant by shape: at N=1 it is simply that one participant's per-exercise means.
        /// </summary>
        public IReadOnlyList<GroupExerciseEffectiveness> GroupExerciseEffectivenessOf(
            IReadOnlyList<IReadOnlyList<ResearchParticipantRow>>? participantSeries)
        {
            var series = NormalizeSeries(participantSeries);

            // exerciseId -> (sum, count, distinct participant indices)
            var sums = new Dictionary<int, double>();
            var counts = new Dictionary<int, int>();
            var contributors = new Dictionary<int, HashSet<int>>();

            for (var p = 0; p < series.Count; p++)
            {
                foreach (var row in series[p])
                {
                    if (row.ExerciseId is not int exerciseId) continue;
                    if (row.ExerciseEffectiveness is not double eff) continue;

                    sums[exerciseId] = sums.GetValueOrDefault(exerciseId) + eff;
                    counts[exerciseId] = counts.GetValueOrDefault(exerciseId) + 1;
                    if (!contributors.TryGetValue(exerciseId, out var set))
                    {
                        set = new HashSet<int>();
                        contributors[exerciseId] = set;
                    }
                    set.Add(p);
                }
            }

            return counts.Keys
                .Select(exerciseId => new GroupExerciseEffectiveness
                {
                    ExerciseId = exerciseId,
                    MeanEffectiveness = sums[exerciseId] / counts[exerciseId],
                    ContributingParticipantCount = contributors[exerciseId].Count,
                    SessionCount = counts[exerciseId]
                })
                .OrderByDescending(g => g.MeanEffectiveness)
                .ThenBy(g => g.ExerciseId)
                .ToList();
        }

        /// <summary>
        /// Share of participants with at least one plateau-flagged session. A participant with
        /// no rows does not count as a plateau participant. At N=1 the result is 0 or 100.
        /// </summary>
        public CohortPlateauFrequency CohortPlateauFrequencyOf(
            IReadOnlyList<IReadOnlyList<ResearchParticipantRow>>? participantSeries)
        {
            var series = NormalizeSeries(participantSeries);
            var participantCount = series.Count;
            var withPlateau = series.Count(s => s.Any(r => r.PlateauActive));

            return new CohortPlateauFrequency
            {
                ParticipantsWithPlateau = withPlateau,
                ParticipantCount = participantCount,
                PlateauFrequencyPercent = participantCount == 0
                    ? 0.0
                    : (double)withPlateau / participantCount * 100.0
            };
        }

        /// <summary>
        /// Distribution (mean / min / max) of the per-participant MEAN recovery score. Each
        /// participant collapses to a single mean first, so individual session values are never
        /// exposed at group level. Participants with no rows are excluded from the distribution.
        /// At N=1, mean = min = max = that participant's recovery mean.
        /// </summary>
        public CohortRecoveryDistribution CohortRecoveryDistributionOf(
            IReadOnlyList<IReadOnlyList<ResearchParticipantRow>>? participantSeries)
        {
            var series = NormalizeSeries(participantSeries);

            var perParticipantMeans = series
                .Where(s => s.Count > 0)
                .Select(s => s.Average(r => r.RecoveryScore0to100))
                .ToList();

            if (perParticipantMeans.Count == 0)
            {
                return new CohortRecoveryDistribution
                {
                    Mean = 0.0, Min = 0.0, Max = 0.0, ParticipantCount = 0
                };
            }

            return new CohortRecoveryDistribution
            {
                Mean = perParticipantMeans.Average(),
                Min = perParticipantMeans.Min(),
                Max = perParticipantMeans.Max(),
                ParticipantCount = perParticipantMeans.Count
            };
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Defensive normalisation: nulls become empty lists so every method tolerates a
        /// null cohort, a null participant series, and a null row list uniformly.
        /// </summary>
        private static List<IReadOnlyList<ResearchParticipantRow>> NormalizeSeries(
            IReadOnlyList<IReadOnlyList<ResearchParticipantRow>>? participantSeries)
        {
            if (participantSeries == null)
                return new List<IReadOnlyList<ResearchParticipantRow>>();

            var result = new List<IReadOnlyList<ResearchParticipantRow>>(participantSeries.Count);
            foreach (var s in participantSeries)
                result.Add(s ?? (IReadOnlyList<ResearchParticipantRow>)Array.Empty<ResearchParticipantRow>());
            return result;
        }

        private static string BuildVolumeCaveat(int participantCount)
        {
            return participantCount <= 1
                ? "Single-participant data (N=1): group statistics describe ONE case and must " +
                  "not be read as a generalisable cohort result. The individual longitudinal " +
                  "series is still valid."
                : $"Small cohort (N={participantCount}, below the minimum of {MinimumCohortSize}): " +
                  "group statistics are not generalisable and should be read as case-level only.";
        }
    }
}
