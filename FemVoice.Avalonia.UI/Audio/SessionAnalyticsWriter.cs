using System;

namespace FemVoice.Avalonia.Audio;

/// <summary>
/// Writes the per-dimension Voice-Intelligence record (<see cref="FemVoiceStudio.Services.SessionAnalyticsRecord"/>)
/// for a finished session, from the session's REAL measured signals via <see cref="SessionAnalyticsScorer"/>.
///
/// Shared by the dashboard and the exercise runtime. It used to live in the dashboard only, so the screens that read
/// these records — the Progression parameter graph, the Clinician voice metrics/trends and the Analysis rings — never
/// saw an exercise session, although exercises are where most practice happens.
///
/// Best-effort by contract: needs the concrete SQLite database, never throws, and never affects the session save.
/// </summary>
public static class SessionAnalyticsWriter
{
    /// <param name="targetAdherencePercent">0–100: share of voiced frames inside the session's target (the
    /// dashboard's comfort zone, or the exercise's own judged target).</param>
    /// <returns>True when a record was written.</returns>
    public static bool Write(FemVoiceStudio.Data.IDatabaseService? database, int sessionId,
        DateTime startUtc, DateTime endUtc, double targetAdherencePercent, double avgResonance100,
        double pitchVariationHz, double avgStability100, double avgHealth100)
    {
        if (database is not FemVoiceStudio.Data.DatabaseService concrete || sessionId <= 0) return false;
        try
        {
            var analytics = new FemVoiceStudio.Services.SessionAnalyticsStore(
                new FemVoiceStudio.Services.SqliteSessionAnalyticsRepository(concrete.ConnectionString));

            // Recovery from the real cross-session history (100 − debt). Empty history → rested; guarded.
            double recovery100 = 100;
            try
            {
                var forecast = new FemVoiceStudio.Services.RecoveryIntelligenceService()
                    .ForecastFromHistoryAsync(analytics, DateTime.UtcNow, 1).GetAwaiter().GetResult();
                recovery100 = Math.Clamp(100 - forecast.RecoveryDebt, 0, 100);
            }
            catch { /* no history yet → rested default */ }

            var d = SessionAnalyticsScorer.Compute(
                targetAdherencePercent, avgResonance100, pitchVariationHz, avgStability100, avgHealth100, recovery100);

            analytics.RecordSessionCompletedAsync(new FemVoiceStudio.Services.SessionAnalyticsRecord
            {
                SessionId = sessionId,
                UserId = 1,
                StartedAt = startUtc,
                EndedAt = endUtc,
                ExerciseCount = 1,
                AverageResonance = d.ResonanceScore100,
                AverageStability = d.ConsistencyScore100,
                AveragePitchComfort = d.ComfortScore100,
                AverageHealthScore = d.HealthScore100,
                ResonanceScore100 = d.ResonanceScore100,
                ComfortScore100 = d.ComfortScore100,
                ConsistencyScore100 = d.ConsistencyScore100,
                IntonationScore100 = d.IntonationScore100,
                VocalWeightScore100 = d.VocalWeightScore100,
                RecoveryScore100 = d.RecoveryScore100,
                PitchScore100 = d.PitchScore100,
                CompositeVoiceScore = d.CompositeVoiceScore,
            }).GetAwaiter().GetResult();
            return true;
        }
        catch { return false; }   // analytics is best-effort — never affects the session save
    }

    /// <summary>Core stability state → 0–100 (shared mapping so both screens score consistency the same way).</summary>
    public static double StabilityTo100(FemVoiceStudio.Models.StabilityState s) => s switch
    {
        FemVoiceStudio.Models.StabilityState.VeryStable => 100,
        FemVoiceStudio.Models.StabilityState.Stable => 80,
        FemVoiceStudio.Models.StabilityState.Developing => 55,
        FemVoiceStudio.Models.StabilityState.Unstable => 30,
        _ => 0,
    };
}
