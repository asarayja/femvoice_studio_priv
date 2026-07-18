using System.Collections.Generic;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FemVoiceStudio.Models;               // ExerciseTargetProfile, ExerciseProfileType, ManualOverrideRequest/Kind/Result, VoiceStyleGoal, RecoverySeverity
using FemVoiceStudio.Services;             // ManualOverrideEngine, ExerciseProfileFactory
using FemVoice.Avalonia.Localization;

namespace FemVoice.Avalonia.ViewModels;

/// <summary>
/// SAFETY-CRITICAL Manual Override panel, ported read-only from the WPF ManualOverrideWindow. A professional enters
/// an INTENDED exercise profile; this runs the FROZEN two-stage safety/recovery clamp (<see cref="ManualOverrideEngine"/>,
/// used verbatim — no clinical/safety logic changed) and presents ONLY the CLAMPED outcome — the raw intent is never
/// echoed back, exactly as WPF. This Avalonia port is DISPLAY-ONLY: it evaluates and shows the clamp result but does
/// NOT persist (ManualOverridesStore) or APPLY the override — the write/apply step is intentionally deferred pending
/// explicit clinical sign-off. Preserving the safety invariant, it can only ever make a profile MORE conservative.
/// </summary>
public partial class ManualOverridePanelViewModel : ObservableObject
{
    private readonly ManualOverrideEngine _engine = new();
    private readonly ExerciseProfileFactory _factory = new();

    public ManualOverridePanelViewModel(System.Action? onBack = null)
    {
        BackCommand = new RelayCommand(() => onBack?.Invoke());
        // Seed the intended fields from the default baseline so the form starts valid + realistic.
        var baseline = _factory.CreateProfile(BaselineProfileType);
        _intendedResonanceMin = baseline.TargetResonanceMin;
        _intendedResonanceMax = baseline.TargetResonanceMax;
        _intendedStabilityThreshold = baseline.StabilityThreshold;
        _intendedRequiredHoldSeconds = baseline.RequiredHoldSeconds;
        Evaluate();   // show an initial (unclamped) baseline result
    }

    public IRelayCommand BackCommand { get; }

    // ── Inputs ───────────────────────────────────────────────────────────────────────────────────────────────
    public IReadOnlyList<ExerciseProfileType> ProfileTypes { get; } =
        new[] { ExerciseProfileType.ResonanceHumming, ExerciseProfileType.ResonanceVowels,
                ExerciseProfileType.CoordinatedGlideUp, ExerciseProfileType.StabilityTraining };
    public IReadOnlyList<VoiceStyleGoal> StyleGoals { get; } =
        new[] { VoiceStyleGoal.Feminine, VoiceStyleGoal.Androgynous, VoiceStyleGoal.DarkFeminine };
    public IReadOnlyList<RecoverySeverity> RecoverySeverities { get; } =
        new[] { RecoverySeverity.None, RecoverySeverity.Watch, RecoverySeverity.Recommend, RecoverySeverity.Urgent };

    [ObservableProperty] private ExerciseProfileType _baselineProfileType = ExerciseProfileType.ResonanceHumming;
    [ObservableProperty] private VoiceStyleGoal _styleGoal = VoiceStyleGoal.Feminine;
    [ObservableProperty] private bool _simulateGateBlocked;
    [ObservableProperty] private RecoverySeverity _recoverySeverity = RecoverySeverity.None;
    [ObservableProperty] private double _intendedResonanceMin;
    [ObservableProperty] private double _intendedResonanceMax;
    [ObservableProperty] private double _intendedStabilityThreshold;
    [ObservableProperty] private double _intendedRequiredHoldSeconds;

    // ── Clamped outcome (the ONLY thing shown — never the raw intent) ────────────────────────────────────────
    [ObservableProperty] private bool _wasApplied;
    [ObservableProperty] private bool _wasClamped;
    [ObservableProperty] private string _blockedReasonCode = "";
    [ObservableProperty] private string _appliedResonance = "—";
    [ObservableProperty] private string _appliedStability = "—";
    [ObservableProperty] private string _appliedHold = "—";
    [ObservableProperty] private string _outcomeText = "";

    // ── Captions ─────────────────────────────────────────────────────────────────────────────────────────────
    public string Title => Localized.Get("Override_Panel_Title", "Manuell overstyring (sikkerhetsklamp)");
    public string BackLabel => Localized.Get("Common_Back", "Tilbake");
    public string EvaluateLabel => Localized.Get("Override_Evaluate", "Kjør sikkerhetsklamp");
    public string Intro => Localized.Get("Override_Intro",
        "En fagperson kan be om en overstyring av en øvelsesprofil. Den to-trinns sikkerhets-/restitusjonsklampen " +
        "kjøres, og KUN det klampede utfallet vises — den rå forespørselen anvendes eller vises aldri. Klampen kan " +
        "bare gjøre profilen mer konservativ.");
    public string SafetyNote => Localized.Get("Override_SafetyNote",
        "Kun visning: klampen beregnes og vises, men lagres ikke og anvendes ikke (skrive-/anvend-steget er utsatt " +
        "til eksplisitt klinisk godkjenning). Ingen klinisk/sikkerhetslogikk er endret — den frosne Core-motoren brukes uendret.");

