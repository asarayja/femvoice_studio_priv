using System;
using System.Collections.Generic;
using FemVoiceStudio.Services;

namespace FemVoiceStudio.Models
{
    // ─────────────────────────────────────────────────────────────────────────────
    // NodeId — lightweight typed identity for graph nodes.
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Stable identity for a graph node. Combines <see cref="Kind"/> with a
    /// domain-level integer key so node equality is deterministic and testable
    /// without reference equality.
    /// </summary>
    public readonly record struct NodeId
    {
        /// <summary>Node kind / layer in the graph.</summary>
        public NodeKind Kind { get; init; }

        /// <summary>
        /// Domain key: UserId, GoalStyleKey hash, SessionId, ExerciseId, etc.
        /// Convention: for nodes that have no natural integer key (e.g. a single
        /// composite insight per user) use the UserId as the key.
        /// </summary>
        public int Key { get; init; }

        /// <summary>Canonical factory.</summary>
        public static NodeId Of(NodeKind kind, int key) => new() { Kind = kind, Key = key };

        /// <inheritdoc/>
        public override string ToString() => $"{Kind}:{Key}";
    }

    /// <summary>
    /// The seven conceptual layers of the voice-intelligence knowledge graph, in the
    /// same traversal order as the chain:
    /// User → Goal → Metric → Insight → Recommendation → Exercise → Outcome.
    /// </summary>
    public enum NodeKind
    {
        User           = 0,
        Goal           = 1,
        Metric         = 2,
        Insight        = 3,
        Recommendation = 4,
        Exercise       = 5,
        Outcome        = 6
    }

    /// <summary>Directed-edge label describing the semantic relationship.</summary>
    public enum EdgeKind
    {
        /// <summary>User pursues a Goal.</summary>
        Pursues,

        /// <summary>Goal is evidenced by a Metric trend point.</summary>
        EvidencedBy,

        /// <summary>Metric produces an Insight.</summary>
        Produces,

        /// <summary>Insight drives a Recommendation.</summary>
        Drives,

        /// <summary>Recommendation targets an Exercise.</summary>
        Targets,

        /// <summary>Exercise yields an Outcome.</summary>
        Yields
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Typed edge.
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A directed, typed edge in the voice-intelligence knowledge graph.
    /// Immutable value: two edges with the same (From, To, Kind) are equal.
    /// </summary>
    public sealed record Edge
    {
        /// <summary>Source node identity.</summary>
        public NodeId From { get; init; }

        /// <summary>Destination node identity.</summary>
        public NodeId To { get; init; }

        /// <summary>Semantic relationship.</summary>
        public EdgeKind Kind { get; init; }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Sealed record nodes — each COMPOSES existing domain types, never redefines.
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Graph node that anchors a user identity.</summary>
    public sealed record UserNode
    {
        /// <inheritdoc cref="NodeId"/>
        public NodeId Id { get; init; }

        /// <summary>The user this node represents.</summary>
        public int UserId { get; init; }
    }

    /// <summary>
    /// Graph node that carries the user's current voice goal profile. Composes
    /// <see cref="VoiceGoalProfile"/> without redefining it.
    /// </summary>
    public sealed record GoalNode
    {
        /// <inheritdoc cref="NodeId"/>
        public NodeId Id { get; init; }

        /// <summary>The goal profile this node wraps. Never null.</summary>
        public VoiceGoalProfile GoalProfile { get; init; } = new();
    }

    /// <summary>
    /// Graph node that carries one Voice Intelligence trend point — a single
    /// chronological snapshot of all seven 0–100 dimension scores. Composes
    /// <see cref="VoiceIntelligenceTrendPoint"/> without redefining it.
    /// </summary>
    public sealed record MetricNode
    {
        /// <inheritdoc cref="NodeId"/>
        public NodeId Id { get; init; }

        /// <summary>The trend-point snapshot this node wraps.</summary>
        public VoiceIntelligenceTrendPoint TrendPoint { get; init; } =
            new VoiceIntelligenceTrendPoint();

        /// <summary>
        /// Per-dimension 0–100 scores, keyed by the canonical <see cref="VoiceDimension"/>
        /// enum. Computed once at node-creation time from <see cref="TrendPoint"/>.
        /// </summary>
        public IReadOnlyDictionary<VoiceDimension, double> DimensionScores { get; init; } =
            new Dictionary<VoiceDimension, double>();
    }

    /// <summary>
    /// Graph node that carries one end-of-session insight. Composes
    /// <see cref="SessionInsight"/> without redefining it.
    /// </summary>
    public sealed record InsightNode
    {
        /// <inheritdoc cref="NodeId"/>
        public NodeId Id { get; init; }

        /// <summary>The session insight this node wraps.</summary>
        public SessionInsight Insight { get; init; } = default!;
    }

