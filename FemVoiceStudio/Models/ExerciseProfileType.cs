using System;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// Identifies which <see cref="ExerciseTargetProfile"/> factory method applies to an exercise.
    /// Stored as INTEGER in the Exercises table for stable SQLite round-tripping.
    /// Add new values at the end only — never renumber existing entries.
    /// </summary>
    public enum ExerciseProfileType
    {
        ResonanceHumming   = 0,
        ResonanceVowels    = 1,
        CoordinatedGlideUp = 2,
        StabilityTraining  = 3,

        // ── New values appended at the END for backward-compatibility with persisted
        //    INTEGER ProfileType values (ordinals 0–3 above must never be renumbered). ──

        /// <summary>
        /// Generic resonance-focused profile (forward formant placement). Maps to
        /// <see cref="ExerciseTargetProfile.ResonanceExercise"/>.
        /// </summary>
        ResonanceExercise  = 4,

        /// <summary>
        /// Pitch-focused profile (normalised pitch relative to the comfort zone). Maps to
        /// <see cref="ExerciseTargetProfile.PitchExercise"/>.
        /// </summary>
        PitchExercise      = 5,

        /// <summary>
        /// Intonation profile (continuous pitch-slope tracking, no static hold). Maps to
        /// <see cref="ExerciseTargetProfile.IntonationExercise"/>.
        /// </summary>
        IntonationExercise = 6,

        /// <summary>
        /// Straw phonation / SOVT profile (semi-occluded vocal tract — airflow + intensity
        /// safety, NOT endurance/hold training). Maps to
        /// <see cref="ExerciseTargetProfile.StrawPhonation"/>.
        /// </summary>
        StrawPhonation     = 7
    }
}
