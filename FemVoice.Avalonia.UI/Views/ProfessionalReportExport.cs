using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using FemVoiceStudio.Services;   // ExportWriter, ExportFormat

namespace FemVoice.Avalonia.Views;

/// <summary>
/// Shared helper for the professional panels (Coach / Clinician): renders a real Core report DTO to PDF / CSV / JSON
/// via the Core <see cref="ExportWriter"/> and writes it to a user-picked file through Avalonia's IStorageProvider
/// (desktop + Android). The report CONTENT is produced entirely by the frozen Core writer — this only runs the
/// picker and streams bytes. Fail-safe: any error is reported through the supplied status callback, never thrown to
/// the UI thread. No clinical logic is involved (serialization only).
/// </summary>
internal static class ProfessionalReportExport
{
    public static async Task SaveAsync(Visual? owner, object? report, ExportFormat format, string baseName, Action<string> status)
    {
        if (report is null) { status(FemVoice.Avalonia.Localization.Localized.Get("Report_NothingToExport", "Ingen rapport å eksportere.")); return; }
        var (ext, mime, typeName) = format switch
        {
            ExportFormat.Pdf => ("pdf", "application/pdf", "PDF"),
            ExportFormat.Csv => ("csv", "text/csv", "CSV"),
            _ => ("json", "application/json", "JSON"),
        };
        try
        {
            var top = owner is null ? null : TopLevel.GetTopLevel(owner);
            if (top is null) { status(FemVoice.Avalonia.Localization.Localized.Get("Error_SaveDialogFailed", "Kunne ikke åpne lagringsdialog.")); return; }

            var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                SuggestedFileName = baseName + "." + ext,
                DefaultExtension = ext,
                FileTypeChoices = new[]
                {
                    new FilePickerFileType(typeName) { Patterns = new[] { "*." + ext }, MimeTypes = new[] { mime } }
                }
            });
            if (file is null) return;   // user cancelled

            await using var stream = await file.OpenWriteAsync();
            // ExportWriter writes synchronously to the stream; the Core writer owns all formatting (incl. QuestPDF PDF).
            new ExportWriter().Write(report, format, stream);
            // Report-generation telemetry (WPF logs the export) — best-effort, never affects the export.
            try { FemVoiceStudio.Services.Rc0RuntimeLog.Write("ReportExport", $"Exported {report?.GetType().Name} as {format} -> {file.Name}"); }
            catch { /* telemetry best-effort */ }
            status($"Lagret: {file.Name}");
        }
        catch (Exception ex)
        {
            status("Eksport mislyktes: " + ex.Message);
        }
    }
}
