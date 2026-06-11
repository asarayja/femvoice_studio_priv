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
            "Report_GenerationFailed",
            "Report_TimeWindow_Days",
            "Report_TimeWindow_LabelFormat",
            "Report_Term_Fatigue",
            "Report_Term_FatigueSignals",
            "Report_Term_VoiceFatigue",
            "Report_Term_VoiceFatigueSignals",
            "Report_Term_TrainingLoad",
            "Report_Term_RecentActivity",
            "Report_Term_RestBehindRecentActivity",
            "Report_Status_SIGNAL_LEVEL_COLLAPSES",
            "Report_Status_EXPECTED_SILENCE",
            "Report_Status_SILENCE_TAIL",
            "Report_Reason_COMFORT_DECLINE",
            "Report_Status_COMFORT_DECLINE",
            "Report_RecommendationComfortDeclineFormat",
            "ReportPdf_RecoveryCostShort"
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
        public void NorwegianCoachReport_DoesNotContainEnglishFatigueTerms()
        {
            LocalizationService.Instance.SetLanguage("nb");
            var outcome = MakeOutcome() with
            {
                ExerciseEffectiveness = new ExerciseEffectivenessSummary
                {
                    Concerns = new[]
                    {
                        new ExerciseEffectivenessFlag
                        {
                            ExerciseId = 1,
                            ReasonCode = "HIGH_FATIGUE",
                            Magnitude = 2.5,
                            Explanation = "Exercise 1 shows repeated fatigue signals."
                        }
                    }
                }
            };
            var report = new ReportAssembler().BuildCoachReport(outcome, T0, T1, Now);
            var text = string.Join("\n", report.FocusAreas.Concat(report.Recommendations));

            Assert.DoesNotContain("fatigue", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("fatigue-signaler", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Høy fatigue", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("stemmetretthet", text, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void NorwegianCoachReport_DoesNotContainRawComfortDeclineEnum()
        {
            LocalizationService.Instance.SetLanguage("nb");
            var outcome = MakeOutcome() with
            {
                ExerciseEffectiveness = new ExerciseEffectivenessSummary
                {
                    Concerns = new[]
                    {
                        new ExerciseEffectivenessFlag
                        {
                            ExerciseId = 3,
                            ReasonCode = "COMFORT_DECLINE",
                            Magnitude = -2.2,
                            Explanation = "Comfort has been easing down."
                        }
                    }
                }
            };
            var report = new ReportAssembler().BuildCoachReport(outcome, T0, T1, Now);
            var text = string.Join("\n", report.FocusAreas.Concat(report.Recommendations));

            Assert.DoesNotContain("COMFORT_DECLINE", text);
            Assert.DoesNotContain("_DECLINE", text);
            Assert.DoesNotContain("HIGH_", text);
            Assert.Contains("Redusert komfort", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Stigende toner", text, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void NorwegianTimelineReport_LocalizesWindowLabels()
        {
            LocalizationService.Instance.SetLanguage("nb");
            var report = new ReportAssembler().BuildTimelineReport(MakeOutcomeWithWindows(), T0, T1, Now);
            var labels = string.Join("\n", report.TimelineEntries.Select(e => e.Label));

            Assert.Contains("7 dager", labels);
            Assert.Contains("30 dager", labels);
            Assert.Contains("90 dager", labels);
            Assert.Contains("180 dager", labels);
            Assert.DoesNotContain("-day", labels, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(" days", labels, StringComparison.OrdinalIgnoreCase);
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
            var cleaned = ReportTextSanitizer.Clean("de\uFFFEprioritising æøå ÆØÅ é ü ñ ç ã ö ä ß – — −");

            Assert.Equal("deprioritising æøå ÆØÅ é ü ñ ç ã ö ä ß – — −", cleaned);
        }

        [Fact]
        public void ExportWriter_UsesCompactHeadersAndWiderRecoveryCostColumn()
        {
            var sourcePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
                "FemVoiceStudio", "Services", "ExportWriter.cs");
            var source = File.ReadAllText(sourcePath);

            Assert.Contains("columns.RelativeColumn(2.8f)", source);
            Assert.Contains(".FontSize(8)", source);
            Assert.Contains("ReportPdf_RecoveryCostShort", source);
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

        private static OutcomeProfile MakeOutcomeWithWindows()
        {
            var outcome = MakeOutcome();
            return outcome with
            {
                LongTermDevelopment = outcome.LongTermDevelopment with
                {
                    WeeklyTrend = new[]
                    {
                        Window(7, T0),
                        Window(30, T0.AddDays(8))
                    },
                    MonthlyTrend = new[]
                    {
                        Window(90, T0.AddDays(39)),
                        Window(180, T0.AddDays(130))
                    }
                }
            };
        }

        private static TrendWindow Window(int days, DateTime from) => new()
        {
            WindowDays = days,
            From = from,
            To = from.AddDays(days),
            CompositeSlope = 1.0,
            CompositeMean = 70,
            SessionCount = 4,
            HasEnoughData = true
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
