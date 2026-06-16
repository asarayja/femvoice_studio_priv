using System;
using System.Collections.Generic;

namespace FemVoiceStudio.Models.VoiceLoad
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Sprint F — Predictive Voice Intelligence (Live Voice Load Monitor).
    //
    // PURE domain models with NO dependency on WPF, audio, or any RC-0/clinical engine.
    // The Sprint F engine only READS existing signals (mapped into VoiceLoadInputs by the
    // caller) and produces conservative, trend-aware, NON-MEDICAL practice guidance.
    //
    // It does not diagnose, does not claim injury/dehydration/pathology, and never
    // overrides a safety lock — it only RECOMMENDS pauses/water/lower intensity/stopping.
    // All user-facing text is delivered as localization KEYS (nb source in Strings.resx);
    // the engine itself contains no user-visible strings.
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Coarse cumulative-load band. Internal score 0–100 is not shown by default.</summary>
    public enum VoiceLoadBand
    {
        InsufficientData,
        Low,                // 0–30
        Moderate,           // 31–60
        High,               // 61–80
        PauseRecommended    // 81–100
    }

    /// <summary>Direction of the within-session load/stability trend.</summary>
    public enum VoiceLoadTrendDirection
    {
        Unknown,
        Improving,
        Stable,
        Worsening
    }

    /// <summary>How urgently a pause is suggested. Never forces; never overrides safety.</summary>
    public enum PauseRecommendationLevel
    {
        None,
        Soon,
        Now,
        EndSession
    }

    /// <summary>Safe hydration CONTEXT — never a dehydration claim.</summary>
    public enum HydrationContextLevel
    {
        None,
        GentleReminder,
        RepeatedLoadContext
    }

    /// <summary>Practice-readiness after a pause. Never a medical-recovery claim.</summary>
    public enum RecoveryReadiness
    {
        InsufficientData,
        Ready,
        ContinueGently,
        WaitLonger,
        EndForToday
    }

    /// <summary>How the voice changed across the session.</summary>
    public enum SessionTrendCategory
    {
        InsufficientData,
        ImprovingStability,
        Stable,
        MildDecline,
        ClearDecline,
        Variable
    }

    /// <summary>User-facing gentle-coach message category (maps to a localization key).</summary>
    public enum GentleCoachCategory
    {
        None,
        ContinueCalmly,
        PauseSoon,
        PauseNow,
        LowerIntensity,
        HydrationGentle,
        EndSession,
        RecoveryWait,
        RecoveryReady,
        InsufficientData
    }

    /// <summary>
    /// Immutable per-tick snapshot the engine consumes. The caller builds ONE per Observe()
    /// call from the in-scope ExerciseLiveState / VocalHealthDecision / HydrationAdvice — the
    /// engine keeps no live references and performs no signal processing.
    /// HealthStateRank decouples the pure model from the Services-layer HealthSafetyState enum:
    /// 0=Normal, 1=Caution, 2=Restrict, 3=Lock.
    /// </summary>
    public sealed record VoiceLoadInputs
    {
        public DateTime Timestamp { get; init; }
        public int SessionElapsedSeconds { get; init; }
        public double StabilityScore { get; init; }      // 0–1
        public double ResonanceScore { get; init; }      // 0–1 (only meaningful when UsesResonanceSignal)
        public bool UsesResonanceSignal { get; init; }
        public double PitchHz { get; init; }             // <= 0 ⇒ unvoiced / dropout this tick
        public double Intensity { get; init; }           // 0–1 RMS; 0 ⇒ not measured this tick
        public bool IsHoldingCorrectly { get; init; }
        public bool IsInComfortZone { get; init; }
        public bool IsSafetyLocked { get; init; }
        public double FatigueScore { get; init; }        // 0–1
        public bool FatigueDetected { get; init; }
        public double StrainScore { get; init; }         // 0–1
        public bool StrainDetected { get; init; }
        public bool PauseRecommendedByHealth { get; init; }
        public bool HydrationSuggestedByHealth { get; init; }
        public bool HydrationSuggestedByAdvisor { get; init; }
        public int HealthStateRank { get; init; }        // 0 Normal .. 3 Lock
    }

    /// <summary>Live cumulative-load state published by the monitor each tick.</summary>
    public sealed record VoiceLoadState
    {
        public int VoiceLoadScore { get; init; }
        public VoiceLoadBand VoiceLoadBand { get; init; } = VoiceLoadBand.InsufficientData;
        public double ActiveVoicedSeconds { get; init; }
        public double TimeSinceLastPauseSeconds { get; init; }
        public int ExerciseCountInSession { get; init; }
        public VoiceLoadTrendDirection TrendDirection { get; init; } = VoiceLoadTrendDirection.Unknown;
        public IReadOnlyList<string> PrimaryLoadDrivers { get; init; } = Array.Empty<string>();
        public double Confidence { get; init; }          // 0–1
        public bool IsDataSufficient { get; init; }
    }

    /// <summary>A single gentle-coach message as a localization key + its category.</summary>
    public sealed record GentleCoachMessage
    {
        public GentleCoachCategory Category { get; init; }
        public string LocalizationKey { get; init; } = string.Empty;
    }

    /// <summary>The per-tick recommendation bundle (pause + hydration + optional message).</summary>
    public sealed record VoiceLoadRecommendation
    {
        public PauseRecommendationLevel Pause { get; init; } = PauseRecommendationLevel.None;
        public HydrationContextLevel Hydration { get; init; } = HydrationContextLevel.None;
        public GentleCoachMessage? Message { get; init; }   // null ⇒ no message this tick (anti-spam)
        public string? SuppressionReason { get; init; }     // why Message is null, for evidence
        public IReadOnlyList<string> Reasons { get; init; } = Array.Empty<string>();
    }

    /// <summary>How the voice changed across the session (assembled at session end).</summary>
    public sealed record SessionTrendSummary
    {
        public SessionTrendCategory TrendCategory { get; init; } = SessionTrendCategory.InsufficientData;
        public double TrendConfidence { get; init; }
        public IReadOnlyList<string> MainChanges { get; init; } = Array.Empty<string>();
        public string RecommendedAdjustmentKey { get; init; } = string.Empty;
    }

    /// <summary>Practice-readiness result after a pause (before/after comparison).</summary>
    public sealed record RecoveryReadinessResult
    {
        public RecoveryReadiness Readiness { get; init; } = RecoveryReadiness.InsufficientData;
        public string MessageKey { get; init; } = string.Empty;
        public IReadOnlyList<string> Reasons { get; init; } = Array.Empty<string>();
    }

    /// <summary>
    /// Evidence record explaining WHY Sprint F advice was shown/suppressed. No free-text,
    /// no medical claims — bands/levels are enum names, drivers are stable codes.
    /// </summary>
    public sealed record VoiceLoadEvidence
    {
        public int VoiceLoadScore { get; init; }
        public string VoiceLoadBand { get; init; } = string.Empty;
        public string PauseRecommendationLevel { get; init; } = string.Empty;
        public string HydrationContextLevel { get; init; } = string.Empty;
        public string RecoveryReadiness { get; init; } = string.Empty;
        public string SessionTrend { get; init; } = string.Empty;
        public IReadOnlyList<string> VoiceLoadDrivers { get; init; } = Array.Empty<string>();
        public string? MessageShownCategory { get; init; }
        public bool MessageSuppressed { get; init; }
        public string? SuppressionReason { get; init; }
        public double TimeSinceLastPauseSeconds { get; init; }
        public double ActiveVoicedSeconds { get; init; }
        public int ExerciseCountInSession { get; init; }
        public double TrendConfidence { get; init; }
    }
}
