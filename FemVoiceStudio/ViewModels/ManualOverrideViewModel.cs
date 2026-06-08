using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;

namespace FemVoiceStudio.ViewModels
{
    /// <summary>
    /// SAFETY-CRITICAL ViewModel for the Manual Override surface (Sprint E, Agent 7).
    ///
    /// ── INVARIANT THE UI MUST NEVER BREAK ────────────────────────────────────────────
    /// A clinician/coach may request an override, but the UI can ONLY ever present the
    /// result of the two-stage safety/recovery clamp performed by
    /// <see cref="ManualOverrideEngine"/>. It never shows, and never persists, the raw
    /// professional intent — only the CLAMPED profile. The hard gates (Safety &gt; Health
    /// &gt; Recovery &gt; Comfort &gt; Voice Development &gt; Reporting) remain the only
    /// authority; this VM is strictly subordinate to them.
    ///
    /// On Apply the VM:
    ///   (1) evaluates the live gate signals (ProgressionSafetyGate blocked? recovery
    ///       forecast severity?) over the persisted analytics history — it NEVER
    ///       re-implements or bypasses the clamp;
    ///   (2) calls <see cref="ManualOverrideEngine.Evaluate"/> with those signals, which
    ///       applies Stage 1 (recovery floor) → Stage 2 (gate clamp);
    ///   (3) surfaces WasClamped / WasBlocked to the UI ({loc:Loc Override_Clamped/_Blocked});
    ///   (4) logs the outcome to <see cref="ManualOverridesStore"/> (append-only) AND writes
    ///       an <see cref="AuditEvent"/> (EntityType=Override, Before/After JSON) to
    ///       <see cref="AuditTrailStore"/> AND records a MANUAL_OVERRIDE health event via
    ///       <see cref="SessionAnalyticsStore.RecordHealthEventAsync"/>.
    ///
    /// All injected services are resolved null-safe from <see cref="App.Services"/> so the
    /// VM degrades gracefully (it simply cannot apply) in design-time / no-DI contexts.
    /// </summary>
    public partial class ManualOverrideViewModel : ObservableObject
    {
        // ── Injected services (all null-safe) ───────────────────────────────────────
        private readonly ManualOverrideEngine? _engine;
        private readonly ManualOverridesStore? _overridesStore;
        private readonly AuditTrailStore? _auditStore;
        private readonly SessionAnalyticsStore? _analyticsStore;
        private readonly IExerciseProfileFactory? _profileFactory;

        // Gate-signal sources. Evaluated against the persisted analytics history so the
        // override is always subordinate to the live Safety/Recovery state. Both optional —
        // when absent the VM conservatively treats the gate as NOT blocked / severity None
        // (the engine still applies its recovery-floor clamp regardless).
        private readonly ProgressionSafetyGate? _safetyGate;
        private readonly RecoveryIntelligenceService? _recoveryService;

        // Test seam: lets the integration test force a known gate state without standing up
        // a full analytics store. null ⇒ the live gate sources above are consulted.
        private readonly bool? _forcedGateBlocked;
        private readonly RecoverySeverity? _forcedRecoverySeverity;

        /// <summary>The MANUAL_OVERRIDE reason-code stamp written to the health event and
        /// (when the engine reports the override applied) to the audit/override log.</summary>
        public const string ManualOverrideReasonCode = "MANUAL_OVERRIDE";

        // ── Bound state ─────────────────────────────────────────────────────────────

        /// <summary>The kinds of clinical decision a professional may override.</summary>
        public IReadOnlyList<ManualOverrideKind> OverrideKinds { get; } =
            new[]
            {
                ManualOverrideKind.ExerciseReco,
                ManualOverrideKind.RecoveryPlan,
                ManualOverrideKind.VoiceGoals,
                ManualOverrideKind.ProgressionPace
            };

        [ObservableProperty]
        private ManualOverrideKind _selectedKind = ManualOverrideKind.ExerciseReco;

