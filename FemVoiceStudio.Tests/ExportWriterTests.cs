using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Tests for <see cref="ExportWriter"/> (W0-A3A10).
    ///
    /// Verified invariants:
    /// <list type="bullet">
    ///   <item><description>JSON round-trip — serialise → deserialise preserves all fields.</description></item>
    ///   <item><description>CSV RFC 4180 escaping — field with comma, double-quote, and line-break is
    ///   correctly quoted.</description></item>
    ///   <item><description>PDF output starts with <c>%PDF</c> magic, confirming QuestPDF licence is
    ///   set and the call-chain is correct.</description></item>
    /// </list>
    ///
    /// All tests use <see cref="MemoryStream"/>. No disk I/O.
    /// </summary>
    public class ExportWriterTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime T1 = new DateTime(2026, 5, 31, 23, 59, 59, DateTimeKind.Utc);
        private static readonly DateTime Now = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

        private readonly ExportWriter _writer = new();

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static ReportPeriod MakePeriod() => new ReportPeriod
        {
            PeriodStart = T0,
            PeriodEnd = T1,
            GeneratedAt = Now
        };

        private static OutcomeProfile MakeMinimalOutcome() => new OutcomeProfile
        {
            UserId = 7,
            GeneratedAt = Now,
            HasEnoughData = true,
            GoalProgress = new GoalProgress
            {
                Goals = new List<GoalProgressEntry>
                {
                    new GoalProgressEntry
                    {
                        GoalType = "pitch",
                        PrimaryFocus = VoiceDimension.Pitch,
                        TargetValue = 220.0,
                        CurrentValue = 195.0,
                        DeltaToGoal = 25.0,
                        PercentComplete = 75.0,
                        IsAchieved = false
                    }
                }
            },
            RecoveryProgress = new RecoveryProgress
            {
                CurrentScore0to100 = 80.0,
                Status = "WellRecovered",
                OvertrainingPredicted = false,
                RecoveryDebt = 5.0,
                AcuteChronicWorkloadRatio = 1.0,
                Severity = "None",
                RecommendationText = "Keep up the good work."
            },
            ExerciseEffectiveness = new ExerciseEffectivenessSummary
            {
                Ranked = new List<ExerciseEffectivenessProfile>
                {
                    new ExerciseEffectivenessProfile
                    {
                        ExerciseId = 2,
                        ResonanceGain = 1.5,
                        ComfortGain = 0.8,
                        ConsistencyGain = 0.5,
                        RecoveryCost = 15.0,
                        UserSuccessRate = 90.0,
                        SessionCount = 8,
                        HasEnoughData = true,
                        CompositeEffectiveness = 68.0,
                        Explanation = "Exercise 2 shows steady resonance gain."
                    }
                },
                Concerns = Array.Empty<ExerciseEffectivenessFlag>()
            },
            LongTermDevelopment = new LongTermDevelopment
            {
                CompositeVoiceScore = 65.0,
                WeeklyTrend = new List<TrendWindow>
                {
                    new TrendWindow
                    {
                        WindowDays = 7,
                        From = T0,
                        To = T0.AddDays(7),
                        CompositeSlope = 0.8,
                        CompositeMean = 64.0,
                        CompositeMin = 58.0,
                        CompositeMax = 70.0,
                        SessionCount = 4,
                        Confidence = 65.0,
                        HasEnoughData = true,
                        DimensionSlopes = new Dictionary<VoiceDimension, double>
                        {
                            { VoiceDimension.Resonance, 0.9 }
                        }
                    }
                },
                MonthlyTrend = Array.Empty<TrendWindow>(),
                Breakthrough = null,
                Plateau = null,
                Regression = null,
                Insights = Array.Empty<LongitudinalInsight>()
            }
        };

        private static OutcomeReport MakeOutcomeReport() =>
            new OutcomeReport
            {
                Title = "Outcome Summary — May 2026",
                Period = MakePeriod(),
                Outcome = MakeMinimalOutcome(),
                HasEnoughData = true,
                CompositeVoiceScore = 65.0,
                GoalProgress = MakeMinimalOutcome().GoalProgress.Goals,
                RecoveryStatus = "WellRecovered",
                RecoveryScore = 80.0,
                TopExercises = MakeMinimalOutcome().ExerciseEffectiveness.Ranked
            };

        private static ClinicalReport MakeClinicalReport() =>
            new ClinicalReport
            {
                Title = "Clinical Progress Report — May 2026",
                Period = MakePeriod(),
                Outcome = MakeMinimalOutcome(),
                Notes = new List<ClinicalNote>
                {
                    new ClinicalNote
                    {
                        NoteId = Guid.NewGuid(),
                        UserId = 7,
                        NoteType = ClinicalNoteType.Coach,
                        AuthorRole = "Coach",
                        CreatedAt = T0.AddDays(10),
                        BodyText = "Session went well."
                    }
                },
                AuditEvents = Array.Empty<AuditEvent>()
            };

        private static TimelineReport MakeTimelineReport()
        {
            var outcome = MakeMinimalOutcome();
            return new TimelineReport
            {
                Title = "Voice Development Timeline — May 2026",
                Period = MakePeriod(),
                Outcome = outcome,
                TimelineEntries = new List<TimelineEntry>
                {
                    new TimelineEntry
                    {
                        Label = "7-day (May 1 – May 7, 2026)",
                        Window = outcome.LongTermDevelopment.WeeklyTrend[0],
                        Direction = "Improving"
                    }
                }
            };
        }

        // ── JSON round-trip ───────────────────────────────────────────────────────

        [Fact]
        public void WriteJson_OutcomeReport_RoundTrip_PreservesKeyFields()
        {
            var report = MakeOutcomeReport();
            using var ms = new MemoryStream();

            _writer.WriteJson(report, ms);

            ms.Position = 0;
            var json = Encoding.UTF8.GetString(ms.ToArray());
            Assert.False(string.IsNullOrWhiteSpace(json));

            var options = new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() }
            };
            var restored = JsonSerializer.Deserialize<OutcomeReport>(json, options);

            Assert.NotNull(restored);
            Assert.Equal(report.Title, restored!.Title);
            Assert.Equal(report.Period.PeriodStart, restored.Period.PeriodStart);
            Assert.Equal(report.Period.PeriodEnd, restored.Period.PeriodEnd);
            Assert.Equal(report.Period.GeneratedAt, restored.Period.GeneratedAt);
            Assert.Equal(report.CompositeVoiceScore, restored.CompositeVoiceScore);
            Assert.Equal(report.RecoveryStatus, restored.RecoveryStatus);
            Assert.Equal(report.RecoveryScore, restored.RecoveryScore);
            Assert.Equal(report.HasEnoughData, restored.HasEnoughData);
        }

        [Fact]
        public void WriteJson_ClinicalReport_RoundTrip_PreservesNoteCount()
        {
            var report = MakeClinicalReport();
            using var ms = new MemoryStream();

            _writer.WriteJson(report, ms);

            ms.Position = 0;
            var json = Encoding.UTF8.GetString(ms.ToArray());
            var options = new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() }
            };
            var restored = JsonSerializer.Deserialize<ClinicalReport>(json, options);

            Assert.NotNull(restored);
            Assert.Equal(report.Title, restored!.Title);
            Assert.Equal(report.Notes.Count, restored.Notes.Count);
        }

        [Fact]
        public void WriteJson_ProducesIndentedJson()
        {
            using var ms = new MemoryStream();
            _writer.WriteJson(MakeOutcomeReport(), ms);

            ms.Position = 0;
            var json = Encoding.UTF8.GetString(ms.ToArray());

            // Indented JSON contains newlines
            Assert.Contains("\n", json);
        }

        [Fact]
        public void Write_Dispatch_Json_ProducesNonEmptyStream()
        {
            using var ms = new MemoryStream();
            _writer.Write(MakeOutcomeReport(), ExportFormat.Json, ms);
            Assert.True(ms.Length > 0);
        }

        // ── CSV escaping ──────────────────────────────────────────────────────────

        [Fact]
        public void EscapeCsvCell_PlainField_ReturnedUnchanged()
        {
            var result = ExportWriter.EscapeCsvCell("plainvalue");
            Assert.Equal("plainvalue", result);
        }

        [Fact]
        public void EscapeCsvCell_FieldWithComma_IsQuoted()
        {
            var result = ExportWriter.EscapeCsvCell("hello, world");
            Assert.Equal("\"hello, world\"", result);
        }

        [Fact]
        public void EscapeCsvCell_FieldWithDoubleQuote_IsQuotedWithInternalQuotesDoubled()
        {
            // Input: She said "hello"
            // Expected RFC 4180: "She said ""hello"""
            var result = ExportWriter.EscapeCsvCell("She said \"hello\"");
            Assert.Equal("\"She said \"\"hello\"\"\"", result);
        }

        [Fact]
        public void EscapeCsvCell_FieldWithLineFeed_IsQuoted()
        {
            var result = ExportWriter.EscapeCsvCell("line1\nline2");
            Assert.Equal("\"line1\nline2\"", result);
        }

        [Fact]
        public void EscapeCsvCell_FieldWithCarriageReturn_IsQuoted()
        {
            var result = ExportWriter.EscapeCsvCell("line1\rline2");
            Assert.Equal("\"line1\rline2\"", result);
        }

        /// <summary>
        /// A field containing a comma, a double-quote, AND a line-break — all three RFC 4180
        /// special characters at once — must be enclosed in double-quotes with the internal
        /// double-quote doubled.
        /// </summary>
        [Fact]
        public void EscapeCsvCell_FieldWithCommaAndQuoteAndNewline_IsCorrectlyQuoted()
        {
            // Input: hello, "world"\nfoo
            var input = "hello, \"world\"\nfoo";
            var result = ExportWriter.EscapeCsvCell(input);

            // Must start and end with a double-quote
            Assert.StartsWith("\"", result);
            Assert.EndsWith("\"", result);

            // The inner content (strip outer quotes) must have the double-quote doubled
            var inner = result.Substring(1, result.Length - 2);
            Assert.Contains("\"\"world\"\"", inner);

            // Comma and newline are preserved as-is (inside quotes)
            Assert.Contains(",", inner);
            Assert.Contains("\n", inner);

            // Full expected value
            Assert.Equal("\"hello, \"\"world\"\"\nfoo\"", result);
        }

        [Fact]
        public void WriteCsv_OutcomeReport_ProducesNonEmptyStream()
        {
            using var ms = new MemoryStream();
            _writer.WriteCsv(MakeOutcomeReport(), ms);

            Assert.True(ms.Length > 0);
            ms.Position = 0;
            var csv = Encoding.UTF8.GetString(ms.ToArray());
            Assert.Contains("CompositeVoiceScore", csv);
        }

        [Fact]
        public void WriteCsv_TimelineReport_ContainsHeaderAndEntry()
        {
            using var ms = new MemoryStream();
            _writer.WriteCsv(MakeTimelineReport(), ms);

            ms.Position = 0;
            var csv = Encoding.UTF8.GetString(ms.ToArray());
            Assert.Contains("Label", csv);
            Assert.Contains("Direction", csv);
            Assert.Contains("Improving", csv);
        }

        [Fact]
        public void Write_Dispatch_Csv_ProducesNonEmptyStream()
        {
            using var ms = new MemoryStream();
            _writer.Write(MakeOutcomeReport(), ExportFormat.Csv, ms);
            Assert.True(ms.Length > 0);
        }

        // ── PDF generation ────────────────────────────────────────────────────────

        /// <summary>
        /// Verifies that the QuestPDF Community licence is set and the full call-chain
        /// executes correctly: WritePdf must produce a non-empty stream whose first four
        /// bytes are the PDF file-magic %PDF.
        /// </summary>
        [Fact]
        public void WritePdf_OutcomeReport_ProducesStreamWithPdfMagic()
        {
            using var ms = new MemoryStream();
            _writer.WritePdf(MakeOutcomeReport(), ms);

            Assert.True(ms.Length > 0, "WritePdf produced an empty stream.");

            ms.Position = 0;
            var header = new byte[4];
            ms.Read(header, 0, 4);
            var magic = Encoding.ASCII.GetString(header);
            Assert.Equal("%PDF", magic);
        }

        [Fact]
        public void WritePdf_ClinicalReport_ProducesStreamWithPdfMagic()
        {
            using var ms = new MemoryStream();
            _writer.WritePdf(MakeClinicalReport(), ms);

            Assert.True(ms.Length > 0);
            ms.Position = 0;
            var header = new byte[4];
            ms.Read(header, 0, 4);
            Assert.Equal("%PDF", Encoding.ASCII.GetString(header));
        }

        [Fact]
        public void WritePdf_TimelineReport_ProducesStreamWithPdfMagic()
        {
            using var ms = new MemoryStream();
            _writer.WritePdf(MakeTimelineReport(), ms);

            Assert.True(ms.Length > 0);
            ms.Position = 0;
            var header = new byte[4];
            ms.Read(header, 0, 4);
            Assert.Equal("%PDF", Encoding.ASCII.GetString(header));
        }

        [Fact]
        public void Write_Dispatch_Pdf_ProducesStreamWithPdfMagic()
        {
            using var ms = new MemoryStream();
            _writer.Write(MakeOutcomeReport(), ExportFormat.Pdf, ms);

            Assert.True(ms.Length > 0);
            ms.Position = 0;
            var header = new byte[4];
            ms.Read(header, 0, 4);
            Assert.Equal("%PDF", Encoding.ASCII.GetString(header));
        }

        // ── Guard tests ───────────────────────────────────────────────────────────

        [Fact]
        public void WriteJson_NullReport_ThrowsArgumentNullException()
        {
            using var ms = new MemoryStream();
            Assert.Throws<ArgumentNullException>(() => _writer.WriteJson(null!, ms));
        }

        [Fact]
        public void WriteCsv_NullReport_ThrowsArgumentNullException()
        {
            using var ms = new MemoryStream();
            Assert.Throws<ArgumentNullException>(() => _writer.WriteCsv(null!, ms));
        }

        [Fact]
        public void WritePdf_NullReport_ThrowsArgumentNullException()
        {
            using var ms = new MemoryStream();
            Assert.Throws<ArgumentNullException>(() => _writer.WritePdf(null!, ms));
        }

        [Fact]
        public void Write_UnknownFormat_ThrowsArgumentOutOfRangeException()
        {
            using var ms = new MemoryStream();
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _writer.Write(MakeOutcomeReport(), (ExportFormat)99, ms));
        }
    }
}
