using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    public sealed class Rc0SystemVerificationReportTests
    {
        [Fact]
        public void GenerateInternalRc0VerificationReport_BlocksWithoutRuntimeAudioEvidence()
        {
            var evidence = Rc0VerificationEvidence.CreateBlockedBaseline();
            var writer = new Rc0VerificationReportWriter(new ReportAssembler());
            var output = writer.Write(evidence, FindRepositoryRoot());

            Assert.Equal("BLOCKED", output.Result);
            Assert.True(File.Exists(output.MarkdownPath));
            Assert.True(File.Exists(output.JsonPath));
            Assert.Contains("RC-0 Result: BLOCKED", File.ReadAllText(output.MarkdownPath));
            Assert.Contains("RC0_BLOCKER_AUDIO_RUNTIME_EVIDENCE_MISSING", File.ReadAllText(output.JsonPath));
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "FemVoiceStudio.slnx")))
                    return directory.FullName;

                directory = directory.Parent;
            }

            return Directory.GetCurrentDirectory();
        }
    }

    internal sealed class Rc0VerificationReportWriter
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly ReportAssembler _reportAssembler;

        public Rc0VerificationReportWriter(ReportAssembler reportAssembler)
        {
            _reportAssembler = reportAssembler;
        }

        public Rc0VerificationOutput Write(Rc0VerificationEvidence evidence, string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);

            var reportChecks = VerifyExistingReports(evidence);
            var result = ComputeResult(evidence, reportChecks);
            var resultEvidence = evidence with
            {
                Rc0Result = result,
                Reports = evidence.Reports with
                {
                    ClinicalReportGenerated = reportChecks.ClinicalReportGenerated,
                    CoachReportGenerated = reportChecks.CoachReportGenerated,
                    OutcomeReportGenerated = reportChecks.OutcomeReportGenerated,
                    TimelineReportGenerated = reportChecks.TimelineReportGenerated
                }
            };

            var markdownPath = Path.Combine(outputDirectory, "RC0_VERIFICATION_REPORT.md");
            var jsonPath = Path.Combine(outputDirectory, "RC0_VERIFICATION_EVIDENCE.json");

            File.WriteAllText(markdownPath, BuildMarkdown(resultEvidence, reportChecks), Encoding.UTF8);
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(resultEvidence, JsonOptions), Encoding.UTF8);

            return new Rc0VerificationOutput(result, markdownPath, jsonPath);
        }

        private Rc0ReportChecks VerifyExistingReports(Rc0VerificationEvidence evidence)
        {
            try
            {
                var outcome = CreateOutcomeProfile(evidence);
                var periodStart = evidence.Session.StartTime ?? evidence.GeneratedAt.AddMinutes(-30);
                var periodEnd = evidence.Session.EndTime ?? evidence.GeneratedAt;
                var now = evidence.GeneratedAt;

                var clinical = _reportAssembler.BuildClinicalReport(
                    outcome,
                    Array.Empty<ClinicalNote>(),
                    Array.Empty<AuditEvent>(),
                    periodStart,
                    periodEnd,
                    now);
                var coach = _reportAssembler.BuildCoachReport(outcome, periodStart, periodEnd, now);
                var outcomeReport = _reportAssembler.BuildOutcomeReport(outcome, periodStart, periodEnd, now);
                var timeline = _reportAssembler.BuildTimelineReport(outcome, periodStart, periodEnd, now);

                return new Rc0ReportChecks(
                    !string.IsNullOrWhiteSpace(clinical.Title),
                    !string.IsNullOrWhiteSpace(coach.Title),
                    !string.IsNullOrWhiteSpace(outcomeReport.Title),
                    !string.IsNullOrWhiteSpace(timeline.Title),
                    Array.Empty<string>());
            }
            catch (Exception ex)
            {
                return new Rc0ReportChecks(false, false, false, false, new[] { ex.Message });
            }
        }

        private static OutcomeProfile CreateOutcomeProfile(Rc0VerificationEvidence evidence)
        {
            var start = evidence.Session.StartTime ?? evidence.GeneratedAt.AddMinutes(-30);
            var end = evidence.Session.EndTime ?? evidence.GeneratedAt;

            return new OutcomeProfile
            {
                UserId = 1,
                GeneratedAt = evidence.GeneratedAt,
                HasEnoughData = evidence.Analytics.MetricsStored && evidence.Persistence.AnalyticsSaved,
                GoalProgress = new GoalProgress
                {
                    Goals = new[]
                    {
                        new GoalProgressEntry
                        {
                            GoalType = "rc0-verification",
                            PrimaryFocus = VoiceDimension.Resonance,
                            TargetValue = 1,
                            CurrentValue = evidence.Analytics.MetricsStored ? 1 : 0,
                            DeltaToGoal = evidence.Analytics.MetricsStored ? 0 : 1,
                            PercentComplete = evidence.Analytics.MetricsStored ? 100 : 0,
                            IsAchieved = evidence.Analytics.MetricsStored
                        }
                    }
                },
                RecoveryProgress = new RecoveryProgress
                {
                    CurrentScore0to100 = evidence.VoiceHealth.NoInvalidClinicalWarnings ? 80 : 40,
                    Status = evidence.VoiceHealth.NoInvalidClinicalWarnings ? "WellRecovered" : "NeedsReview",
                    RecommendationText = evidence.VoiceHealth.NoInvalidClinicalWarnings
                        ? "No invalid clinical warnings detected by RC-0 verification evidence."
                        : "Review voice-health warnings before RC-0 approval."
                },
                LongTermDevelopment = new LongTermDevelopment
                {
                    CompositeVoiceScore = evidence.Analytics.MetricsStored ? 75 : 0,
                    WeeklyTrend = new[]
                    {
                        new TrendWindow
                        {
                            WindowDays = 7,
                            From = start,
                            To = end,
                            CompositeSlope = evidence.Analytics.TrendAnalysisUpdated ? 1 : 0,
                            CompositeMean = evidence.Analytics.MetricsStored ? 75 : 0,
                            SessionCount = evidence.Persistence.SessionLoaded ? 1 : 0,
                            Confidence = evidence.Analytics.DataCanBeReadBack ? 80 : 0,
                            HasEnoughData = evidence.Analytics.DataCanBeReadBack
                        }
                    }
                }
            };
        }

        private static string ComputeResult(Rc0VerificationEvidence evidence, Rc0ReportChecks reportChecks)
        {
            if (evidence.RcBlockers.Count > 0)
                return "BLOCKED";

            var pass =
                evidence.Audio.AudioCaptureStarted &&
                evidence.Audio.RecordingActive &&
                evidence.Audio.DataAvailableTriggered &&
                evidence.Audio.AudioSamplesReceived > 0 &&
                evidence.Audio.PitchValuesReceived > 0 &&
                evidence.Audio.ResonanceValuesReceived > 0 &&
                evidence.Guidance.GuidanceItemsLoaded &&
                evidence.Guidance.GuidanceStepCount > 0 &&
                evidence.Guidance.EmptyGuidanceCheck &&
                evidence.Guidance.LocalizationCheck &&
                evidence.Guidance.MojibakeInvalidCharacterCheck &&
                evidence.Guidance.IconCheck &&
                evidence.SmartCoach.SmartCoachLoaded &&
                evidence.SmartCoach.SmartCoachInsightGenerated &&
                evidence.SmartCoach.DateTimeParsingOk &&
                evidence.SmartCoach.NoFallbackDefaultInvalidData &&
                evidence.SmartCoach.SmartCoachPersistenceOk &&
                evidence.VoiceHealth.FatigueDetectionPolicyExecuted &&
                evidence.VoiceHealth.StrainDetectionPolicyExecuted &&
                evidence.VoiceHealth.RecoveryPolicyExecuted &&
                evidence.VoiceHealth.HydrationAdvisorExecuted &&
                evidence.VoiceHealth.NoInvalidClinicalWarnings &&
                evidence.Analytics.SessionAnalyticsStoreUpdated &&
                evidence.Analytics.TrendAnalysisUpdated &&
                evidence.Analytics.MetricsStored &&
                evidence.Analytics.DataCanBeReadBack &&
                evidence.Analytics.NoMissingAnalyticsFields &&
                reportChecks.AllGenerated &&
                evidence.Persistence.SessionSaved &&
                evidence.Persistence.SessionLoaded &&
                evidence.Persistence.ExerciseDataSaved &&
                evidence.Persistence.AnalyticsSaved &&
                evidence.Persistence.SmartCoachDataSaved &&
                evidence.Persistence.VoiceHealthDataSaved;

            return pass ? "PASS" : "FAIL";
        }

        private static string BuildMarkdown(Rc0VerificationEvidence evidence, Rc0ReportChecks reportChecks)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# FemVoice RC-0 System Verification Report");
            sb.AppendLine();
            sb.AppendLine($"Generated at: {evidence.GeneratedAt:O}");
            sb.AppendLine($"Evidence ID: `{evidence.EvidenceId}`");
            sb.AppendLine($"RC-0 Result: {evidence.Rc0Result}");
            sb.AppendLine();

            Section(sb, "1. Session Summary",
                ("Session ID", Value(evidence.Session.SessionId)),
                ("Exercise ID", Value(evidence.Session.ExerciseId)),
                ("Exercise name", evidence.Session.ExerciseName),
                ("Language", evidence.Session.Language),
                ("Start time", Value(evidence.Session.StartTime)),
                ("End time", Value(evidence.Session.EndTime)),
                ("Duration", evidence.Session.Duration?.ToString() ?? "not captured"),
                ("Completion status", evidence.Session.CompletionStatus));

            Section(sb, "2. Audio Pipeline Verification",
                ("AudioCapture started", PassFail(evidence.Audio.AudioCaptureStarted)),
                ("Recording active", PassFail(evidence.Audio.RecordingActive)),
                ("DataAvailable triggered", PassFail(evidence.Audio.DataAvailableTriggered)),
                ("Audio samples received", evidence.Audio.AudioSamplesReceived.ToString()),
                ("Pitch values received", evidence.Audio.PitchValuesReceived.ToString()),
                ("Resonance values received", evidence.Audio.ResonanceValuesReceived.ToString()),
                ("Monitoring active", PassFail(evidence.Audio.MonitoringActive)),
                ("Audio errors", evidence.Audio.AudioErrors.Count == 0 ? "none" : string.Join("; ", evidence.Audio.AudioErrors)));

            Section(sb, "3. Exercise Guidance Verification",
                ("Guidance items loaded", PassFail(evidence.Guidance.GuidanceItemsLoaded)),
                ("Number of guidance steps", evidence.Guidance.GuidanceStepCount.ToString()),
                ("Empty guidance check", PassFail(evidence.Guidance.EmptyGuidanceCheck)),
                ("Localization check", PassFail(evidence.Guidance.LocalizationCheck)),
                ("Mojibake / invalid character check", PassFail(evidence.Guidance.MojibakeInvalidCharacterCheck)),
                ("Icon check", PassFail(evidence.Guidance.IconCheck)));

            Section(sb, "4. SmartCoach Verification",
                ("SmartCoach loaded", PassFail(evidence.SmartCoach.SmartCoachLoaded)),
                ("SmartCoach insight generated", PassFail(evidence.SmartCoach.SmartCoachInsightGenerated)),
                ("DateTime parsing OK", PassFail(evidence.SmartCoach.DateTimeParsingOk)),
                ("No fallback/default invalid data", PassFail(evidence.SmartCoach.NoFallbackDefaultInvalidData)),
                ("SmartCoach persistence OK", PassFail(evidence.SmartCoach.SmartCoachPersistenceOk)));

            Section(sb, "5. Voice Health Verification",
                ("FatigueDetectionPolicy executed", PassFail(evidence.VoiceHealth.FatigueDetectionPolicyExecuted)),
                ("StrainDetectionPolicy executed", PassFail(evidence.VoiceHealth.StrainDetectionPolicyExecuted)),
                ("RecoveryPolicy executed", PassFail(evidence.VoiceHealth.RecoveryPolicyExecuted)),
                ("HydrationAdvisor executed", PassFail(evidence.VoiceHealth.HydrationAdvisorExecuted)),
                ("No invalid clinical warnings", PassFail(evidence.VoiceHealth.NoInvalidClinicalWarnings)));

            Section(sb, "6. Analytics Verification",
                ("SessionAnalyticsStore updated", PassFail(evidence.Analytics.SessionAnalyticsStoreUpdated)),
                ("Trend analysis updated", PassFail(evidence.Analytics.TrendAnalysisUpdated)),
                ("Metrics stored", PassFail(evidence.Analytics.MetricsStored)),
                ("Data can be read back", PassFail(evidence.Analytics.DataCanBeReadBack)),
                ("No missing analytics fields", PassFail(evidence.Analytics.NoMissingAnalyticsFields)));

            Section(sb, "7. Reports Verification",
                ("Clinical Report generated", PassFail(reportChecks.ClinicalReportGenerated)),
                ("Coach Report generated", PassFail(reportChecks.CoachReportGenerated)),
                ("Outcome Report generated", PassFail(reportChecks.OutcomeReportGenerated)),
                ("Timeline Report generated", PassFail(reportChecks.TimelineReportGenerated)));

            Section(sb, "8. Persistence Verification",
                ("Session saved", PassFail(evidence.Persistence.SessionSaved)),
                ("Session loaded", PassFail(evidence.Persistence.SessionLoaded)),
                ("Exercise data saved", PassFail(evidence.Persistence.ExerciseDataSaved)),
                ("Analytics saved", PassFail(evidence.Persistence.AnalyticsSaved)),
                ("SmartCoach data saved", PassFail(evidence.Persistence.SmartCoachDataSaved)),
                ("Voice Health data saved", PassFail(evidence.Persistence.VoiceHealthDataSaved)));

            sb.AppendLine("## 9. RC-0 Result");
            sb.AppendLine();
            sb.AppendLine($"Result: **{evidence.Rc0Result}**");
            sb.AppendLine();
            sb.AppendLine("RC blockers:");
            foreach (var blocker in evidence.RcBlockers.DefaultIfEmpty("none"))
                sb.AppendLine($"- {blocker}");
            sb.AppendLine();
            sb.AppendLine("Warnings:");
            foreach (var warning in evidence.Warnings.DefaultIfEmpty("none"))
                sb.AppendLine($"- {warning}");
            sb.AppendLine();
            sb.AppendLine("Log references:");
            foreach (var logRef in evidence.LogReferences.DefaultIfEmpty("none"))
                sb.AppendLine($"- {logRef}");
            sb.AppendLine();
            sb.AppendLine($"Suggested next action: {evidence.SuggestedNextAction}");

            return sb.ToString();
        }

        private static void Section(StringBuilder sb, string title, params (string Label, string Value)[] rows)
        {
            sb.AppendLine($"## {title}");
            sb.AppendLine();
            foreach (var row in rows)
                sb.AppendLine($"- {row.Label}: {row.Value}");
            sb.AppendLine();
        }

        private static string PassFail(bool value) => value ? "PASS" : "FAIL";
        private static string Value(int? value) => value?.ToString() ?? "not captured";
        private static string Value(DateTime? value) => value?.ToString("O") ?? "not captured";
    }

    internal sealed record Rc0VerificationOutput(string Result, string MarkdownPath, string JsonPath);

    internal sealed record Rc0ReportChecks(
        bool ClinicalReportGenerated,
        bool CoachReportGenerated,
        bool OutcomeReportGenerated,
        bool TimelineReportGenerated,
        IReadOnlyList<string> Errors)
    {
        public bool AllGenerated =>
            ClinicalReportGenerated &&
            CoachReportGenerated &&
            OutcomeReportGenerated &&
            TimelineReportGenerated &&
            Errors.Count == 0;
    }

    internal sealed record Rc0VerificationEvidence
    {
        public string EvidenceId { get; init; } = "";
        public DateTime GeneratedAt { get; init; }
        public string Rc0Result { get; init; } = "BLOCKED";
        public Rc0SessionSummary Session { get; init; } = new();
        public Rc0AudioEvidence Audio { get; init; } = new();
        public Rc0GuidanceEvidence Guidance { get; init; } = new();
        public Rc0SmartCoachEvidence SmartCoach { get; init; } = new();
        public Rc0VoiceHealthEvidence VoiceHealth { get; init; } = new();
        public Rc0AnalyticsEvidence Analytics { get; init; } = new();
        public Rc0ReportsEvidence Reports { get; init; } = new();
        public Rc0PersistenceEvidence Persistence { get; init; } = new();
        public IReadOnlyList<string> RcBlockers { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> LogReferences { get; init; } = Array.Empty<string>();
        public string SuggestedNextAction { get; init; } = "";

        public static Rc0VerificationEvidence CreateBlockedBaseline()
        {
            var now = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc);
            return new Rc0VerificationEvidence
            {
                EvidenceId = "RC0-BLOCKED-NO-RUNTIME-AUDIO",
                GeneratedAt = now,
                Session = new Rc0SessionSummary
                {
                    Language = "nb-NO",
                    CompletionStatus = "not verified"
                },
                RcBlockers = new[]
                {
                    "RC0_BLOCKER_AUDIO_RUNTIME_EVIDENCE_MISSING"
                },
                Warnings = new[]
                {
                    "This internal report harness is present, but no real exercise-session evidence has been captured in this test run.",
                    "RC-0 cannot be marked PASS until DataAvailable and audio-derived pitch/resonance samples are recorded from runtime."
                },
                LogReferences = new[]
                {
                    "Run the app or an integration harness with microphone capture enabled, then populate this evidence object from runtime counters."
                },
                SuggestedNextAction =
                    "Run a real exercise session with audio capture instrumentation enabled and regenerate this report from captured runtime counters."
            };
        }
    }

    internal sealed record Rc0SessionSummary
    {
        public int? SessionId { get; init; }
        public int? ExerciseId { get; init; }
        public string ExerciseName { get; init; } = "not captured";
        public string Language { get; init; } = "not captured";
        public DateTime? StartTime { get; init; }
        public DateTime? EndTime { get; init; }
        public TimeSpan? Duration { get; init; }
        public string CompletionStatus { get; init; } = "not captured";
    }

    internal sealed record Rc0AudioEvidence
    {
        public bool AudioCaptureStarted { get; init; }
        public bool RecordingActive { get; init; }
        public bool DataAvailableTriggered { get; init; }
        public int AudioSamplesReceived { get; init; }
        public int PitchValuesReceived { get; init; }
        public int ResonanceValuesReceived { get; init; }
        public bool MonitoringActive { get; init; }
        public IReadOnlyList<string> AudioErrors { get; init; } = Array.Empty<string>();
    }

    internal sealed record Rc0GuidanceEvidence
    {
        public bool GuidanceItemsLoaded { get; init; }
        public int GuidanceStepCount { get; init; }
        public bool EmptyGuidanceCheck { get; init; }
        public bool LocalizationCheck { get; init; }
        public bool MojibakeInvalidCharacterCheck { get; init; }
        public bool IconCheck { get; init; }
    }

    internal sealed record Rc0SmartCoachEvidence
    {
        public bool SmartCoachLoaded { get; init; }
        public bool SmartCoachInsightGenerated { get; init; }
        public bool DateTimeParsingOk { get; init; }
        public bool NoFallbackDefaultInvalidData { get; init; }
        public bool SmartCoachPersistenceOk { get; init; }
    }

    internal sealed record Rc0VoiceHealthEvidence
    {
        public bool FatigueDetectionPolicyExecuted { get; init; }
        public bool StrainDetectionPolicyExecuted { get; init; }
        public bool RecoveryPolicyExecuted { get; init; }
        public bool HydrationAdvisorExecuted { get; init; }
        public bool NoInvalidClinicalWarnings { get; init; }
    }

    internal sealed record Rc0AnalyticsEvidence
    {
        public bool SessionAnalyticsStoreUpdated { get; init; }
        public bool TrendAnalysisUpdated { get; init; }
        public bool MetricsStored { get; init; }
        public bool DataCanBeReadBack { get; init; }
        public bool NoMissingAnalyticsFields { get; init; }
    }

    internal sealed record Rc0ReportsEvidence
    {
        public bool ClinicalReportGenerated { get; init; }
        public bool CoachReportGenerated { get; init; }
        public bool OutcomeReportGenerated { get; init; }
        public bool TimelineReportGenerated { get; init; }
    }

    internal sealed record Rc0PersistenceEvidence
    {
        public bool SessionSaved { get; init; }
        public bool SessionLoaded { get; init; }
        public bool ExerciseDataSaved { get; init; }
        public bool AnalyticsSaved { get; init; }
        public bool SmartCoachDataSaved { get; init; }
        public bool VoiceHealthDataSaved { get; init; }
    }
}
