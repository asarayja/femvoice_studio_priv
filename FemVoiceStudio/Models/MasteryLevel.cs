namespace FemVoiceStudio.Models
{
    /// <summary>
    /// Per-exercise mastery classification shown as a badge in the Live Feedback panel.
    /// Computed by <see cref="FemVoiceStudio.Services.MasteryEvaluator"/> from persisted
    /// session analytics — never from score averages alone. Clinical gates (comfort-zone
    /// compliance, safety-lock history, fatigue trend) are hard requirements; see
    /// work-documents/FEMVOICE – Mastery &amp; Progression Clinical Safety Fix – Analyse.md.
    /// </summary>
    public enum MasteryLevel
    {
        /// <summary>Fewer than 3 completed sessions — still learning the exercise.</summary>
        Beginner,

        /// <summary>Default state: building consistency, or recent safety events demoted the level.</summary>
        Developing,

        /// <summary>Consistent resonance/stability over recent sessions with a clean safety window.</summary>
        Stable,

        /// <summary>20+ sessions with verified consistency, comfort compliance and no recent safety events.</summary>
        Mastered
    }
}
