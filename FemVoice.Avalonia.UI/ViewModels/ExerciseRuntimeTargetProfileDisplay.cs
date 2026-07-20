using System.Collections.Generic;
using FemVoiceStudio.Models;     // ExerciseTargetProfile, ExerciseProfileType
using FemVoiceStudio.Services;   // ExerciseProfileFactory, LocalizationService, EnhancedExercise

namespace FemVoice.Avalonia.ViewModels;

/// <summary>
/// Read-only, display-only projection of an exercise's <see cref="ExerciseTargetProfile"/> for the
/// runtime "Mål-profil" panel. Built from <see cref="ExerciseProfileFactory"/> (pure) + localized text
/// (LocalizationService). It surfaces target metadata only — it makes NO clinical decision, enforces
/// no gate, and changes no shared state or definitions. Falls back to the exercise's own targets when
/// no profile is mapped.
/// </summary>
public sealed class ExerciseRuntimeTargetProfileDisplay
{
    private ExerciseRuntimeTargetProfileDisplay() { }

    public bool HasProfile { get; private init; }
    public string ProfileType { get; private init; } = "—";
    public string ProfileStatusMessage { get; private init; } = "";
    public string TargetPitch { get; private init; } = "—";
    public double RequiredHoldSecondsValue { get; private init; }
    public string RequiredHoldSeconds { get; private init; } = "—";
    public string StabilityTarget { get; private init; } = "—";
    public string ResonanceTarget { get; private init; } = "—";
    public string VoiceSkillTargets { get; private init; } = "—";
    public string PurposeText { get; private init; } = "";
    public string FocusText { get; private init; } = "";
    public string SafetyText { get; private init; } = "";
    public string CommonMistakesText { get; private init; } = "";
    public bool HasCommonMistakes => !string.IsNullOrWhiteSpace(CommonMistakesText);

    public static ExerciseRuntimeTargetProfileDisplay From(EnhancedExercise exercise)
    {
        var type = ExerciseProfileMap.ForExerciseId(exercise.Id);
        if (type is null)
        {
            return new ExerciseRuntimeTargetProfileDisplay
            {
                HasProfile = false,
                ProfileType = FemVoice.Avalonia.Localization.Localized.Get("ExProf_NoProfile", "Ingen koblet profil"),
                ProfileStatusMessage = FemVoice.Avalonia.Localization.Localized.Get("ExProf_NoProfileMsg", "Ingen koblet målprofil for denne øvelsen — viser øvelsens egne mål."),
                TargetPitch = ExerciseDisplay.TargetPitch(exercise.TargetPitchMin, exercise.TargetPitchMax),
            };
        }

        ExerciseTargetProfile p = new ExerciseProfileFactory().CreateProfile(type.Value);

        var skills = new List<string>();
        if (p.UsesPitch) skills.Add(FemVoice.Avalonia.Localization.Localized.Get("Goal_Pitch", "Tonehøyde"));
        if (p.UsesResonance) skills.Add(FemVoice.Avalonia.Localization.Localized.Get("Goal_Resonance", "Resonans"));
        if (p.UsesStability) skills.Add(FemVoice.Avalonia.Localization.Localized.Get("Dash_StabilityLabel", "Stabilitet"));
        if (p.UsesIntensity) skills.Add(FemVoice.Avalonia.Localization.Localized.Get("Metric_Intensity", "Intensitet"));

        double pitchMin = p.MinPitch ?? exercise.TargetPitchMin;
        double pitchMax = p.MaxPitch ?? exercise.TargetPitchMax;

        return new ExerciseRuntimeTargetProfileDisplay
        {
            HasProfile = true,
            ProfileType = type.Value.ToString(),
            ProfileStatusMessage = string.Format(FemVoice.Avalonia.Localization.Localized.Get("ExProf_ProfileFormat", "Målprofil: {0}"), type.Value),
            TargetPitch = ExerciseDisplay.TargetPitch(pitchMin, pitchMax),
            RequiredHoldSecondsValue = p.RequiredHoldSeconds,
            RequiredHoldSeconds = p.RequiredHoldSeconds > 0 ? $"{p.RequiredHoldSeconds:F0} s" : "—",
            StabilityTarget = p.StabilityThreshold > 0 ? $"≥ {p.StabilityThreshold:F2}" : "—",
            ResonanceTarget = p.UsesResonance && p.TargetResonanceMax > 0
                ? $"{p.TargetResonanceMin:F0}–{p.TargetResonanceMax:F0}"
                : "—",
            VoiceSkillTargets = skills.Count > 0 ? string.Join(", ", skills) : "—",
            PurposeText = Loc(p.ClinicalPurposeKey, "Mål: jevn, behagelig tone i målområdet."),
            FocusText = Loc(p.PhysicalFocusKey, "Fokus: avslappet hals og pust."),
            SafetyText = Loc(p.SafetyInfoKey, "Stopp ved ubehag — helse og sikkerhet går foran tonehøyde."),
            CommonMistakesText = Loc(p.CommonMistakesKey, ""),
        };
    }

    /// <summary>
    /// Resolves the (pure) <see cref="ExerciseTargetProfile"/> for an exercise via the Id→type map +
    /// <see cref="ExerciseProfileFactory"/>, or <c>null</c> when no profile is mapped. Read-only; used to
    /// drive the VM-local, display-only coordinator readout. Makes no clinical decision, persists nothing.
    /// </summary>
    public static ExerciseTargetProfile? ResolveProfile(EnhancedExercise exercise)
    {
        var type = ExerciseProfileMap.ForExerciseId(exercise.Id);
        return type is null ? null : new ExerciseProfileFactory().CreateProfile(type.Value);
    }

    // Resolve a localization key to text; fall back when the key is null/missing (returns the key).
    private static string Loc(string? key, string fallback)
    {
        if (string.IsNullOrWhiteSpace(key)) return fallback;
        var text = LocalizationService.Instance[key];
        return string.IsNullOrWhiteSpace(text) || text == key ? fallback : text;
    }
}
