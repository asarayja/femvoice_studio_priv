using System;
using FemVoiceStudio.Models;   // ExerciseLiveState

namespace FemVoice.Avalonia.ViewModels;

/// <summary>
/// Immutable, DISPLAY-ONLY snapshot of the (parameterless, VM-local) ExerciseIntelligenceCoordinator's
/// in-memory state, shown alongside the runtime's own derived hold for comparison. The coordinator is
/// driven read-only via UpdateMetrics; nothing here is persisted, gated, scored, or enforced. The
/// safety-lock value is surfaced as read-only text and is explicitly NOT acted upon.
/// </summary>
public sealed class ExerciseCoordinatorReadoutDisplay
{
    public bool IsCoordinatorActive { get; init; }
    public double CoordinatorHoldProgressPercent { get; init; }
    public double CoordinatorHoldSeconds { get; init; }
    public string CoordinatorStatusText { get; init; } = "—";
    public string CoordinatorSafetyLockDisplay { get; init; } = "Sikkerhetslås: — (veiledende)";
    public string CoordinatorGuidanceText { get; init; } = "";
    public string CoordinatorRawStateSummary { get; init; } = "";
    public double DerivedHoldProgressPercent { get; init; }
    public double DerivedHoldSeconds { get; init; }
    public string HoldDifferenceDisplay { get; init; } = "—";
    public string ReadoutMode { get; init; } = "Koordinator-readout (veiledende)";

    public static ExerciseCoordinatorReadoutDisplay Inactive() =>
        new() { IsCoordinatorActive = false, CoordinatorStatusText = "Inaktiv" };

    /// <param name="active">coordinator IsExerciseActive.</param>
    /// <param name="coordHoldFraction">coordinator GetHoldProgress() (0–1).</param>
    /// <param name="holdTargetSeconds">the display-only hold target (profile RequiredHoldSeconds).</param>
    /// <param name="live">latest ExerciseLiveState from the coordinator event (may be null).</param>
    /// <param name="derivedHoldSeconds">the runtime's own in-VM derived hold seconds.</param>
    public static ExerciseCoordinatorReadoutDisplay From(
        bool active, double coordHoldFraction, double holdTargetSeconds,
        ExerciseLiveState? live, double derivedHoldSeconds)
    {
        double coordSeconds = coordHoldFraction * holdTargetSeconds;
        double derivedPct = holdTargetSeconds > 0 ? derivedHoldSeconds / holdTargetSeconds * 100.0 : 0;
        bool locked = live?.IsSafetyLocked ?? false;

        string status = live is null
            ? "—"
            : live.IsHoldingCorrectly ? "Holder riktig"
            : live.IsInComfortZone ? "I komfortsone"
            : "Utenfor mål";

        return new ExerciseCoordinatorReadoutDisplay
        {
            IsCoordinatorActive = active,
            CoordinatorHoldProgressPercent = Math.Round(coordHoldFraction * 100.0, 0),
            CoordinatorHoldSeconds = Math.Round(coordSeconds, 1),
            CoordinatorStatusText = status,
            CoordinatorSafetyLockDisplay = $"Sikkerhetslås: {(locked ? "PÅ" : "AV")} (veiledende)",
            CoordinatorGuidanceText = live is null
                ? "Koordinator starter …"
                : live.IsHoldingCorrectly ? "Koordinator: god holdetilstand." : "Koordinator: juster mot målet.",
            CoordinatorRawStateSummary = live is null
                ? "(ingen live-state ennå)"
                : $"hold={live.HoldProgress:F2} inZone={live.IsInComfortZone} holding={live.IsHoldingCorrectly} " +
                  $"locked={live.IsSafetyLocked} elapsed={live.SessionElapsedSeconds}s quality={live.Quality}",
            DerivedHoldProgressPercent = Math.Round(derivedPct, 0),
            DerivedHoldSeconds = Math.Round(derivedHoldSeconds, 1),
            HoldDifferenceDisplay = $"{coordSeconds - derivedHoldSeconds:+0.0;-0.0;0.0} s (koordinator − avledet)",
            ReadoutMode = "Koordinator-readout (veiledende)",
        };
    }
}