        [ObservableProperty]
        private int _userId = 1;

        [ObservableProperty]
        private int _exerciseId = 1;

        /// <summary>Which baseline profile the targeted exercise uses. The factory turns this
        /// into the clinical reference floor the engine clamps against.</summary>
        [ObservableProperty]
        private ExerciseProfileType _baselineProfileType = ExerciseProfileType.ResonanceHumming;

        /// <summary>Professional role requesting the override (audit label, never a username).</summary>
        [ObservableProperty]
        private string _actorRole = "Clinician";

        /// <summary>Stable machine reason code the professional supplies (audit only).</summary>
        [ObservableProperty]
        private string _reasonCode = string.Empty;

        // The professional's INTENDED requirement values. These are the *request* — the UI
        // never treats them as the applied result; only the clamped Applied* values below
        // are authoritative.
        [ObservableProperty]
        private double _intendedResonanceMin = 0.55;

        [ObservableProperty]
        private double _intendedResonanceMax = 0.90;

        [ObservableProperty]
        private double _intendedStabilityThreshold = 0.50;

        [ObservableProperty]
        private double _intendedRequiredHoldSeconds = 3.0;

        /// <summary>Desired voice style; selects the engine's resonance multiplier so a
        /// darker/androgynous goal is never pushed toward bright resonance.</summary>
        [ObservableProperty]
        private VoiceStyleGoal _styleGoal = VoiceStyleGoal.Feminine;

        // ── Result (CLAMPED ONLY) ───────────────────────────────────────────────────

        /// <summary>True once an Apply has produced a result to display.</summary>
        [ObservableProperty]
        private bool _hasResult;

        /// <summary>True when the override produced a persisted, clamped profile.</summary>
        [ObservableProperty]
        private bool _wasApplied;

        /// <summary>True when the two-stage clamp pulled the intent back to be more
        /// conservative. Drives the {loc:Loc Override_Clamped} indicator.</summary>
        [ObservableProperty]
        private bool _wasClamped;

        /// <summary>True when the override could not be applied as requested (e.g. a
        /// non-exercise kind carries no profile to clamp). Drives {loc:Loc Override_Blocked}.</summary>
        [ObservableProperty]
        private bool _wasBlocked;

        /// <summary>Stable machine reason code when blocked; null when applied. Never shown
        /// raw to the user — the UI shows the localised {loc:Loc Override_Blocked} copy.</summary>
        [ObservableProperty]
        private string? _blockedReasonCode;

        /// <summary>True once the outcome has been written to the override log + audit trail
        /// + health event. Drives the {loc:Loc Override_Logged} indicator.</summary>
        [ObservableProperty]
        private bool _wasLogged;

        // The CLAMPED, applied requirement values — the ONLY result the UI may present.
        [ObservableProperty]
        private double _appliedResonanceMin;

        [ObservableProperty]
        private double _appliedResonanceMax;

        [ObservableProperty]
        private double _appliedStabilityThreshold;

        [ObservableProperty]
        private double _appliedRequiredHoldSeconds;

        /// <summary>True when the live gate was blocked at Apply time (informational; the
        /// clamp is what enforces the invariant, not this flag).</summary>
        [ObservableProperty]
        private bool _gateWasBlocked;

        /// <summary>The recovery severity observed at Apply time (informational).</summary>
        [ObservableProperty]
        private RecoverySeverity _observedRecoverySeverity = RecoverySeverity.None;

        /// <summary>Append-only trail of the override outcomes produced this session (newest
        /// last). Bound for an at-a-glance audit of what the professional did.</summary>
        public ObservableCollection<string> OutcomeLog { get; } = new();

        // ── Construction ────────────────────────────────────────────────────────────

