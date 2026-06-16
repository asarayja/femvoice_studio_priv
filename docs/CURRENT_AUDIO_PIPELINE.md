# FemVoice Studio — Current Audio Pipeline (WPF Baseline)

Audit date: 2026-06-16 · Read-only. — CONFIRMED unless noted. No behavioural change proposed; this is descriptive.

## 1. Overview

Audio lives under `FemVoiceStudio/Audio/` (capture + DSP) with a few pure helpers in `Services/`. The capture layer is **Windows-only via NAudio**; the DSP/analysis layer is **pure C# and portable** (NAudio is touched only for `Complex`/FFT math, which is cross-platform). There is no `Dispatcher` usage in any audio file; the only `System.Windows` reference in the subsystem is `Services/AnalysisChartTheme.cs` (chart theming).

## 2. Capture layer (hardware-coupled, Windows-only)

| File | Role | Capture API | Notes |
| --- | --- | --- | --- |
| `Audio/AudioCaptureService.cs` (~1013 lines) | **Primary active capture** | `WaveInEvent` (NAudio) only | Continuous low-latency recording, float conversion, noise gate, high-pass, optional self-monitoring playback, 2 s watchdog (detects mic-privacy/dead capture), `DeviceLost` safety event, device selection via `MMDeviceEnumerator` + name heuristics. Default 44100 Hz, 16-bit mono, 1024-sample buffer (~23 ms). Defines a thread-safe `CircularBuffer<T>`. |
| `Audio/AudioAnalysisEngine.cs` (~1402 lines) | Self-contained capture + FFT pitch engine | **WASAPI → WaveIn fallback** | The **only** file with a real WASAPI(`WasapiCapture`, shared)→WaveIn fallback. Default 48000 Hz, FFT 2048. Two pitch modes (SimpleFirst, HighPrecision). Marshals events via `SynchronizationContext.Post`. Constructed by `MainViewModel` but front-page `StartRecording` deliberately starts **only** `AudioAnalyzerService` to avoid opening the device twice (PARTIAL: whether the engine's capture is ever started in production is not fully traced). Contains `MockAudioAnalysisEngine` for tests. |
| `Audio/AudioAnalyzerService.cs` (~241 lines) | **Active front-page pitch pipeline** | owns an `AudioCaptureService` | The real front-page mic pipeline (`MainViewModel._audioAnalyzer`). Dispatches each frame to `Task.Run`, runs `PitchDetectionService.DetectPitch`, guards history with `lock`. |
| `Subsystems/Audio/AudioSubsystem.cs` | Async wrapper over `AudioCaptureService` | enumerates devices via `WaveInEvent` | Part of the dead `Subsystems/` layer (not wired in production). |

**Resonance capture** is a separate pipeline: `Views/ResonanceWindow.xaml.cs` constructs its own `AudioCaptureService(44100,1,16)` and feeds frames into `ResonanceProxyEngine.ProcessSamples` — independent of the front page. — CONFIRMED.

## 3. DSP / analysis layer (pure, portable) — CONFIRMED

All use only `System`/`System.Linq`/project models (NAudio FFT math where noted). No WPF, no `Dispatcher`, no capture.

| File | Algorithm (high level) |
| --- | --- |
| `Audio/PitchDetectionService.cs` | **YIN** (difference function → CMND, threshold 0.15, parabolic interp); autocorrelation backup; `AnalyzeIntonation`. |
| `Audio/AdaptivePitchDetector.cs` | YIN with **adaptive threshold** [0.05–0.30] from rolling noise-floor/SNR; defines `RollingStatistics`. |
| `Audio/FormantDetectionService.cs` | **LPC (Levinson-Durbin, order 12)** on pre-emphasized/decimated signal → spectral-envelope peak-picking for F1/F2/F3. ("Spectral centroid" here is a ZCR approximation — PARTIAL.) |
| `Audio/VoiceActivityDetector.cs` | Energy/RMS threshold + hysteresis (MinSpeech 3, MinSilence 10 frames). (`SpectralCentroidThreshold` constant declared but unused.) |
| `Audio/ResonanceProxyEngine.cs` | FFT-based resonance scorer: pre-emphasis → Hann → FFT → centroid + formant peak-picking (200–4000 Hz) → weighted score (formant-shift 0.40 / F2–F1 0.25 / brightness 0.25 / stability 0.10) vs style targets. Uses NAudio `Complex`/FFT only — does **not** capture. Marshals via `SynchronizationContext`. |
| `Audio/ResonansScoringService.cs` | Pure logic: scores F1/F2/ratio/centroid vs style targets, classifies forward/neutral/back, EMA smoothing, session trends. |
| `Audio/VocalWeightAnalyzer.cs` | Weighted anchor-point mapping of centroid/F1/HNR/intensity → 0–100, trimmed-mean session aggregate. |
| `Audio/VoiceStrainDetector.cs` | Heuristic high-amplitude + pitch-instability counting (50-frame window). NOTE stubs: `CalculateStdDev` returns `mean*0.1`; `JitterValue`/`ShimmerValue` always 0. — CONFIRMED (flag in docs). |
| `Audio/SpeechRateAnalyzer.cs` | WPM/syllables-per-second from word count + duration. |
| `Audio/VoiceMetricsCalculator.cs` | Orchestrates pitch+formant → jitter/shimmer/HNR/intonation/resonance/strain/health. HNR is an approximation (confidence×stability×log), not true HNR. — CONFIRMED. |
| `Services/PitchTraceStabilizer.cs` | Rejects fast jumps (>90 Hz / 0.25 s), corrects likely harmonics (÷2/÷3/÷4) toward last accepted pitch. |
| `Services/PitchTargetZonePolicy.cs` | Static difficulty→pitch-range table, 150–240 Hz absolute clamp. |
| `Services/ZoneConfiguration.cs` | Zone DTOs/enums. |
| `Services/LiveMetricsService.cs` | EMA pitch smoothing, stddev-based stability states, strain/fatigue/health states, resonance/F2 proxy from spectral centroid. |
| `Services/SpectrogramResonanceMapper.cs` | Maps a `FormantSnapshot` + score → visual state (tone class Back/Balanced/Forward/Pressed, formant Y-positions, brightness EMA). |
| `Subsystems/Analysis/AnalysisSubsystem.cs` | Composes the pure detectors via `Task.Run` (part of dead Subsystems layer). |

## 4. Microphone calibration — CONFIRMED (portable)

- `Audio/MicrophoneCalibrationService.cs` (~296): builds/blends/persists per-mic profiles from background-noise + comfortable-voice buffers. Measures noise floor (RMS), speech RMS, noise gate `clamp(max(noise×2.8, noise+0.001), 0.0015, 0.08)`, voiced threshold, SNR (dBFS), peak. Quality states (TooLoud/TooQuiet/TooCloseToNoise<8 dB/Good) and compatibility flags (LowOutput, HighNoiseFloor, ClippingRisk, PossibleNoiseGate, PossibleAgcOrCompression). Persists JSON keyed by SHA256 of normalized device name under `LocalApplicationData`. Uses only `System.IO`/`Cryptography`/`Text.Json` — portable.
- `Audio/MicrophoneCalibrationProfile.cs` (~34): DTO + `[Flags] MicrophoneCompatibilityFlags`. (PARTIAL: some `Effective*`/`Calibration*RMS` fields not set in `BuildProfile`/`Blend`.)
- Calibration is applied back into capture: `AudioCaptureService.ApplyStoredCalibration` and `AudioAnalysisEngine.ApplyStoredCalibration` load by device name and adjust gate/RMS thresholds.

## 5. Threading model — CONFIRMED

- Capture `DataAvailable` runs on NAudio's capture thread.
- `AudioAnalyzerService`/`RealtimeAnalysisEngine`/`AnalysisSubsystem` offload per-frame analysis to the thread pool (`Task.Run`); shared state guarded by `lock`/`Interlocked`.
- `AsyncAudioPipeline` uses a bounded `BlockingCollection<float[]>` producer/consumer + long-running consumer Task + `CancellationTokenSource` (appears unused in production — PARTIAL).
- UI marshalling: `AudioAnalysisEngine`/`ResonanceProxyEngine` use `SynchronizationContext.Post` (Avalonia-compatible); `AudioCaptureService`/`AudioAnalyzerService` raise events directly and let view-models marshal via `Dispatcher`.

## 6. Data flow (front page) — CONFIRMED

```
Microphone (Windows)
  → AudioCaptureService (WaveInEvent)          [Windows-only, NAudio]
      → float frames + RMS/peak + noise gate + high-pass
      → AudioAnalyzerService (Task.Run per frame)
          → PitchDetectionService (YIN)         [portable DSP]
          → PitchTraceStabilizer                [portable]
          → LiveMetricsService / FemVoiceScore  [portable]
  → MainViewModel (Dispatcher marshalling)      [WPF]
      → OxyPlot pitch chart + comfort zone + feedback
  → VocalHealthSupervisor / HydrationAdvisor / RecoveryIntelligence   [portable]
  → SessionAnalyticsStore / ExerciseSessionRecorder (SQLite)          [portable]
```

Resonance window runs an analogous but **independent** capture → `ResonanceProxyEngine` → OxyPlot formant scatter/timeline path.

## 7. Known limitations / things to flag — CONFIRMED

- Several quality metrics are documented stubs/approximations: `VoiceStrainDetector` jitter/shimmer = 0 and placeholder stddev; `VoiceMetricsCalculator` HNR approximation; `FormantDetectionService` ZCR-based "centroid"; VAD's spectral-centroid threshold unused.
- Two/three engines coexist for pitch (`AudioAnalyzerService` active; `AudioAnalysisEngine` constructed but front-page capture suppressed; `RealtimeAnalysisEngine` appears dead). This duplication should be consolidated during the port — but **not** in this audit.
- Multiple windows open their **own** `AudioCaptureService` instances (front page vs. ResonanceWindow vs. calibration vs. AnalyzerWindow) — device-contention is managed by not running them simultaneously.

## 8. Avalonia portability verdict — CONFIRMED

- **Replace later for Linux/macOS (Windows-only via NAudio):** `AudioCaptureService`, `AudioAnalysisEngine` (capture half), `AudioSubsystem`, and any window that constructs an `AudioCaptureService`.
- **WPF-coupled:** `Services/AnalysisChartTheme.cs` only.
- **Portable as-is (the bulk):** all DSP analyzers, scoring inputs, calibration, stabilizers, zone/metrics services, `SpectrogramResonanceMapper`, plus `CircularBuffer<T>`/`RollingBuffer<T>`/`RollingStatistics` helpers.

**Recommended boundary:** define `FemVoice.Audio.Abstractions` (`IAudioCaptureService` raising float-frame events + device enumeration), keep the NAudio implementation in `FemVoice.Audio.Windows`, and place all DSP in the shared core. This isolates the single highest-risk cross-platform dependency.