    /// <summary>
    /// Graph node representing a coaching recommendation derived from one or more
    /// insights. The recommendation itself is expressed as a focus dimension and a
    /// confidence score so consuming layers (SmartCoach, dashboard) can localise
    /// their own copy without any prose being embedded here.
    /// </summary>
    public sealed record RecommendationNode
    {
        /// <inheritdoc cref="NodeId"/>
        public NodeId Id { get; init; }

        /// <summary>The dimension this recommendation focuses on.</summary>
        public VoiceDimension Focus { get; init; }

        /// <summary>Confidence in the recommendation, 0–100.</summary>
        public double Confidence { get; init; }

        /// <summary>
        /// Stable machine code identifying the recommendation source
        /// (e.g. "FOCUS_RESONANCE", "FOCUS_COMFORT"). Never user-facing prose.
        /// </summary>
        public string ReasonCode { get; init; } = string.Empty;
    }

    /// <summary>
    /// Graph node that carries one exercise effectiveness profile. Composes
    /// <see cref="ExerciseEffectivenessProfile"/> without redefining it.
    /// </summary>
    public sealed record ExerciseNode
    {
        /// <inheritdoc cref="NodeId"/>
        public NodeId Id { get; init; }

        /// <summary>The exercise catalog id (1–15).</summary>
        public int ExerciseId { get; init; }

        /// <summary>Effectiveness profile for this exercise. May be null when
        /// there is not enough session history.</summary>
        public ExerciseEffectivenessProfile? EffectivenessProfile { get; init; }
    }

    /// <summary>
    /// Graph node that records the observed outcome of recommending and practising a
    /// specific exercise focus. Designed to be populated after a training cycle
    /// completes; empty until then.
    /// </summary>
    public sealed record OutcomeNode
    {
        /// <inheritdoc cref="NodeId"/>
        public NodeId Id { get; init; }

        /// <summary>The exercise this outcome traces back to.</summary>
        public int ExerciseId { get; init; }

        /// <summary>The dimension that was targeted.</summary>
        public VoiceDimension TargetDimension { get; init; }

        /// <summary>
        /// OLS slope of the targeted dimension's score series during the outcome
        /// window (points per session). Positive = improvement. 0 when insufficient data.
        /// </summary>
        public double ObservedSlope { get; init; }

        /// <summary>Success gate: true when the slope is positive and based on
        /// at least three data points.</summary>
        public bool IsPositive { get; init; }

        /// <summary>Number of sessions contributing to <see cref="ObservedSlope"/>.</summary>
        public int SessionCount { get; init; }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // VoiceKnowledgeGraph — in-memory, traversable, query-able.
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Typed, in-memory knowledge graph for voice-intelligence reasoning.
    /// Nodes are strongly typed (sealed records) keyed by <see cref="NodeId"/>;
    /// directed edges are typed <see cref="Edge"/> records.
    ///
    /// <para>No persistence. Built fresh by <see cref="VoiceKnowledgeGraphBuilder"/>
    /// from pre-fetched domain data.</para>
    ///
    /// <para>Safety contract: this graph is DESCRIPTIVE/EXPLANATORY intelligence only.
    /// It must never override safety/health/recovery gates — those live upstream.</para>
    /// </summary>
    public sealed class VoiceKnowledgeGraph
    {
        // Typed node registries.
        private readonly Dictionary<NodeId, UserNode>           _users           = new();
        private readonly Dictionary<NodeId, GoalNode>           _goals           = new();
        private readonly Dictionary<NodeId, MetricNode>         _metrics         = new();
        private readonly Dictionary<NodeId, InsightNode>        _insights        = new();
        private readonly Dictionary<NodeId, RecommendationNode> _recommendations = new();
        private readonly Dictionary<NodeId, ExerciseNode>       _exercises       = new();
        private readonly Dictionary<NodeId, OutcomeNode>        _outcomes        = new();

        // Adjacency lists: forward (From → To) and reverse (To → From).
        private readonly Dictionary<NodeId, List<Edge>> _forward = new();
        private readonly Dictionary<NodeId, List<Edge>> _reverse = new();

        // ── Node registration ────────────────────────────────────────────────────

        internal void AddNode(UserNode node)           { _users[node.Id]           = node; }
        internal void AddNode(GoalNode node)           { _goals[node.Id]           = node; }
        internal void AddNode(MetricNode node)         { _metrics[node.Id]         = node; }
        internal void AddNode(InsightNode node)        { _insights[node.Id]        = node; }
        internal void AddNode(RecommendationNode node) { _recommendations[node.Id] = node; }
        internal void AddNode(ExerciseNode node)       { _exercises[node.Id]       = node; }
        internal void AddNode(OutcomeNode node)        { _outcomes[node.Id]        = node; }

        // ── Edge registration ────────────────────────────────────────────────────

