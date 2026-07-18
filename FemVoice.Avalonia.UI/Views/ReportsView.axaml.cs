using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using FemVoice.Avalonia.ViewModels;

namespace FemVoice.Avalonia.Views;

public partial class ReportsView : UserControl
{
    public ReportsView() => AvaloniaXamlLoader.Load(this);

    private async void OnExportCsvClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ReportsViewModel vm && vm.CanExport)
            await SaveAsync(vm.SuggestedCsvName, "CSV", "text/csv", "csv", vm.BuildCsv());
    }

    private async void OnExportTextClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ReportsViewModel vm && vm.CanExport)
            await SaveAsync(vm.SuggestedTextName, "Tekst", "text/plain", "txt", vm.BuildText());
    }

    // Open a native save-file dialog (works on desktop + Android via IStorageProvider) and write the report content.
    // Purely I/O — the content itself is produced by the VM (unit-tested). Fail-safe: errors surface in the status
    // line instead of throwing to the UI thread.
    private async Task SaveAsync(string suggestedName, string typeName, string mime, string ext, string content)
    {
        try
        {
            var top = TopLevel.GetTopLevel(this);
            if (top is null) { ShowStatus("Kunne ikke åpne lagringsdialog."); return; }

            var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                SuggestedFileName = suggestedName,
                DefaultExtension = ext,
                FileTypeChoices = new[]
                {
                    new FilePickerFileType(typeName) { Patterns = new[] { "*." + ext }, MimeTypes = new[] { mime } }
                }
            });
            if (file is null) return;   // user cancelled

            await using var stream = await file.OpenWriteAsync();
            await using var writer = new System.IO.StreamWriter(stream, new System.Text.UTF8Encoding(false));
            await writer.WriteAsync(content);
            ShowStatus($"Lagret: {file.Name}");
        }
        catch (Exception ex)
        {
            ShowStatus("Eksport mislyktes: " + ex.Message);
        }
    }

    private void ShowStatus(string message)
    {
        if (this.FindControl<TextBlock>("ExportStatus") is { } status)
        {
            status.Text = message;
            status.IsVisible = true;
        }
    }
}
