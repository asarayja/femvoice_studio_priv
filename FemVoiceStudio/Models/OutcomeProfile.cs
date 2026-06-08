using System;
using System.Collections.Generic;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// OUTCOME TRACKING aggregate (Sprint E, W0-A4): a single immutable, professional-grade
    /// snapshot of "is the voice actually getting better?" for one user. It joins four
    /// already-existing intelligence layers into one structure a clinician/coach report can
    /// render directly:
    /// <list type="bullet">
    ///   <item><description><see cref="GoalProgress"/> — explicit goal targets vs current values.</description></item>
    ///   <item><description><see cref="RecoveryProgress"/> — the predictive recovery picture.</description></item>
    ///   <item><description><see cref="ExerciseEffectiveness"/> — which exercises help / concern.</description></item>
    ///   <item><description><see cref="LongTermDevelopment"/> — multi-window trend + pattern intelligence.</description></item>
    /// </list>
    ///
    /// CLINICAL FRAMING: this record is DESCRIPTIVE / REPORTING intelligence ONLY. It never
    /// overrides or substitutes for the Safety &gt; Health &gt; Recovery hierarchy; those gates
    /// remain the only blocking authorities. Consumers MUST treat
    /// <see cref="HasEnoughData"/> == false as "insufficient evidence", never as "no progress".
    /// </summary>
    public sealed record OutcomeProfile
    {
        /// <summary>The user this outcome snapshot belongs to.</summary>
        public int UserId { get; init; }

        /// <summary>When this snapshot was assembled (UTC).</summary>
        public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;

        /// <summary>Goal-by-goal progress toward the user's explicit targets.</summary>
        public GoalProgress GoalProgress { get; init; } = new();

        /// <summary>The predictive recovery picture at snapshot time.</summary>
        public RecoveryProgress RecoveryProgress { get; init; } = new();

        /// <summary>Ranked exercise effectiveness + de-prioritisation concerns.</summary>
        public ExerciseEffectivenessSummary ExerciseEffectiveness { get; init; } = new();

        /// <summary>Multi-window longitudinal development + detected patterns + insights.</summary>
        public LongTermDevelopment LongTermDevelopment { get; init; } = new();

        /// <summary>
        /// True when at least one of the underlying layers carries trustworthy evidence
        /// (a goal with a target, recovery data, an evidenced exercise, or a development
        /// profile with enough data). Consumers MUST treat false as "insufficient evidence",
        /// never as "no progress".
        /// </summary>
        public bool HasEnoughData { get; init; }
    }

    /// <summary>
    /// Goal-by-goal progress block. Empty <see cref="Goals"/> means the user has no active
    /// explicit goals — that is a valid state, not a failure.
    /// </summary>
    public sealed record GoalProgress
    {
        /// <summary>One entry per active goal, in the engine's surfaced order.</summary>
        public IReadOnlyList<GoalProgressEntry> Goals { get; init; } =
            Array.Empty<GoalProgressEntry>();
    }

    /// <summary>
    /// Progress toward one explicit goal. Derived from the legacy SmartCoachGoal
    /// (TargetValue / CurrentValue) joined to the user's <see cref="VoiceGoalProfile"/>
    /// PrimaryFocus via a string→<see cref="VoiceDimension"/> mapping.
    ///
    /// DESCRIPTIVE only: an achieved or unachieved goal never blocks training — the
    /// Safety/Health/Recovery gates are the sole authorities.
    /// </summary>
    public sealed record GoalProgressEntry
    {
        /// <summary>Raw goal type token from the legacy goal row (e.g. "pitch", "resonance").</summary>
        public string GoalType { get; init; } = string.Empty;

        /// <summary>The voice dimension this goal primarily targets, mapped from the user's
        /// goal profile PrimaryFocus (or the goal type when no profile focus is set).</summary>
        public VoiceDimension PrimaryFocus { get; init; }

        /// <summary>The target value the user is working toward.</summary>
        public double TargetValue { get; init; }

        /// <summary>The current measured value for this goal.</summary>
        public double CurrentValue { get; init; }

        /// <summary>Signed remaining distance to the goal (TargetValue − CurrentValue).</summary>
        public double DeltaToGoal { get; init; }

        /// <summary>Progress toward the target, 0–100 (clamped). 100 once the target is met.</summary>
        public double PercentComplete { get; init; }

        /// <summary>True when the current value has reached or passed the target.</summary>
        public bool IsAchieved { get; init; }
    }

    /// <summary>
    /// The predictive recovery picture, copied out of the Services-layer
    /// <c>RecoveryForecast</c> so the model does not depend on Services types.
    ///
    /// CLINICAL INVARIANT: Recovery sits at the top of the global priority order
    /// (Safety &gt; Health &gt; Recovery &gt; …). These figures ADVISE rest earlier and more
    /// softly than a hard gate; they never represent or weaken a Safety decision.
    /// </summary>
    public sealed record RecoveryProgress
    {
        /// <summary>Reactive recovery score, 0 (overtrained) … 100 (fully rested).</summary>
        public double CurrentScore0to100 { get; init; }

        /// <summary>Coarse recovery status bucket (Overtrained/Strained/Adequate/WellRecovered).</summary>
        public string Status { get; init; } = string.Empty;

        /// <summary>True when the predictive overtraining branch fired (high density, little rest).</summary>
        public bool OvertrainingPredicted { get; init; }

        /// <summary>Recovery debt, 0–100 (0 = none, higher = more unpaid rest).</summary>
        public double RecoveryDebt { get; init; }

        /// <summary>Acute:Chronic Workload Ratio actually computed (0 when no chronic base).</summary>
        public double AcuteChronicWorkloadRatio { get; init; }

        /// <summary>How strongly recovery wants to be heard (None…Urgent), as a string.</summary>
        public string Severity { get; init; } = string.Empty;

        /// <summary>Explainable, clinically safe recovery recommendation copy.</summary>
        public string RecommendationText { get; init; } = string.Empty;
    }

    /// <summary>
    /// Exercise effectiveness block: a ranked "most effective first" list plus the
    /// de-prioritisation concerns flagged for taxing / comfort-eroding exercises.
    ///
    /// DESCRIPTIVE only: a concern marks an exercise to DE-PRIORITISE in recommendations;
    /// it is never itself a safety block.
    /// </summary>
    public sealed record ExerciseEffectivenessSummary
    {
        /// <summary>Exercises ranked most-effective first (only data-bearing profiles).</summary>
        public IReadOnlyList<ExerciseEffectivenessProfile> Ranked { get; init; } =
            Array.Empty<ExerciseEffectivenessProfile>();

        /// <summary>Data-driven de-prioritisation flags. Empty when nothing is concerning.</summary>
        public IReadOnlyList<ExerciseEffectivenessFlag> Concerns { get; init; } =
            Array.Empty<ExerciseEffectivenessFlag>();
    }

    /// <summary>
    /// Long-term development block: multi-window trend statistics, the composite voice score,
    /// the detected plateau / breakthrough / regression states, and the longitudinal
    /// coaching insights.
    ///
    /// DESCRIPTIVE / EXPLANATORY only — never a training gate.
    /// </summary>
    public sealed record LongTermDevelopment
    {
        /// <summary>Short-horizon trend windows (7-day, optionally 30-day).</summary>
        public IReadOnlyList<TrendWindow> WeeklyTrend { get; init; } =
            Array.Empty<TrendWindow>();

        /// <summary>Long-horizon trend windows (90-day and 180-day).</summary>
        public IReadOnlyList<TrendWindow> MonthlyTrend { get; init; } =
            Array.Empty<TrendWindow>();

        /// <summary>Hierarchy-weighted composite voice score, 0–100. 0 when insufficient data.</summary>
        public double CompositeVoiceScore { get; init; }

        /// <summary>Detected plateau state, or null when no plateau is active.</summary>
        public PlateauState? Plateau { get; init; }

        /// <summary>Detected breakthrough state, or null when no breakthrough is active.</summary>
        public BreakthroughState? Breakthrough { get; init; }

        /// <summary>Detected regression state, or null when no regression is active.</summary>
        public RegressionState? Regression { get; init; }

        /// <summary>Ranked longitudinal coaching insights (strongest signal first).</summary>
        public IReadOnlyList<LongitudinalInsight> Insights { get; init; } =
            Array.Empty<LongitudinalInsight>();
    }
}
