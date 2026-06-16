namespace FemVoiceStudio.Core.Platform;

// UI-framework-neutral platform contracts. Implemented per-head (WPF / Avalonia) so the shared core
// and shared view-models never touch System.Windows / Avalonia types directly. These are additive —
// no existing domain behaviour depends on them; they exist so the Avalonia head can inject platform
// services. See docs/WPF_DEPENDENCY_MAP.md (Group C — "Must abstract behind interface").

/// <summary>Marshals work onto the UI thread (WPF Dispatcher / Avalonia Dispatcher.UIThread).</summary>
public interface IUiDispatcher
{
    bool CheckAccess();
    void Post(Action action);
    Task InvokeAsync(Action action);
}

/// <summary>Message/confirmation dialogs (replaces WPF MessageBox).</summary>
public interface IDialogService
{
    Task ShowInfoAsync(string title, string message);
    Task ShowWarningAsync(string title, string message);
    Task ShowErrorAsync(string title, string message);
    Task<bool> ConfirmAsync(string title, string message);
}

/// <summary>File open/save (replaces Microsoft.Win32 dialogs; Avalonia uses IStorageProvider).</summary>
public interface IFileDialogService
{
    Task<string?> PickOpenFileAsync(FileDialogRequest request);
    Task<string?> PickSaveFileAsync(FileDialogRequest request);
}

/// <summary>OS theme detection (replaces ThemeManager's Windows Registry read).</summary>
public interface ISystemThemeProvider
{
    SystemTheme GetCurrentTheme();
}

/// <summary>Resolves theme resources by key (replaces Application.Current.TryFindResource).</summary>
public interface IThemeResourceProvider
{
    object? GetResource(string key);
    string GetResourceKey(string key);
}

public enum SystemTheme { Light, Dark }

public sealed record FileDialogRequest(
    string Title = "",
    string? SuggestedFileName = null,
    string? DefaultExtension = null,
    IReadOnlyList<FileDialogFilter>? Filters = null);

public sealed record FileDialogFilter(string Name, IReadOnlyList<string> Extensions);
