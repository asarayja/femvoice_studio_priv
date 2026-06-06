# FEMVOICE - Clinical Validation Review

Status: 2026-06-06

Basis:

- `MainDashboard_TechnicalTruth_Audit.txt`
- `work-documents/FemVoice_Architecture_Documentation.md`
- `work-documents/FEMVOICE – Health Intelligence Layer.md`
- `work-documents/FEMVOICE – ProgressionOrchestrator.md`
- `work-documents/FEMVOICE – FeedbackConsistencyGuard.md`
- `work-documents/FEMVOICE – SessionAnalyticsStore.md`

External clinical reference basis:

- ASHA: Gender-affirming voice work assesses pitch, intonation, voice quality, resonance, vocal health and communication context, and warns against overemphasis on speaking fundamental frequency.
  https://www.asha.org/practice-portal/professional-issues/gender-affirming-voice-and-communication/
- UCSF Transgender Care: voice feminization should address pitch, resonance, intonation and intensity, with safe and efficient production; specialty trained SLPs are best equipped for overall vocal health and efficiency.
  https://transcare.ucsf.edu/guidelines/vocal-health
- Johns Hopkins: gender-diverse voice care is individualized and includes pitch, resonance, intonation, communication style and laryngeal health screening.
  https://www.hopkinsmedicine.org/health/expert-qa/transgender-and-gender-diverse-voice-care

Important scope note: this is an internal clinical-safety review of the current product design. It is not medical clearance and does not replace review by a qualified speech-language pathologist or laryngology/voice clinician.

## Section 1 - Pitch Validation

Displayed pitch systems:

| Item | Current technical truth | Clinical validation |
| ---- | ----------------------- | ------------------- |
| Main pitch graph | Plots detected Hz over time after smoothing/stabilization. Visible axis remains `Frequency (Hz)`. | Clinically meaningful as technical biofeedback, but risky if treated as the main success metric. |
| Main target badge | Shows raw `TargetMinPitch` and `TargetMaxPitch` in Hz. Current policy caps advanced upper zone at 240 Hz. | Safer than the previous wider/high range, but exact Hz display can still encourage number chasing. |
| Main realtime pitch text | Describes under/inside/above target zone and says to avoid pressure when above zone. | Safer wording. Must stay comfort-oriented and avoid "higher is better". |
| Pitch score | Derived 0-100 score from smoothed pitch, target zone, variance and penalties. | Useful only as supportive feedback. It should not dominate resonance, comfort or health. |
| Exercise pitch handling | `ExerciseLiveState` normalizes pitch only when pitch is primary. Non-pitch exercises can hide pitch direction. | Clinically better than the dashboard because pitch is contextual rather than always central. |

Verdict: pitch design is partially safe, but the main dashboard still overexposes raw Hz. The pitch target zone cap at 240 Hz is appropriate for avoiding an unsafe "advanced means higher" message, but the visual center of the main dashboard remains the Hz graph.

Risk flagged: the main dashboard can still imply that moving the pitch line into the green zone is the primary goal, even though the broader system says resonance, stability and comfort should lead.

## Section 2 - Resonance Validation

Resonance is architecturally important:

- `ExerciseTargetProfile` can make resonance the primary metric.
- `ExerciseIntelligenceCoordinator` uses resonance-aware live state.
- `ProgressionOrchestrator` prioritizes resonance before pitch comfort.
- `FemVoiceScore` weights resonance heavily in the scoring model.
- `AnalyzerWindow` uses `ResonanceProxyEngine` and `SpectrogramResonanceMapper` for formant/resonance visualization.

Main dashboard limitation:

- The dashboard `ResonanceScore` is not the same as the formant-based `ResonanceProxyEngine` output.
- It uses fixed F1/F3 placeholders and RMS-derived F2/spectral proxy.

Clinical validation: the training philosophy correctly treats resonance as central, which aligns with modern transfeminine voice work. The risk is presentation: on the main dashboard, resonance can look like a precise clinical score even though it is a proxy. That must not be marketed or interpreted as a definitive resonance measurement.

Verdict: resonance design is clinically aligned in Exercise Guide and Analyzer. Main dashboard resonance needs clearer interpretation before production release.

## Section 3 - Stability Validation

Stability systems:

- Main dashboard `PitchStability` is derived from recent smoothed pitch standard deviation and mapped to labels such as stable/developing/unstable/no voice.
- Exercise Guide uses normalized `StabilityScore` from `ExerciseLiveState`.
- Progression uses stability as a gatekeeper before increasing difficulty.
- Health intelligence watches stability drift and variance as part of strain/fatigue detection.

Clinical validation: stability is a healthy training target because it encourages controlled, repeatable production rather than pushing pitch upward. The current system uses stability in a clinically useful way, especially in Exercise Guide and progression.

Main dashboard caveat: stability is pitch-variance based, so it should be interpreted as pitch stability, not full vocal stability or voice quality.

