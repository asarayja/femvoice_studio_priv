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
    /// NB: an all-default instance has Recovery = default(RecoveryResult), whose Status is
    /// Overtrained (enum value 0) and Score 0 — so the recovery gate fires first and the
    /// engine returns a safe RECOVERY-oriented light pick, not a Foundation starter. A
    /// Foundation starter requires an explicit WellRecovered/Adequate Recovery (Score above
    /// LowRecoveryThreshold) together with a null/neutral LatestVoiceScores — which is how a
    /// genuine new user is represented. Either outcome is clinically safe (light exercise).
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

        /// <summary>
        /// OPTIONAL per-exercise EFFECTIVENESS intelligence (Sprint C.2, Agent EFF), keyed
        /// by ExerciseId. PURELY a WEAK, DATA-driven tie-break/fine-tuning on the final
        /// ranking — it never reorders ahead of the recovery gate, focus, mastery
        /// deprioritisation, or variation. A profile only nudges ranking when
        /// <see cref="ExerciseEffectivenessProfile.HasEnoughData"/> is true; a low-data
        /// profile is treated as NEUTRAL ("insufficient evidence", never "ineffective" —
        /// no lift, no penalty). null ⇒ EXACTLY today's ranking. See
        /// <see cref="ExerciseRecommendationEngine.RankCandidates"/>.
        /// </summary>
        public IReadOnlyDictionary<int, ExerciseEffectivenessProfile>? EffectivenessByExercise { get; init; }

        /// <summary>
        /// OPTIONAL set of ExerciseIds that the effectiveness engine has safety-FLAGGED
        /// (high recovery cost / fatigue / comfort decline — see
        /// <see cref="ExerciseEffectivenessEngine.FlagConcerns"/>). A flagged exercise is
        /// mildly DE-PRIORITISED in the ranking so a safer alternative is preferred on top
        /// — this is DATA-driven nudging, NEVER a safety BLOCK (the ProgressionSafetyGate
        /// owns blocking). null/empty ⇒ no de-prioritisation (today's ranking).
        /// </summary>
        public IReadOnlyCollection<int>? FlaggedExerciseIds { get; init; }
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
                    rationale: BuildRecoveryRationale(input.Recovery),
                    input: input);
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
                    rationale: BuildFoundationRationale(style),
                    input: input);
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
                rationale: BuildFocusRationale(focus, latest, style),
                input: input);
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
            // lightest bucket (isolated sounds / syllables, ids 1..6). A recovering voice
            // is never sent to phrases/sentences even if the user normally trains there.
            var capped = level <= SpeechComplexityLevel.Syllables
                ? level
                : SpeechComplexityLevel.IsolatedSounds;
            return ExerciseIdsForComplexity(capped);
        }

        /// <summary>
        /// Pure mirror of <see cref="ComplexityEngine.GetExerciseIdsForComplexity"/>
        /// (includePreview:false). FANTOM-ID-FIKS: only REAL catalog ids (1–15) are ever
        /// returned — the catalog has exactly 15 seeded exercises, so emitting 16–50 would
        /// recommend a non-existent exercise (GetExerciseById(16+) is null). The 15
        /// exercises are partitioned over the 7 levels (kept in SYNC with the mirrors in
        /// <see cref="ComplexityEngine.GetExerciseIdsForComplexity"/> and
        /// <see cref="LearningPathProfileBuilder"/>):
        ///   IsolatedSounds 1–3 · Syllables 4–6 · Words 7–8 · Phrases 9–10 ·
        ///   StructuredSentences 11–12 · SpontaneousSpeech 13–14 · Conversational 15.
        /// </summary>
        private static IReadOnlyList<int> ExerciseIdsForComplexity(SpeechComplexityLevel level) => level switch
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
            string rationale,
            ExerciseRecommendationInput input)
        {
            var ranked = RankCandidates(
                candidatePool, recent, mastery,
                input.EffectivenessByExercise, input.FlaggedExerciseIds);
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

        /// <summary>Mild, DATA-driven penalty for a safety-FLAGGED exercise (high recovery
        /// cost / fatigue / comfort decline). Deliberately SMALLER than the variation (+2)
        /// and mastery (+4) penalties so it only de-prioritises a flagged id when an
        /// otherwise-equal safer alternative exists — never a safety BLOCK (the
        /// ProgressionSafetyGate owns blocking).</summary>
        private const int FlagPenalty = 1;

        /// <summary>
        /// Stable ranking of a candidate pool. Lower sort key = better:
        ///   • +<see cref="FlagPenalty"/> if the id is safety-flagged by the effectiveness
        ///     engine (a mild, data-driven de-prioritisation; never a block).
        ///   • +2 if the id is in the recent variation window (avoid repeats).
        ///   • +4 if the id is already Mastered (lift weaker, under-covered areas).
        /// Penalty is the PRIMARY key. EFFECTIVENESS is only ever a WEAK tie-break WITHIN an
        /// equal penalty bucket: among candidates with the same penalty, a higher
        /// CompositeEffectiveness ranks first — but ONLY for profiles with
        /// <see cref="ExerciseEffectivenessProfile.HasEnoughData"/> = true. A low-data (or
        /// absent) profile is treated as NEUTRAL: it neither lifts nor penalises, so
        /// "insufficient evidence" never reads as "ineffective". The id tie-break stays last
        /// for determinism. Always returns a non-empty list when the pool is non-empty.
        /// null effectiveness/flags ⇒ EXACTLY the previous penalty-then-id ranking.
        /// </summary>
        private static List<int> RankCandidates(
            IReadOnlyList<int> pool,
            IReadOnlyList<int> recent,
            IReadOnlyDictionary<int, MasteryLevel> mastery,
            IReadOnlyDictionary<int, ExerciseEffectivenessProfile>? effectiveness = null,
            IReadOnlyCollection<int>? flaggedIds = null)
        {
            if (pool.Count == 0) return new List<int>();

            var recentSet = recent.Take(RecentVariationWindow).ToHashSet();
            var flaggedSet = flaggedIds is null or { Count: 0 }
                ? null
                : flaggedIds as HashSet<int> ?? flaggedIds.ToHashSet();

            return pool
                .Distinct()
                .Select(id =>
                {
                    var penalty = 0;
                    if (flaggedSet != null && flaggedSet.Contains(id)) penalty += FlagPenalty;
                    if (recentSet.Contains(id)) penalty += 2;
                    if (mastery.TryGetValue(id, out var level) && level == MasteryLevel.Mastered)
                        penalty += 4;
                    return (Id: id, Penalty: penalty, Effectiveness: EffectivenessKey(id, effectiveness));
                })
                .OrderBy(x => x.Penalty)
                .ThenByDescending(x => x.Effectiveness)  // weak tie-break: more effective first
                .ThenBy(x => x.Id)
                .Select(x => x.Id)
                .ToList();
        }

        /// <summary>
        /// The weak effectiveness tie-break key for an id: the profile's
        /// CompositeEffectiveness when there IS enough data, else the neutral midpoint so a
        /// low-data / absent profile sorts exactly like an "average" candidate (no lift, no
        /// penalty). Mirrors the neutral anchoring in
        /// <see cref="ExerciseEffectivenessProfile.CompositeEffectiveness"/>.
        /// </summary>
        private const double NeutralEffectiveness = 50.0;

        private static double EffectivenessKey(
            int id, IReadOnlyDictionary<int, ExerciseEffectivenessProfile>? effectiveness)
        {
            if (effectiveness != null
                && effectiveness.TryGetValue(id, out var profile)
                && profile is { HasEnoughData: true })
            {
                return profile.CompositeEffectiveness;
            }
            return NeutralEffectiveness;
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
