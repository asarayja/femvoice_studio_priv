using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Tests for <see cref="VoiceKnowledgeGraph"/> and <see cref="VoiceKnowledgeGraphBuilder"/>
    /// (Sprint C.3/C.4, A1 — Knowledge Graph Foundation).
    ///
    /// No mocking. All data is hand-built with fixed dates. Assertions target:
    ///   • NodeId equality and NodeKind correctness.
    ///   • The full User → Goal → Metric → Insight → Recommendation → Exercise → Outcome chain.
    ///   • NeighborsOf, NodesOfKind and FindPath query helpers.
    ///   • Edge connectivity (From/To/Kind).
    ///   • DimensionScores dictionary keyed on real VoiceDimension enum values.
    ///   • OutcomeNode.IsPositive gate (requires ≥3 trend points with positive OLS slope).
    ///   • Graph with no GoalProfile: metric nodes hang off the user node directly.
    ///   • Empty-input guard: UserId 0 / no trend / no insight builds a minimal valid graph.
    /// </summary>
    public class VoiceKnowledgeGraphTests
    {
        private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // ── Builders ─────────────────────────────────────────────────────────────

        private static VoiceIntelligenceTrendPoint TrendPoint(
            int sessionId,
            double resonance = 60,
            double comfort   = 70,
            double composite = 65,
            DateTime? startedAt = null)
        {
            return new VoiceIntelligenceTrendPoint
            {
                SessionId          = sessionId,
                UserId             = 1,
                StartedAt          = startedAt ?? T0.AddDays(sessionId - 1),
                ResonanceScore100  = resonance,
                ComfortScore100    = comfort,
                ConsistencyScore100 = 60,
                IntonationScore100  = 55,
                VocalWeightScore100 = 50,
                RecoveryScore100    = 65,
                PitchScore100       = 45,
                CompositeVoiceScore = composite
            };
        }

        private static SessionInsight Insight(
            int sessionId,
            VoiceDimension focus = VoiceDimension.Resonance,
            int[]? exercises = null)
        {
            return new SessionInsight
            {
                SessionId         = sessionId,
                GeneratedAt       = T0.AddDays(sessionId - 1),
                CompositeVoiceScore = 65,
                IsFirstSession    = sessionId == 1,
                RecoveryNeeds     = new RecoveryNeed
                {
                    Score       = 80,
                    Status      = RecoveryStatus.Adequate,
                    Explanation = string.Empty
                },
                SuggestedFocus    = focus,
                SuggestedExercises = exercises != null
                    ? (IReadOnlyList<int>)exercises
                    : Array.Empty<int>()
            };
        }

        private static VoiceGoalProfile GoalProfile(int userId = 1) => new VoiceGoalProfile
        {
            UserId         = userId,
            GoalStyleKey   = "soft_feminine",
            PrimaryFocus   = "resonance"
        };

        private static ExerciseEffectivenessProfile ExerciseProfile(int exerciseId) =>
            new ExerciseEffectivenessProfile
            {
                ExerciseId           = exerciseId,
                ResonanceGain        = 2.0,
                ComfortGain          = 1.0,
                ConsistencyGain      = 0.5,
                RecoveryCost         = 20.0,
                UserSuccessRate      = 75.0,
                SessionCount         = 5,
                HasEnoughData        = true,
                CompositeEffectiveness = 60.0
            };

        private static VoiceKnowledgeGraphBuilder NewBuilder() => new VoiceKnowledgeGraphBuilder();

        // ── 1. Minimal input: only user, no goal ─────────────────────────────────

        [Fact]
        public void Build_NoGoalProfile_GraphContainsOnlyUserNode()
        {
            var builder = NewBuilder();
            var input = new VoiceKnowledgeGraphInput { UserId = 1 };

            var graph = builder.Build(input);

            Assert.Single(graph.Users);
            Assert.Empty(graph.Goals);
            Assert.Equal(1, graph.NodeCount);
        }

        // ── 2. User node identity ────────────────────────────────────────────────

        [Fact]
        public void Build_UserNode_HasCorrectNodeIdKindAndKey()
        {
            var graph = NewBuilder().Build(new VoiceKnowledgeGraphInput { UserId = 42 });

            var userId = graph.Users.Keys.Single();
            Assert.Equal(NodeKind.User, userId.Kind);
            Assert.Equal(42, userId.Key);
            Assert.Equal(42, graph.Users[userId].UserId);
        }

        // ── 3. Goal node created and linked from user ────────────────────────────

        [Fact]
        public void Build_WithGoalProfile_GoalNodeLinkedFromUser()
        {
            var graph = NewBuilder().Build(new VoiceKnowledgeGraphInput
            {
                UserId      = 1,
                GoalProfile = GoalProfile()
            });

            Assert.Single(graph.Goals);
            var goalId = graph.Goals.Keys.Single();

            var userNeighbors = graph.NeighborsOf(NodeId.Of(NodeKind.User, 1));
            Assert.Single(userNeighbors);
            Assert.Equal(goalId, userNeighbors[0].To);
            Assert.Equal(EdgeKind.Pursues, userNeighbors[0].Kind);
        }

        // ── 4. Metric nodes created for each trend point ─────────────────────────

        [Fact]
        public void Build_ThreeTrendPoints_ThreeMetricNodes()
        {
            var graph = NewBuilder().Build(new VoiceKnowledgeGraphInput
            {
                UserId      = 1,
                GoalProfile = GoalProfile(),
                TrendPoints = new[]
                {
                    TrendPoint(1), TrendPoint(2), TrendPoint(3)
                }
            });

            Assert.Equal(3, graph.Metrics.Count);
        }

        // ── 5. Metric node has DimensionScores keyed on all 7 VoiceDimensions ────

        [Fact]
        public void Build_MetricNode_DimensionScoresContainsAllSevenDimensions()
        {
            var graph = NewBuilder().Build(new VoiceKnowledgeGraphInput
            {
                UserId      = 1,
                GoalProfile = GoalProfile(),
                TrendPoints = new[] { TrendPoint(1, resonance: 72, comfort: 68) }
            });

            var metricNode = graph.Metrics.Values.Single();

            var expectedDimensions = Enum.GetValues<VoiceDimension>();
            foreach (var dim in expectedDimensions)
                Assert.True(metricNode.DimensionScores.ContainsKey(dim),
                    $"DimensionScores should contain {dim}");

            Assert.Equal(72.0, metricNode.DimensionScores[VoiceDimension.Resonance], 6);
            Assert.Equal(68.0, metricNode.DimensionScores[VoiceDimension.Comfort],   6);
        }

        // ── 6. Metric nodes linked from goal (EvidencedBy) ───────────────────────

        [Fact]
        public void Build_MetricNodes_LinkedFromGoalWithEvidencedByEdge()
        {
            var graph = NewBuilder().Build(new VoiceKnowledgeGraphInput
            {
                UserId      = 1,
                GoalProfile = GoalProfile(),
                TrendPoints = new[] { TrendPoint(1), TrendPoint(2) }
            });

            var goalId = NodeId.Of(NodeKind.Goal, 1);
            var goalNeighbors = graph.NeighborsOf(goalId);

            Assert.Equal(2, goalNeighbors.Count);
            Assert.All(goalNeighbors, e => Assert.Equal(EdgeKind.EvidencedBy, e.Kind));
            Assert.All(goalNeighbors, e => Assert.Equal(NodeKind.Metric, e.To.Kind));
        }

        // ── 7. Insight node created, linked from metric (Produces) ───────────────

        [Fact]
        public void Build_InsightForSession_LinkedFromMatchingMetricNode()
        {
            var graph = NewBuilder().Build(new VoiceKnowledgeGraphInput
            {
                UserId      = 1,
                GoalProfile = GoalProfile(),
                TrendPoints = new[] { TrendPoint(1), TrendPoint(2), TrendPoint(3) },
                Insights    = new[] { Insight(2) }
            });

            Assert.Single(graph.Insights);

            var metricId   = NodeId.Of(NodeKind.Metric, 2);
            var metricEdges = graph.NeighborsOf(metricId);
            var producesEdge = metricEdges.SingleOrDefault(e => e.Kind == EdgeKind.Produces);

            Assert.NotNull(producesEdge);
            Assert.Equal(NodeKind.Insight, producesEdge!.To.Kind);
        }

        // ── 8. Recommendation node: correct Focus + ReasonCode ───────────────────

        [Fact]
        public void Build_InsightWithFocusResonance_RecommendationHasCorrectFocusAndReasonCode()
        {
            var graph = NewBuilder().Build(new VoiceKnowledgeGraphInput
            {
                UserId      = 1,
                GoalProfile = GoalProfile(),
                TrendPoints = new[] { TrendPoint(1), TrendPoint(2), TrendPoint(3) },
                Insights    = new[] { Insight(1, VoiceDimension.Resonance) }
            });

            Assert.Single(graph.Recommendations);
            var rec = graph.Recommendations.Values.Single();

            Assert.Equal(VoiceDimension.Resonance, rec.Focus);
            Assert.Equal("FOCUS_RESONANCE", rec.ReasonCode);
            Assert.InRange(rec.Confidence, 0.0, 100.0);
        }

        // ── 9. Recommendation linked from insight (Drives) ───────────────────────

        [Fact]
        public void Build_Recommendation_LinkedFromInsightWithDrivesEdge()
        {
            var graph = NewBuilder().Build(new VoiceKnowledgeGraphInput
            {
                UserId      = 1,
                GoalProfile = GoalProfile(),
                TrendPoints = new[] { TrendPoint(1), TrendPoint(2), TrendPoint(3) },
                Insights    = new[] { Insight(1) }
            });

            var insightId = graph.Insights.Keys.Single();
            var edges     = graph.NeighborsOf(insightId);
            var drivesEdge = edges.SingleOrDefault(e => e.Kind == EdgeKind.Drives);

            Assert.NotNull(drivesEdge);
            Assert.Equal(NodeKind.Recommendation, drivesEdge!.To.Kind);
        }

        // ── 10. Exercise nodes created and linked (Targets) ──────────────────────

        [Fact]
        public void Build_InsightWithSuggestedExercises_ExerciseNodesCreated()
        {
            var graph = NewBuilder().Build(new VoiceKnowledgeGraphInput
            {
                UserId           = 1,
                GoalProfile      = GoalProfile(),
                TrendPoints      = new[] { TrendPoint(1), TrendPoint(2), TrendPoint(3) },
                Insights         = new[] { Insight(1, exercises: new[] { 5, 7 }) },
                ExerciseProfiles = new[] { ExerciseProfile(5), ExerciseProfile(7) }
            });

            Assert.Equal(2, graph.Exercises.Count);

            var exIds = graph.Exercises.Values.Select(e => e.ExerciseId).OrderBy(x => x).ToList();
            Assert.Equal(new[] { 5, 7 }, exIds);

            // Both exercises should have effectiveness profiles attached.
            Assert.All(graph.Exercises.Values, ex => Assert.NotNull(ex.EffectivenessProfile));
        }

        // ── 11. Exercise linked from recommendation (Targets) ────────────────────

        [Fact]
        public void Build_ExerciseNode_LinkedFromRecommendationWithTargetsEdge()
        {
            var graph = NewBuilder().Build(new VoiceKnowledgeGraphInput
            {
                UserId      = 1,
                GoalProfile = GoalProfile(),
                TrendPoints = new[] { TrendPoint(1), TrendPoint(2), TrendPoint(3) },
                Insights    = new[] { Insight(1, exercises: new[] { 3 }) }
            });

            var recId     = graph.Recommendations.Keys.Single();
            var recEdges  = graph.NeighborsOf(recId);
            var targetsEdge = recEdges.SingleOrDefault(e => e.Kind == EdgeKind.Targets);

            Assert.NotNull(targetsEdge);
            Assert.Equal(NodeKind.Exercise, targetsEdge!.To.Kind);
            Assert.Equal(3, targetsEdge.To.Key);
        }

        // ── 12. Outcome nodes created and linked (Yields) ────────────────────────

        [Fact]
        public void Build_ExercisesWithTrendPoints_OutcomeNodesLinkedWithYieldsEdge()
        {
            var graph = NewBuilder().Build(new VoiceKnowledgeGraphInput
            {
                UserId      = 1,
                GoalProfile = GoalProfile(),
                TrendPoints = new[] { TrendPoint(1), TrendPoint(2), TrendPoint(3) },
                Insights    = new[] { Insight(1, exercises: new[] { 4 }) }
            });

            Assert.Single(graph.Outcomes);
            var exId     = NodeId.Of(NodeKind.Exercise, 4);
            var exEdges  = graph.NeighborsOf(exId);
            var yieldsEdge = exEdges.SingleOrDefault(e => e.Kind == EdgeKind.Yields);

            Assert.NotNull(yieldsEdge);
            Assert.Equal(NodeKind.Outcome, yieldsEdge!.To.Kind);
        }

        // ── 13. OutcomeNode.IsPositive with rising trend ──────────────────────────

        [Fact]
        public void Build_OutcomeNode_IsPositiveWhenResonanceTrendRises()
        {
            // Resonance: 50, 60, 70 → slope = +10 → IsPositive = true.
            var graph = NewBuilder().Build(new VoiceKnowledgeGraphInput
            {
                UserId      = 1,
                GoalProfile = GoalProfile(),
                TrendPoints = new[]
                {
                    TrendPoint(1, resonance: 50),
                    TrendPoint(2, resonance: 60),
                    TrendPoint(3, resonance: 70)
                },
                Insights = new[]
                {
                    Insight(1, VoiceDimension.Resonance, exercises: new[] { 2 })
                }
            });

            var outcome = graph.Outcomes.Values.Single();
            Assert.True(outcome.IsPositive);
            Assert.True(outcome.ObservedSlope > 0);
            Assert.Equal(3, outcome.SessionCount);
        }

        // ── 14. OutcomeNode.IsPositive = false with declining trend ───────────────

        [Fact]
        public void Build_OutcomeNode_IsNotPositiveWhenResonanceDeclining()
        {
            // Resonance: 70, 60, 50 → slope = -10 → IsPositive = false.
            var graph = NewBuilder().Build(new VoiceKnowledgeGraphInput
            {
                UserId      = 1,
                GoalProfile = GoalProfile(),
                TrendPoints = new[]
                {
                    TrendPoint(1, resonance: 70),
                    TrendPoint(2, resonance: 60),
                    TrendPoint(3, resonance: 50)
                },
                Insights = new[]
                {
                    Insight(1, VoiceDimension.Resonance, exercises: new[] { 2 })
                }
            });

            var outcome = graph.Outcomes.Values.Single();
            Assert.False(outcome.IsPositive);
            Assert.True(outcome.ObservedSlope < 0);
        }

        // ── 15. NeighborsOf returns empty list for unknown node ───────────────────

        [Fact]
        public void NeighborsOf_UnknownNode_ReturnsEmptyList()
        {
            var graph     = NewBuilder().Build(new VoiceKnowledgeGraphInput { UserId = 1 });
            var neighbors = graph.NeighborsOf(NodeId.Of(NodeKind.Outcome, 99999));

            Assert.Empty(neighbors);
        }

        // ── 16. NodesOfKind returns correct node count per layer ─────────────────

        [Fact]
        public void NodesOfKind_ReturnsCorrectCountForEachLayer()
        {
            var graph = NewBuilder().Build(new VoiceKnowledgeGraphInput
            {
                UserId      = 1,
                GoalProfile = GoalProfile(),
                TrendPoints = new[] { TrendPoint(1), TrendPoint(2), TrendPoint(3) },
                Insights    = new[]
                {
                    Insight(1, exercises: new[] { 5 }),
                    Insight(2, exercises: new[] { 6 })
                },
                ExerciseProfiles = new[] { ExerciseProfile(5), ExerciseProfile(6) }
            });

            Assert.Single(graph.NodesOfKind(NodeKind.User));
            Assert.Single(graph.NodesOfKind(NodeKind.Goal));
            Assert.Equal(3, graph.NodesOfKind(NodeKind.Metric).Count);
            Assert.Equal(2, graph.NodesOfKind(NodeKind.Insight).Count);
            Assert.Equal(2, graph.NodesOfKind(NodeKind.Recommendation).Count);
            Assert.Equal(2, graph.NodesOfKind(NodeKind.Exercise).Count);
            Assert.Equal(2, graph.NodesOfKind(NodeKind.Outcome).Count);
        }

        // ── 17. FindPath: User → Outcome path is traversable ────────────────────

        [Fact]
        public void FindPath_UserToOutcome_ReturnsAtLeastOnePath()
        {
            var graph = NewBuilder().Build(new VoiceKnowledgeGraphInput
            {
                UserId      = 1,
                GoalProfile = GoalProfile(),
                TrendPoints = new[]
                {
                    TrendPoint(1), TrendPoint(2), TrendPoint(3)
                },
                Insights = new[]
                {
                    Insight(1, VoiceDimension.Resonance, exercises: new[] { 8 })
                }
            });

            var paths = graph.FindPath(NodeKind.User, NodeKind.Outcome);

            Assert.NotEmpty(paths);

            // Every path must start at a User node and end at an Outcome node.
            foreach (var path in paths)
            {
                Assert.Equal(NodeKind.User,    path.First().Kind);
                Assert.Equal(NodeKind.Outcome, path.Last().Kind);
            }
        }

        // ── 18. FindPath: User → Goal (single hop) ───────────────────────────────

        [Fact]
        public void FindPath_UserToGoal_ReturnsSingleHopPath()
        {
            var graph = NewBuilder().Build(new VoiceKnowledgeGraphInput
            {
                UserId      = 1,
                GoalProfile = GoalProfile()
            });

            var paths = graph.FindPath(NodeKind.User, NodeKind.Goal);

            Assert.Single(paths);
            Assert.Equal(2, paths[0].Count); // [User, Goal]
            Assert.Equal(NodeKind.User, paths[0][0].Kind);
            Assert.Equal(NodeKind.Goal, paths[0][1].Kind);
        }

        // ── 19. FindPath: no path when goal missing ──────────────────────────────

        [Fact]
        public void FindPath_UserToGoal_EmptyWhenNoGoalConfigured()
        {
            var graph = NewBuilder().Build(new VoiceKnowledgeGraphInput { UserId = 1 });
            var paths = graph.FindPath(NodeKind.User, NodeKind.Goal);

            Assert.Empty(paths);
        }

        // ── 20. NodeCount and EdgeCount reflect full chain ────────────────────────

        [Fact]
        public void Build_FullChain_NodeCountAndEdgeCountAreConsistent()
        {
            var graph = NewBuilder().Build(new VoiceKnowledgeGraphInput
            {
                UserId      = 1,
                GoalProfile = GoalProfile(),
                TrendPoints = new[]
                {
                    TrendPoint(1), TrendPoint(2), TrendPoint(3)
                },
                Insights = new[]
                {
                    Insight(1, VoiceDimension.Resonance, exercises: new[] { 5 })
                },
                ExerciseProfiles = new[] { ExerciseProfile(5) }
            });

            // 1 User + 1 Goal + 3 Metrics + 1 Insight + 1 Rec + 1 Exercise + 1 Outcome = 9
            Assert.Equal(9, graph.NodeCount);

            // Edges:
            //   User → Goal (Pursues)                     = 1
            //   Goal → Metric×3 (EvidencedBy)             = 3
            //   Metric(1) → Insight (Produces)            = 1
            //   Insight → Rec (Drives)                    = 1
            //   Rec → Exercise (Targets)                  = 1
            //   Exercise → Outcome (Yields)               = 1
            // Total = 8
            Assert.Equal(8, graph.EdgeCount);
        }

        // ── 21. No-goal path: metric nodes linked directly from user ──────────────

        [Fact]
        public void Build_NoGoal_MetricNodesLinkedFromUserWithEvidencedByEdge()
        {
            var graph = NewBuilder().Build(new VoiceKnowledgeGraphInput
            {
                UserId      = 1,
                TrendPoints = new[] { TrendPoint(1), TrendPoint(2) }
            });

            Assert.Equal(2, graph.Metrics.Count);
            var userEdges = graph.NeighborsOf(NodeId.Of(NodeKind.User, 1));
            Assert.Equal(2, userEdges.Count);
            Assert.All(userEdges, e => Assert.Equal(EdgeKind.EvidencedBy, e.Kind));
        }

        // ── 22. Duplicate exercises across two insights — only one ExerciseNode ───

        [Fact]
        public void Build_TwoInsightsSameExercise_OnlyOneExerciseNode()
        {
            var graph = NewBuilder().Build(new VoiceKnowledgeGraphInput
            {
                UserId      = 1,
                GoalProfile = GoalProfile(),
                TrendPoints = new[]
                {
                    TrendPoint(1), TrendPoint(2), TrendPoint(3)
                },
                Insights = new[]
                {
                    Insight(1, exercises: new[] { 9 }),
                    Insight(2, exercises: new[] { 9 })
                }
            });

            // Exercise 9 should only appear once.
            Assert.Single(graph.Exercises);
            Assert.Single(graph.Outcomes);
        }

        // ── 23. OutcomeNode.SessionCount reflects trend point count ───────────────

        [Fact]
        public void Build_OutcomeNode_SessionCountMatchesTrendPointCount()
        {
            var trendPoints = new[]
            {
                TrendPoint(1, resonance: 50),
                TrendPoint(2, resonance: 55),
                TrendPoint(3, resonance: 60),
                TrendPoint(4, resonance: 65)
            };

            var graph = NewBuilder().Build(new VoiceKnowledgeGraphInput
            {
                UserId      = 1,
                GoalProfile = GoalProfile(),
                TrendPoints = trendPoints,
                Insights    = new[] { Insight(1, exercises: new[] { 11 }) }
            });

            var outcome = graph.Outcomes.Values.Single();
            Assert.Equal(4, outcome.SessionCount);
        }

        // ── 24. GoalNode carries correct GoalStyleKey from input ──────────────────

        [Fact]
        public void Build_GoalNode_CarriesGoalStyleKeyFromInput()
        {
            var profile = new VoiceGoalProfile
            {
                UserId       = 1,
                GoalStyleKey = "natural_bright"
            };

            var graph = NewBuilder().Build(new VoiceKnowledgeGraphInput
            {
                UserId      = 1,
                GoalProfile = profile
            });

            var goalNode = graph.Goals.Values.Single();
            Assert.Equal("natural_bright", goalNode.GoalProfile.GoalStyleKey);
        }

        // ── 25. NodeId ToString returns "Kind:Key" ────────────────────────────────

        [Fact]
        public void NodeId_ToString_ReturnsKindColonKey()
        {
            var id = NodeId.Of(NodeKind.Metric, 7);
            Assert.Equal("Metric:7", id.ToString());
        }
    }
}
