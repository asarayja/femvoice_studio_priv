using System;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Default implementation of <see cref="IExerciseProfileFactory"/>.
    /// Each case delegates directly to the corresponding factory method on
    /// <see cref="ExerciseTargetProfile"/> — no clinical logic lives here.
    /// </summary>
    public sealed class ExerciseProfileFactory : IExerciseProfileFactory
    {
        /// <inheritdoc/>
        public ExerciseTargetProfile CreateProfile(ExerciseProfileType type) =>
            type switch
            {
                ExerciseProfileType.ResonanceHumming   => ExerciseTargetProfile.CreateResonanceHumming(),
                ExerciseProfileType.ResonanceVowels    => ExerciseTargetProfile.CreateResonanceVowels(),
                ExerciseProfileType.CoordinatedGlideUp => ExerciseTargetProfile.CreateCoordinatedGlideUp(),
                ExerciseProfileType.StabilityTraining  => ExerciseTargetProfile.CreateStabilityTraining(),
                ExerciseProfileType.ResonanceExercise  => ExerciseTargetProfile.ResonanceExercise(),
                ExerciseProfileType.PitchExercise      => ExerciseTargetProfile.PitchExercise(),
                ExerciseProfileType.IntonationExercise => ExerciseTargetProfile.IntonationExercise(),
                ExerciseProfileType.StrawPhonation     => ExerciseTargetProfile.StrawPhonation(),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
    }
}