    // Re-run the clamp whenever an input changes so the UI always reflects the current clamped outcome.
    partial void OnBaselineProfileTypeChanged(ExerciseProfileType value) => Evaluate();
    partial void OnStyleGoalChanged(VoiceStyleGoal value) => Evaluate();
    partial void OnSimulateGateBlockedChanged(bool value) => Evaluate();
    partial void OnRecoverySeverityChanged(RecoverySeverity value) => Evaluate();
    partial void OnIntendedResonanceMinChanged(double value) => Evaluate();
    partial void OnIntendedResonanceMaxChanged(double value) => Evaluate();
    partial void OnIntendedStabilityThresholdChanged(double value) => Evaluate();
    partial void OnIntendedRequiredHoldSecondsChanged(double value) => Evaluate();

    [RelayCommand]
    private void Evaluate()
    {
        try
        {
            var baseline = _factory.CreateProfile(BaselineProfileType);
            var intended = BuildIntendedProfile(baseline);

            var request = new ManualOverrideRequest
            {
                OverrideKind = ManualOverrideKind.ExerciseReco,
                UserId = 1,
                IntendedProfile = intended,
                ReasonCode = "AVALONIA_PREVIEW",
                ActorRole = "clinician",
                RequestedAt = System.DateTime.UtcNow,
            };

            // Run the FROZEN clamp. gateBlocked/severity are the already-evaluated safety signals (here chosen by the
            // clinician to preview the clamp). The result carries ONLY the clamped profile — we display that alone.
            ManualOverrideResult result = _engine.Evaluate(request, baseline, SimulateGateBlocked, RecoverySeverity, StyleGoal);

            WasApplied = result.WasApplied;
            WasClamped = result.WasClamped;
            BlockedReasonCode = result.BlockedReasonCode ?? "";

            // The applied values come from the CLAMPED profile (or the baseline when nothing was applied) — NEVER
            // from the raw intended profile, mirroring the WPF safety rule "the UI never echoes the raw intent".
            var shown = result.AppliedProfile ?? baseline;
            AppliedResonance = $"{shown.TargetResonanceMin.ToString("F0", CultureInfo.InvariantCulture)}–{shown.TargetResonanceMax.ToString("F0", CultureInfo.InvariantCulture)}";
            AppliedStability = shown.StabilityThreshold.ToString("0.00", CultureInfo.InvariantCulture);
            AppliedHold = shown.RequiredHoldSeconds.ToString("F0", CultureInfo.InvariantCulture) + " s";

            OutcomeText = !result.WasApplied
                ? Localized.Get("Override_NotApplied", "Ikke anvendt") + (BlockedReasonCode.Length > 0 ? $" ({BlockedReasonCode})" : "")
                : result.WasClamped
                    ? Localized.Get("Override_Clamped", "Klampet til et mer konservativt utfall (sikkerhet).")
                    : Localized.Get("Override_Unchanged", "Innenfor trygge grenser — ingen klamping nødvendig.");
        }
        catch (System.Exception ex)
        {
            WasApplied = false;
            OutcomeText = Localized.Get("Override_Error", "Kunne ikke beregne klamp: ") + ex.Message;
        }
    }

    // Faithful copy of WPF BuildIntendedProfile: baseline flags/keys/pitch preserved, intended requirement values
    // overlaid, so it is a valid, complete profile to hand to the clamp.
    private ExerciseTargetProfile BuildIntendedProfile(ExerciseTargetProfile baseline) => new()
    {
        UsesResonance = baseline.UsesResonance,
        UsesPitch = baseline.UsesPitch,
        UsesStability = baseline.UsesStability,
        UsesIntensity = baseline.UsesIntensity,
        ClinicalPurposeKey = baseline.ClinicalPurposeKey,
        PhysicalFocusKey = baseline.PhysicalFocusKey,
        CommonMistakesKey = baseline.CommonMistakesKey,
        SafetyInfoKey = baseline.SafetyInfoKey,
        FeedbackModeKey = baseline.FeedbackModeKey,
        ThresholdStrategyKey = baseline.ThresholdStrategyKey,
        IndicatorPackageSummaryKey = baseline.IndicatorPackageSummaryKey,
        MinPitch = baseline.MinPitch,
        MaxPitch = baseline.MaxPitch,
        TargetResonanceMin = IntendedResonanceMin,
        TargetResonanceMax = IntendedResonanceMax,
        StabilityThreshold = IntendedStabilityThreshold,
        RequiredHoldSeconds = IntendedRequiredHoldSeconds,
    };
}
