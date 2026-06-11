using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    [Collection("Localization")]
    public sealed class ReportLocalizationTests
    {
        private static readonly string[] ResourceFiles =
        {
            "Strings.resx",
            "Strings.en.resx",
            "Strings.sv-SE.resx",
            "Strings.da-DK.resx",
            "Strings.fi-FI.resx",
            "Strings.de-DE.resx",
            "Strings.fr-FR.resx",
            "Strings.es-ES.resx",
            "String.pt-BR.resx",
            "Strings.it-IT.resx",
            "Strings.hr-HR.resx"
        };

        private static readonly string[] CoreReportKeys =
        {
            "Report_TitleClinicalProgressFormat",
            "Report_TitleCoachingSummaryFormat",
            "Report_TitleOutcomeSummaryFormat",
            "Report_TitleVoiceTimelineFormat",
            "Report_DirectionImproving",
            "Report_DirectionStable",
            "Report_DirectionDeclining",
            "ReportPdf_Exercise",
            "Report_ExerciseFallbackFormat",
            "Report_Status_OVERTRAINED",
            "Report_Status_HIGH_FATIGUE",
            "Report_Status_HIGH_RECOVERY_COST",
            "Report_Status_PASS",
            "Report_Status_FAIL",
            "Report_Status_NOT_VERIFIED",
            "Report_Status_NOT_IMPLEMENTED",
            "Report_Reason_HIGH_FATIGUE",
            "Report_Reason_HIGH_RECOVERY_COST",
            "Report_RecoveryRecommendationRestFormat",
            "Report_RecommendationHighFatigueFormat",
            "Report_RecommendationHighRecoveryCostFormat",
            "Report_InsightImprovementFormat",
            "Report_GenerationFailed"
        };

        [Fact]
        public void AllSupportedLanguages_HaveCoreReportResourceKeys()
        {
            foreach (var file in ResourceFiles)
            {
                var values = LoadResx(file);
                foreach (var key in CoreReportKeys)
                    Assert.True(values.ContainsKey(key), $"{file} missing {key}");
            }
        }

        [Fact]
        public void NorwegianCoachReport_DoesNotContainObservedEnglishRecoveryRecommendation()
        {
            LocalizationService.Instance.SetLanguage("nb");
            var report = new ReportAssembler().BuildCoachReport(MakeOutcome(), T0, T1, Now);
            var text = string.Join("\n", report.Recommendations);

            Assert.DoesNotContain("A rest day or a very light", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("De-prioritise exercise", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("hviledag", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Grunnleggende humming", text, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void GermanReport_UsesLocalizedTitleAndDirectionLabels()
        {
            LocalizationService.Instance.SetLanguage("de-DE");
            var report = new ReportAssembler().BuildTimelineReport(MakeOutcome(), T0, T1, Now);

            Assert.Contains("Stimme", report.Title, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(report.TimelineEntries, e => e.Direction.Contains("Verbessert", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void OutcomeReport_ResolvesExerciseNamesInsteadOfRawIds()
        {
            LocalizationService.Instance.SetLanguage("nb");

            var name = ReportAssembler.ResolveExerciseName(1);

            Assert.Equal("Grunnleggende humming", name);
            Assert.DoesNotContain("Exercise-ID", name, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ReportTextSanitizer_RemovesBrokenControlArtifacts_ButKeepsLocalizedCharacters()
        {
            var cleaned = ReportTextSanitizer.Clean("de\uFFFEprioritising æøå é ü ñ ç ã ö ä ß – —");

            Assert.Equal("deprioritising æøå é ü ñ ç ã ö ä ß – —", cleaned);
        }

        private static readonly DateTime T0 = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime T1 = new(2026, 5, 31, 23, 59, 59, DateTimeKind.Utc);
        private static readonly DateTime Now = new(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

        private static OutcomeProfile MakeOutcome() => new()
        {
            UserId = 1,
            GeneratedAt = Now,
            HasEnoughData = true,
            RecoveryProgress = new RecoveryProgress
            {
                CurrentScore0to100 = 40,
                Status = "Overtrained",
                OvertrainingPredicted = true,
                RecoveryDebt = 62,
                AcuteChronicWorkloadRatio = 1.42,
                RecommendationText = "A rest day or a very light, easy session looks best right now."
            },
            ExerciseEffectiveness = new ExerciseEffectivenessSummary
            {
                Ranked = new[]
                {
                    new ExerciseEffectivenessProfile
                    {
                        ExerciseId = 1,
                        HasEnoughData = true,
                        CompositeEffectiveness = 70,
                        ResonanceGain = 1,
                        ComfortGain = 1,
                        RecoveryCost = 15,
                        UserSuccessRate = 80,
                        SessionCount = 4
                    }
                },
                Concerns = new[]
                {
                    new ExerciseEffectivenessFlag
                    {
                        ExerciseId = 1,
                        ReasonCode = "HIGH_RECOVERY_COST",
                        Magnitude = 72,
                        Explanation = "Exercise 1 is taxing recently."
                    }
                }
            },
            LongTermDevelopment = new LongTermDevelopment
            {
                CompositeVoiceScore = 70,
                WeeklyTrend = new[]
                {
                    new TrendWindow
                    {
                        WindowDays = 7,
                        From = T0,
                        To = T0.AddDays(7),
                        CompositeSlope = 1.0,
                        CompositeMean = 70,
                        SessionCount = 4,
                        HasEnoughData = true
                    }
                }
            }
        };

        private static Dictionary<string, string> LoadResx(string file)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
                "FemVoiceStudio", "Resources", file);
            return XDocument.Load(path)
                .Root!
                .Elements("data")
                .Where(e => e.Attribute("name") is not null)
                .ToDictionary(
                    e => e.Attribute("name")!.Value,
                    e => e.Element("value")?.Value ?? string.Empty);
        }
    }
}
