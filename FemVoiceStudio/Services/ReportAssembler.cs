using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Assembles professional report DTOs from an already-built <see cref="OutcomeProfile"/>
    /// and supporting data collections.
    ///
    /// CONTRACT:
    /// <list type="bullet">
    ///   <item><description>Pure / deterministic — no I/O, no random, no DateTime.UtcNow.
    ///   Callers supply <paramref name="now"/> explicitly so tests are reproducible.</description></item>
    ///   <item><description>Reads only from the supplied arguments; never queries any store.</description></item>
    ///   <item><description>Descriptive / reporting only — never overrides Safety &gt; Health &gt;
    ///   Recovery gates enforced at the engine level.</description></item>
    /// </list>
    ///
    /// Owned by W0-A3A10 (Report Generation + Professional Exports).
    /// </summary>
    public sealed class ReportAssembler
    {
        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds a <see cref="ClinicalReport"/> from the supplied outcome snapshot, clinical
        /// notes, and audit events for the given period.
        /// </summary>
        /// <param name="outcome">The assembled outcome snapshot — must not be null.</param>
        /// <param name="notes">Clinical/coaching notes; filtered to [periodStart, periodEnd].</param>
        /// <param name="auditEvents">Audit trail events; filtered to [periodStart, periodEnd].</param>
        /// <param name="periodStart">Inclusive start of the report period (UTC).</param>
        /// <param name="periodEnd">Inclusive end of the report period (UTC).</param>
        /// <param name="now">The point in time at which the report is assembled (UTC).</param>
        /// <returns>A populated <see cref="ClinicalReport"/>.</returns>
        public ClinicalReport BuildClinicalReport(
            OutcomeProfile outcome,
            IReadOnlyList<ClinicalNote> notes,
            IReadOnlyList<AuditEvent> auditEvents,
            DateTime periodStart,
            DateTime periodEnd,
            DateTime now)
        {
            if (outcome is null) throw new ArgumentNullException(nameof(outcome));
            if (notes is null) throw new ArgumentNullException(nameof(notes));
            if (auditEvents is null) throw new ArgumentNullException(nameof(auditEvents));

            var period = MakePeriod(periodStart, periodEnd, now);
            var title = $"Clinical Progress Report — {FormatPeriodLabel(periodStart, periodEnd)}";

            var filteredNotes = notes
                .Where(n => n.CreatedAt >= periodStart && n.CreatedAt <= periodEnd)
                .OrderBy(n => n.CreatedAt)
                .ToList();

            var filteredAudit = auditEvents
                .Where(e => e.OccurredAt >= periodStart && e.OccurredAt <= periodEnd)
                .OrderBy(e => e.OccurredAt)
                .ToList();

            return new ClinicalReport
            {
                Title = title,
                Period = period,
                Outcome = outcome,
                Notes = filteredNotes,
                AuditEvents = filteredAudit
            };
        }

        /// <summary>
        /// Builds a <see cref="CoachReport"/> from the outcome snapshot, emphasising focus
        /// areas, recommendations, and detected breakthroughs / plateaus from
        /// <see cref="LongTermDevelopment"/>.
        /// </summary>
        /// <param name="outcome">The assembled outcome snapshot — must not be null.</param>
        /// <param name="periodStart">Inclusive start of the report period (UTC).</param>
        /// <param name="periodEnd">Inclusive end of the report period (UTC).</param>
        /// <param name="now">The point in time at which the report is assembled (UTC).</param>
        /// <returns>A populated <see cref="CoachReport"/>.</returns>
        public CoachReport BuildCoachReport(
            OutcomeProfile outcome,
            DateTime periodStart,
            DateTime periodEnd,
            DateTime now)
        {
            if (outcome is null) throw new ArgumentNullException(nameof(outcome));

            var period = MakePeriod(periodStart, periodEnd, now);
            var title = $"Coaching Summary — {FormatPeriodLabel(periodStart, periodEnd)}";

            var ltd = outcome.LongTermDevelopment;

            var focusAreas = BuildFocusAreas(outcome);
            var recommendations = BuildRecommendations(outcome);

            return new CoachReport
            {
                Title = title,
                Period = period,
                Outcome = outcome,
                FocusAreas = focusAreas,
                Recommendations = recommendations,
                Breakthrough = ltd.Breakthrough,
                Plateau = ltd.Plateau,
                Regression = ltd.Regression,
                Insights = ltd.Insights
            };
        }

        /// <summary>
        /// Builds an <see cref="OutcomeReport"/> containing goal progress, recovery status,
        /// exercise effectiveness, and composite voice score.
        /// </summary>
        /// <param name="outcome">The assembled outcome snapshot — must not be null.</param>
        /// <param name="periodStart">Inclusive start of the report period (UTC).</param>
        /// <param name="periodEnd">Inclusive end of the report period (UTC).</param>
        /// <param name="now">The point in time at which the report is assembled (UTC).</param>
        /// <returns>A populated <see cref="OutcomeReport"/>.</returns>
        public OutcomeReport BuildOutcomeReport(
            OutcomeProfile outcome,
            DateTime periodStart,
            DateTime periodEnd,
            DateTime now)
        {
            if (outcome is null) throw new ArgumentNullException(nameof(outcome));

            var period = MakePeriod(periodStart, periodEnd, now);
            var title = $"Outcome Summary — {FormatPeriodLabel(periodStart, periodEnd)}";

            return new OutcomeReport
            {
                Title = title,
                Period = period,
                Outcome = outcome,
                HasEnoughData = outcome.HasEnoughData,
                CompositeVoiceScore = outcome.LongTermDevelopment.CompositeVoiceScore,
                GoalProgress = outcome.GoalProgress.Goals,
                RecoveryStatus = outcome.RecoveryProgress.Status,
                RecoveryScore = outcome.RecoveryProgress.CurrentScore0to100,
                TopExercises = outcome.ExerciseEffectiveness.Ranked
            };
        }

        /// <summary>
        /// Builds a <see cref="TimelineReport"/> from the WeeklyTrend and MonthlyTrend
        /// windows inside <see cref="OutcomeProfile.LongTermDevelopment"/>, ordered
        /// chronologically (earliest window first).
        /// </summary>
        /// <param name="outcome">The assembled outcome snapshot — must not be null.</param>
        /// <param name="periodStart">Inclusive start of the report period (UTC).</param>
        /// <param name="periodEnd">Inclusive end of the report period (UTC).</param>
        /// <param name="now">The point in time at which the report is assembled (UTC).</param>
        /// <returns>A populated <see cref="TimelineReport"/>.</returns>
        public TimelineReport BuildTimelineReport(
            OutcomeProfile outcome,
            DateTime periodStart,
            DateTime periodEnd,
            DateTime now)
        {
            if (outcome is null) throw new ArgumentNullException(nameof(outcome));

            var period = MakePeriod(periodStart, periodEnd, now);
            var title = $"Voice Development Timeline — {FormatPeriodLabel(periodStart, periodEnd)}";

            var ltd = outcome.LongTermDevelopment;

            // Merge weekly and monthly windows, deduplicate by From/WindowDays, sort chronologically.
            var allWindows = ltd.WeeklyTrend
                .Concat(ltd.MonthlyTrend)
                .OrderBy(w => w.From)
                .ThenBy(w => w.WindowDays)
                .ToList();

            var entries = allWindows.Select(w => new TimelineEntry
            {
                Label = $"{w.WindowDays}-day ({w.From:MMM d} – {w.To:MMM d, yyyy})",
                Window = w,
                Direction = ComputeDirection(w)
            }).ToList();

            return new TimelineReport
            {
                Title = title,
                Period = period,
                Outcome = outcome,
                TimelineEntries = entries
            };
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        private static ReportPeriod MakePeriod(DateTime start, DateTime end, DateTime now) =>
            new ReportPeriod
            {
                PeriodStart = start,
                PeriodEnd = end,
                GeneratedAt = now
            };

        private static string FormatPeriodLabel(DateTime start, DateTime end)
        {
            // Same month/year → "May 2026"; different → "Apr 1 – May 31, 2026"
            if (start.Year == end.Year && start.Month == end.Month)
                return end.ToString("MMMM yyyy", CultureInfo.InvariantCulture);

            return start.Year == end.Year
                ? $"{start.ToString("MMM d", CultureInfo.InvariantCulture)} – {end.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)}"
                : $"{start.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)} – {end.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)}";
        }

        private static string ComputeDirection(TrendWindow w)
        {
            if (!w.HasEnoughData) return "Insufficient data";
            if (w.CompositeSlope > 0.5) return "Improving";
            if (w.CompositeSlope < -0.5) return "Declining";
            return "Stable";
        }

        private static IReadOnlyList<string> BuildFocusAreas(OutcomeProfile outcome)
        {
            var areas = new List<string>();

            // Breakthrough / plateau / regression from long-term development
            var ltd = outcome.LongTermDevelopment;
            if (ltd.Breakthrough is not null)
                areas.Add($"{ltd.Breakthrough.Dimension} — breakthrough detected");
            if (ltd.Plateau is not null)
                areas.Add($"{ltd.Plateau.Dimension} — plateau detected");
            if (ltd.Regression is not null)
                areas.Add($"{ltd.Regression.Dimension} — regression detected");

            // Goals not yet achieved
            foreach (var goal in outcome.GoalProgress.Goals)
            {
                if (!goal.IsAchieved)
                    areas.Add($"{goal.PrimaryFocus} — goal in progress ({goal.PercentComplete:F0}%)");
                else
                    areas.Add($"{goal.PrimaryFocus} — goal achieved");
            }

            // Recovery concern
            if (outcome.RecoveryProgress.OvertrainingPredicted)
                areas.Add("Recovery — overtraining predicted");
            else if (!string.IsNullOrEmpty(outcome.RecoveryProgress.Status))
                areas.Add($"Recovery — {outcome.RecoveryProgress.Status}");

            // Exercise concerns
            foreach (var concern in outcome.ExerciseEffectiveness.Concerns)
                areas.Add($"Exercise {concern.ExerciseId} — {concern.ReasonCode}");

            return areas;
        }

        private static IReadOnlyList<string> BuildRecommendations(OutcomeProfile outcome)
        {
            var recs = new List<string>();

            // Recovery recommendation (highest priority)
            if (!string.IsNullOrEmpty(outcome.RecoveryProgress.RecommendationText))
                recs.Add(outcome.RecoveryProgress.RecommendationText);

            // Exercise de-prioritisation from concerns
            foreach (var concern in outcome.ExerciseEffectiveness.Concerns)
                recs.Add($"De-prioritise exercise {concern.ExerciseId}: {concern.Explanation}");

            // Longitudinal insights (top 3 by confidence)
            var topInsights = outcome.LongTermDevelopment.Insights
                .OrderByDescending(i => i.Confidence)
                .Take(3);
            foreach (var insight in topInsights)
            {
                if (!string.IsNullOrEmpty(insight.What))
                    recs.Add(insight.What);
            }

            return recs;
        }
    }
}
