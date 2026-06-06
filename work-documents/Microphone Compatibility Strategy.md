# Microphone Compatibility Strategy

## Goal

FemVoice Studio should work with common microphones without depending on one vendor stack such as Logitech Blue VO!CE. The app should tolerate USB mics, headset mics, laptop mic arrays, jack/analog mics and driver-processed devices while still protecting measurement quality for voice feminization training.

## Why This Matters

Voice feminization feedback depends on stable pitch, resonance, intensity and voice-quality measurements. Many microphone stacks change the signal before the app receives it:

- Logitech Blue VO!CE can apply EQ, noise reduction, compressor, de-esser, de-popper and limiter.
- Windows audio enhancements can apply noise suppression, voice focus, automatic gain control or device effects.
- Realtek, Nahimic and headset drivers can add noise gates, AGC or beamforming.
- Older analog mics can have weak output, high noise floor or clipping when gain is raised.

These tools can improve communication audio, but they can also distort acoustic measurements. FemVoice should not copy these systems. It should detect likely problems, adapt thresholds and tell the user what to change.

## Product Direction

### 1. Prefer Measurement Accuracy

For pitch, resonance and voice-quality analysis, prefer the least processed microphone signal available. If Windows or the driver supports raw capture, that should be the preferred future path for precise measurement mode.

### 2. Provide Robust Mode

Some users cannot get a clean raw signal. FemVoice should support a robust mode that accepts less ideal microphones:

- lower voiced thresholds for low-output mics
- more tolerant confidence checks when SNR is usable but not ideal
- mild high-pass filtering for rumble
- soft frame-level noise gating instead of hard per-sample gating
- clear warnings when the signal is processed or unreliable

### 3. Separate Voice Detection From Voice Measurement

Noise suppression and VAD can help determine when speech is present, but core acoustic measurements should use the cleanest available signal. A processed signal can be used as a helper channel, not as the only measurement source.

### 4. Calibrate Per Device

Calibration profiles should remain per microphone device and include:

- noise floor RMS
- comfortable voice RMS
- noise gate threshold
- voiced RMS threshold
- signal-to-noise ratio
- peak dBFS
- compatibility flags

## Compatibility Flags

FemVoice now tracks these likely device conditions during calibration:

- `LowOutput`: quiet but usable microphone output
- `HighNoiseFloor`: room or device noise is high
- `ClippingRisk`: peak level is close to digital clipping
- `PossibleNoiseGate`: quiet voice may be cut off by software or driver gating
- `PossibleAgcOrCompression`: automatic gain or compression may be flattening levels

These flags should produce technical advice, never negative feedback about the user's voice.

## User Guidance

For most users:

- Use Windows default input device.
- Run microphone calibration before training.
- Keep the mic close enough for clear signal.
- Avoid clipping by lowering Windows input volume or mic gain.

For Logitech Blue VO!CE / broadcast software:

- Keep EQ flat or gentle for measurement.
- Keep compressor and AGC off or gentle.
- Avoid aggressive noise gate.
- Light noise reduction is acceptable if the room is noisy, but FemVoice may mark the signal as processed.
- Limiter can be useful as safety, but if clipping warnings appear, reduce input gain.

For laptop arrays / Windows Studio Effects:

- Use raw or unprocessed input when possible.
- If voice focus/noise suppression cuts off quiet humming, disable it or use robust mode.

## Implementation Status

Implemented:

- Manual calibration phases for quiet room and voice/humming.
- Live RMS/dBFS/peak display during calibration.
- SNR and peak stored in microphone profiles.
- Compatibility flags stored in microphone profiles.
- Soft frame-level noise gate in normal capture instead of destructive per-sample gating.
- User-facing technical advice for low output, high noise, clipping risk, possible noise gate and possible AGC/compression.
- Calibrated voiced thresholds are applied in analyzer, real-time analysis and subsystem capture flows.
- Main live pitch streaming uses calibration-aware frame RMS and low-output sensitivity.

Future work:

- Add an explicit UI toggle for `Presis måling` vs `Robust mic-modus`.
- Prefer WASAPI raw capture where available, with WaveIn fallback.
- Show current input device and profile health in Settings.
- Add a short "mic health check" before first exercise.
- Consider a dual-path pipeline: processed/VAD helper path plus raw measurement path.
