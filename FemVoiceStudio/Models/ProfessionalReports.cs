using System;
using System.Collections.Generic;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// Period frame shared by all professional report DTOs. Carries the inclusive start/end
    /// of the data window that the report covers, and when the report was assembled.
    /// </summary>
    public sealed record ReportPeriod
    {
        /// <summary>Inclusive start of the report period (UTC).</summary>
        public DateTime PeriodStart { get; init; }

        /// <summary>Inclusive end of the report period (UTC).</summary>
        public DateTime PeriodEnd { get; init; }

        /// <summary>When this report DTO was assembled (UTC). Must be supplied explicitly —
        /// never use DateTime.UtcNow inside a record initialiser.</summary>
        public DateTime GeneratedAt { get; init; }
    }

    /// <summary>
    /// Clinical report DTO: a snapshot of the user's outcome profile together with the
    /// clinical notes and audit events that fall within the report period.
    ///
    /// CLINICAL FRAMING: this is a DESCRIPTIVE / DOCUMENTATION record. It never overrides
    /// the Safety &gt; Health &gt; Recovery hierarchy enforced at the engine level.
    ///
    /// Owned by W0-A3A10 (Report Generation + Professional Exports).
    /// </summary>
    public sealed record ClinicalReport
    {
        /// <summary>Human-readable title for the report (e.g. "Clinical Progress Report — May 2026").</summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>The period this report covers.</summary>
        public ReportPeriod Period { get; init; } = new();

        /// <summary>The outcome snapshot for the covered period.</summary>
        public OutcomeProfile Outcome { get; init; } = new();

        /// <summary>Clinical and coaching notes that fall within the report period,
        /// ordered chronologically ascending.</summary>
        public IReadOnlyList<ClinicalNote> Notes { get; init; } = Array.Empty<ClinicalNote>();

        /// <summary>Audit events that fall within the report period,
        /// ordered chronologically ascending.</summary>
        public IReadOnlyList<AuditEvent> AuditEvents { get; init; } = Array.Empty<AuditEvent>();
    }

    /// <summary>
    /// Coach report DTO: a coaching-oriented summary of the user's outcome, surfacing
    /// focus areas, recommendations, and detected breakthroughs / plateaus.
    ///
    /// DESCRIPTIVE only — this record never overrides any safety, health, or recovery gate.
    ///
    /// Owned by W0-A3A10 (Report Generation + Professional Exports).
    /// </summary>
    public sealed record CoachReport
    {
        /// <summary>Human-readable title for the report.</summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>The period this report covers.</summary>
        public ReportPeriod Period { get; init; } = new();

        /// <summary>The outcome snapshot for the covered period.</summary>
        public OutcomeProfile Outcome { get; init; } = new();

        /// <summary>Ordered list of focus-area labels derived from goal progress and long-term
        /// development patterns (e.g. "Resonance — plateau detected", "Pitch — on track").</summary>
        public IReadOnlyList<string> FocusAreas { get; init; } = Array.Empty<string>();

        /// <summary>Coaching recommendations distilled from the outcome profile
        /// (recovery recommendation, exercise de-prioritisation, goal guidance).</summary>
        public IReadOnlyList<string> Recommendations { get; init; } = Array.Empty<string>();

        /// <summary>Detected breakthrough, if any, from LongTermDevelopment. Null when none is active.</summary>
        public BreakthroughState? Breakthrough { get; init; }

        /// <summary>Detected plateau, if any, from LongTermDevelopment. Null when none is active.</summary>
        public PlateauState? Plateau { get; init; }

        /// <summary>Detected regression, if any, from LongTermDevelopment. Null when none is active.</summary>
        public RegressionState? Regression { get; init; }

        /// <summary>Longitudinal coaching insights surfaced from the outcome profile,
        /// ordered strongest-signal first.</summary>
        public IReadOnlyList<LongitudinalInsight> Insights { get; init; } = Array.Empty<LongitudinalInsight>();
    }

    /// <summary>
    /// Outcome report DTO: a concise outcome summary covering goal achievement, recovery
    /// status, exercise effectiveness, and composite voice score.
    ///
    /// DESCRIPTIVE / REPORTING only — never a safety or training gate.
    ///
    /// Owned by W0-A3A10 (Report Generation + Professional Exports).
    /// </summary>
    public sealed record OutcomeReport
    {
        /// <summary>Human-readable title for the report.</summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>The period this report covers.</summary>
        public ReportPeriod Period { get; init; } = new();

        /// <summary>The outcome snapshot for the covered period.</summary>
        public OutcomeProfile Outcome { get; init; } = new();

        /// <summary>True when the underlying outcome has enough evidence to draw conclusions.
        /// Consumers MUST treat false as "insufficient evidence", never as "no progress".</summary>
        public bool HasEnoughData { get; init; }

        /// <summary>Composite voice score, 0–100, from LongTermDevelopment. 0 when insufficient data.</summary>
        public double CompositeVoiceScore { get; init; }

        /// <summary>Goal-by-goal progress entries, ordered as supplied by the engine.</summary>
        public IReadOnlyList<GoalProgressEntry> GoalProgress { get; init; } = Array.Empty<GoalProgressEntry>();

        /// <summary>Recovery status coarse bucket (e.g. "WellRecovered", "Strained").</summary>
        public string RecoveryStatus { get; init; } = string.Empty;

        /// <summary>Recovery score 0–100 at snapshot time.</summary>
        public double RecoveryScore { get; init; }

        /// <summary>Exercises ranked most-effective first (data-bearing only).</summary>
        public IReadOnlyList<ExerciseEffectivenessProfile> TopExercises { get; init; } =
            Array.Empty<ExerciseEffectivenessProfile>();
    }

    /// <summary>
    /// Timeline report DTO: a voice-development timeline derived from the trend windows
    /// (WeeklyTrend + MonthlyTrend) inside the OutcomeProfile.LongTermDevelopment.
    ///
    /// Each entry in <see cref="TimelineEntries"/> represents one trend window ordered
    /// chronologically, making it straightforward to render as a table or chart.
    ///
    /// DESCRIPTIVE only — this record never overrides any engine gate.
    ///
    /// Owned by W0-A3A10 (Report Generation + Professional Exports).
    /// </summary>
    public sealed record TimelineReport
    {
        /// <summary>Human-readable title for the report (e.g. "Voice Development Timeline — 2026").</summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>The period this report covers.</summary>
        public ReportPeriod Period { get; init; } = new();

        /// <summary>The outcome snapshot from which the timeline was derived.</summary>
        public OutcomeProfile Outcome { get; init; } = new();

        /// <summary>Ordered chronological timeline entries (earliest window first), derived
        /// from OutcomeProfile.LongTermDevelopment.WeeklyTrend + MonthlyTrend.</summary>
        public IReadOnlyList<TimelineEntry> TimelineEntries { get; init; } = Array.Empty<TimelineEntry>();
    }

    /// <summary>
    /// One entry on the voice-development timeline, wrapping a <see cref="TrendWindow"/>
    /// with a human-readable label and a summary of the composite slope direction.
    /// </summary>
    public sealed record TimelineEntry
    {
        /// <summary>Short descriptive label, e.g. "7-day (Jun 1 – Jun 7)".</summary>
        public string Label { get; init; } = string.Empty;

        /// <summary>The underlying trend window this entry represents.</summary>
        public TrendWindow Window { get; init; } = new();

        /// <summary>Direction token: "Improving", "Stable", or "Declining" (derived from
        /// CompositeSlope of the window). "Insufficient data" when HasEnoughData is false.</summary>
        public string Direction { get; init; } = string.Empty;
    }
}
