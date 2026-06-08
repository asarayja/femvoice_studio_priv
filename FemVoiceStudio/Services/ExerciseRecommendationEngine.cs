using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services.Progression;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// The single training axis the next exercise should consolidate. Mirrors the seven
    /// Voice Intelligence dimensions plus two non-dimension states the recommender can
    /// surface: <see cref="Recovery"/> (a Health-driven recovery-oriented pick) and
    /// <see cref="Foundation"/> (a sensible starter when there is no history to reason
    /// from). Never a raw Hz / pitch number — pitch is the lowest-weighted dimension and
    /// is only ever a focus when it is genuinely the weakest measured axis.
    /// </summary>
    public enum RecommendationFocus
    {
        /// <summary>Health-first: rest / recovery-oriented, lighter load. Wins over every goal.</summary>
        Recovery,

        /// <summary>Comfort / ease of production (Health-adjacent).</summary>
        Comfort,

        /// <summary>Resonance — the primary feminisation training dimension.</summary>
        Resonance,

        /// <summary>Frame-to-frame consistency / stability.</summary>
        Consistency,

        /// <summary>Natural intonation / pitch-variation patterns.</summary>
        Intonation,

        /// <summary>Spectral vocal weight / tilt.</summary>
        VocalWeight,

        /// <summary>Fundamental pitch. Lowest weight; only when genuinely weakest.</summary>
        Pitch,

        /// <summary>No measurable history yet — a gentle, broad foundation pick.</summary>
        Foundation
    }

    /// <summary>
    /// Pure, side-effect-free input snapshot for <see cref="ExerciseRecommendationEngine"/>.
    /// Every field is something a caller has already aggregated from the stores
    /// (Voice Intelligence trend, RecoveryScorer, MasteryEvaluator, recent sessions,
    /// goal/profile). The engine never reads a DB itself — it only reasons over numbers.
    ///
    /// An all-default instance represents a brand-new user with no history; the engine
    /// then returns a safe <see cref="RecommendationFocus.Foundation"/> starter.
    /// </summary>
    public readonly record struct ExerciseRecommendationInput
    {
        /// <summary>Goal style + primary focus (style colours which dimension we push).</summary>
        public VoiceGoalProfile? GoalProfile { get; init; }

        /// <summary>Style goal + training frequency. <see cref="VoiceStyleGoal"/> colours focus.</summary>
        public UserVoiceProfile? UserProfile { get; init; }

        /// <summary>
        /// Latest Voice Intelligence trend point (eight 0–100 scores). null ⇒ no measured
        /// history ⇒ Foundation starter. Dimensions left at the neutral 50.0 fallback are
        /// treated as UNMEASURED and never selected as a weakest-dimension focus.
        /// </summary>
        public VoiceIntelligenceTrendPoint? LatestVoiceScores { get; init; }

        /// <summary>
        /// Recovery state (RecoveryScorer output). When Status is Overtrained/Strained OR
        /// the score is below <see cref="ExerciseRecommendationEngine.LowRecoveryThreshold"/>,
        /// the recommendation is forced recovery-oriented/light — Health &gt; Goals, this can
        /// never be overridden by a style/goal preference.
        /// </summary>
        public RecoveryResult Recovery { get; init; }

        /// <summary>
        /// Exercise ids of the most recent sessions, MOST-RECENT-FIRST. Used purely for
        /// variation: the engine avoids re-recommending anything in the recent window so
        /// the loop stops repeating the same exercise. May be null/empty.
        /// </summary>
        public IReadOnlyList<int>? RecentExerciseIds { get; init; }

        /// <summary>
        /// Per-exercise mastery (MasteryEvaluator output) for candidates the caller knows
        /// about. A <see cref="MasteryLevel.Mastered"/> exercise is deprioritised in favour
        /// of an exercise that serves a weaker, under-covered focus area. May be null/empty.
        /// </summary>
        public IReadOnlyDictionary<int, MasteryLevel>? MasteryByExercise { get; init; }

        /// <summary>
        /// The user's current speech-complexity level. The engine reuses
        /// <see cref="ComplexityEngine.GetExerciseIdsForComplexity"/> to derive a
        /// level-appropriate candidate id pool, so it never recommends an exercise above
        /// the user's level. Defaults to <see cref="SpeechComplexityLevel.IsolatedSounds"/>.
        /// </summary>
        public SpeechComplexityLevel ComplexityLevel { get; init; }
    }

    /// <summary>
    /// An explainable next-exercise recommendation: a primary exercise id, ranked
    /// alternatives, the focus area it serves, a human-readable rationale, and whether it
    /// is recovery-oriented (lighter load).
    /// </summary>
    public sealed record ExerciseRecommendationResult
    {
        /// <summary>Primary recommended exercise id (always a valid candidate id).</summary>
        public int ExerciseId { get; init; }

        /// <summary>
        /// The full ranked tail of alternatives (best first), excluding
        /// <see cref="ExerciseId"/>. Best-first means deprioritised ids (recent / Mastered)
        /// sort toward the end, so a caller can take however many it needs and still honour
        /// the variation + mastery ordering. Empty only when the pool had a single candidate.
        /// </summary>
        public IReadOnlyList<int> AlternativeExerciseIds { get; init; } = Array.Empty<int>();

        /// <summary>The training axis this recommendation serves.</summary>
        public RecommendationFocus Focus { get; init; }

        /// <summary>Short, clinician-friendly axis label (e.g. "recovery", "resonance").</summary>
        public string FocusArea { get; init; } = string.Empty;

        /// <summary>Human-readable explanation of WHY this exercise was chosen.</summary>
        public string Rationale { get; init; } = string.Empty;

        /// <summary>True when the pick is recovery-oriented / lighter load (Health-first).</summary>
        public bool IsRecoveryOriented { get; init; }
    }

    /// <summary>
    /// Unified, explainable next-exercise recommender. Replaces the purely-random
    /// training loop (<c>MainViewModel.LoadNextExercise → GetRandomText</c>) and the
    /// SmartCoach <c>RecommendedExerciseId</c> that never surfaced, with one deterministic,
    /// pure function over already-aggregated data.
    ///
    /// ── CLINICAL ORDERING (ufravikelig) ─────────────────────────────────────────────
    /// Safety &gt; Health &gt; Recovery &gt; Comfort &gt; Goals &gt; Progression &gt; Coaching.
    /// The selection pipeline therefore runs strictly in this order:
    ///
    ///   1. RECOVERY GATE (Health &gt; Goals — can NEVER be overridden). When recovery is
    ///      low/Overtrained/Strained, the focus is forced to <see cref="RecommendationFocus.Recovery"/>
    ///      and the candidate pool is the LIGHTEST level (isolated sounds / humming).
    ///      No goal or style can pull a depleted voice toward a demanding exercise.
    ///
    ///   2. FOUNDATION (no measured history). With no Voice Intelligence point, return a
    ///      gentle broad starter from the user's (clamped) complexity pool.
    ///
    ///   3. FOCUS = weakest MEASURED dimension. We mirror SmartCoach's focus selection:
    ///      strictly-lower score wins; ties go to the clinically higher-priority axis
    ///      (Recovery/Comfort outrank Resonance outrank … outrank Pitch). Dimensions at
    ///      the neutral 50.0 fallback are UNMEASURED and skipped. If nothing is weak,
    ///      goal focus (then a default) seeds the axis.
    ///
    ///   4. STYLE COLOURING (Goals). The style goal nudges the focus only WITHIN clinical
    ///      bounds: a DarkFeminine / Androgynous goal is never pushed toward bright
    ///      resonance — its resonance "weakness" is down-weighted so weight/comfort lead.
    ///      Style colours focus; it can never override the recovery gate.
    ///
    ///   5. MASTERY DEPRIORITISATION (Progression). Already-Mastered exercises are ranked
    ///      last so an under-covered weak area is lifted instead of drilling a solved one.
    ///
    ///   6. VARIATION. Anything in the recent window is pushed down the ranking so the
    ///      loop stops repeating the same exercise back-to-back.
    ///
    /// Pure and total: any input (including all-default) yields a valid recommendation
    /// with a non-empty candidate id. No IO, no DateTime.Now, no DB.
    /// </summary>
    public sealed class ExerciseRecommendationEngine
    {
        /// <summary>At/below this recovery score the recommendation is forced light.</summary>
        public const double LowRecoveryThreshold = 50.0;

        /// <summary>Scores at/below this are "weak" enough to drive focus (matches SmartCoach).</summary>
        public const double DimensionWeakThreshold = 55.0;

        /// <summary>The neutral fallback an unmeasured dimension reports; never a focus.</summary>
        private const double NeutralFallback = 50.0;
        private const double NeutralEpsilon = 1e-6;

        /// <summary>How many recent ids count as "just done" for variation purposes.</summary>
        private const int RecentVariationWindow = 5;

        /// <summary>
        /// Recommends the next exercise(s). See the type-level summary for the full,
        /// ordered selection logic. Never throws on bad input; returns a Foundation
        /// starter when there is nothing to reason from.
        /// </summary>
        public ExerciseRecommendationResult RecommendNext(ExerciseRecommendationInput input)
        {
            var style = ResolveStyle(input);
            var recent = NormaliseRecent(input.RecentExerciseIds);
            var mastery = input.MasteryByExercise ?? EmptyMastery;

            // ── 1. RECOVERY GATE — Health > Goals, never overridden ──────────────────
            if (IsLowRecovery(input.Recovery))
            {
                return BuildRecommendation(
                    focus: RecommendationFocus.Recovery,
                    candidatePool: LightRecoveryPool(input.ComplexityLevel),
                    recent: recent,
                    mastery: mastery,
                    isRecoveryOriented: true,
                    rationale: BuildRecoveryRationale(input.Recovery));
            }

            // ── 2. FOUNDATION — no measured history ──────────────────────────────────
            var latest = input.LatestVoiceScores;
            if (latest is null || IsAllNeutral(latest))
            {
                return BuildRecommendation(
                    focus: RecommendationFocus.Foundation,
                    candidatePool: LevelPool(input.ComplexityLevel),
                    recent: recent,
                    mastery: mastery,
                    isRecoveryOriented: false,
                    rationale: BuildFoundationRationale(style));
            }

            // ── 3+4. FOCUS = weakest measured dimension, coloured by style goal ───────
            var focus = SelectFocus(latest, input.GoalProfile, style);
            var pool = LevelPool(input.ComplexityLevel);

            // ── 5+6. mastery deprioritisation + variation happen in BuildRecommendation ─
            return BuildRecommendation(
                focus: focus,
                candidatePool: pool,
                recent: recent,
                mastery: mastery,
                isRecoveryOriented: false,
                rationale: BuildFocusRationale(focus, latest, style));
        }

        // ── Candidate-pool helpers ──────────────────────────────────────────────────

        private static readonly IReadOnlyDictionary<int, MasteryLevel> EmptyMastery =
            new Dictionary<int, MasteryLevel>();

        /// <summary>
        /// Level-appropriate candidate ids. Reuses the exact bucketing
        /// <see cref="ComplexityEngine.GetExerciseIdsForComplexity"/> uses (kept in sync,
        /// duplicated here only so the engine stays pure and needs no DB-backed engine).
        /// </summary>
        private static IReadOnlyList<int> LevelPool(SpeechComplexityLevel level)
            => ExerciseIdsForComplexity(level);

        /// <summary>
        /// The LIGHTEST pool, used when recovery is low: never harder than the user's own
        /// level, and never above words-level — isolated sounds / syllables are the
        /// gentlest load and are the only thing offered to a depleted voice.
        /// </summary>
        private static IReadOnlyList<int> LightRecoveryPool(SpeechComplexityLevel level)
        {
            // Clamp the recovery pool to at most the user's level, and never above the
            // lightest bucket (isolated sounds / syllables, ids 1..15). A recovering voice
            // is never sent to phrases/sentences even if the user normally trains there.
            var capped = level <= SpeechComplexityLevel.Syllables
                ? level
                : SpeechComplexityLevel.IsolatedSounds;
            return ExerciseIdsForComplexity(capped);
        }

        /// <summary>
        /// Pure mirror of <see cref="ComplexityEngine.GetExerciseIdsForComplexity"/>
        /// (includePreview:false). Same id buckets: sounds/syllables 1–15, words/phrases
        /// 16–35, sentences/spontaneous/conversational 36–50.
        /// </summary>
        private static IReadOnlyList<int> ExerciseIdsForComplexity(SpeechComplexityLevel level)
        {
            var ids = new List<int>();
            switch (level)
            {
                case SpeechComplexityLevel.IsolatedSounds:
                case SpeechComplexityLevel.Syllables:
                    for (var i = 1; i <= 15; i++) ids.Add(i);
                    break;
                case SpeechComplexityLevel.Words:
                case SpeechComplexityLevel.Phrases:
                    for (var i = 16; i <= 35; i++) ids.Add(i);
                    break;
                case SpeechComplexityLevel.StructuredSentences:
                case SpeechComplexityLevel.SpontaneousSpeech:
                case SpeechComplexityLevel.Conversational:
                    for (var i = 36; i <= 50; i++) ids.Add(i);
                    break;
                default:
                    for (var i = 1; i <= 15; i++) ids.Add(i);
                    break;
            }
            return ids;
        }

        // ── Recovery gate ───────────────────────────────────────────────────────────

        private static bool IsLowRecovery(RecoveryResult recovery)
            => recovery.Status == RecoveryStatus.Overtrained
               || recovery.Status == RecoveryStatus.Strained
               || recovery.Score <= LowRecoveryThreshold;

        // ── Focus selection (mirrors SmartCoachEngine.SelectVoiceIntelligenceFocus) ──

        /// <summary>
        /// Picks the focus axis: the weakest MEASURED dimension (strictly-lower wins; ties
        /// resolve to the clinically higher-priority axis). Recovery/Comfort outrank
        /// Resonance outranks Consistency/Intonation/VocalWeight outranks Pitch — so a
        /// merely-tied Pitch can never steal focus from a real Health/Resonance weakness.
        /// Style colouring (<see cref="ApplyStyleColouring"/>) is applied to the scores
        /// BEFORE ranking. If no dimension is weak, the goal's PrimaryFocus (then Resonance)
        /// seeds the axis.
        /// </summary>
        private static RecommendationFocus SelectFocus(
            VoiceIntelligenceTrendPoint? p,
            VoiceGoalProfile? goal,
            VoiceStyleGoal style)
        {
            if (p is null) return GoalSeededFocus(goal);

            // (axis, score, priority) — lower priority value = clinically more important.
            var candidates = new (RecommendationFocus Focus, double Score, int Priority)[]
            {
                (RecommendationFocus.Comfort,     p.ComfortScore100,     1),
                (RecommendationFocus.Resonance,   p.ResonanceScore100,   2),
                (RecommendationFocus.Consistency, p.ConsistencyScore100, 3),
                (RecommendationFocus.Intonation,  p.IntonationScore100,  4),
                (RecommendationFocus.VocalWeight, p.VocalWeightScore100, 4),
                (RecommendationFocus.Pitch,       p.PitchScore100,       5),
            };

            // Style colours the scores: a dark/androgynous goal must NOT be pushed toward
            // bright resonance, so its resonance "weakness" is down-weighted.
            candidates = candidates
                .Select(c => (c.Focus, Score: ApplyStyleColouring(c.Focus, c.Score, style), c.Priority))
                .ToArray();

            RecommendationFocus? best = null;
            var bestScore = double.MaxValue;
            var bestPriority = int.MaxValue;

            foreach (var c in candidates)
            {
                // Only genuinely-weak dimensions drive focus.
                if (c.Score >= DimensionWeakThreshold)
                    continue;

                // Skip UNMEASURED dimensions sitting on the neutral 50.0 fallback — an
                // unmeasured Pitch must never capture focus from real measured data.
                if (Math.Abs(c.Score - NeutralFallback) < NeutralEpsilon)
                    continue;

                if (c.Score < bestScore ||
                    (c.Score == bestScore && c.Priority < bestPriority))
                {
                    best = c.Focus;
                    bestScore = c.Score;
                    bestPriority = c.Priority;
                }
            }

            // Nothing weak ⇒ fall back to the goal's stated focus (then a sane default).
            return best ?? GoalSeededFocus(goal);
        }

        /// <summary>
        /// Style colouring (Goals layer): adjusts a dimension's effective score so the
        /// style goal can never push the voice toward an off-target dimension. For a
        /// DarkFeminine / Androgynous goal, "low resonance" is intentional, not a fault —
        /// so we lift its effective resonance score above the weak threshold so the engine
        /// does NOT chase bright resonance, letting VocalWeight/Comfort lead instead.
        /// Feminine/Situational/Custom goals leave scores untouched.
        /// </summary>
        private static double ApplyStyleColouring(
            RecommendationFocus focus, double score, VoiceStyleGoal style)
        {
            if (focus == RecommendationFocus.Resonance &&
                (style == VoiceStyleGoal.DarkFeminine || style == VoiceStyleGoal.Androgynous))
            {
                // Push resonance out of "weak" range so it is never chased for this goal.
                return Math.Max(score, DimensionWeakThreshold);
            }
            return score;
        }

        /// <summary>
        /// Maps a goal's free-text PrimaryFocus to a focus axis; defaults to Resonance
        /// (the primary feminisation dimension) when unknown/empty.
        /// </summary>
        private static RecommendationFocus GoalSeededFocus(VoiceGoalProfile? goal)
        {
            var focusText = goal?.PrimaryFocus;
            if (string.IsNullOrWhiteSpace(focusText))
                return RecommendationFocus.Resonance;

            var f = focusText.Trim().ToLowerInvariant();
            if (f.Contains("recover") || f.Contains("rest")) return RecommendationFocus.Recovery;
            if (f.Contains("comfort") || f.Contains("ease")) return RecommendationFocus.Comfort;
            if (f.Contains("reson")) return RecommendationFocus.Resonance;
            if (f.Contains("consist") || f.Contains("stab")) return RecommendationFocus.Consistency;
            if (f.Contains("inton") || f.Contains("melod") || f.Contains("pros")) return RecommendationFocus.Intonation;
            if (f.Contains("weight") || f.Contains("dark") || f.Contains("tilt")) return RecommendationFocus.VocalWeight;
            if (f.Contains("pitch")) return RecommendationFocus.Pitch;
            return RecommendationFocus.Resonance;
        }

        // ── Ranking: focus → candidate pool, with mastery + variation ───────────────

        /// <summary>
        /// Builds the final recommendation: ranks the candidate pool, then constructs the
        /// explainable record. Ranking applies (in order): mastery deprioritisation
        /// (Mastered last), variation (recent ids last), and a stable tie-break (id order)
        /// so the result is deterministic.
        /// </summary>
        private ExerciseRecommendationResult BuildRecommendation(
            RecommendationFocus focus,
            IReadOnlyList<int> candidatePool,
            IReadOnlyList<int> recent,
            IReadOnlyDictionary<int, MasteryLevel> mastery,
            bool isRecoveryOriented,
            string rationale)
        {
            var ranked = RankCandidates(candidatePool, recent, mastery);
            var primary = ranked.Count > 0 ? ranked[0] : FallbackId(candidatePool);
            // Full ranked tail (best-first) — callers take however many they need.
            var alternatives = ranked.Skip(1).ToArray();

            return new ExerciseRecommendationResult
            {
                ExerciseId = primary,
                AlternativeExerciseIds = alternatives,
                Focus = focus,
                FocusArea = FocusLabel(focus),
                Rationale = rationale,
                IsRecoveryOriented = isRecoveryOriented
            };
        }

        /// <summary>
        /// Stable ranking of a candidate pool. Lower sort key = better:
        ///   • +2 if the id is in the recent variation window (avoid repeats).
        ///   • +4 if the id is already Mastered (lift weaker, under-covered areas).
        /// Ties break by ascending id for determinism. Always returns a non-empty list
        /// when the pool is non-empty.
        /// </summary>
        private static List<int> RankCandidates(
            IReadOnlyList<int> pool,
            IReadOnlyList<int> recent,
            IReadOnlyDictionary<int, MasteryLevel> mastery)
        {
            if (pool.Count == 0) return new List<int>();

            var recentSet = recent.Take(RecentVariationWindow).ToHashSet();

            return pool
                .Distinct()
                .Select(id =>
                {
                    var penalty = 0;
                    if (recentSet.Contains(id)) penalty += 2;
                    if (mastery.TryGetValue(id, out var level) && level == MasteryLevel.Mastered)
                        penalty += 4;
                    return (Id: id, Penalty: penalty);
                })
                .OrderBy(x => x.Penalty)
                .ThenBy(x => x.Id)
                .Select(x => x.Id)
                .ToList();
        }

        private static int FallbackId(IReadOnlyList<int> pool)
            => pool.Count > 0 ? pool[0] : 1;

        // ── Normalisation ───────────────────────────────────────────────────────────

        private static IReadOnlyList<int> NormaliseRecent(IReadOnlyList<int>? recent)
            => recent ?? Array.Empty<int>();

        private static VoiceStyleGoal ResolveStyle(ExerciseRecommendationInput input)
        {
            // Prefer the explicit typed style; fall back to mapping the goal's style key.
            if (input.UserProfile is not null)
                return input.UserProfile.PreferredVoiceStyle;
            return UserVoiceProfile.FromGoalStyleKey(input.GoalProfile?.GoalStyleKey);
        }

        /// <summary>True when every measured dimension sits on the neutral 50.0 fallback.</summary>
        private static bool IsAllNeutral(VoiceIntelligenceTrendPoint? p)
        {
            if (p is null) return true;
            return IsNeutral(p.ComfortScore100)
                && IsNeutral(p.ResonanceScore100)
                && IsNeutral(p.ConsistencyScore100)
                && IsNeutral(p.IntonationScore100)
                && IsNeutral(p.VocalWeightScore100)
                && IsNeutral(p.PitchScore100);
        }

        private static bool IsNeutral(double score)
            => Math.Abs(score - NeutralFallback) < NeutralEpsilon;

        // ── Explainability ──────────────────────────────────────────────────────────

        /// <summary>Short, clinician-friendly axis label. No raw Hz; pitch is a label only.</summary>
        public static string FocusLabel(RecommendationFocus focus) => focus switch
        {
            RecommendationFocus.Recovery => "recovery",
            RecommendationFocus.Comfort => "comfort",
            RecommendationFocus.Resonance => "resonance",
            RecommendationFocus.Consistency => "consistency",
            RecommendationFocus.Intonation => "intonation",
            RecommendationFocus.VocalWeight => "vocal weight",
            RecommendationFocus.Pitch => "pitch",
            RecommendationFocus.Foundation => "foundation",
            _ => "foundation"
        };

        private static string BuildRecoveryRationale(RecoveryResult recovery)
            => string.Create(CultureInfo.InvariantCulture,
                $"Recovery is low ({StatusLabel(recovery.Status)}, {recovery.Score:0}/100) — " +
                $"a lighter, recovery-oriented exercise is recommended so your voice can " +
                $"restitute. Health comes before goals.");

        private static string BuildFoundationRationale(VoiceStyleGoal style)
            => $"No measured history yet — starting with a gentle foundation exercise that " +
               $"fits a {StyleLabel(style)} goal. We will tailor the focus as your sessions build up.";

        private static string BuildFocusRationale(
            RecommendationFocus focus, VoiceIntelligenceTrendPoint? p, VoiceStyleGoal style)
        {
            var axis = FocusLabel(focus);
            var scoreText = p is null ? string.Empty : ScoreFor(focus, p);
            var styleClause = StyleClause(focus, style);
            return string.Create(CultureInfo.InvariantCulture,
                $"Your {axis} is the area with the most room to grow{scoreText} — this " +
                $"exercise targets it{styleClause}.");
        }

        private static string ScoreFor(RecommendationFocus focus, VoiceIntelligenceTrendPoint p)
        {
            var score = focus switch
            {
                RecommendationFocus.Comfort => p.ComfortScore100,
                RecommendationFocus.Resonance => p.ResonanceScore100,
                RecommendationFocus.Consistency => p.ConsistencyScore100,
                RecommendationFocus.Intonation => p.IntonationScore100,
                RecommendationFocus.VocalWeight => p.VocalWeightScore100,
                RecommendationFocus.Pitch => p.PitchScore100,
                _ => double.NaN
            };
            return double.IsNaN(score)
                ? string.Empty
                : string.Create(CultureInfo.InvariantCulture, $" ({score:0}/100)");
        }

        private static string StyleClause(RecommendationFocus focus, VoiceStyleGoal style)
        {
            if ((style == VoiceStyleGoal.DarkFeminine || style == VoiceStyleGoal.Androgynous)
                && focus == RecommendationFocus.VocalWeight)
                return ", in line with your darker / androgynous voice goal";
            return string.Empty;
        }

        private static string StatusLabel(RecoveryStatus status) => status switch
        {
            RecoveryStatus.WellRecovered => "well recovered",
            RecoveryStatus.Adequate => "adequate",
            RecoveryStatus.Strained => "strained",
            RecoveryStatus.Overtrained => "overtrained",
            _ => "unknown"
        };

        private static string StyleLabel(VoiceStyleGoal style) => style switch
        {
            VoiceStyleGoal.Feminine => "feminine",
            VoiceStyleGoal.Androgynous => "androgynous",
            VoiceStyleGoal.DarkFeminine => "darker feminine",
            VoiceStyleGoal.Situational => "situational",
            _ => "personal"
        };
    }
}
