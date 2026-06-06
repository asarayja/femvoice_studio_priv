# Clinical Voice Feminization Signal Strategy

FemVoice Studio should treat pitch as one biofeedback signal, not as a standalone
definition of a feminine voice. Clinical and speech-language sources describe
gender-affirming voice work as a combination of pitch, resonance/formants,
intonation, voice quality, intensity, communication style, and vocal health.

## Practical Clinical Model

- Pitch/F0: useful for training, but only when the detected value is the true
  fundamental frequency. A harmonic at 2x or 3x F0 must not be rewarded.
- Resonance/formants: should have higher score weight than pitch. Forward/oral
  resonance and formant movement are relevant, but fixed F1/F2 thresholds should
  be treated as training heuristics, not universal clinical truth.
- Intonation/prosody: pitch variation and phrase movement should support a
  natural speaking pattern instead of a single static target.
- Vocal health: strain, fatigue, high intensity, unstable pitch, jitter/shimmer,
  and user-reported discomfort must override performance praise.

## Current Live Pitch Risk

The main-page live graph receives pitch from `AudioAnalysisEngine`, which uses
FFT peak selection. This is fast, but a strong second or third harmonic can be
misread as F0. For example, a true dark voice around 90 Hz can be displayed as
180 Hz or 270 Hz if the overtone has the strongest spectral peak.

To reduce this failure mode, the live engine now checks lower subharmonic
candidates before accepting the FFT peak. It compares harmonic support for the
detected frequency against plausible F0 candidates at 1/2 and 1/3 of the detected
frequency, and it uses the last accepted pitch to reject abrupt octave jumps.

## UI/Feedback Principle

The green zone in the pitch chart means "pitch target range", not "voice is
clinically feminine". The app should avoid praise when pitch data is unreliable
or when resonance/health do not support the pitch behavior.

## Sources Reviewed

- ASHA: Gender affirming voice and communication therapy covers pitch,
  resonance, speech, nonverbal communication, and vocal health; cis women's
  voices vary, so there is no single definition of feminization.
- UCSF: Pitch is an important gender cue, but raising pitch alone does not
  necessarily make a voice perceived as female; resonance, intonation, voice
  quality, and intensity also matter.
- de Cheveigne and Kawahara, 2002: YIN is a fundamental frequency estimator
  designed to reduce common F0 errors.
- Praat manual: clinical/research pitch tracking uses voicing thresholds and
  octave costs to reduce octave errors.

## Next Quality Steps

- Add an explicit `PitchQualityState` to distinguish reliable pitch from weak
  signal, unstable signal, and possible octave error.
- Route live scoring through reliable pitch only.
- Replace RMS-derived live resonance proxies with validated formant or spectral
  measures when confidence is high; otherwise mark resonance as uncertain.
- Keep post-session graph review interactive while preserving live auto-scroll
  during recording.
