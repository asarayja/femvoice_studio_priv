using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FemVoiceStudio.Services;   // ExportFormat
using FemVoice.Avalonia.ViewModels;

namespace FemVoice.Avalonia.Views;

public partial class TimelinePanelView : UserControl
{
    public TimelinePanelView() => AvaloniaXamlLoader.Load(this);

    private async void OnExportPdf(object? s, global::Avalonia.Interactivity.RoutedEventArgs e) => await Export(ExportFormat.Pdf);
    private async void OnExportCsv(object? s, global::Avalonia.Interactivity.RoutedEventArgs e) => await Export(ExportFormat.Csv);
    private async void OnExportJson(object? s, global::Avalonia.Interactivity.RoutedEventArgs e) => await Export(ExportFormat.Json);

    private async System.Threading.Tasks.Task Export(ExportFormat format)
    {
        if (DataContext is TimelinePanelViewModel vm)
            await ProfessionalReportExport.SaveAsync(this, vm.Report, format, "utviklingstidslinje", ShowStatus);
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