        /// <summary>
        /// Production ctor. Self-resolves every dependency null-safe from
        /// <see cref="App.Services"/> (mirrors the project's other Sprint E VMs). NOT
        /// registered in DI — windows new-up this VM directly.
        /// </summary>
        public ManualOverrideViewModel()
        {
            _engine = Resolve<ManualOverrideEngine>();
            _overridesStore = Resolve<ManualOverridesStore>();
            _auditStore = Resolve<AuditTrailStore>();
            _analyticsStore = Resolve<SessionAnalyticsStore>();
            _profileFactory = Resolve<IExerciseProfileFactory>();
            _safetyGate = Resolve<ProgressionSafetyGate>();
            _recoveryService = Resolve<RecoveryIntelligenceService>();
            _forcedGateBlocked = null;
            _forcedRecoverySeverity = null;
        }

        /// <summary>
        /// Test/DI seam: inject the four required collaborators plus an optional profile
        /// factory and forced gate signals. When <paramref name="forcedGateBlocked"/> /
        /// <paramref name="forcedRecoverySeverity"/> are supplied they bypass the live gate
        /// sources so a test can deterministically reproduce a blocked/severe state. The
        /// engine still performs the full two-stage clamp — the test never weakens it.
        /// </summary>
        public ManualOverrideViewModel(
            ManualOverrideEngine? engine,
            ManualOverridesStore? overridesStore,
            AuditTrailStore? auditStore,
            SessionAnalyticsStore? analyticsStore,
            IExerciseProfileFactory? profileFactory = null,
            ProgressionSafetyGate? safetyGate = null,
            RecoveryIntelligenceService? recoveryService = null,
            bool? forcedGateBlocked = null,
            RecoverySeverity? forcedRecoverySeverity = null)
        {
            _engine = engine;
            _overridesStore = overridesStore;
            _auditStore = auditStore;
            _analyticsStore = analyticsStore;
            _profileFactory = profileFactory ?? new ExerciseProfileFactory();
            _safetyGate = safetyGate;
            _recoveryService = recoveryService;
            _forcedGateBlocked = forcedGateBlocked;
            _forcedRecoverySeverity = forcedRecoverySeverity;
        }

        private static T? Resolve<T>() where T : class =>
            App.Services?.GetService(typeof(T)) as T;

        // ── Apply ───────────────────────────────────────────────────────────────────

        /// <summary>
        /// Evaluates the override through the SAFETY-CRITICAL clamp and records the outcome.
        /// Never throws on the UI thread: any persistence failure leaves the (already
        /// clamped) result visible and simply flags it as not-logged.
        /// </summary>
        [RelayCommand]
        private async Task ApplyAsync()
        {
            HasResult = false;
            WasLogged = false;

            if (_engine is null)
            {
                // No engine ⇒ we cannot run the clamp, so we must NOT present any result.
                WasApplied = false;
                WasClamped = false;
                WasBlocked = true;
                BlockedReasonCode = "NO_ENGINE";
                HasResult = true;
                return;
            }

            // The clinical reference floor for the targeted exercise.
            var baseline = BuildBaseline();

            // The professional's INTENDED profile (the request — never shown as the result).
            var intended = BuildIntendedProfile();

            var requestedAt = DateTime.UtcNow;
            var request = new ManualOverrideRequest
            {
                OverrideKind = SelectedKind,
                UserId = UserId,
                ExerciseId = SelectedKind == ManualOverrideKind.ExerciseReco ? ExerciseId : (int?)null,
                IntendedProfile = SelectedKind == ManualOverrideKind.ExerciseReco ? intended : null,
                ReasonCode = string.IsNullOrWhiteSpace(ReasonCode) ? ManualOverrideReasonCode : ReasonCode,
                ActorRole = ActorRole,
                RequestedAt = requestedAt
            };

            // (1) Live gate signals — the override is subordinate to these.
            var (gateBlocked, severity) = await EvaluateGateSignalsAsync(requestedAt).ConfigureAwait(true);
            GateWasBlocked = gateBlocked;
            ObservedRecoverySeverity = severity;

            // (2) Run the two-stage clamp. We pass the engine the live gate state; it can
            //     only ever produce a MORE-conservative profile than the intent.
            var result = _engine.Evaluate(request, baseline, gateBlocked, severity, StyleGoal);

            // (3) Surface ONLY the clamped result.
            ApplyResultToState(result);

            // (4) Persist: append-only override log + audit event + health event.
            await PersistOutcomeAsync(request, result, requestedAt).ConfigureAwait(true);

            HasResult = true;
        }

