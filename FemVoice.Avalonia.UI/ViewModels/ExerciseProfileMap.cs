using System.Collections.Generic;
using FemVoiceStudio.Models;   // ExerciseProfileType

namespace FemVoice.Avalonia.ViewModels;

/// <summary>
/// Avalonia-only, READ-ONLY map from catalog exercise Id (1–15) to <see cref="ExerciseProfileType"/>.
/// The mapping is copied verbatim from the canonical SQLite seed in
/// FemVoice.Core/Data/ExerciseDataService.cs (SortOrder → ProfileType); the shared catalog and
/// ExerciseDataService are NOT modified or read at runtime (no DB dependency). Unknown ids fail safe
/// to <c>null</c> (the runtime then shows the exercise's own targets + a documented fallback).
/// </summary>
internal static class ExerciseProfileMap
{
    // Source of truth: ExerciseDataService seed (SortOrder = ProfileType), 2026-06-16.
    private static readonly IReadOnlyDictionary<int, ExerciseProfileType> Map = new Dictionary<int, ExerciseProfileType>
    {
        [1]  = ExerciseProfileType.ResonanceHumming,
        [2]  = ExerciseProfileType.ResonanceVowels,
        [3]  = ExerciseProfileType.CoordinatedGlideUp,
        [4]  = ExerciseProfileType.CoordinatedGlideUp,
        [5]  = ExerciseProfileType.StabilityTraining,
        [6]  = ExerciseProfileType.StabilityTraining,
        [7]  = ExerciseProfileType.IntonationExercise,
        [8]  = ExerciseProfileType.IntonationExercise,
        [9]  = ExerciseProfileType.ResonanceVowels,
        [10] = ExerciseProfileType.ResonanceVowels,
        [11] = ExerciseProfileType.ResonanceHumming,
        [12] = ExerciseProfileType.PitchExercise,
        [13] = ExerciseProfileType.CoordinatedGlideUp,
        [14] = ExerciseProfileType.StrawPhonation,
        [15] = ExerciseProfileType.CoordinatedGlideUp,
        [16] = ExerciseProfileType.StrawPhonation,   // Boblefonasjon / Lax Vox (SOVT vann-variant)
        [17] = ExerciseProfileType.ResonanceExercise, // Stor hund / liten hund (resonanskontrast)
    };

    /// <summary>Profile type for a catalog exercise id, or null if unmapped (fail-safe).</summary>
    public static ExerciseProfileType? ForExerciseId(int id)
        => Map.TryGetValue(id, out var t) ? t : null;
}
