using FemVoice.Avalonia.Localization;   // Localized (safe read-only localization resolver)

namespace FemVoice.Avalonia.ViewModels;

/// <summary>
/// A purely STATIC, display-only placeholder page for a WPF surface that has not been ported yet
/// (Settings, Analysis, Reports, Diagnostics, Progression, SmartCoach, microphone calibration, …).
/// It holds NO services, NO state machine, is NOT IDisposable, and performs NO side effects — it only
/// shows a "this surface is deferred" message. Navigating to it starts nothing and persists nothing.
/// Title/message resolve through the safe read-only localization adapter (fallback = the current text).
/// </summary>
public sealed class DeferredSurfaceViewModel
{
    public DeferredSurfaceViewModel(string surfaceName)
    {
        SurfaceName = surfaceName;
        Title = Localized.Get($"Shell_Deferred_{surfaceName}_Title", $"{surfaceName} — utsatt");
        Message = Localized.Get($"Shell_Deferred_{surfaceName}_Message",
            $"«{surfaceName}» er ikke portert til Avalonia ennå. Denne flaten er en visning-bare " +
            "plassholder uten funksjonalitet: ingen lagring, ingen klinisk logikk, ingen mikrofon, " +
            "ingen SmartCoach/progresjon. Den kommer i en senere fase av porteringen.");
        // Footnote resolved here (non-subscribing path) rather than via {loc:Tr} in the transient template,
        // so re-navigating to deferred surfaces does not accrue orphaned localization subscribers.
        Footnote = Localized.Get("Shell_DeferredFootnote",
            "Denne flaten utfører ingenting og endrer ingen klinisk/lagret tilstand.");
    }

    public string SurfaceName { get; }
    public string Title { get; }
    public string Message { get; }
    public string Footnote { get; }
}