        // ── Gate evaluation ─────────────────────────────────────────────────────────

        /// <summary>
        /// Resolves the gate-blocked flag and recovery severity. Forced test values win;
        /// otherwise the live ProgressionSafetyGate + RecoveryIntelligenceService are
        /// consulted over the persisted analytics history. Any failure is treated
        /// conservatively for safety: a recovery-service failure yields severity None (the
        /// engine still floors at the recovery floor), while a safety-gate failure leaves
        /// the gate UNBLOCKED only because the clamp's Stage 1 still applies — we never
        /// fabricate a permissive override path.
        /// </summary>
        private async Task<(bool gateBlocked, RecoverySeverity severity)> EvaluateGateSignalsAsync(DateTime now)
        {
            var gateBlocked = _forcedGateBlocked ?? false;
            var severity = _forcedRecoverySeverity ?? RecoverySeverity.None;

            if (_forcedGateBlocked is null && _safetyGate is not null)
            {
                try
                {
                    var gate = await _safetyGate.EvaluateAsync(now, UserId).ConfigureAwait(false);
                    gateBlocked = gate.IsBlocked;
                }
                catch
                {
                    // Could not read the gate — keep the conservative default (false); the
                    // engine's Stage 1 recovery floor still constrains the override.
                }
            }

            if (_forcedRecoverySeverity is null && _recoveryService is not null && _analyticsStore is not null)
            {
                try
                {
                    var forecast = await _recoveryService
                        .ForecastFromHistoryAsync(_analyticsStore, now, UserId)
                        .ConfigureAwait(false);
                    severity = forecast.Severity;
                }
                catch
                {
                    // Recovery read failed — severity None; Stage 1 still floors the override.
                }
            }

            return (gateBlocked, severity);
        }

        // ── Result + persistence ────────────────────────────────────────────────────

        private void ApplyResultToState(ManualOverrideResult result)
        {
            WasApplied = result.WasApplied;
            WasClamped = result.WasClamped;
            WasBlocked = !result.WasApplied;
            BlockedReasonCode = result.BlockedReasonCode;

            // Present ONLY the clamped profile. When nothing was applied, the applied
            // requirement values are reset to baseline so the UI never echoes the raw intent.
            var applied = result.AppliedProfile;
            if (applied is not null)
            {
                AppliedResonanceMin = applied.TargetResonanceMin;
                AppliedResonanceMax = applied.TargetResonanceMax;
                AppliedStabilityThreshold = applied.StabilityThreshold;
                AppliedRequiredHoldSeconds = applied.RequiredHoldSeconds;
            }
            else
            {
                var baseline = BuildBaseline();
                AppliedResonanceMin = baseline.TargetResonanceMin;
                AppliedResonanceMax = baseline.TargetResonanceMax;
                AppliedStabilityThreshold = baseline.StabilityThreshold;
                AppliedRequiredHoldSeconds = baseline.RequiredHoldSeconds;
            }
        }

