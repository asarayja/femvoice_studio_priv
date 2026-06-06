namespace FemVoiceStudio.Models
{
    /// <summary>
    /// Progression decision made after each session evaluation.
    /// Only ONE main direction per session - no hybrid modes.
    /// </summary>
    public enum ProgressionDecision
    {
        /// <summary>
        /// Increase pitch comfort zone by 2-5 Hz
        /// </summary>
        IncreasePitchZone,
        
        /// <summary>
        /// Focus on resonance refinement (stricter resonance requirements)
        /// </summary>
        FocusResonance,
        
        /// <summary>
        /// Expand prosody/intonation range while holding pitch/resonance constant
        /// </summary>
        ExpandProsody,
        
        /// <summary>
        /// Maintain current level - no active progression
        /// </summary>
        Maintain,
        
        /// <summary>
        /// Recovery mode - reduced load, focus on rest and consolidation
        /// </summary>
        Recover
    }
    
    /// <summary>
    /// Current progression mode determining training focus.
    /// Based on clinical progression: Resonance → Pitch → Prosody → Endurance → Maintenance
    /// </summary>
    public enum ProgressionMode
    {
        /// <summary>
        /// Active pitch progression - increase comfort zone 2-5 Hz per cycle
        /// Gates must be open: ResonanceScore > 60, VoiceHealthScore > 70, Stable, ≥3 sessions
        /// </summary>
        PitchProgression,
        
        /// <summary>
        /// Resonance refinement - stricter resonance requirements (target +5-10)
        /// No pitch increase. Focus: deeper resonance stability.
        /// </summary>
        ResonanceRefinement,
        
        /// <summary>
        /// Prosody expansion - expand intonation range +10-30%
        /// Pitch and resonance held constant. Focus: natural flow and variation.
        /// </summary>
        ProsodyExpansion,
        
        /// <summary>
        /// Health recovery - reduced load (easier exercises)
        /// Consolidation of current level. Focus: restitution and sustainability.
        /// </summary>
        HealthRecovery,
        
        /// <summary>
        /// Maintenance - no active progression
        /// Preserve current capacity. Focus: consistency and automation.
        /// </summary>
        Maintenance
    }
}
