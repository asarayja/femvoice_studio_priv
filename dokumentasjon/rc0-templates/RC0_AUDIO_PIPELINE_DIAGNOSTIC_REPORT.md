# RC0 Audio Pipeline Diagnostic Report

## Summary
Status: BLOCKED

No real Windows microphone session has been executed in this environment. The application now has developer-only runtime logging and evidence export hooks that can classify the failure after a real Start Økt run.

Runtime exports are written to `Documents/FemVoiceStudio/RC0_Evidence/RC0_EVIDENCE_yyyy-MM-dd_HHmm`.

## Device Comparison
- Device A: Corsair HS60 Pro Surround - NOT RUN
- Device B: Logitech G Pro X USB - NOT RUN
- Device C: Logitech G Pro X 3.5mm analog - NOT RUN

## Front-page Pitch Graph Findings
Pending runtime evidence from `FrontPagePitchMonitor`, `AudioCaptureHealth`, and `PitchPipeline` log entries.

## Exercise Score Findings
Pending runtime evidence from `ExerciseScore` entries. The export records whether score used real session runtime evidence or a fallback/no-data path.

## Audio Capture Timeline
Pending runtime evidence:
- Device selection
- DataAvailable count
- Bytes and samples received
- Callback interval
- Dropped callback count
- Time since last frame

## Input Level Timeline
Pending runtime evidence:
- RMS
- Peak
- Input level percent
- Noise floor estimate
- Signal-to-noise estimate
- Level collapse flag

## Pitch Detection Timeline
Pending runtime evidence:
- Pitch detector call count
- Pitch sample count
- Pitch rejection count
- Success rate
- Rejection reason

## Graph Update Timeline
Pending runtime evidence:
- Graph update count
- Last graph update path via exercise live metrics

## Score Calculation Evidence
Pending runtime evidence:
- ScoreSource
- EvaluatedTicks
- AverageResonance
- AverageStability
- ComfortCompliance
- SessionHealthScore

## Root Cause Classification
UNKNOWN until real-device evidence is collected.

Allowed classifications:
`CAPTURE_STOPS`, `SIGNAL_LEVEL_COLLAPSES`, `SILENCE_GATE_REJECTS_SIGNAL`, `PITCH_DETECTOR_REJECTS_SIGNAL`, `GRAPH_UPDATE_STOPS`, `SCORE_USES_LOW_INPUT`, `SCORE_FALLBACK_VALUE`, `DEVICE_SELECTION_ERROR`, `WINDOWS_OR_DRIVER_LEVEL_ISSUE`, `UNKNOWN`.

## Recommended RC-0 Fix
Run the required A/B sessions and use the generated `RC0_EVIDENCE_*` folders to identify whether the Logitech issue is capture loss, low signal, gate rejection, pitch rejection, graph stop, score fallback, or Windows/driver level collapse. Do not change clinical scoring until this evidence exists.