Verdict: stability handling is broadly safe and clinically useful, as long as labels do not overclaim.

## Section 4 - Quality Score Validation

Quality meanings differ by area:

| Area | What quality/score represents | Validation |
| ---- | ----------------------------- | ---------- |
| Main dashboard `OverallScore` | Live proxy score from `FemVoiceScore.Calculate(input)` using dashboard inputs. | Useful as approximate training feedback, but not a clinical quality measure. |
| Main component scores | Resonance, pitch, intonation and health scores, all 0-100. | Good for overview, but risk of score chasing. |
| Exercise `Quality` | `ExerciseLiveState.Quality` from coordinator. Safety lock forces poor/blocked state. | Clinically stronger because health and profile context gate the result. |
| Exercise `DisplayQuality` | Composite of primary metric and stability. | Safer because it reflects exercise-specific success, not raw pitch. |

Clinical validation: the Exercise Guide quality loop is safer than the main dashboard scoring loop. The dashboard quality/score values can be misleading because some inputs are proxy-based and can arrive from overlapping audio pipelines.

Verdict: quality scoring is acceptable for internal feedback and controlled beta use, but production UI should avoid presenting proxy scores as clinical truth.

## Section 5 - Comfort Zone Validation

Positive findings:

- Pitch is treated as a target zone, not a maximum-value contest.
- Main target policy now caps advanced upper target at 240 Hz.
- Realtime text warns against pressure when above the zone.
- `ComfortZoneController` exists for adaptive comfort/safety.
- Exercise flow can use comfort zone as a safety condition rather than a simple score target.

Technical concern:

- The main dashboard has two related but separate systems: `ActivePitchTargetZone` and `ComfortZone`.
- `OnPitchUpdated()` can use `ActivePitchTargetZone`, while `OnPitchAnalyzed()` can use `ComfortZone`.
- The visible graph and the feedback text can therefore disagree if those ranges diverge.

Clinical validation: comfort-zone framing is the right philosophy, but multiple sources of truth are a production risk because inconsistent feedback can make a user push or compensate incorrectly.

Verdict: conceptually safe; implementation consistency must be verified before release candidate.

## Section 6 - Health Validation

Positive findings:

- `VocalHealthSupervisor` evaluates normalized live metrics, not UI quality.
- Health states are explicit: `Normal`, `Caution`, `Restrict`, `Lock`.
- Strain and fatigue are separated.
- Strain can react quickly; fatigue is trend-based.
- Recovery requires stable improvement and does not instantly reset.
- Hydration support never controls session flow.
- Low performance quality alone is not treated as health risk.
- `FeedbackConsistencyGuard` prioritizes safety/health over praise and progression.

Main dashboard limitation:

- Main dashboard `HealthIndicator` and `VoiceHealthScore` are simplified and do not represent the full `VocalHealthSupervisor` path.

Clinical validation: the Exercise Guide health system is strong and safety-first. The main dashboard health display should be treated as a lightweight indicator, not a complete vocal health decision.

Verdict: health protection architecture is clinically strong, with a main-dashboard interpretation gap.

## Section 7 - Coaching Validation

Positive findings:

- Feedback language has RESX safety policy tests.
- Humming exercises are treated as humming, not forced speech.
- `FeedbackConsistencyGuard` suppresses praise during health risk.
- Progression and health feedback go through mappers/pipeline before UI.
- Realtime dashboard text avoids "push higher" and uses comfort-zone wording.

Clinical validation: coaching direction is good. It supports learning without shame, avoids binary "correct voice" language, and prioritizes comfort/safety. The remaining risk is not the language layer itself, but the visible score/Hz emphasis on the main dashboard.

Verdict: coaching quality is good for release verification, pending manual language review in all supported languages.

## Section 8 - Progression Validation

Positive findings:

- `ProgressionOrchestrator` uses high-level session data, not raw audio.
- Progression requires consistent improvement over multiple sessions.
- Health, fatigue, safety events and subjective report can pause or regress progression.
- Resonance is prioritized before pitch.
- Pitch comfort scales only after resonance/stability.
- Recovery practice can count positively without inflating performance averages.
- `SessionAnalyticsStore` avoids raw audio storage and stores clinically useful summaries.

Clinical validation: progression is aligned with sustainable habit formation. It rewards consistency, resonance, stability and health rather than one high-scoring session.

Verdict: progression design is clinically appropriate and one of the stronger parts of the application.

## Section 9 - User Psychology Audit

What the system trains the user to repeat:

