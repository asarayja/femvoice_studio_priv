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
        StabilityTraining  = 3
    }
}
