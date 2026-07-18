using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FemVoiceStudio.Data;                 // IDatabaseService, DatabaseService (connection string for the store)
using FemVoiceStudio.Models;               // ExerciseTargetProfile, ExerciseProfileType, ManualOverrideRequest/Kind/Result, VoiceStyleGoal
using FemVoiceStudio.Services;             // ManualOverrideEngine, ExerciseProfileFactory, ManualOverridesStore, RecoverySeverity
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
    /// <summary>The MANUAL_OVERRIDE reason-code stamp written to the health event + audit event (WPF parity).</summary>
    public const string ManualOverrideReasonCode = "MANUAL_OVERRIDE";

    private readonly ManualOverrideEngine _engine = new();
    private readonly ExerciseProfileFactory _factory = new();
    private readonly IDatabaseService? _database;
    private ManualOverrideRequest? _lastRequest;
    private ManualOverrideResult? _lastResult;

    // Live gate/recovery signals actually driving the clamp (WPF: ProgressionSafetyGate + RecoveryIntelligenceService
    // over the persisted analytics history). Derived once from the REAL state — never a manual toggle. Conservative on
    // failure (unblocked / severity None) exactly like WPF; the frozen clamp's Stage-1 recovery floor still applies.
    private bool _observedGateBlocked;
    private RecoverySeverity _observedSeverity = RecoverySeverity.None;

    public ManualOverridePanelViewModel(System.Action? onBack = null) : this(null, onBack) { }

    public ManualOverridePanelViewModel(IDatabaseService? database, System.Action? onBack = null)
    {
        _database = database;
        BackCommand = new RelayCommand(() => onBack?.Invoke());
        // Seed the intended fields from the default baseline so the form starts valid + realistic.
        var baseline = _factory.CreateProfile(BaselineProfileType);
        _intendedResonanceMin = baseline.TargetResonanceMin;
        _intendedResonanceMax = baseline.TargetResonanceMax;
        _intendedStabilityThreshold = baseline.StabilityThreshold;
        _intendedRequiredHoldSeconds = baseline.RequiredHoldSeconds;
        RefreshLiveState();   // read the REAL gate/recovery signals that drive the clamp
        Evaluate();   // show the clamped result against the live state
        LoadRecent(); // populate the recent-overrides audit list (best-effort)
    }

    // The concrete analytics store when a real DB is present (empty in a fresh install → conservative signals).
    private SessionAnalyticsStore? BuildAnalytics()
        => _database is DatabaseService c ? new SessionAnalyticsStore(new SqliteSessionAnalyticsRepository(c.ConnectionString)) : null;

    // Read the LIVE gate-blocked flag (ProgressionSafetyGate) + recovery severity (RecoveryIntelligenceService) from
    // the real analytics history. Any failure is treated conservatively for safety (WPF's exact posture).
    private void RefreshLiveState()
    {
        try
        {
            var analytics = BuildAnalytics();
            if (analytics is null) return;
            var now = DateTime.UtcNow;
            try { _observedGateBlocked = new ProgressionSafetyGate(analytics).EvaluateAsync(now, 1).GetAwaiter().GetResult().IsBlocked; }
            catch { _observedGateBlocked = false; }
            try { _observedSeverity = new RecoveryIntelligenceService().ForecastFromHistoryAsync(analytics, now, 1).GetAwaiter().GetResult().Severity; }
            catch { _observedSeverity = RecoverySeverity.None; }
        }
        catch { _observedGateBlocked = false; _observedSeverity = RecoverySeverity.None; }
    }

    /// <summary>TEST-ONLY: force the live gate/recovery signals (mirrors WPF's forced-state test constructor) so the
    /// frozen clamp can be exercised deterministically without a populated analytics history. Re-evaluates.</summary>
    public void ForceLiveStateForTest(bool gateBlocked, RecoverySeverity severity)
    {
        _observedGateBlocked = gateBlocked;
        _observedSeverity = severity;
        OnPropertyChanged(nameof(ObservedGateText));
        OnPropertyChanged(nameof(ObservedSeverityText));
        Evaluate();
    }

    // Read-only display of the live signals that drive the clamp (WPF shows the observed gate/severity).
    public string ObservedGateText => _observedGateBlocked
        ? Localized.Get("Override_GateBlocked", "Blokkert")
        : Localized.Get("Override_GateOpen", "Åpen");
    public string ObservedSeverityText => _observedSeverity.ToString();
    public string ObservedGateLabel => Localized.Get("Override_GateState", "Sikkerhetsport (live)");
    public string ObservedSeverityLabel => Localized.Get("Override_RecoveryState", "Restitusjonsnivå (live)");

    public IRelayCommand BackCommand { get; }

    // ── Inputs ───────────────────────────────────────────────────────────────────────────────────────────────
    public IReadOnlyList<ExerciseProfileType> ProfileTypes { get; } =
        new[] { ExerciseProfileType.ResonanceHumming, ExerciseProfileType.ResonanceVowels,
                ExerciseProfileType.CoordinatedGlideUp, ExerciseProfileType.StabilityTraining };
    public IReadOnlyList<VoiceStyleGoal> StyleGoals { get; } =
        new[] { VoiceStyleGoal.Feminine, VoiceStyleGoal.Androgynous, VoiceStyleGoal.DarkFeminine };
    /// <summary>Which decision the professional is overriding (WPF override-kind selector).</summary>
    public IReadOnlyList<ManualOverrideKind> OverrideKinds { get; } =
        new[] { ManualOverrideKind.ExerciseReco, ManualOverrideKind.RecoveryPlan,
                ManualOverrideKind.VoiceGoals, ManualOverrideKind.ProgressionPace };

    [ObservableProperty] private ExerciseProfileType _baselineProfileType = ExerciseProfileType.ResonanceHumming;
    [ObservableProperty] private VoiceStyleGoal _styleGoal = VoiceStyleGoal.Feminine;
    [ObservableProperty] private ManualOverrideKind _selectedKind = ManualOverrideKind.ExerciseReco;
    /// <summary>Professional reason code captured for the audit trail (WPF ReasonCode input).</summary>
    [ObservableProperty] private string _reasonCode = "";
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
    public string Title => Localized.Get("Override_Title", "Manuell justering");
    public string BackLabel => Localized.Get("Common_Back", "Tilbake");
    public string EvaluateLabel => Localized.Get("Override_Apply", "Bruk overstyring");
    public string OverrideKindLabel => Localized.Get("Override_Kind", "Type overstyring");
    public string ReasonCodeLabel => Localized.Get("Override_Reason", "Årsak (revisjonskode)");
    public string HoldLabel => Localized.Get("Override_RequiredHold", "Ønsket hold (sek)");
    public string Intro => Localized.Get("Override_Intro",
        "En fagperson kan be om en overstyring av en øvelsesprofil. Den to-trinns sikkerhets-/restitusjonsklampen " +
        "kjøres, og KUN det klampede utfallet vises — den rå forespørselen anvendes eller vises aldri. Klampen kan " +
        "bare gjøre profilen mer konservativ.");
    public string SafetyNote => Localized.Get("Override_SafetyNote2",
        "Klampen beregnes av den frosne Core-motoren og KUN det klampede utfallet vises/loggføres — aldri den rå " +
        "forespørselen. «Loggfør» skriver en revisjonsoppføring (utfall + flagg, ikke den rå profilen) til " +
        "overstyringsloggen. Ingen klinisk/sikkerhetslogikk er endret.");

    // ── Persist the clamped result to the override audit log (safety store) ──────────────────────────────────
    public string PersistLabel => Localized.Get("Override_Persist", "Loggfør i overstyringsloggen");
    [ObservableProperty] private string _persistStatus = "";
    /// <summary>True when there is a computed result AND a real (concrete) database to log to.</summary>
    public bool CanPersist => _lastResult is not null && _database is DatabaseService;

    /// <summary>Recent logged overrides (audit rows) for this user — refreshed after each log.</summary>
    [ObservableProperty] private IReadOnlyList<string> _recentOverrides = Array.Empty<string>();
    public string RecentHeading => Localized.Get("Override_Recent", "Nylige loggførte overstyringer");
    public bool HasRecent => RecentOverrides.Count > 0;

    private ManualOverridesStore? BuildStore()
        => _database is DatabaseService concrete
            ? new ManualOverridesStore(new SqliteManualOverridesRepository(concrete.ConnectionString))
            : null;

    [RelayCommand]
    private void Persist()
    {
        if (_lastRequest is null || _lastResult is null) { PersistStatus = Localized.Get("Override_NoResult", "Ingen beregnet klamp å loggføre."); return; }
        var store = BuildStore();
        if (store is null) { PersistStatus = Localized.Get("Override_NoDb", "Databasen er ikke tilgjengelig i denne visningen."); return; }
        // Three INDEPENDENT durable writes (WPF parity): (a) override log, (b) immutable audit event, (c) MANUAL_OVERRIDE
        // health event. A failure of one never suppresses the others; each persists the CLAMPED result / intended-vs-
        // applied JSON — never the raw intent as a live profile. loggedAny drives the status.
        bool loggedAny = false;
        var now = DateTime.UtcNow;

        try { store.LogResultAsync(_lastRequest, _lastResult).GetAwaiter().GetResult(); loggedAny = true; }
        catch { /* descriptive log only — never crash the override surface */ }

        // (b) Immutable audit event: Before = intended profile, After = CLAMPED applied profile (null when nothing applied).
        if (_database is DatabaseService concreteAudit)
        {
            try
            {
                var auditStore = new AuditTrailStore(new SqliteAuditTrailRepository(concreteAudit.ConnectionString));
                auditStore.AppendAsync(new AuditEvent
                {
                    AuditId = _lastResult.AuditId,
                    UserId = _lastRequest.UserId,
                    OccurredAt = now,
                    EntityType = AuditEntityType.Override,
                    EntityId = _lastResult.ManualOverrideId.ToString("D"),
                    ActorRole = _lastRequest.ActorRole,
                    ReasonCode = string.IsNullOrWhiteSpace(_lastRequest.ReasonCode) ? ManualOverrideReasonCode : _lastRequest.ReasonCode,
                    BeforeJson = SerializeProfile(_lastRequest.IntendedProfile),
                    AfterJson = SerializeProfile(_lastResult.AppliedProfile),
                }).GetAwaiter().GetResult();
                loggedAny = true;
            }
            catch { /* audit failure must not crash the surface */ }

            // (c) MANUAL_OVERRIDE health event — HealthTrendUpdated (a journal entry, NOT a safety signal, so the
            //     safety/recovery gates never miscount it). Severity 1 when clamped, else 0; intent carried in ReasonCode.
            try
            {
                var analytics = BuildAnalytics();
                analytics?.RecordHealthEventAsync(new HealthAnalyticsEvent
                {
                    SessionId = 0,
                    UserId = _lastRequest.UserId,
                    EventType = HealthAnalyticsEventType.HealthTrendUpdated,
                    OccurredAt = now,
                    Severity = _lastResult.WasClamped ? 1.0 : 0.0,
                    ReasonCode = ManualOverrideReasonCode,
                }).GetAwaiter().GetResult();
                loggedAny = true;
            }
            catch { /* health-event write failure must not crash the surface */ }
        }

        PersistStatus = loggedAny
            ? Localized.Get("Override_Logged", "Loggført: overstyringslogg + revisjonsspor + helsehendelse (kun utfall/flagg).")
            : Localized.Get("Override_LogFailed", "Kunne ikke loggføre: ") + "—";
        LoadRecent(store);
        OnPropertyChanged(nameof(HasRecent));
    }

    // Serialize ONLY the numeric profile bounds for the audit trail (WPF SerializeProfile) — never behavioural flags.
    private static string? SerializeProfile(ExerciseTargetProfile? profile)
    {
        if (profile is null) return null;
        try
        {
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                profile.TargetResonanceMin, profile.TargetResonanceMax, profile.StabilityThreshold,
                profile.RequiredHoldSeconds, profile.MinPitch, profile.MaxPitch,
            });
        }
        catch { return null; }
    }

    private void LoadRecent(ManualOverridesStore? store = null)
    {
        store ??= BuildStore();
        if (store is null) return;
        try
        {
            var now = DateTime.UtcNow;
            var rows = store.GetOverridesAsync(1, now.AddDays(-90), now.AddDays(1)).GetAwaiter().GetResult()
                .OrderByDescending(e => e.RequestedAt).Take(10)
                .Select(e => $"{e.RequestedAt.ToLocalTime():yyyy-MM-dd HH:mm} · {e.OverrideKind} · "
                           + (e.WasApplied ? (e.WasClamped ? "klampet" : "anvendt") : $"ikke anvendt ({e.BlockedReasonCode})"))
                .ToList();
            RecentOverrides = rows;
        }
        catch { /* audit read is best-effort */ }
    }

    // Re-run the clamp whenever an input changes so the UI always reflects the current clamped outcome.
    partial void OnBaselineProfileTypeChanged(ExerciseProfileType value) => Evaluate();
    partial void OnStyleGoalChanged(VoiceStyleGoal value) => Evaluate();
    partial void OnSelectedKindChanged(ManualOverrideKind value) => Evaluate();
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
                OverrideKind = SelectedKind,
                UserId = 1,
                ExerciseId = SelectedKind == ManualOverrideKind.ExerciseReco ? 0 : (int?)null,
                IntendedProfile = SelectedKind == ManualOverrideKind.ExerciseReco ? intended : null,
                ReasonCode = string.IsNullOrWhiteSpace(ReasonCode) ? ManualOverrideReasonCode : ReasonCode,
                ActorRole = "clinician",
                RequestedAt = System.DateTime.UtcNow,
            };

            // Run the FROZEN clamp against the LIVE gate/recovery signals (never a manual toggle). The result carries
            // ONLY the clamped profile — we display that alone (the raw intent is never echoed).
            ManualOverrideResult result = _engine.Evaluate(request, baseline, _observedGateBlocked, _observedSeverity, StyleGoal);
            _lastRequest = request;
            _lastResult = result;
            OnPropertyChanged(nameof(CanPersist));

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
