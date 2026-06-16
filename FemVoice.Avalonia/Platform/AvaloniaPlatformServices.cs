using Avalonia.Threading;
using FemVoiceStudio.Core.Platform;

namespace FemVoice.Avalonia.Platform;

/// <summary>Avalonia UI-thread dispatcher (replaces WPF Application.Current.Dispatcher).</summary>
public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    public bool CheckAccess() => Dispatcher.UIThread.CheckAccess();
    public void Post(Action action) => Dispatcher.UIThread.Post(action);
    public Task InvokeAsync(Action action) => Dispatcher.UIThread.InvokeAsync(action).GetTask();
}

/// <summary>
/// Placeholder dialog service. A real implementation will use a MessageBox library or a custom
/// Avalonia dialog window during the UI-parity phases (not yet ported).
/// </summary>
public sealed class AvaloniaDialogService : IDialogService
{
    public Task ShowInfoAsync(string title, string message) { Console.WriteLine($"[INFO] {title}: {message}"); return Task.CompletedTask; }
    public Task ShowWarningAsync(string title, string message) { Console.WriteLine($"[WARN] {title}: {message}"); return Task.CompletedTask; }
    public Task ShowErrorAsync(string title, string message) { Console.WriteLine($"[ERROR] {title}: {message}"); return Task.CompletedTask; }
    public Task<bool> ConfirmAsync(string title, string message) { Console.WriteLine($"[CONFIRM] {title}: {message}"); return Task.FromResult(false); }
}

/// <summary>
/// Placeholder file-dialog service. A real implementation will use Avalonia IStorageProvider
/// (TopLevel.StorageProvider) during the reports/settings parity phases (not yet ported).
/// </summary>
public sealed class AvaloniaFileDialogService : IFileDialogService
{
    public Task<string?> PickOpenFileAsync(FileDialogRequest request) => Task.FromResult<string?>(null);
    public Task<string?> PickSaveFileAsync(FileDialogRequest request) => Task.FromResult<string?>(null);
}

/// <summary>OS theme detection via Avalonia's actual theme variant (replaces the WPF Registry read).</summary>
public sealed class AvaloniaSystemThemeProvider : ISystemThemeProvider
{
    public SystemTheme GetCurrentTheme()
    {
        var variant = global::Avalonia.Application.Current?.ActualThemeVariant;
        return variant == global::Avalonia.Styling.ThemeVariant.Dark ? SystemTheme.Dark : SystemTheme.Light;
    }
}
