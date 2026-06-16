namespace FemVoice.Avalonia.ViewModels;

/// <summary>
/// A purely STATIC, display-only placeholder page for a WPF surface that has not been ported yet
/// (Settings, Analysis, Reports, Diagnostics, Progression, SmartCoach, microphone calibration, …).
/// It holds NO services, NO state machine, is NOT IDisposable, and performs NO side effects — it only
/// shows a "this surface is deferred" message. Navigating to it starts nothing and persists nothing.
/// </summary>
public sealed class DeferredSurfaceViewModel
{
    public DeferredSurfaceViewModel(string surfaceName)
    {
        SurfaceName = surfaceName;
        Title = $"{surfaceName} — utsatt";
        Message =
            $"«{surfaceName}» er ikke portert til Avalonia ennå. Denne flaten er en visning-bare " +
            "plassholder uten funksjonalitet: ingen lagring, ingen klinisk logikk, ingen mikrofon, " +
            "ingen SmartCoach/progresjon. Den kommer i en senere fase av porteringen.";
    }

    public string SurfaceName { get; }
    public string Title { get; }
    public string Message { get; }
}