| Area | Reinforced behavior | Clinical interpretation |
| ---- | ------------------- | ----------------------- |
| Exercise Guide | Stay comfortable, stabilize, hold safely, improve resonance-specific goals. | Good. This supports sustainable learning. |
| Health layer | Stop, pause, recover or hydrate when load rises. | Good. This discourages overtraining. |
| Progression | Improve over multiple safe sessions, not one spike. | Good. This supports long-term habit formation. |
| Main dashboard graph | Watch pitch line, target zone, Hz numbers and component scores. | Mixed. Useful for awareness, but can reinforce pitch/score chasing. |
| Analyzer spectrogram | Inspect resonance/formant behavior. | Good if framed as exploratory feedback, not perfection target. |

Primary psychology risk: a motivated transfeminine user may naturally focus on the most visible moving element. On the main dashboard, that element is still the pitch trace and green Hz zone. This can unintentionally train "match the pitch number" behavior even though the rest of the system has a safer philosophy.

Clinical validation: Exercise Guide is the better primary training surface. Main dashboard should be treated as overview/monitoring, not the main clinical training authority.

## Section 10 - Clinical Risk Register

| Risk | Severity | Cause | Recommendation |
| ---- | -------- | ----- | -------------- |
| Pitch chasing on main dashboard | High | Raw Hz target badge, Hz axis, moving pitch line and green zone are visually central. | Before production, verify copy and layout make pitch supportive, not primary success. Consider hiding exact Hz outside advanced/debug contexts in a later design pass. |
| Resonance score overclaim | High | Main dashboard resonance is proxy-based, not the full `ResonanceProxyEngine` path. | Label as guidance/proxy or route dashboard resonance through the same resonance engine before clinical release. |
| Inconsistent target feedback | High | `ActivePitchTargetZone` and `ComfortZone` can both influence feedback paths. | Use one visible source of truth for main dashboard target/feedback before release candidate. |
| Simplified health display overclaim | Medium-High | Main dashboard health is not full `VocalHealthSupervisor`. | Make dashboard health wording non-medical and ensure Exercise Guide health decisions remain authoritative. |
| Score chasing | Medium-High | Overall/component scores are visible as percentages. | Keep feedback comfort/resonance-first; avoid achievement language that rewards max scores. |
| Misinterpreting stability | Medium | Main dashboard stability is pitch stability, not full voice stability. | Ensure labels/copy do not imply complete vocal quality assessment. |
| Mic/device signal bias | Medium | Different mics can affect RMS, confidence and resonance proxy values. | Continue release QA with USB, jack/analog, headset and laptop microphones; treat low confidence as technical signal issue. |
| Overtraining | Medium | Highly motivated users may practice too long despite positive scores. | Current health/progression layers mitigate this; manual QA must verify pause/restrict/lock is visible and respected. |
| Clinical authority overstatement | Medium | App has clinical language and health intelligence but no therapist validation yet. | Product copy must avoid claiming clinical validation until reviewed by qualified voice professionals. |
| Main dashboard/exercise mismatch | Medium | Main dashboard and Exercise Guide use different live state paths. | Document clearly and unify later if production testing shows user confusion. |

## Section 11 - Final Clinical Verdict

| Area | Score / 10 |
| ---- | ---------- |
| Pitch Design | 6 |
| Resonance Design | 7 |
| Stability Design | 8 |
| Health Protection | 8 |
| Coaching Quality | 8 |
| Progression Design | 8 |
| Clinical Safety | 7 |
| Feminization Effectiveness | 7 |

Overall verdict:

FemVoice is clinically aligned in philosophy and has a strong safety-first exercise architecture. It is not yet clinically validated for production release because the main dashboard still exposes raw Hz prominently, uses proxy resonance/health scores, and has multiple sources of truth for pitch/comfort feedback.

## Final Summary

1. Is FemVoice clinically safe?

Mostly for release-verification/beta use, assuming clear disclaimers and continued manual QA. It should not yet be presented as clinically validated production software.

2. Is FemVoice aligned with modern transfeminine voice training?

Yes in the core training philosophy: resonance, comfort, stability, health and progression are treated as central. This aligns with modern guidance that pitch alone is insufficient.

3. Does the application overemphasize pitch?

The Exercise Guide does not overemphasize pitch. The main dashboard still does, because the pitch graph, target Hz badge and Hz axis are visually dominant.

4. Does the application properly emphasize resonance?

Architecturally yes, especially in Exercise Guide, progression and analyzer. On the main dashboard, resonance is undercut by being proxy-based and less visually central than pitch.

5. Would a voice therapist likely approve the training philosophy?

Likely yes for the philosophy: safety-first, resonance-aware, comfort-zone based, progression-gated and non-shaming. A therapist would likely require clearer limits around raw Hz, proxy labels, mic variability and clinical claims before production release.

6. What must be fixed before production release?

- Main dashboard must not imply "higher pitch equals better".
- Dashboard resonance must either use the real resonance path or be clearly labeled as an approximate guide.
- Dashboard target zone and realtime feedback must use one source of truth.
- Dashboard health indicators must not overclaim compared with `VocalHealthSupervisor`.
- Full manual UI, microphone, localization and clinical language review must be completed.

