# FemVoice Studio — Current Runtime Workflows (WPF Baseline)

Audit date: 2026-06-16 · Read-only. — CONFIRMED unless noted. Describes how the live WPF app behaves end-to-end. These flows must be preserved (re-implemented faithfully) in the Avalonia port.

## 1. App startup — CONFIRMED

```
App.OnStartup
  ├─ RegisterGlobalExceptionLogging()      (Dispatcher / AppDomain / TaskScheduler → Rc0RuntimeLog)
  ├─ Rc0StartupBootstrap.Run()             (BEFORE DI; writes baseline evidence; never throws)
  ├─ ServiceCollection → ConfigureServices() → BuildServiceProvider()  (App.Services set)
  ├─ FirstTimeSetupService.IsFirstTime
  ├─ ThemeManager.Initialize()             (load Light/Dark, system theme via Registry)
  ├─ DebugSettingsService.EnsureDebugSection()
  ├─ CreateSplash().Show() + DispatcherHelper.DoEvents()
  ├─ if first time: FirstTimeSetupWindow.ShowDialog() (modal; cancel → Shutdown)
  ├─ MainWindow.Show()
  └─ Dispatcher.BeginInvoke(close splash, Background priority)
  (on exception: log, MessageBox, attempt MainWindow anyway, else Shutdown(1))
```
Avalonia note: `DispatcherHelper.DoEvents()` (`DispatcherFrame`/`PushFrame`) and the programmatic splash `Window` have no direct Avalonia equivalent — startup must be reworked.

## 2. Exercise selection & start — CONFIRMED

1. User opens the exercise guide (`ExerciseWindow`); `ExerciseListViewModel` lists the 15 catalog exercises (read from SQLite, seeded by `ExerciseDataService`), filtered by category.
2. Selecting an exercise resolves a transient `ExerciseDetailViewModel` (DI), which loads the target profile (`ExerciseProfileFactory`/`ExerciseTargetProfile`), guidance text (localized), indicator package, and mastery badge (`ExerciseDataService`).
3. Start triggers the exercise session lifecycle (timer, audio capture, live feedback).

## 3. Recording & audio capture — CONFIRMED

- Front page: `MainViewModel.StartRecording` starts **only** `AudioAnalyzerService` (which owns an `AudioCaptureService`/WaveIn) to avoid double-opening the device.
- Exercise window & resonance window open their **own** capture instances when active.
- Frames flow off the NAudio capture thread → `Task.Run` analysis → results marshalled to the UI via `Application.Current.Dispatcher`.

## 4. Pitch sample generation — CONFIRMED

```
float frames → AudioCaptureService (noise gate, high-pass)
  → PitchDetectionService (YIN; confidence; intonation)
  → PitchTraceStabilizer (reject jumps, correct harmonics)
  → LiveMetricsService (EMA smoothing, stability state)
  → MainViewModel pitch history → OxyPlot pitch chart + comfort zone (PitchTargetZonePolicy)
```

## 5. Resonance sample generation — CONFIRMED

```
float frames → ResonanceProxyEngine (pre-emphasis → Hann → FFT → centroid + formant peaks)
  + FormantDetectionService (LPC F1/F2/F3)
  → ResonansScoringService / SpectrogramResonanceMapper (forward/neutral/back, tone class, score)
  → ResonanceWindow OxyPlot scatter + formant timeline (themed, bounded zoom/pan)
```

## 6. Exercise live feedback & hold progress — CONFIRMED

```
ExerciseIntelligenceCoordinator (wires ResonanceProxyEngine, FemVoiceScoreEngine,
  ComfortZoneController, + analyzers)
  → ExerciseLiveState (resonance, pitch, stability, intensity, hold progress, safety, inline coach)
  → VocalHealthSupervisor.Evaluate(liveState)   (Normal→Caution→Restrict→Lock)
  → FeedbackPipeline → FeedbackConsistencyGuard  (priority + suppression + rate limit)
  → ExerciseDetailViewModel (Dispatcher) → UI (hold arc, messages, safety freeze)
```
Safety lock or wrong target state freezes/stops the hold.

## 7. Session save / load — CONFIRMED