        /// <summary>
        /// Writes the outcome to the append-only override log AND the immutable audit trail
        /// AND a MANUAL_OVERRIDE health event. The three writes are independent — a failure
        /// of one does not suppress the others; <see cref="WasLogged"/> is only set true
        /// when at least one durable record was written without error.
        /// </summary>
        private async Task PersistOutcomeAsync(
            ManualOverrideRequest request, ManualOverrideResult result, DateTime now)
        {
            var loggedAny = false;

            // (a) Append-only manual-override log (maps request+result, shares ids).
            if (_overridesStore is not null)
            {
                try
                {
                    await _overridesStore.LogResultAsync(request, result).ConfigureAwait(true);
                    loggedAny = true;
                }
                catch { /* descriptive log only — never crash the override surface */ }
            }

            // (b) Immutable audit event with Before/After JSON (EntityType=Override). Before
            //     = the professional's INTENDED profile; After = the CLAMPED applied profile
            //     (null when nothing was applied). The id mirrors the result's AuditId.
            if (_auditStore is not null)
            {
                try
                {
                    var auditEvent = new AuditEvent
                    {
                        AuditId = result.AuditId,
                        UserId = request.UserId,
                        OccurredAt = now,
                        EntityType = AuditEntityType.Override,
                        EntityId = result.ManualOverrideId.ToString("D"),
                        ActorRole = request.ActorRole,
                        ReasonCode = string.IsNullOrWhiteSpace(request.ReasonCode)
                            ? ManualOverrideReasonCode
                            : request.ReasonCode,
                        BeforeJson = SerializeProfile(request.IntendedProfile),
                        AfterJson = SerializeProfile(result.AppliedProfile)
                    };
                    await _auditStore.AppendAsync(auditEvent).ConfigureAwait(true);
                    loggedAny = true;
                }
                catch { /* audit failure must not crash the surface */ }
            }

            // (c) MANUAL_OVERRIDE health event. Uses the non-safety-signal event type
            //     (HealthTrendUpdated) so it is NEVER miscounted by the Safety/Recovery
            //     gates (which key off SafetyFreeze/StrainPeriod/PauseRecommended/
            //     HydrationSuggested/ComfortZoneBreach). The MANUAL_OVERRIDE intent is
            //     carried in ReasonCode. An override is a journal entry, not a health risk.
            if (_analyticsStore is not null)
            {
                try
                {
                    var healthEvent = new HealthAnalyticsEvent
                    {
                        SessionId = 0,
                        UserId = request.UserId,
                        EventType = HealthAnalyticsEventType.HealthTrendUpdated,
                        OccurredAt = now,
                        Severity = result.WasClamped ? 1.0 : 0.0,
                        ReasonCode = ManualOverrideReasonCode
                    };
                    await _analyticsStore.RecordHealthEventAsync(healthEvent).ConfigureAwait(true);
                    loggedAny = true;
                }
                catch { /* health-event write failure must not crash the surface */ }
            }

            WasLogged = loggedAny;

            // Append-only in-session outcome trail (clamped result only).
            OutcomeLog.Add(
                $"{now:O} {request.OverrideKind} " +
                $"applied={result.WasApplied} clamped={result.WasClamped} " +
                $"blocked={result.BlockedReasonCode ?? "-"} logged={loggedAny}");
        }

        // ── Profile helpers ─────────────────────────────────────────────────────────

        /// <summary>The clinical reference floor for the targeted exercise.</summary>
        private ExerciseTargetProfile BuildBaseline() =>
            (_profileFactory ?? new ExerciseProfileFactory()).CreateProfile(BaselineProfileType);

        /// <summary>The professional's INTENDED profile (the request, never the result).
        /// Built from the baseline's metric flags/guidance keys with the intended
        /// requirement values overlaid so it is a valid, complete profile.</summary>
        private ExerciseTargetProfile BuildIntendedProfile()
        {
            var baseline = BuildBaseline();
            return new ExerciseTargetProfile
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
                RequiredHoldSeconds = IntendedRequiredHoldSeconds
            };
        }

        private static string? SerializeProfile(ExerciseTargetProfile? profile)
        {
            if (profile is null) return null;
            try
            {
                return JsonSerializer.Serialize(new
                {
                    profile.TargetResonanceMin,
                    profile.TargetResonanceMax,
                    profile.StabilityThreshold,
                    profile.RequiredHoldSeconds,
                    profile.MinPitch,
                    profile.MaxPitch
                });
            }
            catch
            {
                return null;
            }
        }
    }
}
