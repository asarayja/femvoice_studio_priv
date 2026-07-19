using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using FemVoice.Avalonia.ViewModels;

namespace FemVoice.Avalonia.Views;

public partial class DiagnosticsView : UserControl
{
    public DiagnosticsView() => AvaloniaXamlLoader.Load(this);

    // Native save dialog → write the real diagnostics text (built by the VM). Fail-safe: errors surface in the status
    // line instead of throwing. Timestamp is stamped here so the VM builder stays pure/testable.
    private async void OnExportClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not DiagnosticsViewModel vm) return;
        try
        {
            var top = TopLevel.GetTopLevel(this);
            if (top is null) { ShowStatus("Kunne ikke åpne lagringsdialog."); return; }

            var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                SuggestedFileName = vm.SuggestedName,
                DefaultExtension = "txt",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Tekst") { Patterns = new[] { "*.txt" }, MimeTypes = new[] { "text/plain" } }
                }
            });
            if (file is null) return;   // user cancelled

            await using var stream = await file.OpenWriteAsync();
            await using var writer = new System.IO.StreamWriter(stream, new System.Text.UTF8Encoding(false));
            await writer.WriteAsync(vm.BuildDiagnosticsText(DateTime.Now));
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
