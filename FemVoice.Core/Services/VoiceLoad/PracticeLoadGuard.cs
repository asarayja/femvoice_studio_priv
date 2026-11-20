using System;
using FemVoiceStudio.Models.VoiceLoad;

namespace FemVoiceStudio.Services.VoiceLoad
{
    /// <summary>How strongly a rest is suggested. Advisory only — it never blocks or ends practice.</summary>
    public enum RestAdviceLevel
    {
        /// <summary>Nothing to say (also while there is not yet enough voiced practice to judge).</summary>
        None,
        /// <summary>The user has practised for a while — a short rest soon is a good idea.</summary>
        Soon,
        /// <summary>A long stretch without a rest — rest now.</summary>
        Now
    }

    /// <summary>The current rest advice plus the plain facts it was derived from (shown/tested, never hidden).</summary>
    public readonly record struct RestAdvice(
        RestAdviceLevel Level,
        int LoadScore,
        double VoicedSeconds,
        double SecondsSinceRest);

    /// <summary>
    /// Rest guidance for one SITTING (every exercise and free-practice session until the user has rested for a long
    /// time), built on the existing <see cref="LiveVoiceLoadMonitor"/> / <see cref="VoiceLoadScoreEngine"/>.
    ///
    /// WHY THIS EXISTS: the cross-platform app measured pitch and brightness but had no notion of HOW LONG the voice
    /// had been working. Over-practising without rests is the most common way to tire or hurt a voice in training, and
    /// it is also the one risk an app can measure reliably: voiced time and time since the last rest are plain clock
    /// facts. Each exercise also reset everything, so ten exercises back to back looked like ten fresh starts.
    ///
    /// WHAT IT DELIBERATELY DOES NOT DO: it does not claim to detect strain, tension or fatigue. The Core health
    /// supervisor's strain/fatigue rules expect normalized score signals this app does not produce, and feeding them
    /// approximations would yield either constant false alarms or false reassurance. So the monitor is driven ONLY by
    /// what is truly measured — voiced time, time since the last rest, and time spent above the target zone — with the
    /// stability/strain/fatigue inputs held neutral. The advice is therefore honest about being time-based, and the
    /// user is still expected to stop at any discomfort regardless of what this says.
    ///
    /// LEVELS (thresholds are the Core load model's own, nothing new is invented):
    ///   • <see cref="RestAdviceLevel.Soon"/> — the Core load band is Moderate or above (score above 30).
    ///   • <see cref="RestAdviceLevel.Now"/>  — the load score has reached <see cref="RestNowScore"/>, i.e. BOTH Core
    ///     time drivers are saturated (≈15 min of voicing in the sitting and ≈8 min since the last rest), or the same
    ///     total is reached sooner because the voice keeps going above the target zone.
    /// A real rest of <see cref="RestGap"/> resets "time since rest"; <see cref="NewSittingGap"/> without practice
    /// starts a fresh sitting. Thread-safe; time is always passed in, so it is fully deterministic under test.
    /// </summary>
    public sealed class PracticeLoadGuard
    {
        /// <summary>The app-wide sitting (dashboard and exercises share one voice).</summary>
        public static PracticeLoadGuard Shared { get; } = new();

        /// <summary>No practice for this long counts as a real rest.</summary>
        public static readonly TimeSpan RestGap = TimeSpan.FromMinutes(2);

        /// <summary>No practice for this long starts a new sitting (all accumulated load is cleared).</summary>
        public static readonly TimeSpan NewSittingGap = TimeSpan.FromMinutes(30);

        /// <summary>Load score at which "soon" becomes "now": the sum of the Core model's two time-driver caps
        /// (duration 22 + time-since-pause 18).</summary>
        public const int RestNowScore = 40;

        // Held neutral on purpose (see the type remarks): at/above the Core model's 0.70 floor = no instability points.
        private const double NeutralStability = 1.0;

        private readonly LiveVoiceLoadMonitor _monitor = new();
        private readonly object _sync = new();
        private DateTime _lastActivity;
        private bool _started;

        /// <summary>Call when a session (exercise or free practice) starts.</summary>
        public void BeginSession(DateTime now)
        {
            lock (_sync)
            {
                if (!_started || now - _lastActivity >= NewSittingGap)
                {
                    _monitor.Reset(now);          // first use, or a long rest → a fresh sitting
                    _started = true;
                }
                else if (now - _lastActivity >= RestGap)
                {
                    _monitor.NotePauseTaken(now); // a real rest → "time since rest" starts over, voiced total is kept
                }
                _lastActivity = now;
            }
        }

        /// <summary>
        /// Feed one analysis frame. <paramref name="voicedPitchHz"/> is the measured pitch, or 0 when the frame is not
        /// voiced; <paramref name="intensity"/> the frame RMS (0 when not voiced); <paramref name="aboveTargetZone"/>
        /// whether the voice is currently ABOVE the top of the exercise/comfort zone.
        /// </summary>
        public RestAdvice Observe(DateTime now, int sessionElapsedSeconds, double voicedPitchHz, double intensity, bool aboveTargetZone)
        {
            lock (_sync)
            {
                if (!_started) { _monitor.Reset(now); _started = true; }
                bool voiced = voicedPitchHz > 0;
                var observation = _monitor.Observe(new VoiceLoadInputs
                {
                    Timestamp = now,
                    SessionElapsedSeconds = sessionElapsedSeconds,
                    PitchHz = voiced ? voicedPitchHz : 0,
                    Intensity = voiced ? Math.Max(0, intensity) : 0,   // silence between phrases is not a "dropout"
                    IsInComfortZone = !(voiced && aboveTargetZone),
                    StabilityScore = NeutralStability,                 // neutral: no validated instability signal
                    UsesResonanceSignal = false,
                    StrainScore = 0, FatigueScore = 0,                 // absent, not measured — never fabricated
                    HealthStateRank = 0,
                });
                _lastActivity = now;

                var state = observation.State;
                // Decided from the load BAND/score only. The Core pause engine additionally reacts to a "worsening
                // stability trend", but stability is held neutral here, so that trend is not a measurement (it even
                // reads "worsening" for the first second while its average warms up) and must not produce advice.
                RestAdviceLevel level =
                    !state.IsDataSufficient ? RestAdviceLevel.None :
                    state.VoiceLoadScore >= RestNowScore ? RestAdviceLevel.Now :
                    state.VoiceLoadBand is VoiceLoadBand.Moderate or VoiceLoadBand.High or VoiceLoadBand.PauseRecommended
                        ? RestAdviceLevel.Soon :
                    RestAdviceLevel.None;
                return new RestAdvice(level, state.VoiceLoadScore, state.ActiveVoicedSeconds, state.TimeSinceLastPauseSeconds);
            }
        }

        /// <summary>Forget everything (tests; and when the user clears their data).</summary>
        public void Reset()
        {
            lock (_sync) { _started = false; _lastActivity = default; }
        }
    }
}
