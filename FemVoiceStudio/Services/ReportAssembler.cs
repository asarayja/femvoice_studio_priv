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
        private static string T(string key) => LocalizationService.Instance.GetString(key);

        private static string Tf(string key, params object[] args) =>
            LocalizationService.Instance.GetFormattedString(key, args);

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
            var title = Tf("Report_TitleClinicalProgressFormat", FormatPeriodLabel(periodStart, periodEnd));

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
            var title = Tf("Report_TitleCoachingSummaryFormat", FormatPeriodLabel(periodStart, periodEnd));

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
            var title = Tf("Report_TitleOutcomeSummaryFormat", FormatPeriodLabel(periodStart, periodEnd));

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
            var title = Tf("Report_TitleVoiceTimelineFormat", FormatPeriodLabel(periodStart, periodEnd));

            var ltd = outcome.LongTermDevelopment;

            // Merge weekly and monthly windows, deduplicate by From/WindowDays, sort chronologically.
            var allWindows = ltd.WeeklyTrend
                .Concat(ltd.MonthlyTrend)
                .OrderBy(w => w.From)
                .ThenBy(w => w.WindowDays)
                .ToList();

            var entries = allWindows.Select(w => new TimelineEntry
            {
                Label = FormatWindowLabel(w),
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
            var culture = CultureInfo.CurrentUICulture;

            if (start.Year == end.Year && start.Month == end.Month)
                return end.ToString("MMMM yyyy", culture);

            return start.Year == end.Year
                ? $"{start.ToString("MMM d", culture)} – {end.ToString("MMM d, yyyy", culture)}"
                : $"{start.ToString("MMM d, yyyy", culture)} – {end.ToString("MMM d, yyyy", culture)}";
        }

        private static string FormatWindowLabel(TrendWindow window)
        {
            var culture = CultureInfo.CurrentUICulture;
            var windowName = Tf("Report_TimeWindow_Days", window.WindowDays);
            return Tf(
                "Report_TimeWindow_LabelFormat",
                windowName,
                window.From.ToString("MMM d", culture),
                window.To.ToString("MMM d, yyyy", culture));
        }

        private static string ComputeDirection(TrendWindow w)
        {
            if (!w.HasEnoughData) return T("Report_DirectionInsufficientData");
            if (w.CompositeSlope > 0.5) return T("Report_DirectionImproving");
            if (w.CompositeSlope < -0.5) return T("Report_DirectionDeclining");
            return T("Report_DirectionStable");
        }

        private static IReadOnlyList<string> BuildFocusAreas(OutcomeProfile outcome)
        {
            var areas = new List<string>();

            // Breakthrough / plateau / regression from long-term development
            var ltd = outcome.LongTermDevelopment;
            if (ltd.Breakthrough is not null)
                areas.Add(Tf("Report_FocusBreakthroughFormat", DimensionLabel(ltd.Breakthrough.Dimension)));
            if (ltd.Plateau is not null)
                areas.Add(Tf("Report_FocusPlateauFormat", DimensionLabel(ltd.Plateau.Dimension)));
            if (ltd.Regression is not null)
                areas.Add(Tf("Report_FocusRegressionFormat", DimensionLabel(ltd.Regression.Dimension)));

            // Goals not yet achieved
            foreach (var goal in outcome.GoalProgress.Goals)
            {
                if (!goal.IsAchieved)
                    areas.Add(Tf("Report_FocusGoalInProgressFormat", DimensionLabel(goal.PrimaryFocus), goal.PercentComplete));
                else
                    areas.Add(Tf("Report_FocusGoalAchievedFormat", DimensionLabel(goal.PrimaryFocus)));
            }

            // Recovery concern
            if (outcome.RecoveryProgress.OvertrainingPredicted)
                areas.Add(T("Report_FocusRecoveryOvertraining"));
            else if (!string.IsNullOrEmpty(outcome.RecoveryProgress.Status))
                areas.Add(Tf("Report_FocusRecoveryStatusFormat", LocalizeStatus(outcome.RecoveryProgress.Status)));

            // Exercise concerns
            foreach (var concern in outcome.ExerciseEffectiveness.Concerns)
                areas.Add(Tf("Report_FocusExerciseConcernFormat",
                    ResolveExerciseName(concern.ExerciseId),
                    LocalizeReasonCode(concern.ReasonCode)));

            return areas;
        }

        private static IReadOnlyList<string> BuildRecommendations(OutcomeProfile outcome)
        {
            var recs = new List<string>();

            // Recovery recommendation (highest priority)
            if (!string.IsNullOrEmpty(outcome.RecoveryProgress.RecommendationText))
                recs.Add(LocalizeRecoveryRecommendation(outcome.RecoveryProgress));

            // Exercise de-prioritisation from concerns
            foreach (var concern in outcome.ExerciseEffectiveness.Concerns)
                recs.Add(BuildConcernRecommendation(concern));

            // Longitudinal insights (top 3 by confidence)
            var topInsights = outcome.LongTermDevelopment.Insights
                .OrderByDescending(i => i.Confidence)
                .Take(3);
            foreach (var insight in topInsights)
            {
                if (!string.IsNullOrEmpty(insight.What))
                    recs.Add(LocalizeInsight(insight));
            }

            return recs;
        }

        public static string ResolveExerciseName(int exerciseId)
        {
            var titleKey = $"Exercise_{100 + exerciseId}_Title";
            var localized = T(titleKey);
            if (!string.IsNullOrWhiteSpace(localized) && localized != titleKey)
                return localized;

            var fallback = Tf("Report_ExerciseFallbackFormat", exerciseId);
            return !string.IsNullOrWhiteSpace(fallback) && fallback != "Report_ExerciseFallbackFormat"
                ? fallback
                : $"Exercise ID {exerciseId}";
        }

        private static string DimensionLabel(VoiceDimension dimension)
        {
            var key = $"Dimension_{dimension}";
            var localized = T(key);
            return string.IsNullOrWhiteSpace(localized) || localized == key
                ? dimension.ToString()
                : localized;
        }

        public static string LocalizeStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return string.Empty;

            var key = $"Report_Status_{status.Trim().ToUpperInvariant()}";
            var localized = T(key);
            return localized == key ? status : localized;
        }

        public static string LocalizeReasonCode(string reasonCode)
        {
            if (string.IsNullOrWhiteSpace(reasonCode))
                return string.Empty;

            var key = $"Report_Reason_{reasonCode.Trim().ToUpperInvariant()}";
            var localized = T(key);
            if (localized != key)
                return localized;

            Rc0RuntimeLog.Write("Localization",
                $"MissingReportReasonCode; Key={key}; RawReasonCode={reasonCode}");
            return HumanizeReasonCode(reasonCode);
        }

        public static string LocalizeRecoveryRecommendation(RecoveryProgress recovery)
        {
            if (recovery.OvertrainingPredicted
                || recovery.RecommendationText.Contains("rest day", StringComparison.OrdinalIgnoreCase)
                || recovery.RecommendationText.Contains("very light", StringComparison.OrdinalIgnoreCase))
            {
                return Tf("Report_RecoveryRecommendationRestFormat",
                    recovery.AcuteChronicWorkloadRatio,
                    recovery.RecoveryDebt);
            }

            return ReportTextSanitizer.Clean(recovery.RecommendationText);
        }

        private static string BuildConcernRecommendation(ExerciseEffectivenessFlag concern)
        {
            var exercise = ResolveExerciseName(concern.ExerciseId);
            var reason = concern.ReasonCode.Trim().ToUpperInvariant();

            return reason switch
            {
                "HIGH_FATIGUE" => Tf("Report_RecommendationHighFatigueFormat", exercise, concern.Magnitude),
                "HIGH_RECOVERY_COST" => Tf("Report_RecommendationHighRecoveryCostFormat", exercise, concern.Magnitude),
                "COMFORT_DECLINE" => Tf("Report_RecommendationComfortDeclineFormat", exercise, concern.Magnitude),
                _ => Tf("Report_RecommendationDeprioritizeFormat",
                    exercise,
                    LocalizeReasonCode(concern.ReasonCode))
            };
        }

        private static string HumanizeReasonCode(string reasonCode)
        {
            var words = reasonCode
                .Trim()
                .Replace('_', ' ')
                .ToLowerInvariant();

            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(words);
        }

        private static string LocalizeInsight(LongitudinalInsight insight)
        {
            if (string.Equals(insight.ReasonCode, "IMPROVEMENT", StringComparison.OrdinalIgnoreCase)
                && TryReadInsightDelta(insight, out var delta))
            {
                return Tf("Report_InsightImprovementFormat", DimensionLabel(insight.Dimension), delta);
            }

            return ReportTextSanitizer.Clean(insight.What);
        }

        private static bool TryReadInsightDelta(LongitudinalInsight insight, out double delta)
        {
            foreach (var item in insight.Evidence ?? Array.Empty<string>())
            {
                var parts = item.Split('=', 2);
                if (parts.Length == 2
                    && (parts[0].Equals("slope", StringComparison.OrdinalIgnoreCase)
                        || parts[0].Equals("delta", StringComparison.OrdinalIgnoreCase))
                    && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out delta))
                {
                    return true;
                }
            }

            delta = 0;
            return false;
        }
    }
}
