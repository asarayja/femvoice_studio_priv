using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Input bundle for <see cref="VoiceKnowledgeGraphBuilder.Build"/>. All data must be
    /// pre-fetched by the caller so the builder itself has no async dependencies and is
    /// purely functional (pure transformation, no I/O).
    /// </summary>
    public sealed record VoiceKnowledgeGraphInput
    {
        /// <summary>The user the graph is built for.</summary>
        public int UserId { get; init; }

        /// <summary>The user's current goal profile. May be null when not yet configured.</summary>
        public VoiceGoalProfile? GoalProfile { get; init; }

        /// <summary>
        /// Chronological Voice Intelligence trend points for the user (oldest first).
        /// Each point becomes a <see cref="MetricNode"/> in the graph.
        /// </summary>
        public IReadOnlyList<VoiceIntelligenceTrendPoint> TrendPoints { get; init; } =
            Array.Empty<VoiceIntelligenceTrendPoint>();

        /// <summary>
        /// End-of-session insights for the user. Each becomes an <see cref="InsightNode"/>
        /// connected to the corresponding metric (trend) node via SessionId.
        /// </summary>
        public IReadOnlyList<SessionInsight> Insights { get; init; } =
            Array.Empty<SessionInsight>();

        /// <summary>
        /// Per-exercise effectiveness profiles for the exercises recommended by insights.
        /// Used to build <see cref="ExerciseNode"/> nodes.
        /// </summary>
        public IReadOnlyList<ExerciseEffectivenessProfile> ExerciseProfiles { get; init; } =
            Array.Empty<ExerciseEffectivenessProfile>();
    }

    /// <summary>
    /// Builds a <see cref="VoiceKnowledgeGraph"/> from pre-fetched domain data.
    ///
    /// <para>Chain constructed:
    /// User → Goal → Metric(s) → Insight(s) → Recommendation(s) → Exercise(s) → Outcome(s).
    /// </para>
    ///
    /// <para>Safety contract: this builder is DESCRIPTIVE/EXPLANATORY intelligence only.
    /// It produces data for coaching layers and must never override safety/health/recovery
    /// gates — those live upstream.</para>
    ///
    /// <para>OLS slope helper is copied verbatim from
    /// <c>LearningPathProfileBuilder.LinearSlope</c> (single private copy per motor,
    /// per project convention).</para>
    /// </summary>
    public sealed class VoiceKnowledgeGraphBuilder
    {
        // ── Verbatim OLS slope copy (from LearningPathProfileBuilder.LinearSlope) ──
        // Returns 0 when undetermined (n < 2 or near-zero denominator).

        /// <summary>
        /// Ordinary-least-squares slope of y over x = 0, 1, 2, … Returns 0 when
        /// undetermined. Copied verbatim from LearningPathProfileBuilder.LinearSlope.
        /// </summary>
        private static double LinearSlope(IReadOnlyList<double> y)
        {
            var n = y.Count;
            if (n < 2) return 0.0;

            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            for (var i = 0; i < n; i++)
            {
                sumX  += i;
                sumY  += y[i];
                sumXY += i * y[i];
                sumX2 += (double)i * i;
            }

            var denominator = n * sumX2 - sumX * sumX;
            if (Math.Abs(denominator) < 0.0001) return 0.0;
            return (n * sumXY - sumX * sumY) / denominator;
        }

        // ── Recommendation confidence (mirrors BuildConfidence in LearningPathProfileBuilder)

        private const int    VolumeCap             = 12;
        private const double VolumeWeight          = 25.0;
        private const double TrendWeight           = 20.0;
        private const double StabilityWeight       = 20.0;
        private const double EmptyHistoryConfidence = 35.0;
        private const double BaseRetention         = 1.0;
        private const int    MinPointsForTrend     = 3;

        private static double BuildRecommendationConfidence(IReadOnlyList<double> scores)
        {
            if (scores.Count == 0) return EmptyHistoryConfidence;

            var volume = Math.Min(scores.Count, VolumeCap) / (double)VolumeCap * VolumeWeight;

            double trendComponent;
            if (scores.Count >= MinPointsForTrend)
            {
                var slope      = LinearSlope(scores);
                var normalised = Math.Clamp(slope, -1.0, 1.0);
                var trend01    = (normalised + 1.0) / 2.0;
                trendComponent = trend01 * TrendWeight;
            }
            else
            {
                trendComponent = 0.5 * TrendWeight;
            }

            var mean      = scores.Average();
            var sumSq     = scores.Sum(v => (v - mean) * (v - mean));
            var stdDev    = scores.Count >= 2
                ? Math.Sqrt(sumSq / scores.Count)
                : 0.0;
            var normVol   = Math.Clamp(stdDev / 20.0, 0.0, 1.0);
            var stability = (1.0 - normVol) * StabilityWeight;

            var raw = EmptyHistoryConfidence * BaseRetention + volume + trendComponent + stability;
            return Math.Clamp(raw, 0.0, 100.0);
        }

        // ── Public Build entry-point ─────────────────────────────────────────────

        /// <summary>
        /// Builds and returns a fully connected <see cref="VoiceKnowledgeGraph"/> from the
        /// supplied <paramref name="input"/>. Pure transformation — no side-effects, no I/O.
        /// </summary>
        /// <param name="input">Pre-fetched domain data bundle.</param>
        /// <returns>A new, populated graph.</returns>
        public VoiceKnowledgeGraph Build(VoiceKnowledgeGraphInput input)
        {
            ArgumentNullException.ThrowIfNull(input);

            var graph = new VoiceKnowledgeGraph();

            // ── 1. User node ─────────────────────────────────────────────────────
            var userId   = input.UserId;
            var userNode = new UserNode
            {
                Id     = NodeId.Of(NodeKind.User, userId),
                UserId = userId
            };
            graph.AddNode(userNode);

            // ── 2. Goal node (optional) ──────────────────────────────────────────
            if (input.GoalProfile is not null)
            {
                var goalId = NodeId.Of(NodeKind.Goal, userId);
                var goalNode = new GoalNode
                {
                    Id          = goalId,
                    GoalProfile = input.GoalProfile
                };
                graph.AddNode(goalNode);
                graph.AddEdge(new Edge
                {
                    From = userNode.Id,
                    To   = goalId,
                    Kind = EdgeKind.Pursues
                });

                // ── 3. Metric nodes (one per trend point) ────────────────────────
                var trendPoints = input.TrendPoints
                    .OrderBy(p => p.StartedAt)
                    .ToList();

                foreach (var tp in trendPoints)
                {
                    var metricId   = NodeId.Of(NodeKind.Metric, tp.SessionId);
                    var metricNode = new MetricNode
                    {
                        Id          = metricId,
                        TrendPoint  = tp,
                        DimensionScores = BuildDimensionScores(tp)
                    };
                    graph.AddNode(metricNode);
                    graph.AddEdge(new Edge
                    {
                        From = goalId,
                        To   = metricId,
                        Kind = EdgeKind.EvidencedBy
                    });
                }

                // ── 4. Insight nodes + Recommendation nodes ──────────────────────
                // Index metric nodes by SessionId for O(1) linkage.
                var metricBySession = trendPoints.ToDictionary(p => p.SessionId);

                // For confidence: collect composites from all trend points.
                var composites = trendPoints
                    .Select(p => p.CompositeVoiceScore)
                    .ToList();

                // Index exercise profiles for O(1) lookup.
                var exerciseProfileMap = input.ExerciseProfiles
                    .ToDictionary(ep => ep.ExerciseId);

                // Track which exercise nodes have been added (avoid duplicates).
                var addedExercises = new HashSet<int>();
                var addedOutcomes  = new HashSet<int>();

                foreach (var insight in input.Insights)
                {
                    var insightKey = insight.SessionId > 0 ? insight.SessionId : -userId;
                    var insightId  = NodeId.Of(NodeKind.Insight, insightKey);
                    var insightNode = new InsightNode
                    {
                        Id      = insightId,
                        Insight = insight
                    };
                    graph.AddNode(insightNode);

                    // Connect metric → insight when we have a matching trend point.
                    if (insight.SessionId > 0 && metricBySession.ContainsKey(insight.SessionId))
                    {
                        graph.AddEdge(new Edge
                        {
                            From = NodeId.Of(NodeKind.Metric, insight.SessionId),
                            To   = insightId,
                            Kind = EdgeKind.Produces
                        });
                    }

                    // ── 5. Recommendation node per insight ───────────────────────
                    var focus = insight.SuggestedFocus;

                    // Use per-dimension slope of the focus dimension as trend signal for confidence.
                    var dimScores = trendPoints
                        .Select(p => ScoreForDimension(p, focus))
                        .ToList();
                    var confidence = BuildRecommendationConfidence(
                        composites.Count > 0 ? composites : dimScores);

                    var recKey = RecommendationKey(userId, insight.SessionId, focus);
                    var recId  = NodeId.Of(NodeKind.Recommendation, recKey);
                    var recNode = new RecommendationNode
                    {
                        Id         = recId,
                        Focus      = focus,
                        Confidence = confidence,
                        ReasonCode = $"FOCUS_{focus.ToString().ToUpperInvariant()}"
                    };
                    graph.AddNode(recNode);
                    graph.AddEdge(new Edge
                    {
                        From = insightId,
                        To   = recId,
                        Kind = EdgeKind.Drives
                    });

                    // ── 6. Exercise nodes (one per suggested exercise) ───────────
                    foreach (var exId in insight.SuggestedExercises)
                    {
                        if (!addedExercises.Contains(exId))
                        {
                            var exNodeId = NodeId.Of(NodeKind.Exercise, exId);
                            var exNode   = new ExerciseNode
                            {
                                Id                  = exNodeId,
                                ExerciseId          = exId,
                                EffectivenessProfile = exerciseProfileMap.TryGetValue(exId, out var ep)
                                    ? ep : null
                            };
                            graph.AddNode(exNode);
                            addedExercises.Add(exId);
                        }

                        graph.AddEdge(new Edge
                        {
                            From = recId,
                            To   = NodeId.Of(NodeKind.Exercise, exId),
                            Kind = EdgeKind.Targets
                        });

                        // ── 7. Outcome node per exercise ─────────────────────────
                        if (!addedOutcomes.Contains(exId))
                        {
                            var outcomeId = NodeId.Of(NodeKind.Outcome, exId);
                            var outcome   = BuildOutcome(outcomeId, exId, focus, trendPoints);
                            graph.AddNode(outcome);
                            addedOutcomes.Add(exId);

                            graph.AddEdge(new Edge
                            {
                                From = NodeId.Of(NodeKind.Exercise, exId),
                                To   = outcomeId,
                                Kind = EdgeKind.Yields
                            });
                        }
                    }
                }
            }
            else
            {
                // No goal yet — still add metric nodes directly off the user node so
                // the graph is not empty and trend queries still work.
                foreach (var tp in input.TrendPoints.OrderBy(p => p.StartedAt))
                {
                    var metricId = NodeId.Of(NodeKind.Metric, tp.SessionId);
                    graph.AddNode(new MetricNode
                    {
                        Id             = metricId,
                        TrendPoint     = tp,
                        DimensionScores = BuildDimensionScores(tp)
                    });
                    graph.AddEdge(new Edge
                    {
                        From = userNode.Id,
                        To   = metricId,
                        Kind = EdgeKind.EvidencedBy
                    });
                }
            }

            return graph;
        }

        // ── Private helpers ──────────────────────────────────────────────────────

        /// <summary>
        /// Extracts the 0–100 score for each <see cref="VoiceDimension"/> from a
        /// <see cref="VoiceIntelligenceTrendPoint"/>, keyed by the real enum values.
        /// </summary>
        private static IReadOnlyDictionary<VoiceDimension, double> BuildDimensionScores(
            VoiceIntelligenceTrendPoint tp)
        {
            return new Dictionary<VoiceDimension, double>
            {
                [VoiceDimension.Recovery]    = tp.RecoveryScore100,
                [VoiceDimension.Comfort]     = tp.ComfortScore100,
                [VoiceDimension.Resonance]   = tp.ResonanceScore100,
                [VoiceDimension.Consistency] = tp.ConsistencyScore100,
                [VoiceDimension.Intonation]  = tp.IntonationScore100,
                [VoiceDimension.VocalWeight] = tp.VocalWeightScore100,
                [VoiceDimension.Pitch]       = tp.PitchScore100
            };
        }

        /// <summary>Extracts the 0–100 score for a single dimension from a trend point.</summary>
        private static double ScoreForDimension(VoiceIntelligenceTrendPoint tp, VoiceDimension dim)
            => dim switch
            {
                VoiceDimension.Recovery    => tp.RecoveryScore100,
                VoiceDimension.Comfort     => tp.ComfortScore100,
                VoiceDimension.Resonance   => tp.ResonanceScore100,
                VoiceDimension.Consistency => tp.ConsistencyScore100,
                VoiceDimension.Intonation  => tp.IntonationScore100,
                VoiceDimension.VocalWeight => tp.VocalWeightScore100,
                VoiceDimension.Pitch       => tp.PitchScore100,
                _                          => tp.CompositeVoiceScore
            };

        /// <summary>
        /// Builds an <see cref="OutcomeNode"/> for a given exercise by computing the OLS
        /// slope of the target dimension across the supplied trend points.
        /// Requires ≥ 3 points for a positive outcome signal (mirrors HasEnoughData convention).
        /// </summary>
        private static OutcomeNode BuildOutcome(
            NodeId outcomeId,
            int exerciseId,
            VoiceDimension targetDimension,
            IReadOnlyList<VoiceIntelligenceTrendPoint> trendPoints)
        {
            var scores = trendPoints
                .Select(p => ScoreForDimension(p, targetDimension))
                .ToList();

            var slope        = LinearSlope(scores);
            var isPositive   = scores.Count >= 3 && slope > 0.0;

            return new OutcomeNode
            {
                Id              = outcomeId,
                ExerciseId      = exerciseId,
                TargetDimension = targetDimension,
                ObservedSlope   = slope,
                IsPositive      = isPositive,
                SessionCount    = scores.Count
            };
        }

        /// <summary>
        /// Deterministic integer key for a recommendation node. Combines userId,
        /// sessionId (0 for aggregate) and the dimension ordinal into a single int.
        /// Domain: userId 1–9999, sessionId 0–9999, dimension 0–6 → key stays positive.
        /// </summary>
        private static int RecommendationKey(int userId, int sessionId, VoiceDimension dim)
        {
            // Pack: userId * 1_000_000 + sessionId * 10 + (int)dim
            // Safe up to userId 2147 with sessionId ≤ 99999. Sufficient for this engine.
            return userId * 1_000_000 + (sessionId & 0xFFFF) * 10 + (int)dim;
        }
    }
}