- On stop, `ExerciseSessionRecorder` aggregates the live-state stream, wakes `VocalHealthSupervisor` (strain/fatigue), computes Voice-Intelligence scores, and persists the session to `SessionAnalyticsStore` (SQLite, no raw audio).
- `MasteryEvaluator` and `ProgressionSafetyGate` read the persisted history to gate mastery and difficulty promotion.
- Personal profile tweaks persist via `ExerciseProfileStore`.

## 8. Subjective report & progression — CONFIRMED

```
After stop → user reports comfort/fatigue/pressure/motivation (SubjectiveReport)
  → ProgressionOrchestrator.EvaluateAsync (safety/recovery gates FIRST, additive scoring after)
  → keep / adapt / pause / regress the exercise profile
  → AdaptiveTargetZoneService (max ~5 Hz/session, resonance-gated)
```

## 9. Analytics generation — CONFIRMED

`TrendEngineService` (OLS over 7/30/90/180-day windows) + `LongitudinalInsightEngine` + `VoicePatternDetector` (plateau/breakthrough/regression) read `SessionAnalyticsStore`. `ExerciseEffectivenessEngine` ranks exercises by observed effectiveness. All are descriptive — they never gate.

## 10. SmartCoach output — CONFIRMED

```
SmartCoachEngine.GenerateDailyRecommendation
  reads: goal profile, session history (VoiceMetrics trend), RecoveryIntelligence forecast,
         LearningPath stage, ComplexityEngine, ExerciseEffectiveness, longitudinal insight,
         knowledge graph, SmartCoach memory
  gates: strain → recovery-before-goals (Recovery > Goals)
  → daily recommendation + focus axis (weakest dimension after health gate)
  → SmartCoachFeedbackMapper → FeedbackPipeline → SmartCoachViewModel / SmartCoachDetailView
  → persists advice + outcome to SmartCoachMemoryStore
```

## 11. Voice Health evaluation — CONFIRMED

`VocalHealthSupervisor.Evaluate` runs an EMA state machine over live metrics (Normal→Caution→Restrict→Lock) and emits Strain/Fatigue/Pause/Restrict/Lock events. `HydrationAdvisor` emits soft hydration/pause suggestions (never a gate; 120 s min, max 3/session). `RecoveryScorer` (reactive 0–100) and `RecoveryIntelligenceService` (predictive forecast: debt, ACWR, severity) feed the gates — and can never weaken an existing safety decision.

## 12. Report generation — CONFIRMED

```
ReportExportViewModel (choose type 0–3 + format)
  → ReportAssembler.Build{Clinical|Coach|Outcome|Timeline}Report  (localized, key-fallback)
  → ExportWriter.Write (PDF via QuestPDF / CSV RFC 4180 / JSON)
       text sanitized by ReportTextSanitizer; PDFs are text/table (no charts)
  → SaveFileDialog (Microsoft.Win32) → file on disk
  → ReportVerificationTracker records the export
```

## 13. Diagnostics / support package export — CONFIRMED

`SupportPackageService` collects the latest evidence/log/verification artifacts + generated app/system/privacy/settings summaries into a privacy-filtered `.zip` under `Documents\FemVoiceStudio\SupportPackages`. Sensitive content (clinical/personal/free text, secrets) excluded by default. Failures routed to `Rc0WriteFailureSink`. See [`CURRENT_DIAGNOSTICS_AND_EVIDENCE.md`](CURRENT_DIAGNOSTICS_AND_EVIDENCE.md).

## 14. Language switching — CONFIRMED

`SettingsWindow` → `LocalizationService.SetLanguage(culture)` sets thread cultures and raises `PropertyChanged("Item[]")`; bindings using `{loc:Loc Key}` / `LocConverter` refresh live without restart. Preference persisted to `language.txt`. Default neutral language is Norwegian.

## 15. Cross-cutting safety invariant — CONFIRMED

Every output path (feedback, coaching, progression, manual override, reporting) is subordinate to **Safety > Health > Recovery > Comfort > Voice Development > Reporting**. Reporting/research/coaching/overrides are descriptive or more conservative and can never override the safety/health/recovery gates. This invariant is covered by `SafetyOverrideInvariantTests`, `SafetyPriorityEngineTests`, `ManualOverrideClampTests`, `FeedbackPriorityMatrixTests`.