        internal void AddEdge(Edge edge)
        {
            if (!_forward.TryGetValue(edge.From, out var fwd))
                _forward[edge.From] = fwd = new List<Edge>();
            fwd.Add(edge);

            if (!_reverse.TryGetValue(edge.To, out var rev))
                _reverse[edge.To] = rev = new List<Edge>();
            rev.Add(edge);
        }

        // ── Typed node accessors ─────────────────────────────────────────────────

        /// <summary>Returns all user nodes in the graph.</summary>
        public IReadOnlyDictionary<NodeId, UserNode> Users => _users;

        /// <summary>Returns all goal nodes in the graph.</summary>
        public IReadOnlyDictionary<NodeId, GoalNode> Goals => _goals;

        /// <summary>Returns all metric (trend point) nodes in the graph.</summary>
        public IReadOnlyDictionary<NodeId, MetricNode> Metrics => _metrics;

        /// <summary>Returns all insight nodes in the graph.</summary>
        public IReadOnlyDictionary<NodeId, InsightNode> Insights => _insights;

        /// <summary>Returns all recommendation nodes in the graph.</summary>
        public IReadOnlyDictionary<NodeId, RecommendationNode> Recommendations => _recommendations;

        /// <summary>Returns all exercise nodes in the graph.</summary>
        public IReadOnlyDictionary<NodeId, ExerciseNode> Exercises => _exercises;

        /// <summary>Returns all outcome nodes in the graph.</summary>
        public IReadOnlyDictionary<NodeId, OutcomeNode> Outcomes => _outcomes;

        // ── Traversal helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Returns all edges leaving <paramref name="nodeId"/> (forward neighbours).
        /// Returns an empty list when the node has no outgoing edges.
        /// </summary>
        public IReadOnlyList<Edge> NeighborsOf(NodeId nodeId)
        {
            return _forward.TryGetValue(nodeId, out var edges)
                ? edges
                : Array.Empty<Edge>();
        }

        /// <summary>
        /// Returns all nodes of the given <paramref name="kind"/>, typed as
        /// <see cref="NodeId"/> references, regardless of their domain content.
        /// </summary>
        public IReadOnlyList<NodeId> NodesOfKind(NodeKind kind) => kind switch
        {
            NodeKind.User           => CollectIds(_users),
            NodeKind.Goal           => CollectIds(_goals),
            NodeKind.Metric         => CollectIds(_metrics),
            NodeKind.Insight        => CollectIds(_insights),
            NodeKind.Recommendation => CollectIds(_recommendations),
            NodeKind.Exercise       => CollectIds(_exercises),
            NodeKind.Outcome        => CollectIds(_outcomes),
            _                       => Array.Empty<NodeId>()
        };

        private static IReadOnlyList<NodeId> CollectIds<T>(Dictionary<NodeId, T> dict)
        {
            var ids = new NodeId[dict.Count];
            var i   = 0;
            foreach (var k in dict.Keys) ids[i++] = k;
            return ids;
        }

        /// <summary>
        /// BFS from any node whose <see cref="NodeId.Kind"/> equals
        /// <paramref name="fromKind"/>, following forward edges, stopping when a node
        /// of <paramref name="toKind"/> is reached. Returns all reachable paths
        /// expressed as ordered lists of <see cref="NodeId"/> (source-inclusive,
        /// destination-inclusive). Returns an empty list when no path exists.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<NodeId>> FindPath(NodeKind fromKind, NodeKind toKind)
        {
            var results = new List<IReadOnlyList<NodeId>>();
            var sources = NodesOfKind(fromKind);

            foreach (var source in sources)
            {
                // BFS: queue items are (currentNode, pathSoFar).
                var queue   = new Queue<(NodeId Current, List<NodeId> Path)>();
                var visited = new HashSet<NodeId>();
                queue.Enqueue((source, new List<NodeId> { source }));

                while (queue.Count > 0)
                {
                    var (current, path) = queue.Dequeue();
                    if (visited.Contains(current)) continue;
                    visited.Add(current);

                    if (current.Kind == toKind && current != source)
                    {
                        results.Add(path.AsReadOnly());
                        continue; // don't expand further past the target layer
                    }

                    foreach (var edge in NeighborsOf(current))
                    {
                        if (!visited.Contains(edge.To))
                        {
                            var next = new List<NodeId>(path) { edge.To };
                            queue.Enqueue((edge.To, next));
                        }
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Total number of nodes across all layers.
        /// </summary>
        public int NodeCount =>
            _users.Count + _goals.Count + _metrics.Count + _insights.Count +
            _recommendations.Count + _exercises.Count + _outcomes.Count;

        /// <summary>
        /// Total number of directed edges in the graph.
        /// </summary>
        public int EdgeCount
        {
            get
            {
                var n = 0;
                foreach (var list in _forward.Values) n += list.Count;
                return n;
            }
        }
    }
}
