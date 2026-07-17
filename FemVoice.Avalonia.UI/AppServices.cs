using Microsoft.Extensions.DependencyInjection;
using FemVoiceStudio.Audio.Abstractions;
using FemVoiceStudio.Core.Platform;
using FemVoiceStudio.Data;
using FemVoiceStudio.Services;
using FemVoice.Avalonia.Platform;
using FemVoice.Avalonia.ViewModels;

namespace FemVoice.Avalonia;

/// <summary>
/// Shared DI composition for the Avalonia UI, in the platform-neutral UI library so BOTH heads reach it: the desktop
/// head builds it up front in <c>Program.Main</c>, and the Android head reaches it from
/// <see cref="App.OnFrameworkInitializationCompleted"/> (whose entry point is its MainActivity, not Main). Lazy so no
/// startup I/O happens until first access. Registers only Avalonia platform-service implementations + shared
/// FemVoice.Core / FemVoice.Audio.Abstractions services — no Avalonia.Desktop, no clinical/WPF/DB dependency.
/// </summary>
public static class AppServices
{
    private static IServiceProvider? _services;

    /// <summary>Shared DI container, built on first access.</summary>
    public static IServiceProvider Services => _services ??= BuildServices();

    private static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        // ── Platform abstractions (Avalonia implementations) ──────────────────────
        services.AddSingleton<IUiDispatcher, AvaloniaUiDispatcher>();
        services.AddSingleton<IDialogService, AvaloniaDialogService>();
        services.AddSingleton<IFileDialogService, AvaloniaFileDialogService>();
        services.AddSingleton<ISystemThemeProvider, AvaloniaSystemThemeProvider>();

        // ── Audio capture ─────────────────────────────────────────────────────────
        // The live runtime uses the REAL cross-platform capture backend when a microphone is actually available
        // (Linux/ALSA today) and falls back to the synthetic display-only backend on headless/CI/no-mic hosts. Only
        // the SOURCE of frames changes; the shared pitch/stability/health services consuming them are unchanged.
        // (The Windows real path stays its own Windows composition-root concern — not wired here.)
        services.AddSingleton<IAudioCaptureService>(_ => AudioCaptureBackendFactory.CreateForRuntime());

        // ── Shared, UI-free core ─────────────────────────────────────────────────
        services.AddSingleton<ILocalizationService>(_ => LocalizationService.Instance);

        // ── Real SQLite database (Core DatabaseService) ───────────────────────────
        // The SAME store the WPF app uses (<MyDocuments>/FemVoiceStudio/femvoice.db) so Avalonia works on REAL data,
        // not demo data. Cross-platform via SQLitePCLRaw (fresh DB on Linux/macOS; shared with WPF on Windows). Lazy
        // singleton — the DB is only created/opened on first resolve, so the headless smokes that don't need it never
        // touch it. Schema is created idempotently (CREATE TABLE IF NOT EXISTS); no clinical logic is changed.
        services.AddSingleton<DatabaseService>();
        services.AddSingleton<IDatabaseService>(sp => sp.GetRequiredService<DatabaseService>());

        // ── Shared exercise catalog (read-only; pure, no DB/WPF) ───────────────────
        services.AddSingleton<VoiceFeminizationExerciseService>();

        // ── View-models ───────────────────────────────────────────────────────────
        services.AddSingleton<MainDashboardViewModel>();
        services.AddSingleton<ShellViewModel>();

        return services.BuildServiceProvider();
    }
}
