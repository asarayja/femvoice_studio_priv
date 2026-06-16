namespace FemVoiceStudio.Subsystems.Analysis
{
    /// <summary>
    /// Resonance category classification.
    /// Relocated from Subsystems/Analysis/IAnalysisSubsystem.cs into FemVoice.Core during the Linux
    /// portable-core split, because live Models (TrainingSession, Feedback) depend on this enum.
    /// Namespace preserved so all existing references resolve unchanged. The rest of the (dead)
    /// IAnalysisSubsystem.cs stays in the WPF project.
    /// </summary>
    public enum ResonanceCategory
    {
        Unknown = 0,
        Back = 1,      // Masculine
        Neutral = 2,
        Forward = 3    // Feminine
    }
}
