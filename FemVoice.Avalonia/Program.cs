using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using FemVoiceStudio.Audio.Abstractions;
using FemVoiceStudio.Core.Platform;
using FemVoiceStudio.Services;
using FemVoice.Avalonia.Platform;
using FemVoice.Avalonia.ViewModels;

namespace FemVoice.Avalonia;

internal static class Program
{
    public static IServiceProvider Services { get; private set; } = null!;

    [STAThread]
    public static int Main(string[] args)
    {
        Services = BuildServices();

        // Headless verification paths (no display needed) — used by scripts/linux-portable-gate.sh.
        if (args.Contains("--smoke")) return Smoke();
        if (args.Contains("--dashboard-smoke")) return DashboardSmoke().GetAwaiter().GetResult();
        if (args.Contains("--exercise-smoke")) return ExerciseSmoke();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();

    private static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        // ── Platform abstractions (Avalonia implementations) ──────────────────────
        services.AddSingleton<IUiDispatcher, AvaloniaUiDispatcher>();
        services.AddSingleton<IDialogService, AvaloniaDialogService>();
        services.AddSingleton<IFileDialogService, AvaloniaFileDialogService>();
        services.AddSingleton<ISystemThemeProvider, AvaloniaSystemThemeProvider>();

        // ── Audio capture ─────────────────────────────────────────────────────────
        // Avalonia head uses the synthetic backend (no NAudio capture, no Windows-only dep). On Windows
        // the real NAudioCaptureService would be wired in a Windows-specific composition root — NOT here.
        services.AddSingleton<IAudioCaptureService, SyntheticAudioCaptureService>();

        // ── Shared, UI-free core ─────────────────────────────────────────────────
        services.AddSingleton<ILocalizationService>(_ => LocalizationService.Instance);

        // ── Shared exercise catalog (read-only; pure, no DB/WPF) ───────────────────
        services.AddSingleton<VoiceFeminizationExerciseService>();

        // ── View-models ───────────────────────────────────────────────────────────
        services.AddSingleton<MainDashboardViewModel>();
        services.AddSingleton<ShellViewModel>();

        return services.BuildServiceProvider();
    }

    private static int Smoke()
    {
        var loc = Services.GetRequiredService<ILocalizationService>();
        var dispatcher = Services.GetRequiredService<IUiDispatcher>();
        var capture = Services.GetRequiredService<IAudioCaptureService>();
        Console.WriteLine($"[smoke] ILocalizationService -> {loc.GetType().FullName}");
        Console.WriteLine($"[smoke] IUiDispatcher        -> {dispatcher.GetType().Name}");
        Console.WriteLine($"[smoke] capture backend       -> {capture.GetType().Name} (devices={capture.GetInputDevices().Count})");
        Console.WriteLine($"[smoke] localized 'Common_Yes' -> {LocalizationService.Instance["Common_Yes"]}");
        Console.WriteLine("[smoke] OK: shared FemVoice.Core services resolve on Linux via the Avalonia head DI.");
        return 0;
    }

    // Headless drive of the dashboard VM through real shared services + synthetic audio (no display).
    private static async Task<int> DashboardSmoke()
    {
        var synth = new SyntheticAudioCaptureService();
        using var vm = new MainDashboardViewModel(synth, new InlineUiDispatcher());
        Console.WriteLine($"[dash] comfort zone ({vm.SelectedDifficulty}): {vm.ComfortZoneLow:F0}-{vm.ComfortZoneHigh:F0} Hz");
        await vm.StartCommand.ExecuteAsync(null);
        foreach (var mode in new[]
                 {
                     SyntheticAudioMode.StablePitch, SyntheticAudioMode.PitchRampUp,
                     SyntheticAudioMode.UnstablePitch, SyntheticAudioMode.Silence,
                 })
        {
            vm.SyntheticAudioMode = mode;
            await Task.Delay(500);
            Console.WriteLine(
                $"[dash] mode={mode,-14} pitch={vm.CurrentPitch,6:F1}Hz  signal={vm.CurrentSignalStatus,-24} " +
                $"stability={vm.PitchStability,-18} health={vm.HealthStatusDisplay,-12} trace={vm.PitchSamples.Count,3}  " +
                $"feedback=\"{vm.CurrentFeedbackMessage}\"");
        }
        await vm.StopCommand.ExecuteAsync(null);
        Console.WriteLine($"[dash] stopped. IsRecording={vm.IsRecording}");
        Console.WriteLine("[dash] OK: MainDashboardViewModel drives real pitch/stability/health from synthetic audio on Linux.");
        return 0;
    }

    // Headless verification of the Exercise Guide + Detail slice (no display): catalog loads, detail
    // opens, and shell navigation dashboard -> guide -> detail -> guide works.
    private static int ExerciseSmoke()
    {
        var svc = new VoiceFeminizationExerciseService();
        var guide = new ExerciseGuideViewModel(svc, _ => { });
        int count = guide.Count;
        Console.WriteLine($"[exercise] Exercises: {count}");
        if (count == 0) { Console.WriteLine("[exercise] Exercise smoke FAIL: no exercises"); return 1; }
        Console.WriteLine($"[exercise] First: {guide.Exercises[0].Name}");
        Console.WriteLine($"[exercise] Categories: {string.Join(", ", guide.Categories)}");

        // Shell navigation: dashboard -> guide -> detail -> guide
        var dash = new MainDashboardViewModel(new NoopAudioCaptureService(), new InlineUiDispatcher());
        var shell = new ShellViewModel(dash, svc);
        bool onDashboard = shell.CurrentPage is MainDashboardViewModel;
        shell.ShowGuideCommand.Execute(null);
        var guidePage = shell.CurrentPage as ExerciseGuideViewModel;
        bool onGuide = guidePage is not null;
        guidePage!.OpenExerciseCommand.Execute(guidePage.Exercises[0]);
        var detail = shell.CurrentPage as ExerciseDetailViewModel;
        bool onDetail = detail is not null;
        Console.WriteLine($"[exercise] Detail: {(onDetail ? "OK" : "FAIL")}");
        if (onDetail)
            Console.WriteLine($"[exercise] Detail title='{detail!.Title}', steps={detail.Steps.Count}, targetPitch={detail.TargetPitchText}");
        detail!.BackCommand.Execute(null);
        bool backToGuide = shell.CurrentPage is ExerciseGuideViewModel;
        Console.WriteLine($"[exercise] nav: dashboard={onDashboard} guide={onGuide} detail={onDetail} back-to-guide={backToGuide}");

        bool ok = count == 15 && onDashboard && onGuide && onDetail && backToGuide && detail.Steps.Count > 0;
        Console.WriteLine(ok ? "[exercise] Exercise smoke OK" : "[exercise] Exercise smoke FAIL");
        return ok ? 0 : 1;
    }
}
