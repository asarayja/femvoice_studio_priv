using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Creates an <see cref="ExerciseTargetProfile"/> for a given <see cref="ExerciseProfileType"/>.
    /// Keeps profile construction out of the presentation layer.
    /// </summary>
    public interface IExerciseProfileFactory
    {
        /// <summary>
        /// Returns the <see cref="ExerciseTargetProfile"/> that corresponds to <paramref name="type"/>.
        /// </summary>
        /// <param name="type">The profile type stored on the exercise record.</param>
        /// <returns>A freshly constructed, validated profile instance.</returns>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// Thrown when <paramref name="type"/> is not a recognised enum value.
        /// </exception>
        ExerciseTargetProfile CreateProfile(ExerciseProfileType type);
    }
}