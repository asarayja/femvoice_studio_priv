using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services.Progression;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Builds the <see cref="LearningPathProfile"/> aggregate (Sprint C, Agent LP) from
    /// ALREADY-FETCHED inputs. This class is intentionally PURE — it performs no DB/IO,
    /// it only reasons over the numbers a caller has already aggregated
    /// (a Voice Intelligence trend, a recovery result, a complexity evaluation, and an
    /// optional mastery summary). That keeps it deterministic and trivially testable with
    /// real classes + in-memory data (no mocking).
    ///
    /// ── STAGE MAPPING (5 readable stages over the 7-level complexity ladder) ──────────
    ///   IsolatedSounds, Syllables   ⇒ Foundation
    ///   Words, Phrases              ⇒ Building
    ///   StructuredSentences         ⇒ Refining
    ///   SpontaneousSpeech           ⇒ Integrating
    ///   Conversational              ⇒ Maintaining
    /// Mastery nuance (additive, bounded, never demotes): if the complexity level is the
    /// TOP level of its stage band AND mastery is Mastered, the stage may advance by one
    /// (capped at Maintaining). Empty history ⇒ ComplexityEngine reports IsolatedSounds
    /// ⇒ Foundation.
    ///
    /// ── FOCUS HIERARCHY ──────────────────────────────────────────────────────────────
    /// Active focus areas are the genuinely-weak dimensions (latest score &lt;
    /// <see cref="WeaknessThreshold"/>), ordered Recovery &gt; Comfort &gt; Resonance &gt;
    /// Consistency &gt; Intonation &gt; VocalWeight &gt; Pitch (the <see cref="VoiceDimension"/>
    /// enum value IS that priority order). A learner with no weak dimension gets an empty
    /// focus list — never a fabricated one.
    ///
    /// ── CONFIDENCE FORMULA (explainable, deterministic, 0–100) ────────────────────────
    /// Confidence reflects how much CONSISTENT, POSITIVE history backs the picture:
    ///   • Base (empty history)         = <see cref="EmptyHistoryConfidence"/> (35) —
    ///     a new learner's path is reasonable but unevidenced (never 0).
    ///   • Volume term  = min(points, <see cref="VolumeCap"/>) / VolumeCap × <see cref="VolumeWeight"/>
    ///     — more sessions ⇒ more evidence (saturates at <see cref="VolumeCap"/> = 12).
    ///   • Trend term   = clamp(compositeSlope, −1, +1) mapped 0..1 × <see cref="TrendWeight"/>
    ///     — a longer CONSISTENT POSITIVE composite trend raises confidence; a declining
    ///     trend lowers it; flat is neutral (half weight). Needs ≥3 points to be meaningful.
    ///   • Stability term = (1 − normalisedVolatility) × <see cref="StabilityWeight"/>
    ///     — low session-to-session composite volatility ⇒ steadier evidence.
    /// For non-empty history: Confidence = Base×baseRetention + volume + trend + stability,
    /// clamped 0–100. Buckets: ≥70 Established, ≥45 Moderate, else Emerging.
    /// </summary>
    public sealed class LearningPathProfileBuilder
    {
        // ── Weakness / strength threshold ───────────────────────────────────────────
        // A dimension is "genuinely weak" (eligible for active focus) below this score.
        private const double WeaknessThreshold = 60.0;

        // How many strengths / weaknesses to surface at most.
        private const int MaxStrengths = 3;
        private const int MaxWeaknesses = 3;
        private const int MaxFocusAreas = 3;
        private const int MaxRecommendations = 4;

        // ── Confidence weights (sum of the three evidence terms + retained base) ─────
        private const double EmptyHistoryConfidence = 35.0;
        private const double BaseRetention = 0.5;          // how much of the base survives once we have data
        private const double VolumeWeight = 25.0;
        private const double TrendWeight = 25.0;
        private const double StabilityWeight = 32.5;
        private const int VolumeCap = 12;                  // sessions at which volume evidence saturates
        private const int MinPointsForTrend = 3;
        private const double VolatilityNormaliser = 25.0;  // composite std-dev mapped onto 0..1 for the stability term

        // ── Confidence buckets ──────────────────────────────────────────────────────
        private const double EstablishedThreshold = 70.0;
        private const double ModerateThreshold = 45.0;

        /// <summary>
        /// Builds a <see cref="LearningPathProfile"/> from pre-fetched inputs. Pure and
        /// total: any input (including an empty trend) yields a well-formed profile.
        /// </summary>
        /// <param name="trend">Chronological Voice Intelligence trend (may be empty).</param>
        /// <param name="recovery">Recovery result from <see cref="RecoveryScorer"/>.</param>
        /// <param name="complexity">Current complexity evaluation from <see cref="ComplexityEngine"/>.</param>
        /// <param name="mastery">Optional mastery summary; nuances the stage upward only.</param>
        /// <param name="effectiveness">
        /// OPTIONAL per-exercise EFFECTIVENESS intelligence (Sprint C.2, Agent EFF). When
        /// supplied, <see cref="BuildRecommendations"/> orders the current-level
        /// recommended exercises MOST-EFFECTIVE-FIRST (by CompositeEffectiveness) for
        /// profiles with enough data; otherwise it keeps today's provisional band logic.
        /// Profiles without enough data sit at the neutral midpoint, so "insufficient
        /// evidence" never reads as "ineffective". null ⇒ EXACTLY today's recommendations.
        /// </param>
        public LearningPathProfile Build(
            IReadOnlyList<VoiceIntelligenceTrendPoint> trend,
            RecoveryResult recovery,
            ComplexityEvaluation complexity,
            MasteryEvaluation? mastery = null,
            IReadOnlyList<ExerciseEffectivenessProfile>? effectiveness = null)
        {
            if (complexity == null) throw new ArgumentNullException(nameof(complexity));

            var points = trend ?? Array.Empty<VoiceIntelligenceTrendPoint>();
            var latest = points.Count > 0 ? points[points.Count - 1] : null;

            var (stage, stageExplanation) = DeriveStage(complexity.CurrentLevel, mastery?.Level);
            var assessments = latest is null
                ? Array.Empty<DimensionAssessment>()
                : BuildAssessments(latest);

            var strengths = assessments
                .OrderByDescending(a => a.Score)
                .ThenBy(a => (int)a.Dimension)
                .Take(MaxStrengths)
                .ToList();

            var weaknesses = assessments
                .OrderBy(a => a.Score)
                .ThenBy(a => (int)a.Dimension)
                .Take(MaxWeaknesses)
                .ToList();

            // Active focus = genuinely-weak dimensions, ordered by the clinical hierarchy
            // (VoiceDimension enum value == priority). Only reuse the latest snapshot.
            var focusAreas = assessments
                .Where(a => a.Score < WeaknessThreshold)
                .OrderBy(a => (int)a.Dimension)
                .Take(MaxFocusAreas)
                .Select(a => a.Dimension)
                .ToList();

            var recommendations = BuildRecommendations(focusAreas, complexity.CurrentLevel, effectiveness);
            var recoveryReq = BuildRecoveryRequirement(recovery);
            var (confidenceScore, confidenceLevel, confidenceExplanation) =
                BuildConfidence(points);

            return new LearningPathProfile
            {
                CurrentStage = stage,
                StageExplanation = stageExplanation,
                Strengths = strengths,
                Weaknesses = weaknesses,
                ActiveFocusAreas = focusAreas,
                RecommendedExercises = recommendations,
                RecoveryRequirements = recoveryReq,
                ConfidenceLevel = confidenceLevel,
                ConfidenceScore = confidenceScore,
                ConfidenceExplanation = confidenceExplanation
            };
        }

        // ── Stage ───────────────────────────────────────────────────────────────────

        private static (LearningStage Stage, string Explanation) DeriveStage(
            SpeechComplexityLevel level, MasteryLevel? mastery)
        {
            var baseStage = MapComplexityToStage(level);
            var stage = baseStage;

            // Mastery nuance: only when at the top of the stage band AND fully mastered,
            // and only ever upward, capped at Maintaining. Never demotes.
            var atTopOfBand = IsTopOfStageBand(level);
            var mastered = mastery == MasteryLevel.Mastered;
            if (atTopOfBand && mastered && stage < LearningStage.Maintaining)
            {
                stage = (LearningStage)((int)stage + 1);
            }

            var explanation = stage == baseStage
                ? string.Create(CultureInfo.InvariantCulture,
                    $"Stage {stage} derived from complexity level {level}.")
                : string.Create(CultureInfo.InvariantCulture,
                    $"Stage {stage}: complexity level {level} consolidated (mastered), advanced one step.");

            return (stage, explanation);
        }

        private static LearningStage MapComplexityToStage(SpeechComplexityLevel level) => level switch
        {
            SpeechComplexityLevel.IsolatedSounds => LearningStage.Foundation,
            SpeechComplexityLevel.Syllables => LearningStage.Foundation,
            SpeechComplexityLevel.Words => LearningStage.Building,
            SpeechComplexityLevel.Phrases => LearningStage.Building,
            SpeechComplexityLevel.StructuredSentences => LearningStage.Refining,
            SpeechComplexityLevel.SpontaneousSpeech => LearningStage.Integrating,
            SpeechComplexityLevel.Conversational => LearningStage.Maintaining,
            _ => LearningStage.Foundation
        };

        // The highest complexity level inside each stage band — the only place mastery is
        // allowed to nudge the stage up by one.
        private static bool IsTopOfStageBand(SpeechComplexityLevel level) => level switch
        {
            SpeechComplexityLevel.Syllables => true,           // top of Foundation
            SpeechComplexityLevel.Phrases => true,             // top of Building
            SpeechComplexityLevel.StructuredSentences => true, // (single-level) Refining
            SpeechComplexityLevel.SpontaneousSpeech => true,   // (single-level) Integrating
            _ => false
        };

        // ── Dimension assessments (from the latest trend point) ──────────────────────

        private static IReadOnlyList<DimensionAssessment> BuildAssessments(VoiceIntelligenceTrendPoint p)
        {
            return new[]
            {
                Assess(VoiceDimension.Recovery, p.RecoveryScore100),
                Assess(VoiceDimension.Comfort, p.ComfortScore100),
                Assess(VoiceDimension.Resonance, p.ResonanceScore100),
                Assess(VoiceDimension.Consistency, p.ConsistencyScore100),
                Assess(VoiceDimension.Intonation, p.IntonationScore100),
                Assess(VoiceDimension.VocalWeight, p.VocalWeightScore100),
                Assess(VoiceDimension.Pitch, p.PitchScore100)
            };
        }

        private static DimensionAssessment Assess(VoiceDimension dimension, double rawScore)
        {
            var score = Clamp01To100(rawScore);
            var band = score >= 75 ? "strong"
                     : score >= WeaknessThreshold ? "solid"
                     : score >= 40 ? "developing"
                     : "needs support";
            return new DimensionAssessment
            {
                Dimension = dimension,
                Score = score,
                Explanation = string.Create(CultureInfo.InvariantCulture,
                    $"{dimension} {score:0}/100 ({band}).")
            };
        }

        // ── Provisional recommendations (Bølge 2 replaces this body, not the shape) ──

        private static IReadOnlyList<RecommendedExercise> BuildRecommendations(
            IReadOnlyList<VoiceDimension> focusAreas, SpeechComplexityLevel level,
            IReadOnlyList<ExerciseEffectivenessProfile>? effectiveness = null)
        {
            // Effectiveness ORDERING (Sprint C.2): when observed effectiveness is supplied,
            // present the current-level ids MOST-EFFECTIVE-FIRST so the recommendations lead
            // with what has been working for this learner. Ids without enough data (or with
            // no profile) sit at the neutral midpoint and keep their natural id order — so
            // "insufficient evidence" never reads as "ineffective". null ⇒ today's band order.
            var levelIds = OrderByEffectiveness(ExerciseIdsForLevel(level), effectiveness);
            if (levelIds.Count == 0)
                return Array.Empty<RecommendedExercise>();

            // No weak dimension ⇒ keep working the current level broadly (no fabricated
            // focus dimension). Otherwise, surface current-level ids tagged with the
            // highest-priority focus dimensions in turn. The id selection is deliberately
            // simple here; Agent 2's recommender supersedes it in Bølge 2.
            var result = new List<RecommendedExercise>();
            if (focusAreas.Count == 0)
            {
                foreach (var id in levelIds.Take(MaxRecommendations))
                {
                    result.Add(new RecommendedExercise
                    {
                        ExerciseId = id,
                        TargetDimension = null,
                        Reason = "Continue at the current level."
                    });
                }
                return result;
            }

            var idx = 0;
            foreach (var dim in focusAreas)
            {
                if (result.Count >= MaxRecommendations) break;
                var id = levelIds[idx % levelIds.Count];
                idx++;
                result.Add(new RecommendedExercise
                {
                    ExerciseId = id,
                    TargetDimension = dim,
                    Reason = string.Create(CultureInfo.InvariantCulture,
                        $"Supports {dim}, a current focus area.")
                });
            }
            return result;
        }

        /// <summary>
        /// The neutral effectiveness midpoint a low-data / absent profile reports — anchored
        /// at the same midpoint as <see cref="ExerciseEffectivenessProfile.CompositeEffectiveness"/>
        /// so an unevidenced id sorts like an "average" one (no lift, no penalty).
        /// </summary>
        private const double NeutralEffectiveness = 50.0;

        /// <summary>
        /// Orders level ids MOST-EFFECTIVE-FIRST using observed CompositeEffectiveness for
        /// profiles WITH enough data; absent or low-data ids use the neutral midpoint. Ties
        /// (including the whole list when <paramref name="effectiveness"/> is null) fall back
        /// to ascending id, so the null path is byte-identical to today's band order.
        /// </summary>
        private static IReadOnlyList<int> OrderByEffectiveness(
            IReadOnlyList<int> ids,
            IReadOnlyList<ExerciseEffectivenessProfile>? effectiveness)
        {
            if (effectiveness is null || effectiveness.Count == 0 || ids.Count <= 1)
                return ids;

            var byId = new Dictionary<int, double>(effectiveness.Count);
            foreach (var p in effectiveness)
            {
                if (p is { HasEnoughData: true })
                    byId[p.ExerciseId] = p.CompositeEffectiveness;
            }
            if (byId.Count == 0)
                return ids;

            return ids
                .OrderByDescending(id => byId.TryGetValue(id, out var c) ? c : NeutralEffectiveness)
                .ThenBy(id => id)
                .ToList();
        }

        // Mirrors ComplexityEngine.GetExerciseIdsForComplexity's id banding WITHOUT taking
        // a dependency on the (DB-constructing) engine instance — this builder must stay
        // pure. FANTOM-ID-FIKS: only REAL catalog ids (1–15) are returned — the catalog has
        // exactly 15 seeded exercises, so the old 16–35 / 36–50 bands pointed at non-existent
        // rows. The 15 exercises are partitioned over the 7 levels, kept in SYNC with the
        // mirrors in ComplexityEngine.GetExerciseIdsForComplexity and
        // ExerciseRecommendationEngine.ExerciseIdsForComplexity:
        //   IsolatedSounds 1–3 · Syllables 4–6 · Words 7–8 · Phrases 9–10 ·
        //   StructuredSentences 11–12 · SpontaneousSpeech 13–14 · Conversational 15.
        private static IReadOnlyList<int> ExerciseIdsForLevel(SpeechComplexityLevel level) => level switch
        {
            SpeechComplexityLevel.IsolatedSounds      => new[] { 1, 2, 3 },
            SpeechComplexityLevel.Syllables           => new[] { 4, 5, 6 },
            SpeechComplexityLevel.Words               => new[] { 7, 8 },
            SpeechComplexityLevel.Phrases             => new[] { 9, 10 },
            SpeechComplexityLevel.StructuredSentences => new[] { 11, 12 },
            SpeechComplexityLevel.SpontaneousSpeech   => new[] { 13, 14 },
            SpeechComplexityLevel.Conversational      => new[] { 15 },
            _                                         => new[] { 1, 2, 3 }
        };

        // ── Recovery requirement (copy out of RecoveryResult, no Services coupling in model) ──

        private static RecoveryRequirement BuildRecoveryRequirement(RecoveryResult recovery)
        {
            // Rest is recommended (recovery authoritative over goals/coaching) when the
            // voice is not at least Adequate — i.e. Strained or Overtrained.
            var restRecommended =
                recovery.Status == RecoveryStatus.Strained ||
                recovery.Status == RecoveryStatus.Overtrained;

            return new RecoveryRequirement
            {
                Score = recovery.Score,
                Status = recovery.Status.ToString(),
                RestRecommended = restRecommended,
                Explanation = recovery.Explanation ?? string.Empty
            };
        }

        // ── Confidence ────────────────────────────────────────────────────────────────

        private static (double Score, LearningConfidenceLevel Level, string Explanation) BuildConfidence(
            IReadOnlyList<VoiceIntelligenceTrendPoint> points)
        {
            if (points.Count == 0)
            {
                return (
                    EmptyHistoryConfidence,
                    LearningConfidenceLevel.Emerging,
                    "No session history yet — starting confidence is modest until trends accrue.");
            }

            // Volume evidence (saturates at VolumeCap).
            var volume = Math.Min(points.Count, VolumeCap) / (double)VolumeCap * VolumeWeight;

            // Trend evidence from the composite slope (only meaningful with ≥3 points).
            var composites = points.Select(p => p.CompositeVoiceScore).ToList();
            double trendComponent;
            string trendNote;
            if (points.Count >= MinPointsForTrend)
            {
                var slope = LinearSlope(composites);            // composite units per session step
                var normalised = Math.Clamp(slope, -1.0, 1.0);  // map a meaningful slope onto [-1,1]
                var trend01 = (normalised + 1.0) / 2.0;          // 0 (declining) .. 0.5 (flat) .. 1 (rising)
                trendComponent = trend01 * TrendWeight;
                trendNote = slope > 0.05 ? "improving" : slope < -0.05 ? "declining" : "flat";
            }
            else
            {
                trendComponent = 0.5 * TrendWeight;              // too short to judge ⇒ neutral half-weight
                trendNote = "too short to judge";
            }

            // Stability evidence: low composite volatility ⇒ steadier picture.
            var volatility = StandardDeviation(composites);
            var normVolatility = Math.Clamp(volatility / VolatilityNormaliser, 0.0, 1.0);
            var stabilityComponent = (1.0 - normVolatility) * StabilityWeight;

            var raw = EmptyHistoryConfidence * BaseRetention + volume + trendComponent + stabilityComponent;
            var score = Math.Clamp(raw, 0.0, 100.0);
            var level = score >= EstablishedThreshold ? LearningConfidenceLevel.Established
                      : score >= ModerateThreshold ? LearningConfidenceLevel.Moderate
                      : LearningConfidenceLevel.Emerging;

            var explanation = string.Create(CultureInfo.InvariantCulture,
                $"Confidence {score:0}/100 ({level}): {points.Count} session(s), trend {trendNote}.");

            return (score, level, explanation);
        }

        // Ordinary-least-squares slope of y over x = 0,1,2,…  Returns 0 when undetermined.
        private static double LinearSlope(IReadOnlyList<double> y)
        {
            var n = y.Count;
            if (n < 2) return 0.0;

            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            for (var i = 0; i < n; i++)
            {
                sumX += i;
                sumY += y[i];
                sumXY += i * y[i];
                sumX2 += (double)i * i;
            }

            var denominator = n * sumX2 - sumX * sumX;
            if (Math.Abs(denominator) < 0.0001) return 0.0;
            return (n * sumXY - sumX * sumY) / denominator;
        }

        private static double StandardDeviation(IReadOnlyList<double> values)
        {
            if (values.Count < 2) return 0.0;
            var mean = values.Average();
            var sumSq = values.Sum(v => (v - mean) * (v - mean));
            return Math.Sqrt(sumSq / values.Count);
        }

        private static double Clamp01To100(double v)
        {
            if (double.IsNaN(v)) return 0.0;
            return Math.Clamp(v, 0.0, 100.0);
        }
    }
}
