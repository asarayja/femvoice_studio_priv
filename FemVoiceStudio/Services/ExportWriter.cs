using System;
using System.IO;
using System.Text;
using System.Text.Json;
using FemVoiceStudio.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Supported export formats for professional reports.
    /// </summary>
    public enum ExportFormat
    {
        /// <summary>Portable Document Format via QuestPDF.</summary>
        Pdf,

        /// <summary>Comma-Separated Values (RFC 4180) flat projection.</summary>
        Csv,

        /// <summary>UTF-8 JSON (System.Text.Json, indented).</summary>
        Json
    }

    /// <summary>
    /// Writes a professional report DTO (<see cref="ClinicalReport"/>, <see cref="CoachReport"/>,
    /// <see cref="OutcomeReport"/>, or <see cref="TimelineReport"/>) to a <see cref="Stream"/>
    /// in the requested <see cref="ExportFormat"/>.
    ///
    /// CONTRACT:
    /// <list type="bullet">
    ///   <item><description>The static constructor sets the QuestPDF Community licence once,
    ///   idempotently. No external licence file is needed.</description></item>
    ///   <item><description>All methods write to the supplied <see cref="Stream"/> and leave
    ///   the stream open (callers own the stream lifetime).</description></item>
    ///   <item><description>CSV escaping follows RFC 4180: fields containing commas, double-quotes,
    ///   or line-breaks are enclosed in double-quotes; embedded double-quotes are doubled.</description></item>
    ///   <item><description>PDF output is text/table based — no OxyPlot chart embedding in
    ///   this wave (charts require a UI thread and are deferred to the UI wave).</description></item>
    /// </list>
    ///
    /// Owned by W0-A3A10 (Report Generation + Professional Exports).
    /// </summary>
    public sealed class ExportWriter
    {
        // ── QuestPDF community licence — set once, idempotent ─────────────────────

        static ExportWriter()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        // ── Dispatch ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Writes <paramref name="report"/> to <paramref name="destination"/> in the
        /// requested <paramref name="format"/>.
        /// </summary>
        /// <param name="report">A non-null report DTO (ClinicalReport, CoachReport,
        /// OutcomeReport, or TimelineReport).</param>
        /// <param name="format">The desired export format.</param>
        /// <param name="destination">The stream to write to (must be writable).</param>
        public void Write(object report, ExportFormat format, Stream destination)
        {
            if (report is null) throw new ArgumentNullException(nameof(report));
            if (destination is null) throw new ArgumentNullException(nameof(destination));

            switch (format)
            {
                case ExportFormat.Json:
                    WriteJson(report, destination);
                    break;
                case ExportFormat.Csv:
                    WriteCsv(report, destination);
                    break;
                case ExportFormat.Pdf:
                    WritePdf(report, destination);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown export format.");
            }
        }

        // ── JSON ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Serialises <paramref name="report"/> as indented UTF-8 JSON and writes it to
        /// <paramref name="destination"/>. Leaves the stream open.
        /// </summary>
        public void WriteJson(object report, Stream destination)
        {
            if (report is null) throw new ArgumentNullException(nameof(report));
            if (destination is null) throw new ArgumentNullException(nameof(destination));

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                // Preserve enum names for readability
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            };

            var bytes = JsonSerializer.SerializeToUtf8Bytes(report, report.GetType(), options);
            destination.Write(bytes, 0, bytes.Length);
        }

        // ── CSV ───────────────────────────────────────────────────────────────────

        /// <summary>
        /// Writes a flat RFC 4180 CSV projection of <paramref name="report"/> to
        /// <paramref name="destination"/>. Handles all four report types. Leaves the stream open.
        /// </summary>
        public void WriteCsv(object report, Stream destination)
        {
            if (report is null) throw new ArgumentNullException(nameof(report));
            if (destination is null) throw new ArgumentNullException(nameof(destination));

            var sb = new StringBuilder();

            switch (report)
            {
                case ClinicalReport r:
                    AppendClinicalCsv(r, sb);
                    break;
                case CoachReport r:
                    AppendCoachCsv(r, sb);
                    break;
                case OutcomeReport r:
                    AppendOutcomeCsv(r, sb);
                    break;
                case TimelineReport r:
                    AppendTimelineCsv(r, sb);
                    break;
                default:
                    // Generic fallback: title + JSON summary
                    sb.AppendLine("Field,Value");
                    AppendCsvRow(sb, "ReportType", report.GetType().Name);
                    break;
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            destination.Write(bytes, 0, bytes.Length);
        }

        // ── PDF ───────────────────────────────────────────────────────────────────

        /// <summary>
        /// Renders <paramref name="report"/> as a QuestPDF document and writes the PDF
        /// bytes to <paramref name="destination"/>. Leaves the stream open.
        ///
        /// NOTE: No OxyPlot chart embedding in this wave — charts require a UI thread and
        /// are deferred to the UI wave.
        /// </summary>
        public void WritePdf(object report, Stream destination)
        {
            if (report is null) throw new ArgumentNullException(nameof(report));
            if (destination is null) throw new ArgumentNullException(nameof(destination));

            switch (report)
            {
                case ClinicalReport r:
                    BuildClinicalPdf(r).GeneratePdf(destination);
                    break;
                case CoachReport r:
                    BuildCoachPdf(r).GeneratePdf(destination);
                    break;
                case OutcomeReport r:
                    BuildOutcomePdf(r).GeneratePdf(destination);
                    break;
                case TimelineReport r:
                    BuildTimelinePdf(r).GeneratePdf(destination);
                    break;
                default:
                    BuildGenericPdf(report).GeneratePdf(destination);
                    break;
            }
        }

        // ── CSV helpers ───────────────────────────────────────────────────────────

        private static void AppendClinicalCsv(ClinicalReport r, StringBuilder sb)
        {
            sb.AppendLine("Field,Value");
            AppendCsvRow(sb, "Title", r.Title);
            AppendCsvRow(sb, "PeriodStart", r.Period.PeriodStart.ToString("O"));
            AppendCsvRow(sb, "PeriodEnd", r.Period.PeriodEnd.ToString("O"));
            AppendCsvRow(sb, "GeneratedAt", r.Period.GeneratedAt.ToString("O"));
            AppendCsvRow(sb, "UserId", r.Outcome.UserId.ToString());
            AppendCsvRow(sb, "HasEnoughData", r.Outcome.HasEnoughData.ToString());
            AppendCsvRow(sb, "CompositeVoiceScore", r.Outcome.LongTermDevelopment.CompositeVoiceScore.ToString("F2"));
            AppendCsvRow(sb, "RecoveryStatus", r.Outcome.RecoveryProgress.Status);
            AppendCsvRow(sb, "RecoveryScore", r.Outcome.RecoveryProgress.CurrentScore0to100.ToString("F2"));
            AppendCsvRow(sb, "NoteCount", r.Notes.Count.ToString());
            AppendCsvRow(sb, "AuditEventCount", r.AuditEvents.Count.ToString());

            if (r.Notes.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("NoteId,NoteType,AuthorRole,CreatedAt,BodyText");
                foreach (var n in r.Notes)
                {
                    AppendCsvRow(sb,
                        n.NoteId.ToString(),
                        n.NoteType.ToString(),
                        n.AuthorRole,
                        n.CreatedAt.ToString("O"),
                        n.BodyText);
                }
            }
        }

        private static void AppendCoachCsv(CoachReport r, StringBuilder sb)
        {
            sb.AppendLine("Field,Value");
            AppendCsvRow(sb, "Title", r.Title);
            AppendCsvRow(sb, "PeriodStart", r.Period.PeriodStart.ToString("O"));
            AppendCsvRow(sb, "PeriodEnd", r.Period.PeriodEnd.ToString("O"));
            AppendCsvRow(sb, "GeneratedAt", r.Period.GeneratedAt.ToString("O"));
            AppendCsvRow(sb, "UserId", r.Outcome.UserId.ToString());
            AppendCsvRow(sb, "CompositeVoiceScore", r.Outcome.LongTermDevelopment.CompositeVoiceScore.ToString("F2"));
            AppendCsvRow(sb, "Breakthrough", r.Breakthrough?.ReasonCode ?? string.Empty);
            AppendCsvRow(sb, "Plateau", r.Plateau?.ReasonCode ?? string.Empty);
            AppendCsvRow(sb, "Regression", r.Regression?.ReasonCode ?? string.Empty);

            if (r.FocusAreas.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("FocusArea");
                foreach (var fa in r.FocusAreas)
                    AppendCsvRow(sb, fa);
            }

            if (r.Recommendations.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Recommendation");
                foreach (var rec in r.Recommendations)
                    AppendCsvRow(sb, rec);
            }
        }

        private static void AppendOutcomeCsv(OutcomeReport r, StringBuilder sb)
        {
            sb.AppendLine("Field,Value");
            AppendCsvRow(sb, "Title", r.Title);
            AppendCsvRow(sb, "PeriodStart", r.Period.PeriodStart.ToString("O"));
            AppendCsvRow(sb, "PeriodEnd", r.Period.PeriodEnd.ToString("O"));
            AppendCsvRow(sb, "GeneratedAt", r.Period.GeneratedAt.ToString("O"));
            AppendCsvRow(sb, "UserId", r.Outcome.UserId.ToString());
            AppendCsvRow(sb, "HasEnoughData", r.HasEnoughData.ToString());
            AppendCsvRow(sb, "CompositeVoiceScore", r.CompositeVoiceScore.ToString("F2"));
            AppendCsvRow(sb, "RecoveryStatus", r.RecoveryStatus);
            AppendCsvRow(sb, "RecoveryScore", r.RecoveryScore.ToString("F2"));

            if (r.GoalProgress.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("GoalType,PrimaryFocus,TargetValue,CurrentValue,PercentComplete,IsAchieved");
                foreach (var g in r.GoalProgress)
                {
                    AppendCsvRow(sb,
                        g.GoalType,
                        g.PrimaryFocus.ToString(),
                        g.TargetValue.ToString("F2"),
                        g.CurrentValue.ToString("F2"),
                        g.PercentComplete.ToString("F2"),
                        g.IsAchieved.ToString());
                }
            }

            if (r.TopExercises.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("ExerciseId,CompositeEffectiveness,ResonanceGain,ComfortGain,RecoveryCost,UserSuccessRate");
                foreach (var ex in r.TopExercises)
                {
                    AppendCsvRow(sb,
                        ex.ExerciseId.ToString(),
                        ex.CompositeEffectiveness.ToString("F2"),
                        ex.ResonanceGain.ToString("F2"),
                        ex.ComfortGain.ToString("F2"),
                        ex.RecoveryCost.ToString("F2"),
                        ex.UserSuccessRate.ToString("F2"));
                }
            }
        }

        private static void AppendTimelineCsv(TimelineReport r, StringBuilder sb)
        {
            sb.AppendLine("Field,Value");
            AppendCsvRow(sb, "Title", r.Title);
            AppendCsvRow(sb, "PeriodStart", r.Period.PeriodStart.ToString("O"));
            AppendCsvRow(sb, "PeriodEnd", r.Period.PeriodEnd.ToString("O"));
            AppendCsvRow(sb, "GeneratedAt", r.Period.GeneratedAt.ToString("O"));
            AppendCsvRow(sb, "UserId", r.Outcome.UserId.ToString());

            if (r.TimelineEntries.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Label,WindowDays,From,To,CompositeSlope,CompositeMean,SessionCount,Direction");
                foreach (var e in r.TimelineEntries)
                {
                    AppendCsvRow(sb,
                        e.Label,
                        e.Window.WindowDays.ToString(),
                        e.Window.From.ToString("O"),
                        e.Window.To.ToString("O"),
                        e.Window.CompositeSlope.ToString("F4"),
                        e.Window.CompositeMean.ToString("F2"),
                        e.Window.SessionCount.ToString(),
                        e.Direction);
                }
            }
        }

        /// <summary>
        /// Appends one RFC 4180 CSV row. Each cell is individually quoted when it contains
        /// a comma, double-quote, carriage-return, or line-feed. Internal double-quotes
        /// are escaped by doubling (RFC 4180 §2.7).
        /// </summary>
        private static void AppendCsvRow(StringBuilder sb, params string[] cells)
        {
            for (var i = 0; i < cells.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(EscapeCsvCell(cells[i] ?? string.Empty));
            }
            sb.AppendLine();
        }

        /// <summary>
        /// Applies RFC 4180 quoting to a single field value. Returns the field as-is when
        /// it contains no special characters, or wraps it in double-quotes (doubling any
        /// embedded double-quotes) when it contains comma, double-quote, CR, or LF.
        /// </summary>
        public static string EscapeCsvCell(string value)
        {
            if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
                return value;

            // Enclose in double-quotes and double every embedded double-quote.
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        // ── PDF document builders ─────────────────────────────────────────────────

        private static IDocument BuildClinicalPdf(ClinicalReport r) =>
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.DefaultTextStyle(t => t.FontSize(11));

                    page.Header().Column(col =>
                    {
                        col.Item().Text(r.Title).FontSize(18).Bold();
                        col.Item().Text(PeriodLine(r.Period)).FontSize(10).FontColor(Colors.Grey.Darken1);
                    });

                    page.Content().PaddingTop(1, Unit.Centimetre).Column(col =>
                    {
                        // Outcome summary section
                        AddOutcomeSection(col, r.Outcome);

                        // Clinical notes section
                        if (r.Notes.Count > 0)
                        {
                            col.Item().PaddingTop(0.5f, Unit.Centimetre)
                                .Text("Clinical Notes").FontSize(13).Bold();
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(7);
                                });
                                AddTableHeader(table, "Date", "Author Role", "Note");
                                foreach (var note in r.Notes)
                                {
                                    table.Cell().Text(note.CreatedAt.ToString("yyyy-MM-dd"));
                                    table.Cell().Text(note.AuthorRole);
                                    table.Cell().Text(note.BodyText);
                                }
                            });
                        }

                        // Audit events section
                        if (r.AuditEvents.Count > 0)
                        {
                            col.Item().PaddingTop(0.5f, Unit.Centimetre)
                                .Text("Audit Trail").FontSize(13).Bold();
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(4);
                                });
                                AddTableHeader(table, "Date", "Entity Type", "Reason Code");
                                foreach (var ev in r.AuditEvents)
                                {
                                    table.Cell().Text(ev.OccurredAt.ToString("yyyy-MM-dd"));
                                    table.Cell().Text(ev.EntityType.ToString());
                                    table.Cell().Text(ev.ReasonCode);
                                }
                            });
                        }
                    });

                    page.Footer().AlignRight().Text(t =>
                    {
                        t.Span("Generated: ").FontSize(9);
                        t.Span(r.Period.GeneratedAt.ToString("yyyy-MM-dd HH:mm UTC")).FontSize(9);
                    });
                });
            });

        private static IDocument BuildCoachPdf(CoachReport r) =>
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.DefaultTextStyle(t => t.FontSize(11));

                    page.Header().Column(col =>
                    {
                        col.Item().Text(r.Title).FontSize(18).Bold();
                        col.Item().Text(PeriodLine(r.Period)).FontSize(10).FontColor(Colors.Grey.Darken1);
                    });

                    page.Content().PaddingTop(1, Unit.Centimetre).Column(col =>
                    {
                        AddOutcomeSection(col, r.Outcome);

                        // Patterns
                        if (r.Breakthrough is not null || r.Plateau is not null || r.Regression is not null)
                        {
                            col.Item().PaddingTop(0.5f, Unit.Centimetre).Text("Detected Patterns").FontSize(13).Bold();
                            if (r.Breakthrough is not null)
                                col.Item().Text($"Breakthrough: {r.Breakthrough.ReasonCode} ({r.Breakthrough.Dimension})");
                            if (r.Plateau is not null)
                                col.Item().Text($"Plateau: {r.Plateau.ReasonCode} ({r.Plateau.Dimension})");
                            if (r.Regression is not null)
                                col.Item().Text($"Regression: {r.Regression.ReasonCode} ({r.Regression.Dimension})");
                        }

                        // Focus areas
                        if (r.FocusAreas.Count > 0)
                        {
                            col.Item().PaddingTop(0.5f, Unit.Centimetre).Text("Focus Areas").FontSize(13).Bold();
                            foreach (var fa in r.FocusAreas)
                                col.Item().Text($"• {fa}");
                        }

                        // Recommendations
                        if (r.Recommendations.Count > 0)
                        {
                            col.Item().PaddingTop(0.5f, Unit.Centimetre).Text("Recommendations").FontSize(13).Bold();
                            foreach (var rec in r.Recommendations)
                                col.Item().Text($"• {rec}");
                        }
                    });

                    page.Footer().AlignRight().Text(t =>
                    {
                        t.Span("Generated: ").FontSize(9);
                        t.Span(r.Period.GeneratedAt.ToString("yyyy-MM-dd HH:mm UTC")).FontSize(9);
                    });
                });
            });

        private static IDocument BuildOutcomePdf(OutcomeReport r) =>
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.DefaultTextStyle(t => t.FontSize(11));

                    page.Header().Column(col =>
                    {
                        col.Item().Text(r.Title).FontSize(18).Bold();
                        col.Item().Text(PeriodLine(r.Period)).FontSize(10).FontColor(Colors.Grey.Darken1);
                    });

                    page.Content().PaddingTop(1, Unit.Centimetre).Column(col =>
                    {
                        col.Item().Text($"Composite Voice Score: {r.CompositeVoiceScore:F1} / 100").FontSize(12);
                        col.Item().Text($"Recovery: {r.RecoveryStatus} ({r.RecoveryScore:F1}/100)");
                        col.Item().Text($"Data sufficient: {r.HasEnoughData}");

                        // Goals table
                        if (r.GoalProgress.Count > 0)
                        {
                            col.Item().PaddingTop(0.5f, Unit.Centimetre).Text("Goal Progress").FontSize(13).Bold();
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(1);
                                });
                                AddTableHeader(table, "Goal Type", "Target", "Current", "Progress %", "Achieved");
                                foreach (var g in r.GoalProgress)
                                {
                                    table.Cell().Text(g.GoalType);
                                    table.Cell().Text(g.TargetValue.ToString("F2"));
                                    table.Cell().Text(g.CurrentValue.ToString("F2"));
                                    table.Cell().Text(g.PercentComplete.ToString("F0") + "%");
                                    table.Cell().Text(g.IsAchieved ? "Yes" : "No");
                                }
                            });
                        }

                        // Exercise effectiveness table
                        if (r.TopExercises.Count > 0)
                        {
                            col.Item().PaddingTop(0.5f, Unit.Centimetre).Text("Exercise Effectiveness (ranked)").FontSize(13).Bold();
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(60);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                });
                                AddTableHeader(table, "Ex ID", "Composite", "Resonance", "Comfort", "RecoveryCost");
                                foreach (var ex in r.TopExercises)
                                {
                                    table.Cell().Text(ex.ExerciseId.ToString());
                                    table.Cell().Text(ex.CompositeEffectiveness.ToString("F1"));
                                    table.Cell().Text(ex.ResonanceGain.ToString("F2"));
                                    table.Cell().Text(ex.ComfortGain.ToString("F2"));
                                    table.Cell().Text(ex.RecoveryCost.ToString("F1"));
                                }
                            });
                        }
                    });

                    page.Footer().AlignRight().Text(t =>
                    {
                        t.Span("Generated: ").FontSize(9);
                        t.Span(r.Period.GeneratedAt.ToString("yyyy-MM-dd HH:mm UTC")).FontSize(9);
                    });
                });
            });

        private static IDocument BuildTimelinePdf(TimelineReport r) =>
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.DefaultTextStyle(t => t.FontSize(11));

                    page.Header().Column(col =>
                    {
                        col.Item().Text(r.Title).FontSize(18).Bold();
                        col.Item().Text(PeriodLine(r.Period)).FontSize(10).FontColor(Colors.Grey.Darken1);
                    });

                    page.Content().PaddingTop(1, Unit.Centimetre).Column(col =>
                    {
                        col.Item().Text($"Composite Voice Score: {r.Outcome.LongTermDevelopment.CompositeVoiceScore:F1} / 100").FontSize(12);

                        if (r.TimelineEntries.Count > 0)
                        {
                            col.Item().PaddingTop(0.5f, Unit.Centimetre).Text("Voice Development Timeline").FontSize(13).Bold();
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(5);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(3);
                                });
                                AddTableHeader(table, "Period", "Sessions", "Mean Score", "Slope", "Direction");
                                foreach (var entry in r.TimelineEntries)
                                {
                                    table.Cell().Text(entry.Label);
                                    table.Cell().Text(entry.Window.SessionCount.ToString());
                                    table.Cell().Text(entry.Window.CompositeMean.ToString("F1"));
                                    table.Cell().Text(entry.Window.CompositeSlope.ToString("F3"));
                                    table.Cell().Text(entry.Direction);
                                }
                            });
                        }
                        else
                        {
                            col.Item().Text("No timeline data available for this period.");
                        }
                    });

                    page.Footer().AlignRight().Text(t =>
                    {
                        t.Span("Generated: ").FontSize(9);
                        t.Span(r.Period.GeneratedAt.ToString("yyyy-MM-dd HH:mm UTC")).FontSize(9);
                    });
                });
            });

        private static IDocument BuildGenericPdf(object report) =>
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.Header().Text(report.GetType().Name).FontSize(16).Bold();
                    page.Content().PaddingTop(1, Unit.Centimetre).Text("No specific layout for this report type.");
                });
            });

        // ── Shared PDF helpers ────────────────────────────────────────────────────

        private static string PeriodLine(ReportPeriod period) =>
            $"Period: {period.PeriodStart:yyyy-MM-dd} – {period.PeriodEnd:yyyy-MM-dd}";

        private static void AddOutcomeSection(ColumnDescriptor col, OutcomeProfile outcome)
        {
            col.Item().Text("Outcome Summary").FontSize(13).Bold();
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(4);
                    columns.RelativeColumn(6);
                });
                AddTableHeader(table, "Metric", "Value");
                AddMetricRow(table, "Composite Voice Score",
                    $"{outcome.LongTermDevelopment.CompositeVoiceScore:F1} / 100");
                AddMetricRow(table, "Recovery Status",
                    $"{outcome.RecoveryProgress.Status} ({outcome.RecoveryProgress.CurrentScore0to100:F1}/100)");
                AddMetricRow(table, "Recovery Debt",
                    outcome.RecoveryProgress.RecoveryDebt.ToString("F1"));
                AddMetricRow(table, "Overtraining Predicted",
                    outcome.RecoveryProgress.OvertrainingPredicted ? "Yes" : "No");
                AddMetricRow(table, "Goals Tracked",
                    outcome.GoalProgress.Goals.Count.ToString());
                AddMetricRow(table, "Exercises Ranked",
                    outcome.ExerciseEffectiveness.Ranked.Count.ToString());
                AddMetricRow(table, "Data Sufficient",
                    outcome.HasEnoughData ? "Yes" : "No");
            });

            if (!string.IsNullOrEmpty(outcome.RecoveryProgress.RecommendationText))
            {
                col.Item().PaddingTop(0.3f, Unit.Centimetre)
                    .Text($"Recovery recommendation: {outcome.RecoveryProgress.RecommendationText}")
                    .Italic();
            }
        }

        private static void AddTableHeader(TableDescriptor table, params string[] headers)
        {
            // QuestPDF: Header() must be called EXACTLY ONCE per table — every header
            // cell goes inside the single Header layer (calling Header() per column
            // throws "The 'Table.Header' layer has already been defined").
            table.Header(header =>
            {
                foreach (var h in headers)
                    header.Cell().Background(Colors.Grey.Lighten2).Text(h).Bold();
            });
        }

        private static void AddMetricRow(TableDescriptor table, string metric, string value)
        {
            table.Cell().Text(metric);
            table.Cell().Text(value);
        }
    }
}
