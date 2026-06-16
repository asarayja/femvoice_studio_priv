using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Assembles a <see cref="SessionInsight"/> at session end from the building blocks the
    /// rest of the system already produces: the freshly computed
    /// <see cref="VoiceIntelligenceScores"/>, the prior Voice Intelligence trend history,
    /// the per-session <see cref="ExerciseSessionOutcome"/> and the
    /// <see cref="RecoveryResult"/>.
    ///
    /// PURE / TESTABLE: <see cref="Build"/> takes all its data as parameters and performs
    /// no IO, randomness or clock reads beyond stamping <see cref="SessionInsight.GeneratedAt"/>
    /// (overridable). Deterministic for a given input.
    ///
    /// CLINICAL FRAMING: improvements are framed as encouragement ("Resonance +6 since last
    /// session"); risks use calm, non-shaming copy; the suggested focus is the dimension to
    /// gently explore next — never a deficiency verdict. The insight is a reflection
    /// surface, NOT a gate: it reports recovery needs so the UI can prioritise them but
    /// never overrides the Safety &gt; Health &gt; Recovery hierarchy.
    /// </summary>
    public sealed class SessionInsightBuilder
    {
        /// <summary>
        /// Minimum positive score delta (points, 0–100 scale) for a dimension to count as a
        /// real improvement. Filters out trivial frame-to-frame noise so the user only sees
        /// genuine, encouraging gains.
        /// </summary>
        public const double ImprovementThreshold = 1.0;

        /// <summary>
        /// Localization source for all user-facing copy. The calm, non-shaming clinical
        /// framing is authored verbatim as the neutral-resource default; only the lookup
        /// indirection is added here (same pattern as ExerciseCoach_*/AdaptiveComfort_*).
        /// Defaults to the shared <see cref="LocalizationService.Instance"/> so existing
        /// callers (<c>new SessionInsightBuilder()</c>) keep working unchanged.
        /// </summary>
        private readonly ILocalizationService _localization;

        /// <summary>Production constructor — resolves copy via the shared localization service.</summary>
        public SessionInsightBuilder() : this(LocalizationService.Instance)
        {
        }

        /// <summary>Constructor with an injectable localization service (testability / DI).</summary>
        public SessionInsightBuilder(ILocalizationService localization)
        {
            _localization = localization ?? LocalizationService.Instance;
        }

        /// <summary>
        /// Resolves a localized string by key, falling back to the supplied verbatim English
        /// default when the key is missing (the indexer returns the key itself on a miss).
        /// The default preserves the exact clinical wording so behaviour is unchanged even
        /// before a locale has been translated.
        /// </summary>
        private string L(string key, string fallback)
        {
            var value = _localization[key];
            return string.IsNullOrEmpty(value) || value == key ? fallback : value;
        }

        /// <summary>
        /// Resolves a localized format string by key (verbatim English fallback) and formats
        /// it with the supplied arguments. Numeric arguments are pre-formatted with
        /// <see cref="CultureInfo.InvariantCulture"/> by the callers, so the user-facing
        /// number rendering is stable across locales (task mandate).
        /// </summary>
        private string LF(string key, string fallback, params object[] args)
        {
            var format = L(key, fallback);
            try { return string.Format(CultureInfo.InvariantCulture, format, args); }
            catch { return format; }
        }

        /// <summary>
        /// Light static focus→exercise mapping used until the Agent-2 recommender lands in
        /// Bølge 2. Each dimension maps to a small, fixed set of exercise ids whose primary
        /// training target is that dimension. Intentionally conservative and side-effect
        /// free; Bølge 2 replaces this with the real recommender.
        /// </summary>
        private static readonly IReadOnlyDictionary<VoiceDimension, IReadOnlyList<int>> FocusExerciseMap =
            new Dictionary<VoiceDimension, IReadOnlyList<int>>
            {
                [VoiceDimension.Comfort] = new[] { 1, 2 },
                [VoiceDimension.Recovery] = new[] { 1, 3 },
                [VoiceDimension.Resonance] = new[] { 10, 11, 12 },
                [VoiceDimension.Consistency] = new[] { 20, 21 },
                [VoiceDimension.Intonation] = new[] { 30, 31 },
                [VoiceDimension.VocalWeight] = new[] { 40, 41 },
                [VoiceDimension.Pitch] = new[] { 50, 51 },
            };

        /// <summary>
        /// Builds the end-of-session insight.
        /// </summary>
        /// <param name="current">This session's Voice Intelligence scores (required).</param>
        /// <param name="priorTrend">Chronological prior trend points (oldest → newest). The
        /// most recent point is used as the per-dimension improvement reference. May be empty
        /// (first session) — improvements are then empty but the insight is still valid.</param>
        /// <param name="outcome">This session's clinical outcome (drives the risk flags).</param>
        /// <param name="recovery">The recovery snapshot for this session.</param>
        /// <param name="sessionId">Session id to stamp on the insight (optional).</param>
        /// <param name="generatedAt">Override for the generation timestamp (testability).</param>
        public SessionInsight Build(
            VoiceIntelligenceScores current,
            IReadOnlyList<VoiceIntelligenceTrendPoint>? priorTrend,
            ExerciseSessionOutcome outcome,
            RecoveryResult recovery,
            int sessionId = 0,
            DateTime? generatedAt = null)
        {
            ArgumentNullException.ThrowIfNull(current);
            ArgumentNullException.ThrowIfNull(outcome);

            var reference = MostRecentPriorPoint(priorTrend);
            var isFirstSession = reference is null;

            var improvements = isFirstSession
                ? Array.Empty<DimensionImprovement>()
                : BuildImprovements(current, reference!);

            var risks = BuildRisks(outcome);
            var recoveryNeed = RecoveryNeed.FromResult(recovery);
            var focus = DetermineSuggestedFocus(current);
            var suggestedExercises = ExercisesForFocus(focus);

            var insight = new SessionInsight
            {
                SessionId = sessionId,
                GeneratedAt = generatedAt ?? DateTime.Now,
                CompositeVoiceScore = current.CompositeVoiceScore,
                IsFirstSession = isFirstSession,
                Improvements = improvements,
                Risks = risks,
                RecoveryNeeds = recoveryNeed,
                SuggestedFocus = focus,
                SuggestedExercises = suggestedExercises,
            };

            return insight with { Summary = BuildSummary(insight) };
        }

        // ── Improvements ─────────────────────────────────────────────────────────

        /// <summary>
        /// Per-dimension delta vs the most recent prior point. Only genuine rises
        /// (delta ≥ <see cref="ImprovementThreshold"/>) are kept, strongest first, with a
        /// stable hierarchy tie-break for equal deltas.
        /// </summary>
        private IReadOnlyList<DimensionImprovement> BuildImprovements(
            VoiceIntelligenceScores current, VoiceIntelligenceTrendPoint reference)
        {
            var pairs = new (VoiceDimension Dim, double Current, double Previous)[]
            {
                (VoiceDimension.Comfort,     current.Comfort.Score,     reference.ComfortScore100),
                (VoiceDimension.Recovery,    current.Recovery.Score,    reference.RecoveryScore100),
                (VoiceDimension.Resonance,   current.Resonance.Score,   reference.ResonanceScore100),
                (VoiceDimension.Consistency, current.Consistency.Score, reference.ConsistencyScore100),
                (VoiceDimension.Intonation,  current.Intonation.Score,  reference.IntonationScore100),
                (VoiceDimension.VocalWeight, current.VocalWeight.Score, reference.VocalWeightScore100),
                (VoiceDimension.Pitch,       current.Pitch.Score,       reference.PitchScore100),
            };

            return pairs
                .Where(p => p.Current - p.Previous >= ImprovementThreshold)
                .OrderByDescending(p => p.Current - p.Previous)
                .ThenBy(p => (int)p.Dim) // hierarchy tie-break: Health-first
                .Select(p => new DimensionImprovement
                {
                    Dimension = p.Dim,
                    CurrentScore = p.Current,
                    PreviousScore = p.Previous,
                    // "{label} +{delta} since last session" — number pre-formatted Invariant.
                    Explanation = LF(
                        "SessionInsight_Improvement",
                        "{0} +{1} since last session",
                        DimensionLabel(p.Dim),
                        (p.Current - p.Previous).ToString("0", CultureInfo.InvariantCulture)),
                })
                .ToArray();
        }

        // ── Risks ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Derives the risk flags from the session outcome. Most clinically severe first:
        /// safety locks, then strain, then comfort breaches. Calm, non-shaming copy.
        /// Comfort breaches only flag at the same ≥3 threshold the recorder uses to journal
        /// a ComfortZoneBreach health event, so the insight matches the durable history.
        /// </summary>
        private IReadOnlyList<SessionRisk> BuildRisks(ExerciseSessionOutcome outcome)
        {
            var risks = new List<SessionRisk>();

            var safetyLocks = Math.Max(0, outcome.SafetyLockEpisodes);
            if (safetyLocks > 0)
            {
                risks.Add(new SessionRisk
                {
                    ReasonCode = "SAFETY_LOCK",
                    Count = safetyLocks,
                    Description = L(
                        "SessionInsight_Risk_SafetyLock",
                        "Your voice took a protective pause this session — a good moment to rest."),
                });
            }

            var strain = Math.Max(0, outcome.StrainDetections);
            if (strain > 0)
            {
                risks.Add(new SessionRisk
                {
                    ReasonCode = "STRAIN_DETECTED",
                    Count = strain,
                    Description = L(
                        "SessionInsight_Risk_Strain",
                        "Some effort showed up in your voice — easing off keeps it comfortable."),
                });
            }

            var breaches = Math.Max(0, outcome.ComfortBreachEpisodes);
            if (breaches >= ComfortBreachFlagThreshold)
            {
                risks.Add(new SessionRisk
                {
                    ReasonCode = "COMFORT_BREACH",
                    Count = breaches,
                    Description = L(
                        "SessionInsight_Risk_ComfortBreach",
                        "Your voice drifted outside its comfort zone a few times — staying within it feels easier."),
                });
            }

            return risks;
        }

        /// <summary>Comfort-breach episode count at which a risk is flagged. Mirrors the
        /// recorder's ComfortZoneBreach journaling threshold so insight and history agree.</summary>
        public const int ComfortBreachFlagThreshold = 3;

        // ── Suggested focus ──────────────────────────────────────────────────────

        /// <summary>
        /// The weakest Voice Intelligence dimension is the suggested next focus. Ties are
        /// broken by the clinical hierarchy (Health-first): when two dimensions share the
        /// lowest score, the higher-priority (lower enum value) one is chosen so recovery /
        /// comfort surface before pitch.
        /// </summary>
        private static VoiceDimension DetermineSuggestedFocus(VoiceIntelligenceScores current)
        {
            var scored = new (VoiceDimension Dim, double Score)[]
            {
                (VoiceDimension.Comfort,     current.Comfort.Score),
                (VoiceDimension.Recovery,    current.Recovery.Score),
                (VoiceDimension.Resonance,   current.Resonance.Score),
                (VoiceDimension.Consistency, current.Consistency.Score),
                (VoiceDimension.Intonation,  current.Intonation.Score),
                (VoiceDimension.VocalWeight, current.VocalWeight.Score),
                (VoiceDimension.Pitch,       current.Pitch.Score),
            };

            return scored
                .OrderBy(s => s.Score)
                .ThenBy(s => (int)s.Dim) // hierarchy tie-break
                .First().Dim;
        }

        private static IReadOnlyList<int> ExercisesForFocus(VoiceDimension focus) =>
            FocusExerciseMap.TryGetValue(focus, out var ids)
                ? ids
                : Array.Empty<int>();

        // ── Summary copy ─────────────────────────────────────────────────────────

        /// <summary>
        /// Assembles the encouraging, non-shaming summary line from the structured fields.
        /// Mastery/exploration angle: leads with a gain when there is one, always offers a
        /// gentle next focus, and surfaces recovery first when it needs attention (Health
        /// before Goals). Deterministic; safe for clinical surfaces.
        /// </summary>
        private string BuildSummary(SessionInsight insight)
        {
            var parts = new List<string>();

            // Recovery takes the lead when it needs attention — Health before Goals.
            if (insight.RecoveryNeeds.NeedsAttention)
            {
                parts.Add(L(
                    "SessionInsight_Summary_RecoveryLead",
                    "Your voice could use some rest — let's keep things gentle."));
            }

            if (insight.Improvements.Count > 0)
            {
                var top = insight.Improvements[0];
                parts.Add(LF(
                    "SessionInsight_Summary_Improvement",
                    "Nice work — {0} grew {1} since last time.",
                    DimensionLabel(top.Dimension).ToLowerInvariant(),
                    top.Delta.ToString("0", CultureInfo.InvariantCulture)));
            }
            else if (insight.IsFirstSession)
            {
                parts.Add(L(
                    "SessionInsight_Summary_FirstSession",
                    "Great first session — this is your starting point to explore from."));
            }
            else
            {
                parts.Add(L(
                    "SessionInsight_Summary_Steady",
                    "Steady session — you're holding your ground."));
            }

            parts.Add(LF(
                "SessionInsight_Summary_NextFocus",
                "Next, you might gently explore {0}.",
                DimensionLabel(insight.SuggestedFocus).ToLowerInvariant()));

            return string.Join(" ", parts);
        }

        // ── Labels ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Stable English label for a dimension, used in the structured explanations and the
        /// summary. When this copy is promoted to RESX the keys map per-dimension and the
        /// result must pass <see cref="ClinicalLanguagePolicy"/>.
        /// </summary>
        private string DimensionLabel(VoiceDimension dimension) => dimension switch
        {
            VoiceDimension.Comfort => L("DimensionLabel_Comfort", "Comfort"),
            VoiceDimension.Recovery => L("DimensionLabel_Recovery", "Recovery"),
            VoiceDimension.Resonance => L("DimensionLabel_Resonance", "Resonance"),
            VoiceDimension.Consistency => L("DimensionLabel_Consistency", "Consistency"),
            VoiceDimension.Intonation => L("DimensionLabel_Intonation", "Intonation"),
            VoiceDimension.VocalWeight => L("DimensionLabel_VocalWeight", "Vocal weight"),
            VoiceDimension.Pitch => L("DimensionLabel_Pitch", "Pitch"),
            _ => dimension.ToString()
        };

        private static VoiceIntelligenceTrendPoint? MostRecentPriorPoint(
            IReadOnlyList<VoiceIntelligenceTrendPoint>? priorTrend)
        {
            if (priorTrend is null || priorTrend.Count == 0)
                return null;

            // priorTrend is documented chronological (oldest → newest), but defensively pick
            // the latest by StartedAt so an out-of-order caller still gets the right reference.
            VoiceIntelligenceTrendPoint latest = priorTrend[0];
            foreach (var point in priorTrend)
            {
                if (point.StartedAt >= latest.StartedAt)
                    latest = point;
            }
            return latest;
        }
    }
}
