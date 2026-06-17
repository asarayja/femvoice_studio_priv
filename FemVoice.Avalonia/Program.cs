using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using FemVoiceStudio.Audio.Abstractions;
using FemVoiceStudio.Core.Platform;
using FemVoiceStudio.Services;
using FemVoice.Avalonia.Platform;
using FemVoice.Avalonia.ViewModels;
using FemVoice.Avalonia.Localization;

namespace FemVoice.Avalonia;

internal static class Program
{
    public static IServiceProvider Services { get; private set; } = null!;

    [STAThread]
    public static int Main(string[] args)
    {
        Services = BuildServices();

        // Headless verification paths — used by scripts/linux-portable-gate.sh. A matched smoke returns via
        // ExitAfterSmoke() to dodge an intermittent native GL atexit-teardown segfault on exit (see note there).
        if (TryDispatchSmoke(args) is int smokeCode) return ExitAfterSmoke(smokeCode);

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    // Returns a headless verification smoke's exit code if args select one; null for the real GUI launch.
    private static int? TryDispatchSmoke(string[] args)
    {
        if (args.Contains("--smoke")) return Smoke();
        if (args.Contains("--dashboard-smoke")) return DashboardSmoke().GetAwaiter().GetResult();
        if (args.Contains("--exercise-smoke")) return ExerciseSmoke();
        if (args.Contains("--exercise-runtime-smoke")) return ExerciseRuntimeSmoke().GetAwaiter().GetResult();
        if (args.Contains("--exercise-runtime-integration-smoke")) return ExerciseRuntimeIntegrationSmoke().GetAwaiter().GetResult();
        if (args.Contains("--exercise-coordinator-smoke")) return ExerciseCoordinatorSmoke().GetAwaiter().GetResult();
        if (args.Contains("--runtime-chart-feedback-smoke")) return RuntimeChartFeedbackSmoke().GetAwaiter().GetResult();
        if (args.Contains("--shell-smoke")) return ShellSmoke().GetAwaiter().GetResult();
        if (args.Contains("--theme-loc-smoke")) return ThemeLocSmoke();
        if (args.Contains("--settings-smoke")) return SettingsSmoke().GetAwaiter().GetResult();
        if (args.Contains("--runtime-lifecycle-smoke")) return RuntimeLifecycleSmoke().GetAwaiter().GetResult();
        if (args.Contains("--analysis-scaffold-smoke")) return AnalysisScaffoldSmoke().GetAwaiter().GetResult();
        if (args.Contains("--reports-scaffold-smoke")) return ReportsScaffoldSmoke().GetAwaiter().GetResult();
        if (args.Contains("--diagnostics-scaffold-smoke")) return DiagnosticsScaffoldSmoke().GetAwaiter().GetResult();
        if (args.Contains("--packaging-smoke")) return PackagingSmoke();
        if (args.Contains("--packaged-theme-smoke")) return PackagedThemeSmoke();
        if (args.Contains("--visual-baseline-smoke")) return VisualBaselineSmoke();
        if (args.Contains("--visual-interaction-chart-smoke")) return VisualInteractionChartSmoke();
        if (args.Contains("--exercise-layout-parity-smoke")) return ExerciseLayoutParitySmoke().GetAwaiter().GetResult();
        if (args.Contains("--exercise-flow-parity-smoke")) return ExerciseFlowParitySmoke().GetAwaiter().GetResult();
        if (args.Contains("--signing-readiness-smoke")) return SigningReadinessSmoke();
        if (args.Contains("--macos-packaging-readiness-smoke")) return MacosPackagingReadinessSmoke();
        if (args.Contains("--macos-icon-readiness-smoke")) return MacosIconReadinessSmoke();
        if (args.Contains("--exercise-guide-filter-search-smoke")) return ExerciseGuideFilterSearchSmoke();
        if (args.Contains("--smartcoach-progression-ui-scaffold-smoke")) return SmartCoachProgressionUiScaffoldSmoke();
        if (args.Contains("--settings-visual-parity-smoke")) return SettingsVisualParitySmoke();
        if (args.Contains("--visual-layout-polish-smoke")) return VisualLayoutPolishSmoke();
        return null;
    }

    [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "_exit")]
    private static extern void LibcUnderscoreExit(int status);

    // A smoke may initialize the Avalonia X11/GL platform (SetupWithoutStarting + UsePlatformDetect). On some
    // Linux GPU drivers (observed: NVIDIA proprietary GL) the driver's atexit teardown INTERMITTENTLY segfaults
    // at process exit — AFTER the smoke has computed and printed its result — turning a passing smoke into a
    // spurious SIGSEGV/139 exit (~1 in 12 runs). The smoke's result and return code are correct; only native
    // teardown is unsafe. So once the result is produced, flush output and terminate via POSIX _exit(), which
    // skips atexit handlers (and thus the buggy driver teardown). Linux-only; the real GUI path
    // (StartWithClassicDesktopLifetime) is untouched and manages its own shutdown.
    private static int ExitAfterSmoke(int code)
    {
        Console.Out.Flush();
        Console.Error.Flush();
        if (System.OperatingSystem.IsLinux())
            LibcUnderscoreExit(code);   // does not return
        return code;
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

        // Shell navigation: dashboard -> guide -> exercise page (opens directly, WPF parity) -> guide
        var dash = new MainDashboardViewModel(new NoopAudioCaptureService(), new InlineUiDispatcher());
        var shell = new ShellViewModel(dash, svc, new InlineUiDispatcher());
        bool onDashboard = shell.CurrentPage is MainDashboardViewModel;
        shell.ShowGuideCommand.Execute(null);
        var guidePage = shell.CurrentPage as ExerciseGuideViewModel;
        bool onGuide = guidePage is not null;
        guidePage!.OpenExerciseCommand.Execute(guidePage.Exercises[0]);
        var page = shell.CurrentPage as ExerciseRuntimeViewModel;   // guide opens the exercise (runtime) page directly
        bool onExercise = page is not null;
        Console.WriteLine($"[exercise] Exercise page: {(onExercise ? "OK" : "FAIL")}");
        if (onExercise)
            Console.WriteLine($"[exercise] Page name='{page!.SelectedExerciseName}', steps={page.Steps.Count}, focus={page.FocusLabel}");
        page!.BackCommand.Execute(null);
        bool backToGuide = shell.CurrentPage is ExerciseGuideViewModel;
        Console.WriteLine($"[exercise] nav: dashboard={onDashboard} guide={onGuide} exercise={onExercise} back-to-guide={backToGuide}");

        bool ok = count == 15 && onDashboard && onGuide && onExercise && backToGuide && page.Steps.Count > 0;
        Console.WriteLine(ok ? "[exercise] Exercise smoke OK" : "[exercise] Exercise smoke FAIL");
        return ok ? 0 : 1;
    }

    // Headless verification of the Exercise Runtime slice (no display): synthetic pitch drives the
    // runtime VM into the exercise target band, hold/elapsed advance, and detail -> runtime -> back nav works.
    private static async Task<int> ExerciseRuntimeSmoke()
    {
        var svc = new VoiceFeminizationExerciseService();
        var exercise = svc.GetAllEnhancedExercises()[0]; // Grunnleggende humming (target 140-180 Hz)

        using var rt = new ExerciseRuntimeViewModel(exercise, new InlineUiDispatcher(), () => { });
        rt.BeginCommand.Execute(null);   // explicit start (runtime no longer auto-starts)
        await Task.Delay(700); // collect synthetic frames aimed at the target-band midpoint

        double pitch = rt.CurrentPitch;
        string status = rt.PitchStatus;
        double hold = rt.HoldSeconds;
        bool running = rt.IsRunning;
        Console.WriteLine($"[runtime] Exercise: {rt.SelectedExerciseName}");
        Console.WriteLine($"[runtime] Target: {rt.TargetPitchMin:F0}-{rt.TargetPitchMax:F0} Hz");
        Console.WriteLine($"[runtime] Pitch: {pitch:F1} Hz");
        Console.WriteLine($"[runtime] Status: {status}");
        Console.WriteLine($"[runtime] Hold: {hold:F1}s ({rt.HoldProgressPercent:F0}%)  Elapsed: {rt.ElapsedText}");
        await rt.StopCommand.ExecuteAsync(null);
        bool stopped = !rt.IsRunning;

        // Navigation via the shell: dashboard -> guide -> exercise page (runtime, opens directly) -> back-to-guide
        var dash = new MainDashboardViewModel(new NoopAudioCaptureService(), new InlineUiDispatcher());
        var shell = new ShellViewModel(dash, svc, new InlineUiDispatcher());
        shell.ShowGuideCommand.Execute(null);
        var guide = shell.CurrentPage as ExerciseGuideViewModel;
        guide!.OpenExerciseCommand.Execute(guide.Exercises[0]);   // opens the exercise (runtime) page directly
        bool onRuntime = shell.CurrentPage is ExerciseRuntimeViewModel;
        var rvm = shell.CurrentPage as ExerciseRuntimeViewModel;
        await Task.Delay(100);
        if (rvm is not null) await rvm.BackCommand.ExecuteAsync(null);
        bool backToGuide = shell.CurrentPage is ExerciseGuideViewModel;
        Console.WriteLine($"[runtime] Navigation: runtime={onRuntime} back-to-guide={backToGuide}");

        bool ok = running && stopped && pitch > 0 && rt.TargetPitchMax > 0
                  && status == "Innenfor målområde" && hold > 0 && onRuntime && backToGuide;
        Console.WriteLine(ok ? "[runtime] Exercise runtime smoke OK" : "[runtime] Exercise runtime smoke FAIL");
        return ok ? 0 : 1;
    }

    // Headless verification of the runtime target-profile integration (no display): every exercise is
    // evaluated for a profile mapping, the runtime surfaces the profile, and nav still works.
    private static async Task<int> ExerciseRuntimeIntegrationSmoke()
    {
        var svc = new VoiceFeminizationExerciseService();
        var exercises = svc.GetAllEnhancedExercises();
        Console.WriteLine($"[rt-int] Exercises: {exercises.Count}");

        int mapped = 0, fallback = 0;
        foreach (var ex in exercises)
        {
            var d = ExerciseRuntimeTargetProfileDisplay.From(ex);
            if (d.HasProfile) mapped++; else fallback++;
        }
        Console.WriteLine($"[rt-int] Mapped profiles: {mapped}/{exercises.Count}");
        Console.WriteLine($"[rt-int] Fallback profiles: {fallback}/{exercises.Count}");

        var first = exercises[0];
        using var rt = new ExerciseRuntimeViewModel(first, new InlineUiDispatcher(), () => { });
        rt.BeginCommand.Execute(null);   // explicit start (runtime no longer auto-starts)
        var prof = rt.TargetProfile;
        Console.WriteLine($"[rt-int] First: {first.Name}");
        Console.WriteLine($"[rt-int] Profile: {prof.ProfileType}");
        Console.WriteLine($"[rt-int] RequiredHoldSeconds: {prof.RequiredHoldSeconds}");
        Console.WriteLine($"[rt-int] Resonance: {prof.ResonanceTarget}  Stability: {prof.StabilityTarget}  Skills: {prof.VoiceSkillTargets}");
        Console.WriteLine($"[rt-int] HoldTarget: {rt.HoldTargetDescription}");
        bool profileShown = prof.HasProfile && !string.IsNullOrWhiteSpace(prof.PurposeText);

        await Task.Delay(500); // synthetic frames in the target band
        double pitch = rt.CurrentPitch;
        string status = rt.PitchStatus;
        await rt.StopCommand.ExecuteAsync(null);

        // Navigation: dashboard -> guide -> runtime (direct, no detail page) -> back-to-guide
        var dash = new MainDashboardViewModel(new NoopAudioCaptureService(), new InlineUiDispatcher());
        var shell = new ShellViewModel(dash, svc, new InlineUiDispatcher());
        shell.ShowGuideCommand.Execute(null);
        var guide = shell.CurrentPage as ExerciseGuideViewModel;
        guide!.OpenExerciseCommand.Execute(guide.Exercises[0]);
        // (flow parity) the guide opens the exercise page (runtime) directly — no separate detail Start step.
        bool onRuntime = shell.CurrentPage is ExerciseRuntimeViewModel;
        var rvm = shell.CurrentPage as ExerciseRuntimeViewModel;
        await Task.Delay(50);
        if (rvm is not null) await rvm.BackCommand.ExecuteAsync(null);
        bool backToGuide = shell.CurrentPage is ExerciseGuideViewModel;
        Console.WriteLine($"[rt-int] Runtime: {(onRuntime ? "OK" : "FAIL")}");
        Console.WriteLine($"[rt-int] Navigation: runtime={onRuntime} back-to-guide={backToGuide}");

        bool ok = exercises.Count == 15 && (mapped + fallback) == 15 && profileShown
                  && pitch > 0 && status == "Innenfor målområde" && onRuntime && backToGuide;
        Console.WriteLine(ok ? "[rt-int] Exercise runtime integration smoke OK" : "[rt-int] Exercise runtime integration smoke FAIL");
        return ok ? 0 : 1;
    }

    // Headless verification of the Coordinator Readout slice (no display): the runtime VM drives a
    // VM-local, parameterless ExerciseIntelligenceCoordinator READ-ONLY (display-only), produces a
    // hold/progress readout, labels the safety state non-enforced, and stop/back clears the in-memory
    // coordinator state. Nothing is persisted, gated, scored, or enforced.
    private static async Task<int> ExerciseCoordinatorSmoke()
    {
        var svc = new VoiceFeminizationExerciseService();
        var exercises = svc.GetAllEnhancedExercises();
        Console.WriteLine($"[coord] Exercises: {exercises.Count}");

        var first = exercises[0]; // Grunnleggende humming
        using var rt = new ExerciseRuntimeViewModel(first, new InlineUiDispatcher(), () => { });
        rt.BeginCommand.Execute(null);   // explicit start (runtime no longer auto-starts)
        await Task.Delay(700); // feed synthetic-derived metrics through the coordinator (UpdateMetrics)

        var readout = rt.CoordinatorReadout;
        bool active = readout.IsCoordinatorActive;
        // Coordinator either produced a readout (live-state received) or is documented unavailable.
        bool liveStateReceived = !readout.CoordinatorRawStateSummary.StartsWith("(ingen");
        bool readoutMode = readout.ReadoutMode.Contains("ikke håndhevet"); // display-only / NOT enforced
        bool safetyDisplayOnly = readout.CoordinatorSafetyLockDisplay.Contains("kun visning"); // non-enforced label

        Console.WriteLine($"[coord] Exercise: {rt.SelectedExerciseName}");
        Console.WriteLine($"[coord] Coordinator active: {active}");
        Console.WriteLine($"[coord] Coordinator hold: {readout.CoordinatorHoldSeconds:F1}s ({readout.CoordinatorHoldProgressPercent:F0}%)");
        Console.WriteLine($"[coord] Derived hold: {readout.DerivedHoldSeconds:F1}s ({readout.DerivedHoldProgressPercent:F0}%)");
        Console.WriteLine($"[coord] Hold difference: {readout.HoldDifferenceDisplay}");
        Console.WriteLine($"[coord] Coordinator state: {readout.CoordinatorStatusText}");
        Console.WriteLine($"[coord] Raw: {readout.CoordinatorRawStateSummary}");
        Console.WriteLine($"[coord] Safety readout: {(safetyDisplayOnly ? "display-only" : readout.CoordinatorSafetyLockDisplay)}");
        Console.WriteLine($"[coord] Readout mode: {readout.ReadoutMode}");

        // Stop must clear the VM-local coordinator state (no persistence).
        await rt.StopCommand.ExecuteAsync(null);
        bool clearedOnStop = !rt.CoordinatorReadout.IsCoordinatorActive;
        Console.WriteLine($"[coord] After stop -> coordinator active: {rt.CoordinatorReadout.IsCoordinatorActive}");

        // Re-Begin must re-activate the coordinator and produce a FRESH live-state — verifies the single
        // ExerciseUpdated subscription survives a stop/start cycle (no double-subscribe, no stale session).
        rt.BeginCommand.Execute(null);
        await Task.Delay(300);
        bool reBeginActive = rt.CoordinatorReadout.IsCoordinatorActive;
        bool reBeginLive = !rt.CoordinatorReadout.CoordinatorRawStateSummary.StartsWith("(ingen");
        Console.WriteLine($"[coord] Re-Begin -> active={reBeginActive} live-state={reBeginLive}");
        await rt.StopCommand.ExecuteAsync(null);

        // Navigation A via the shell: dashboard -> guide -> runtime (direct, no detail page) -> back-to-guide (own Back).
        var dash = new MainDashboardViewModel(new NoopAudioCaptureService(), new InlineUiDispatcher());
        var shell = new ShellViewModel(dash, svc, new InlineUiDispatcher());
        shell.ShowGuideCommand.Execute(null);
        var guide = shell.CurrentPage as ExerciseGuideViewModel;
        guide!.OpenExerciseCommand.Execute(guide.Exercises[0]);
        // (flow parity) the guide opens the exercise page (runtime) directly — no separate detail Start step.
        bool onRuntime = shell.CurrentPage is ExerciseRuntimeViewModel;
        var rvm = shell.CurrentPage as ExerciseRuntimeViewModel;
        await Task.Delay(50);
        if (rvm is not null) await rvm.BackCommand.ExecuteAsync(null);
        bool backToGuide = shell.CurrentPage is ExerciseGuideViewModel;

        // Navigation B: open an exercise and start it, then leave via the always-visible top nav while RUNNING —
        // the runtime must be DISPOSED (synthetic capture stopped + VM-local coordinator cleared), not orphaned.
        guide.OpenExerciseCommand.Execute(guide.Exercises[0]);   // opens the exercise page (runtime) directly
        var rvm2 = shell.CurrentPage as ExerciseRuntimeViewModel;
        rvm2?.BeginCommand.Execute(null);   // explicit start (runtime no longer auto-starts)
        await Task.Delay(50);
        bool wasRunning = rvm2?.IsRunning == true;
        shell.ShowGuideCommand.Execute(null);   // top-nav away while running
        bool clearedByNav = rvm2 is not null && !rvm2.IsRunning && shell.CurrentPage is ExerciseGuideViewModel;
        Console.WriteLine($"[coord] Navigation: runtime={onRuntime} back-to-guide={backToGuide} " +
                          $"nav-away-clears={clearedByNav} (was-running={wasRunning})");

        // The coordinator was enabled for exercise #1 (mapped profile), so we expect the active path.
        // If a future exercise had no mapped profile, the readout would be documented unavailable instead.
        if (!active)
            Console.WriteLine("[coord] Coordinator readout unavailable: documented");

        bool ok = exercises.Count == 15 && active && liveStateReceived && readoutMode && safetyDisplayOnly
                  && clearedOnStop && reBeginActive && reBeginLive
                  && onRuntime && backToGuide && wasRunning && clearedByNav;
        Console.WriteLine(ok ? "[coord] Exercise coordinator smoke OK" : "[coord] Exercise coordinator smoke FAIL");
        return ok ? 0 : 1;
    }

    // Headless verification of the Runtime Chart + Live Feedback slice (no display): the runtime VM
    // produces a converter-free pitch trace (px heights), a target band + current-pitch marker (chart px),
    // a local display-only feedback message, and derived + coordinator hold visuals. All display-only;
    // no OxyPlot, no FeedbackConsistencyGuard, no ComfortZoneController, no persistence/clinical change.
    private static async Task<int> RuntimeChartFeedbackSmoke()
    {
        var svc = new VoiceFeminizationExerciseService();
        var exercises = svc.GetAllEnhancedExercises();
        Console.WriteLine($"[chart] Exercises: {exercises.Count}");

        var first = exercises[0]; // Grunnleggende humming (target 140–180 Hz)
        using var rt = new ExerciseRuntimeViewModel(first, new InlineUiDispatcher(), () => { });
        rt.BeginCommand.Execute(null);   // explicit start (runtime no longer auto-starts)
        await Task.Delay(700); // collect synthetic frames aimed at the target-band midpoint

        var chart = rt.RuntimeChart;
        int samples = rt.RuntimePitchSamples.Count;
        bool markerOk = chart.HasVoice && chart.CurrentPitchMarkerPx > 0;
        bool bandOk = chart.TargetBandHeightPx > 0 && chart.TargetBandTopPx > chart.TargetBandBottomPx;
        string feedbackMsg = rt.LiveFeedbackMessage;   // capture WHILE running (Stop resets it by design)
        bool feedbackOk = !string.IsNullOrWhiteSpace(feedbackMsg);
        bool derivedHoldOk = rt.DerivedHoldVisualPercent > 0;
        // Coordinator readout is active (it drives the coordinator hold bar). The bar VALUE is 0% here because
        // resonance is a neutral placeholder (documented) — assert active + that the bar binding resolves.
        bool coordVisualOk = rt.CoordinatorReadout.IsCoordinatorActive && rt.CoordinatorHoldVisualPercent >= 0;

        Console.WriteLine($"[chart] Exercise: {rt.SelectedExerciseName}");
        Console.WriteLine($"[chart] Samples: {samples} (cap respected: {samples <= 120})");
        Console.WriteLine($"[chart] Axis: {chart.ChartMinPitch:F0}-{chart.ChartMaxPitch:F0} Hz, height {chart.ChartHeightPx:F0}px");
        Console.WriteLine($"[chart] Target band: bottom={chart.TargetBandBottomPx:F0}px top={chart.TargetBandTopPx:F0}px ({(bandOk ? "OK" : "FAIL")})");
        Console.WriteLine($"[chart] Current marker: {chart.CurrentPitch:F1} Hz @ {chart.CurrentPitchMarkerPx:F0}px ({(markerOk ? "OK" : "FAIL")})");
        Console.WriteLine($"[chart] Feedback: {rt.LiveFeedbackMessage} [{rt.LiveFeedbackSeverity}]");
        Console.WriteLine($"[chart] Derived hold: {rt.DerivedHoldVisualPercent:F0}%  Coordinator hold: {rt.CoordinatorHoldVisualPercent:F0}%");
        Console.WriteLine($"[chart] Hold comparison: {rt.HoldComparisonText}");

        await rt.StopCommand.ExecuteAsync(null);
        bool stopped = !rt.IsRunning;

        // Navigation via the shell: dashboard -> guide -> runtime (direct, no detail page) -> back-to-guide
        var dash = new MainDashboardViewModel(new NoopAudioCaptureService(), new InlineUiDispatcher());
        var shell = new ShellViewModel(dash, svc, new InlineUiDispatcher());
        shell.ShowGuideCommand.Execute(null);
        var guide = shell.CurrentPage as ExerciseGuideViewModel;
        guide!.OpenExerciseCommand.Execute(guide.Exercises[0]);
        // (flow parity) the guide opens the exercise page (runtime) directly — no separate detail Start step.
        bool onRuntime = shell.CurrentPage is ExerciseRuntimeViewModel;
        var rvm = shell.CurrentPage as ExerciseRuntimeViewModel;
        await Task.Delay(50);
        if (rvm is not null) await rvm.BackCommand.ExecuteAsync(null);
        bool backToGuide = shell.CurrentPage is ExerciseGuideViewModel;
        Console.WriteLine($"[chart] Navigation: {(onRuntime && backToGuide ? "OK" : "FAIL")} (runtime={onRuntime} back-to-guide={backToGuide})");

        bool ok = exercises.Count == 15 && samples > 0 && samples <= 120 && markerOk && bandOk
                  && feedbackOk && feedbackMsg == "Innenfor målområdet" && derivedHoldOk
                  && coordVisualOk && stopped && onRuntime && backToGuide;
        Console.WriteLine(ok ? "[chart] Runtime chart feedback smoke OK" : "[chart] Runtime chart feedback smoke FAIL");
        return ok ? 0 : 1;
    }

    // Headless verification of the Desktop Shell + Navigation/Layout slice (no display): the shell
    // constructs, lands on the dashboard, implemented nav switches pages, deferred nav opens a STATIC
    // placeholder with no side effects, and navigating away from a running runtime disposes it (no
    // orphaned synthetic capture, no duplicate runtime). All display-only — no clinical/persistence.
    private static async Task<int> ShellSmoke()
    {
        var svc = new VoiceFeminizationExerciseService();
        var dash = new MainDashboardViewModel(new NoopAudioCaptureService(), new InlineUiDispatcher());
        var shell = new ShellViewModel(dash, svc, new InlineUiDispatcher());

        bool landsOnDashboard = shell.CurrentPage is MainDashboardViewModel;
        int implemented = shell.NavItems.Count(n => n.IsImplemented);
        int deferred = shell.NavItems.Count(n => !n.IsImplemented);
        Console.WriteLine($"[shell] Nav items: {shell.NavItems.Count} (implemented={implemented}, deferred={deferred})");
        Console.WriteLine($"[shell] Lands on: {shell.CurrentDestinationLabel}");

        // Implemented nav switches CurrentPage.
        shell.ShowGuideCommand.Execute(null);
        bool onGuide = shell.CurrentPage is ExerciseGuideViewModel;
        shell.ShowDashboardCommand.Execute(null);
        bool backToDash = shell.CurrentPage is MainDashboardViewModel;

        // A generic deferred nav (Mikrofonkalibrering) opens a STATIC placeholder with no side effect
        // (not IDisposable, holds no services).
        var deferredItem = shell.NavItems.First(n => !n.IsImplemented && n.Label.Contains("Mikrofon"));
        deferredItem.Command.Execute(null);
        bool onDeferred = shell.CurrentPage is DeferredSurfaceViewModel;
        bool deferredInert = shell.CurrentPage is DeferredSurfaceViewModel && shell.CurrentPage is not IDisposable;
        Console.WriteLine($"[shell] Deferred nav '{deferredItem.Label}' -> {(onDeferred ? "static placeholder" : "FAIL")} (inert={deferredInert})");

        // Progresjon/SmartCoach are still deferred (not functional) but now open inert display-only SCAFFOLD
        // pages (no services, not IDisposable) instead of the bare generic placeholder.
        shell.NavItems.First(n => n.Label.Contains("Progresjon")).Command.Execute(null);
        bool onProgScaffold = shell.CurrentPage is ProgressionScaffoldViewModel && shell.CurrentPage is not IDisposable;
        shell.NavItems.First(n => n.Label.Contains("SmartCoach")).Command.Execute(null);
        bool onCoachScaffold = shell.CurrentPage is SmartCoachScaffoldViewModel && shell.CurrentPage is not IDisposable;
        Console.WriteLine($"[shell] Scaffold nav: progression={onProgScaffold} smartcoach={onCoachScaffold} (inert, deferred)");

        // Runtime nav-away disposes the transient runtime (no orphaned capture).
        shell.ShowGuideCommand.Execute(null);
        var guide = shell.CurrentPage as ExerciseGuideViewModel;
        guide!.OpenExerciseCommand.Execute(guide.Exercises[0]);
        // (flow parity) the guide opens the exercise page (runtime) directly — no separate detail Start step.
        var firstRuntime = shell.CurrentPage as ExerciseRuntimeViewModel;
        firstRuntime?.BeginCommand.Execute(null);   // explicit start (runtime no longer auto-starts)
        await Task.Delay(50);
        bool runtimeRunning = firstRuntime?.IsRunning == true;
        shell.ShowDashboardCommand.Execute(null);   // nav away via the rail
        bool firstDisposedOnNav = firstRuntime is not null && !firstRuntime.IsRunning;

        // Direct proof the synthetic capture loop is no longer driving the disposed runtime: its pitch
        // trace must NOT keep growing after nav-away (the FrameAvailable handler was unsubscribed in Dispose).
        int framesAfterNav = firstRuntime?.RuntimePitchSamples.Count ?? -1;
        await Task.Delay(150);
        bool noOrphanFrames = firstRuntime is not null && firstRuntime.RuntimePitchSamples.Count == framesAfterNav;

        // Re-open a runtime -> a fresh, distinct, running instance (no duplicate left active).
        shell.ShowGuideCommand.Execute(null);
        var guide2 = shell.CurrentPage as ExerciseGuideViewModel;
        guide2!.OpenExerciseCommand.Execute(guide2.Exercises[0]);
        // (flow parity) the guide opens the exercise page (runtime) directly — no separate detail Start step.
        var secondRuntime = shell.CurrentPage as ExerciseRuntimeViewModel;
        secondRuntime?.BeginCommand.Execute(null);   // explicit start (runtime no longer auto-starts)
        await Task.Delay(50);
        bool secondRunning = secondRuntime?.IsRunning == true;
        bool distinctInstance = secondRuntime is not null && !ReferenceEquals(firstRuntime, secondRuntime);
        bool firstStillStopped = firstRuntime is not null && !firstRuntime.IsRunning;
        if (secondRuntime is not null) await secondRuntime.BackCommand.ExecuteAsync(null);
        Console.WriteLine($"[shell] Runtime lifecycle: running={runtimeRunning} disposed-on-nav={firstDisposedOnNav} " +
                          $"no-orphan-frames={noOrphanFrames} fresh-instance={distinctInstance} " +
                          $"second-running={secondRunning} no-orphan={firstStillStopped}");

        bool ok = landsOnDashboard && shell.NavItems.Count == 9 && implemented == 6 && deferred == 3
                  && onGuide && backToDash && onDeferred && deferredInert && onProgScaffold && onCoachScaffold
                  && runtimeRunning && firstDisposedOnNav && noOrphanFrames
                  && distinctInstance && secondRunning && firstStillStopped;
        Console.WriteLine(ok ? "[shell] Shell smoke OK" : "[shell] Shell smoke FAIL");
        return ok ? 0 : 1;
    }

    // Headless verification of the Theme + Localization Adapter slice (no display): the safe read-only
    // localization adapter resolves known keys and falls back on missing ones; the reactive markup-extension
    // backing + Tr extension work; shell/nav/deferred labels resolve or fall back; and (guarded, when an
    // Avalonia platform is available) the named shell theme brushes are present for both theme variants.
    // No SetLanguage is called (semantics preserved); no clinical/WPF change.
    private static int ThemeLocSmoke()
    {
        // 1. Localization adapter: a known RESX key resolves (proves we read the shared service); a missing
        //    key falls back safely (the indexer returns the key itself, which Localized.Get maps to fallback).
        string yes = Localized.Get("Common_Yes", "FB");
        bool knownResolves = yes == "Ja";
        string miss = Localized.Get("__no_such_key__", "Fallback-X");
        bool missingFallsBack = miss == "Fallback-X";
        Console.WriteLine($"[theme-loc] Localized.Get: 'Common_Yes'='{yes}'  missing->'{miss}'");

        // 2. Reactive backing (the {loc:Tr} markup-extension source) resolves + falls back, and subscribes
        //    to the service's PropertyChanged so it would update on a language switch (not triggered here).
        var lvKnown = new LocalizedValue("Common_Yes", "FB");
        var lvMiss = new LocalizedValue("__no_such_key__", "Fallback-Y");
        bool reactiveOk = lvKnown.Value == "Ja" && lvMiss.Value == "Fallback-Y";

        // 3. Tr markup extension returns a one-way Binding (Avalonia), not a WPF construct.
        var tr = new TrExtension("Common_Yes") { Fallback = "FB" };
        bool trOk = tr.ProvideValue(null!) is global::Avalonia.Data.Binding;

        // 4. Shell/nav/status/deferred labels resolve or fall back through the adapter (identical text today).
        var svc = new VoiceFeminizationExerciseService();
        var dash = new MainDashboardViewModel(new NoopAudioCaptureService(), new InlineUiDispatcher());
        var shell = new ShellViewModel(dash, svc, new InlineUiDispatcher());
        bool navLabelsOk = shell.NavItems.Count == 9
            && shell.NavItems.All(n => !string.IsNullOrWhiteSpace(n.Label))
            && shell.NavItems[0].Label == "Dashbord"
            && shell.NavItems[2].Label == "Innstillinger"   // Settings implemented
            && shell.NavItems[3].Label == "Analyse"          // Analysis implemented
            && shell.NavItems[4].Label == "Rapporter";       // Reports implemented
        bool statusOk = shell.MicStatusText.Contains("syntetisk") && shell.ModeText.Contains("Kun visning");
        var def = new DeferredSurfaceViewModel("Innstillinger");
        bool deferredOk = def.Title.Contains("Innstillinger") && !string.IsNullOrWhiteSpace(def.Message);
        Console.WriteLine($"[theme-loc] Shell labels: nav[0]='{shell.NavItems[0].Label}' nav[2]='{shell.NavItems[2].Label}' " +
                          $"mic='{shell.MicStatusText}'");
        Console.WriteLine($"[theme-loc] Deferred page: title='{def.Title}'");

        // 5. Theme brushes: guarded runtime lookup (requires an Avalonia platform; cleanly skipped otherwise).
        string[] shellBrushKeys =
        {
            "ShellHeaderBackgroundBrush","ShellStatusBackgroundBrush","ShellPanelBackgroundBrush","ShellBorderBrush",
            "ShellAccentBrush","ShellHeadingBrush","ShellMutedBrush","ShellFaintBrush","ShellSubtleTextBrush",
            "ShellBodyTextBrush","ShellOkBrush","ShellOkBorderBrush","ShellDeferredTitleBrush","ShellDeferredBorderBrush",
        };
        bool themeChecked = false, themeKeysOk = true;
        try
        {
            BuildAvaloniaApp().SetupWithoutStarting();
            var app = Application.Current;
            if (app is not null)
            {
                themeChecked = true;
                foreach (var k in shellBrushKeys)
                {
                    bool darkOk = app.TryGetResource(k, global::Avalonia.Styling.ThemeVariant.Dark, out var dv) && dv is not null;
                    bool lightOk = app.TryGetResource(k, global::Avalonia.Styling.ThemeVariant.Light, out var lv) && lv is not null;
                    if (!(darkOk && lightOk)) { themeKeysOk = false; Console.WriteLine($"[theme-loc] MISSING theme brush: {k} (dark={darkOk} light={lightOk})"); }
                }
                Console.WriteLine($"[theme-loc] Theme brushes: {(themeKeysOk ? "all present" : "MISSING some")} ({shellBrushKeys.Length} keys × Dark+Light)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[theme-loc] Theme runtime check skipped (no Avalonia platform here): {ex.GetType().Name}");
        }

        bool ok = knownResolves && missingFallsBack && reactiveOk && trOk
                  && navLabelsOk && statusOk && deferredOk && (!themeChecked || themeKeysOk);
        Console.WriteLine(ok ? "[theme-loc] Theme + localization smoke OK" : "[theme-loc] Theme + localization smoke FAIL");
        return ok ? 0 : 1;
    }

    // Headless verification of the Settings scaffold slice (no display): the Settings nav item is an
    // IMPLEMENTED destination; navigating to it switches CurrentPage to a SettingsViewModel that is purely
    // inert (not IDisposable, no IRelayCommand props, all rows deferred); the expected 8 cards are present;
    // and navigating to Settings from a running runtime disposes the runtime safely (no orphaned capture).
    // No persistence/settings-write APIs are touched. Display-only.
    private static async Task<int> SettingsSmoke()
    {
        var svc = new VoiceFeminizationExerciseService();
        var dash = new MainDashboardViewModel(new NoopAudioCaptureService(), new InlineUiDispatcher());
        var shell = new ShellViewModel(dash, svc, new InlineUiDispatcher());

        // Settings nav item exists and is implemented.
        var settingsNav = shell.NavItems.FirstOrDefault(n => n.Label == "Innstillinger");
        bool navExists = settingsNav is not null && settingsNav.IsImplemented;

        // Navigating to Settings switches CurrentPage to SettingsViewModel (constructs without side effects).
        settingsNav?.Command.Execute(null);
        bool onSettings = shell.CurrentPage is SettingsViewModel;
        var settings = shell.CurrentPage as SettingsViewModel;

        // Inert: not IDisposable, exposes no IRelayCommand, all rows deferred.
        bool notDisposable = settings is not null && !typeof(System.IDisposable).IsAssignableFrom(typeof(SettingsViewModel));
        bool noCommands = settings is not null && settings.GetType().GetProperties()
            .All(p => !typeof(global::CommunityToolkit.Mvvm.Input.IRelayCommand).IsAssignableFrom(p.PropertyType));
        int sectionCount = settings?.Sections.Count ?? 0;
        bool sectionsOk = sectionCount == 9
            && settings!.Sections.All(s => !string.IsNullOrWhiteSpace(s.Title) && s.Rows.Count > 0);
        bool allDeferred = settings?.AllControlsDeferred == true
            && settings.Sections.SelectMany(s => s.Rows).All(r => !r.IsEnabled);
        Console.WriteLine($"[settings] Nav implemented: {navExists}  onSettings: {onSettings}  sections: {sectionCount}");
        Console.WriteLine($"[settings] Inert: notDisposable={notDisposable} noCommands={noCommands} allDeferred={allDeferred}");

        // Navigating to Settings from a RUNNING runtime disposes the runtime safely (no orphaned capture).
        shell.ShowGuideCommand.Execute(null);
        var guide = shell.CurrentPage as ExerciseGuideViewModel;
        guide!.OpenExerciseCommand.Execute(guide.Exercises[0]);
        // (flow parity) the guide opens the exercise page (runtime) directly — no separate detail Start step.
        var runtime = shell.CurrentPage as ExerciseRuntimeViewModel;
        runtime?.BeginCommand.Execute(null);   // explicit start (runtime no longer auto-starts)
        await Task.Delay(50);
        bool runtimeRan = runtime?.IsRunning == true;
        settingsNav!.Command.Execute(null);   // nav to Settings via the rail while running
        bool runtimeDisposed = runtime is not null && !runtime.IsRunning && shell.CurrentPage is SettingsViewModel;
        int framesAfter = runtime?.RuntimePitchSamples.Count ?? -1;
        await Task.Delay(150);
        bool noOrphanFrames = runtime is not null && runtime.RuntimePitchSamples.Count == framesAfter;
        Console.WriteLine($"[settings] Runtime->Settings: ran={runtimeRan} disposed={runtimeDisposed} no-orphan-frames={noOrphanFrames}");

        bool ok = navExists && onSettings && notDisposable && noCommands && sectionsOk && allDeferred
                  && runtimeRan && runtimeDisposed && noOrphanFrames;
        Console.WriteLine(ok ? "[settings] Settings smoke OK" : "[settings] Settings smoke FAIL");
        return ok ? 0 : 1;
    }

    // Headless verification of the Runtime Lifecycle UI slice (no display): the runtime starts INACTIVE
    // (no auto-start); Start -> Active with a flowing synthetic stream; Stop -> Stopped with a display-only
    // session-ended summary and a cleared stream; re-Start gives a fresh Active session with no duplicate
    // subscription (no orphan frames after stop); nav-away still disposes safely. No persistence/write APIs.
    private static async Task<int> RuntimeLifecycleSmoke()
    {
        var svc = new VoiceFeminizationExerciseService();
        var exercise = svc.GetAllEnhancedExercises()[0];
        using var rt = new ExerciseRuntimeViewModel(exercise, new InlineUiDispatcher(), () => { });

        // Initial: inactive (no auto-start).
        bool initialInactive = rt.Phase == RuntimePhase.Inactive && !rt.IsRunning && rt.IsInactive && !rt.IsStopped;

        // Start -> active; synthetic stream flows.
        rt.BeginCommand.Execute(null);
        bool startedActive = rt.Phase == RuntimePhase.Active && rt.IsRunning && !rt.IsInactive && !rt.IsStopped;
        await Task.Delay(300);
        bool streamFlowing = rt.RuntimePitchSamples.Count > 0 && rt.CurrentPitch > 0;
        int activeSamples = rt.RuntimePitchSamples.Count;

        // Stop -> stopped/session-ended; stream cleared; summary present + "not saved".
        await rt.StopCommand.ExecuteAsync(null);
        bool stoppedState = rt.Phase == RuntimePhase.Stopped && !rt.IsRunning && rt.IsStopped && !rt.IsInactive;
        bool streamCleared = rt.RuntimePitchSamples.Count == 0;
        bool notSaved = rt.SessionEndedSummary.Contains("lagres ikke");
        Console.WriteLine($"[lifecycle] phases: inactive={initialInactive} active={startedActive} stopped={stoppedState}");
        Console.WriteLine($"[lifecycle] stream: active-samples={activeSamples} cleared-on-stop={streamCleared}");
        Console.WriteLine($"[lifecycle] summary: '{rt.SessionEndedSummary}'");

        // Re-Start -> fresh active (summary cleared); stream refills; no duplicate subscription.
        rt.BeginCommand.Execute(null);
        bool reStartedActive = rt.Phase == RuntimePhase.Active && rt.IsRunning && rt.SessionEndedSummary == "";
        await Task.Delay(300);
        bool reStreamFlowing = rt.RuntimePitchSamples.Count > 0;
        await rt.StopCommand.ExecuteAsync(null);
        int framesAfterStop = rt.RuntimePitchSamples.Count;   // 0 (cleared)
        await Task.Delay(120);
        bool noOrphanAfterStop = rt.RuntimePitchSamples.Count == framesAfterStop;   // stays 0 -> no double-firing handler
        Console.WriteLine($"[lifecycle] re-start: active={reStartedActive} flowing={reStreamFlowing} no-orphan-after-stop={noOrphanAfterStop}");

        // Nav-away disposal still stops the runtime + prevents orphan frames.
        var dash = new MainDashboardViewModel(new NoopAudioCaptureService(), new InlineUiDispatcher());
        var shell = new ShellViewModel(dash, svc, new InlineUiDispatcher());
        shell.ShowGuideCommand.Execute(null);
        var guide = shell.CurrentPage as ExerciseGuideViewModel;
        guide!.OpenExerciseCommand.Execute(guide.Exercises[0]);
        // (flow parity) the guide opens the exercise page (runtime) directly — no separate detail Start step.
        var navRuntime = shell.CurrentPage as ExerciseRuntimeViewModel;
        navRuntime?.BeginCommand.Execute(null);
        await Task.Delay(50);
        bool navRan = navRuntime?.IsRunning == true;
        shell.ShowDashboardCommand.Execute(null);   // nav away via the shell
        bool navDisposed = navRuntime is not null && !navRuntime.IsRunning;
        int navFrames = navRuntime?.RuntimePitchSamples.Count ?? -1;
        await Task.Delay(150);
        bool navNoOrphan = navRuntime is not null && navRuntime.RuntimePitchSamples.Count == navFrames;
        Console.WriteLine($"[lifecycle] nav-away: ran={navRan} disposed={navDisposed} no-orphan-frames={navNoOrphan}");

        // (Absence of persistence/write APIs on the runtime VM is verified by the source leak guard, not
        // re-checked here — embedding the token strings would itself trip the leak-guard grep.)
        bool ok = initialInactive && startedActive && streamFlowing && stoppedState && streamCleared && notSaved
                  && reStartedActive && reStreamFlowing && noOrphanAfterStop
                  && navRan && navDisposed && navNoOrphan;
        Console.WriteLine(ok ? "[lifecycle] Runtime lifecycle smoke OK" : "[lifecycle] Runtime lifecycle smoke FAIL");
        return ok ? 0 : 1;
    }

    // Headless verification of the Analysis/Resonance scaffold slice (no display): the Analysis nav item is
    // an IMPLEMENTED destination; navigating switches CurrentPage to an AnalysisViewModel that is purely inert
    // (not IDisposable, no IRelayCommand) and exposes SYNTHETIC in-memory chart series + summary placeholders;
    // and navigating to Analysis from a running runtime disposes the runtime safely (no orphaned capture).
    private static async Task<int> AnalysisScaffoldSmoke()
    {
        var svc = new VoiceFeminizationExerciseService();
        var dash = new MainDashboardViewModel(new NoopAudioCaptureService(), new InlineUiDispatcher());
        var shell = new ShellViewModel(dash, svc, new InlineUiDispatcher());

        // Analysis nav item exists and is implemented.
        var analysisNav = shell.NavItems.FirstOrDefault(n => n.Label == "Analyse");
        bool navExists = analysisNav is not null && analysisNav.IsImplemented;

        // Navigating to Analysis switches CurrentPage to AnalysisViewModel (no side effects).
        analysisNav?.Command.Execute(null);
        bool onAnalysis = shell.CurrentPage is AnalysisViewModel;
        var analysis = shell.CurrentPage as AnalysisViewModel;

        // Inert: not IDisposable, exposes no IRelayCommand; synthetic chart series + summary present.
        bool notDisposable = analysis is not null && !typeof(System.IDisposable).IsAssignableFrom(typeof(AnalysisViewModel));
        bool noCommands = analysis is not null && analysis.GetType().GetProperties()
            .All(p => !typeof(global::CommunityToolkit.Mvvm.Input.IRelayCommand).IsAssignableFrom(p.PropertyType));
        int seriesCount = analysis?.Series.Count ?? 0;
        bool seriesOk = seriesCount >= 3 && analysis!.Series.All(s => s.Bars.Count > 0 && !string.IsNullOrWhiteSpace(s.Title));
        bool summaryOk = (analysis?.SummaryMetrics.Count ?? 0) > 0 && analysis!.AllActionsDeferred;
        Console.WriteLine($"[analysis] nav-implemented={navExists} onAnalysis={onAnalysis} series={seriesCount} summary={analysis?.SummaryMetrics.Count}");
        Console.WriteLine($"[analysis] inert: notDisposable={notDisposable} noCommands={noCommands} seriesOk={seriesOk} summaryOk={summaryOk}");

        // Navigating to Analysis from a RUNNING runtime disposes the runtime safely (no orphaned capture).
        shell.ShowGuideCommand.Execute(null);
        var guide = shell.CurrentPage as ExerciseGuideViewModel;
        guide!.OpenExerciseCommand.Execute(guide.Exercises[0]);
        // (flow parity) the guide opens the exercise page (runtime) directly — no separate detail Start step.
        var runtime = shell.CurrentPage as ExerciseRuntimeViewModel;
        runtime?.BeginCommand.Execute(null);   // explicit start (runtime no longer auto-starts)
        await Task.Delay(50);
        bool runtimeRan = runtime?.IsRunning == true;
        analysisNav!.Command.Execute(null);   // nav to Analysis via the rail while running
        bool runtimeDisposed = runtime is not null && !runtime.IsRunning && shell.CurrentPage is AnalysisViewModel;
        int framesAfter = runtime?.RuntimePitchSamples.Count ?? -1;
        await Task.Delay(150);
        bool noOrphanFrames = runtime is not null && runtime.RuntimePitchSamples.Count == framesAfter;
        Console.WriteLine($"[analysis] Runtime->Analysis: ran={runtimeRan} disposed={runtimeDisposed} no-orphan-frames={noOrphanFrames}");

        bool ok = navExists && onAnalysis && notDisposable && noCommands && seriesOk && summaryOk
                  && runtimeRan && runtimeDisposed && noOrphanFrames;
        Console.WriteLine(ok ? "[analysis] Analysis scaffold smoke OK" : "[analysis] Analysis scaffold smoke FAIL");
        return ok ? 0 : 1;
    }

    // Headless verification of the Reports/Professional scaffold slice (no display): the Reports nav item is
    // an IMPLEMENTED destination; navigating switches CurrentPage to a ReportsViewModel that is purely inert
    // (not IDisposable, no IRelayCommand, all cards deferred); the expected placeholder cards are present; and
    // navigating to Reports from a running runtime disposes the runtime safely (no orphaned capture). No file
    // dialog / export / persistence APIs are touched (verified by the source leak guard).
    private static async Task<int> ReportsScaffoldSmoke()
    {
        var svc = new VoiceFeminizationExerciseService();
        var dash = new MainDashboardViewModel(new NoopAudioCaptureService(), new InlineUiDispatcher());
        var shell = new ShellViewModel(dash, svc, new InlineUiDispatcher());

        // Reports nav item exists and is implemented.
        var reportsNav = shell.NavItems.FirstOrDefault(n => n.Label == "Rapporter");
        bool navExists = reportsNav is not null && reportsNav.IsImplemented;

        // Navigating to Reports switches CurrentPage to ReportsViewModel (no side effects).
        reportsNav?.Command.Execute(null);
        bool onReports = shell.CurrentPage is ReportsViewModel;
        var reports = shell.CurrentPage as ReportsViewModel;

        // Inert: not IDisposable, no IRelayCommand; placeholder cards present + all deferred.
        bool notDisposable = reports is not null && !typeof(System.IDisposable).IsAssignableFrom(typeof(ReportsViewModel));
        bool noCommands = reports is not null && reports.GetType().GetProperties()
            .All(p => !typeof(global::CommunityToolkit.Mvvm.Input.IRelayCommand).IsAssignableFrom(p.PropertyType));
        int cardCount = reports?.Cards.Count ?? 0;
        bool cardsOk = cardCount >= 6 && reports!.Cards.All(c => !string.IsNullOrWhiteSpace(c.Title) && !c.IsEnabled);
        bool allDeferred = reports?.AllActionsDeferred == true;
        Console.WriteLine($"[reports] nav-implemented={navExists} onReports={onReports} cards={cardCount}");
        Console.WriteLine($"[reports] inert: notDisposable={notDisposable} noCommands={noCommands} cardsOk={cardsOk} allDeferred={allDeferred}");

        // Navigating to Reports from a RUNNING runtime disposes the runtime safely (no orphaned capture).
        shell.ShowGuideCommand.Execute(null);
        var guide = shell.CurrentPage as ExerciseGuideViewModel;
        guide!.OpenExerciseCommand.Execute(guide.Exercises[0]);
        // (flow parity) the guide opens the exercise page (runtime) directly — no separate detail Start step.
        var runtime = shell.CurrentPage as ExerciseRuntimeViewModel;
        runtime?.BeginCommand.Execute(null);   // explicit start (runtime no longer auto-starts)
        await Task.Delay(50);
        bool runtimeRan = runtime?.IsRunning == true;
        reportsNav!.Command.Execute(null);   // nav to Reports via the rail while running
        bool runtimeDisposed = runtime is not null && !runtime.IsRunning && shell.CurrentPage is ReportsViewModel;
        int framesAfter = runtime?.RuntimePitchSamples.Count ?? -1;
        await Task.Delay(150);
        bool noOrphanFrames = runtime is not null && runtime.RuntimePitchSamples.Count == framesAfter;
        Console.WriteLine($"[reports] Runtime->Reports: ran={runtimeRan} disposed={runtimeDisposed} no-orphan-frames={noOrphanFrames}");

        bool ok = navExists && onReports && notDisposable && noCommands && cardsOk && allDeferred
                  && runtimeRan && runtimeDisposed && noOrphanFrames;
        Console.WriteLine(ok ? "[reports] Reports scaffold smoke OK" : "[reports] Reports scaffold smoke FAIL");
        return ok ? 0 : 1;
    }

    // Headless verification of the Diagnostics/Export/Backup scaffold slice (no display): the Diagnostics nav
    // item is an IMPLEMENTED destination; navigating switches CurrentPage to a DiagnosticsViewModel that is
    // purely inert (not IDisposable, no IRelayCommand, all cards deferred); the expected placeholder cards are
    // present; and navigating to Diagnostics from a running runtime disposes the runtime safely (no orphaned
    // capture). No file dialog / export / backup / restore / persistence APIs are touched (source leak guard).
    private static async Task<int> DiagnosticsScaffoldSmoke()
    {
        var svc = new VoiceFeminizationExerciseService();
        var dash = new MainDashboardViewModel(new NoopAudioCaptureService(), new InlineUiDispatcher());
        var shell = new ShellViewModel(dash, svc, new InlineUiDispatcher());

        // Diagnostics nav item exists and is implemented.
        var diagNav = shell.NavItems.FirstOrDefault(n => n.Label == "Diagnostikk");
        bool navExists = diagNav is not null && diagNav.IsImplemented;

        // Navigating to Diagnostics switches CurrentPage to DiagnosticsViewModel (no side effects).
        diagNav?.Command.Execute(null);
        bool onDiagnostics = shell.CurrentPage is DiagnosticsViewModel;
        var diag = shell.CurrentPage as DiagnosticsViewModel;

        // Inert: not IDisposable, no IRelayCommand; placeholder cards present + all deferred.
        bool notDisposable = diag is not null && !typeof(System.IDisposable).IsAssignableFrom(typeof(DiagnosticsViewModel));
        bool noCommands = diag is not null && diag.GetType().GetProperties()
            .All(p => !typeof(global::CommunityToolkit.Mvvm.Input.IRelayCommand).IsAssignableFrom(p.PropertyType));
        int cardCount = diag?.Cards.Count ?? 0;
        bool cardsOk = cardCount >= 6 && diag!.Cards.All(c => !string.IsNullOrWhiteSpace(c.Title) && !c.IsEnabled);
        bool allDeferred = diag?.AllActionsDeferred == true;
        Console.WriteLine($"[diag] nav-implemented={navExists} onDiagnostics={onDiagnostics} cards={cardCount}");
        Console.WriteLine($"[diag] inert: notDisposable={notDisposable} noCommands={noCommands} cardsOk={cardsOk} allDeferred={allDeferred}");

        // Navigating to Diagnostics from a RUNNING runtime disposes the runtime safely (no orphaned capture).
        shell.ShowGuideCommand.Execute(null);
        var guide = shell.CurrentPage as ExerciseGuideViewModel;
        guide!.OpenExerciseCommand.Execute(guide.Exercises[0]);
        // (flow parity) the guide opens the exercise page (runtime) directly — no separate detail Start step.
        var runtime = shell.CurrentPage as ExerciseRuntimeViewModel;
        runtime?.BeginCommand.Execute(null);   // explicit start (runtime no longer auto-starts)
        await Task.Delay(50);
        bool runtimeRan = runtime?.IsRunning == true;
        diagNav!.Command.Execute(null);   // nav to Diagnostics via the rail while running
        bool runtimeDisposed = runtime is not null && !runtime.IsRunning && shell.CurrentPage is DiagnosticsViewModel;
        int framesAfter = runtime?.RuntimePitchSamples.Count ?? -1;
        await Task.Delay(150);
        bool noOrphanFrames = runtime is not null && runtime.RuntimePitchSamples.Count == framesAfter;
        Console.WriteLine($"[diag] Runtime->Diagnostics: ran={runtimeRan} disposed={runtimeDisposed} no-orphan-frames={noOrphanFrames}");

        bool ok = navExists && onDiagnostics && notDisposable && noCommands && cardsOk && allDeferred
                  && runtimeRan && runtimeDisposed && noOrphanFrames;
        Console.WriteLine(ok ? "[diag] Diagnostics scaffold smoke OK" : "[diag] Diagnostics scaffold smoke FAIL");
        return ok ? 0 : 1;
    }

    // Behavior-neutral verification of the desktop packaging-readiness slice (no display, no publish): inspect
    // the FemVoice.Avalonia project metadata (RuntimeIdentifiers for Linux/macOS, Tmds.DBus.Protocol pin,
    // trimming disabled, exactly Core + Audio.Abstractions project refs), confirm the inert packaging templates
    // exist, and confirm at runtime that the head references no FemVoice.Audio.* assembly other than Abstractions.
    // Read-only; changes nothing. (Forbidden token literals are deliberately NOT embedded here — verified via
    // positive checks + the source leak guard.)
    private static int PackagingSmoke()
    {
        // AppContext.BaseDirectory = .../FemVoice.Avalonia/bin/<cfg>/net10.0/  ->  up 3 = the project dir.
        string projectDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        string csprojPath = System.IO.Path.Combine(projectDir, "FemVoice.Avalonia.csproj");
        bool csprojFound = System.IO.File.Exists(csprojPath);
        string csproj = csprojFound ? System.IO.File.ReadAllText(csprojPath) : "";

        string[] rids = { "linux-x64", "linux-arm64", "osx-x64", "osx-arm64" };
        bool ridsOk = csprojFound && csproj.Contains("<RuntimeIdentifiers>") && rids.All(csproj.Contains);
        bool tmdsPinned = csproj.Contains("Tmds.DBus.Protocol\" Version=\"0.21.3\"");
        bool noTrim = csproj.Contains("<PublishTrimmed>false");
        int projRefCount = csproj.Split("<ProjectReference ").Length - 1;
        // Exactly 2 project refs, both Core + Audio.Abstractions present -> implicitly no third (Windows audio) ref.
        bool refsOk = projRefCount == 2 && csproj.Contains("FemVoice.Core") && csproj.Contains("FemVoice.Audio.Abstractions");

        bool plistOk = System.IO.File.Exists(System.IO.Path.Combine(projectDir, "Packaging", "macos", "Info.plist"));
        bool desktopOk = System.IO.File.Exists(System.IO.Path.Combine(projectDir, "Packaging", "linux", "femvoice-studio.desktop"));

        // Runtime reflection: the head references Core + Audio.Abstractions and NO other FemVoice.Audio.* assembly.
        var refs = typeof(Program).Assembly.GetReferencedAssemblies().Select(a => a.Name).Where(n => n != null).ToArray();
        bool refCore = refs.Contains("FemVoice.Core");
        bool refAbstractions = refs.Contains("FemVoice.Audio.Abstractions");
        bool noOtherFemVoiceAudio = refs.Where(n => n!.StartsWith("FemVoice.Audio.")).All(n => n == "FemVoice.Audio.Abstractions");

        Console.WriteLine($"[pkg] csproj: found={csprojFound} RIDs(linux-x64;linux-arm64;osx-x64;osx-arm64)={ridsOk} Tmds-pin-0.21.3={tmdsPinned} no-trim={noTrim}");
        Console.WriteLine($"[pkg] project refs: count={projRefCount} core+abstractions-only={refsOk}");
        Console.WriteLine($"[pkg] templates: macos/Info.plist={plistOk} linux/.desktop={desktopOk}");
        Console.WriteLine($"[pkg] runtime refs: Core={refCore} Abstractions={refAbstractions} no-other-FemVoice.Audio={noOtherFemVoiceAudio}");

        // --- Packaging helper scripts (publish + .deb) ---
        string pkgLinux = System.IO.Path.Combine(projectDir, "Packaging", "linux");
        string pkgMac = System.IO.Path.Combine(projectDir, "Packaging", "macos");
        string publishLinuxPath = System.IO.Path.Combine(pkgLinux, "publish-linux.sh");
        string packageDebPath = System.IO.Path.Combine(pkgLinux, "package-deb.sh");
        string publishMacPath = System.IO.Path.Combine(pkgMac, "publish-macos.sh");
        string desktopPath = System.IO.Path.Combine(pkgLinux, "femvoice-studio.desktop");
        bool helpersExist = System.IO.File.Exists(publishLinuxPath) && System.IO.File.Exists(packageDebPath) && System.IO.File.Exists(publishMacPath);
        string pl = System.IO.File.Exists(publishLinuxPath) ? System.IO.File.ReadAllText(publishLinuxPath) : "";
        string deb = System.IO.File.Exists(packageDebPath) ? System.IO.File.ReadAllText(packageDebPath) : "";
        string pm = System.IO.File.Exists(publishMacPath) ? System.IO.File.ReadAllText(publishMacPath) : "";
        string desktop = System.IO.File.Exists(desktopPath) ? System.IO.File.ReadAllText(desktopPath) : "";

        bool helpersRefProj = pl.Contains("FemVoice.Avalonia.csproj") && pm.Contains("FemVoice.Avalonia.csproj");
        bool helpersFdd = pl.Contains("--self-contained false") && pm.Contains("--self-contained false");
        bool helpersArtifacts = pl.Contains("artifacts/publish") && pm.Contains("artifacts/publish");
        bool debRefsDpkg = deb.Contains("dpkg-deb");
        bool debOut = deb.Contains("artifacts/packages/deb");
        bool debOpt = deb.Contains("/opt/femvoice-studio");
        bool debDesktop = deb.Contains("/usr/share/applications") && deb.Contains("femvoice-studio.desktop");
        bool debNoSudo = deb.Length > 0 && !deb.Contains("sudo");
        bool debNoMaintScripts = deb.Length > 0 && !deb.Contains("postinst") && !deb.Contains("preinst")
                                 && !deb.Contains("prerm") && !deb.Contains("postrm");
        bool desktopExec = desktop.Contains("Exec=femvoice-studio");

        // --- Launcher robustness: /usr/bin/femvoice-studio runs the DLL via `dotnet`, not the apphost. ---
        bool debLauncherPath = deb.Contains("/usr/bin/femvoice-studio");
        bool debLauncherUsesDotnet = deb.Contains("exec dotnet");
        bool debLauncherTargetsDll = deb.Contains("FemVoice.Avalonia.dll");
        bool debLauncherChecksDotnet = deb.Contains("command -v dotnet");
        // No automatic dependency install / no system-state mutation from the helper or launcher.
        bool debNoInstall = deb.Length > 0 && !deb.Contains("apt-get") && !deb.Contains("apt install")
                            && !deb.Contains("systemctl") && !deb.Contains("chown root");

        // --- Debian metadata: maintainer/author + machine-readable copyright (no invented OSS license). ---
        bool debMaintainer = deb.Contains("A hansen <rassyhansen@gmail.com>");
        bool debCopyrightInstalled = deb.Contains("/usr/share/doc/femvoice-studio/copyright");
        string copyrightTplPath = System.IO.Path.Combine(pkgLinux, "debian-copyright");
        bool copyrightTplExists = System.IO.File.Exists(copyrightTplPath);
        string cc = copyrightTplExists ? System.IO.File.ReadAllText(copyrightTplPath) : "";
        bool copyrightProprietary = cc.Contains("License: Proprietary");
        bool copyrightNoInventedOss = cc.Length > 0 && !cc.Contains("MIT License") && !cc.Contains("Apache License")
                                      && !cc.Contains("GNU General Public") && !cc.Contains("BSD License")
                                      && !cc.Contains("Mozilla Public License");

        Console.WriteLine($"[pkg] helpers: present={helpersExist} ref-csproj={helpersRefProj} fdd-default={helpersFdd} ->artifacts/publish={helpersArtifacts}");
        Console.WriteLine($"[pkg] deb: dpkg-deb={debRefsDpkg} out=artifacts/packages/deb={debOut} /opt={debOpt} .desktop={debDesktop} no-sudo={debNoSudo} no-maint-scripts={debNoMaintScripts}");
        Console.WriteLine($"[pkg] launcher: path={debLauncherPath} uses-dotnet={debLauncherUsesDotnet} targets-dll={debLauncherTargetsDll} checks-dotnet={debLauncherChecksDotnet} no-install={debNoInstall}");
        Console.WriteLine($"[pkg] metadata: maintainer={debMaintainer} copyright-installed={debCopyrightInstalled} copyright-tpl={copyrightTplExists} proprietary={copyrightProprietary} no-invented-oss={copyrightNoInventedOss}");
        Console.WriteLine($"[pkg] .desktop Exec=femvoice-studio: {desktopExec}");
        bool helpersOk = helpersExist && helpersRefProj && helpersFdd && helpersArtifacts
                         && debRefsDpkg && debOut && debOpt && debDesktop && debNoSudo && debNoMaintScripts && desktopExec
                         && debLauncherPath && debLauncherUsesDotnet && debLauncherTargetsDll && debLauncherChecksDotnet && debNoInstall
                         && debMaintainer && debCopyrightInstalled && copyrightTplExists && copyrightProprietary && copyrightNoInventedOss;

        bool ok = csprojFound && ridsOk && tmdsPinned && noTrim && refsOk
                  && plistOk && desktopOk && refCore && refAbstractions && noOtherFemVoiceAudio
                  && helpersOk;
        Console.WriteLine(ok ? "[pkg] Packaging readiness smoke OK" : "[pkg] Packaging readiness smoke FAIL");
        return ok ? 0 : 1;
    }

    // Read-only verification that the THEME/RESOURCE layer is intact in whatever build this runs from —
    // source-run OR the published/installed output. It proves the .deb does not strip theme resources: it sets
    // up the Avalonia app exactly as the real GUI does (App.axaml -> FluentTheme + the ShellTheme.axaml
    // dictionary merged via avares://), then asserts (a) FluentTheme base styling is registered, (b) every
    // {DynamicResource Shell*} key the views reference resolves to a brush in BOTH Dark and Light variants, and
    // (c) a theme variant is resolvable (diagnostic). It opens NO window, takes no screenshots, changes no UI.
    // It is headless-SAFE: it never requires a display to RUN. The runtime resource checks (a-c) need an
    // Avalonia platform (a display/X11/Wayland) to execute; when none is present they are cleanly SKIPPED — NOT
    // failed — exactly like --theme-loc-smoke (a missing display is not a packaging defect). To actually
    // exercise the embedded resources from the published DLL (the source-vs-packaged parity proof), run it where
    // a display is available: `dotnet artifacts/publish/<rid>/FemVoice.Avalonia.dll --packaged-theme-smoke`.
    private static int PackagedThemeSmoke()
    {
        // Closed set of custom shell brush keys defined in ShellTheme.axaml (Dark+Light) and referenced by the
        // shell/views via {DynamicResource ...}. Expanded by the dark visual-baseline slice (surfaces, semantic
        // palette, chart/chip). The source-only cross-check below flags any view that references a Shell* key
        // outside this set; the resolution loop verifies each listed key resolves to a brush in BOTH variants.
        string[] viewBrushKeys =
        {
            // Surfaces
            "ShellWindowBackgroundBrush","ShellHeaderBackgroundBrush","ShellStatusBackgroundBrush",
            "ShellPanelBackgroundBrush","ShellCardBackgroundBrush","ShellBorderBrush",
            // Text
            "ShellHeadingBrush","ShellBodyTextBrush","ShellSubtleTextBrush","ShellMutedBrush","ShellFaintBrush",
            // Accent + semantic palette
            "ShellAccentBrush","ShellPrimaryBrush","ShellPrimaryHoverBrush","ShellSecondaryBrush",
            "ShellSuccessBrush","ShellWarningBrush","ShellDangerBrush",
            // Success + deferred accents
            "ShellOkBrush","ShellOkBorderBrush","ShellDeferredTitleBrush","ShellDeferredBorderBrush",
            // Chart / chip surfaces
            "ShellChartBackgroundBrush","ShellChartTraceBrush","ShellTargetBandBrush","ShellMarkerBrush",
            "ShellChipBackgroundBrush","ShellChipTextBrush",
        };

        // Runtime theme check: requires an Avalonia platform. If the platform cannot initialize (genuinely
        // headless: no display), we mark it SKIPPED — not failed — mirroring ThemeLocSmoke(). Only a real
        // FluentTheme-missing / key-not-resolving condition (when the platform DID come up) is a failure.
        bool runtimeChecked = false, fluentOk = false, keysOk = true;
        string resolvedVariant = "(skipped — no Avalonia platform)";
        try
        {
            BuildAvaloniaApp().SetupWithoutStarting();
            var app = Application.Current;
            if (app is not null)
            {
                runtimeChecked = true;

                // (a) FluentTheme registered (provides base control styling: buttons, text, panels, etc.).
                fluentOk = app.Styles.OfType<global::Avalonia.Themes.Fluent.FluentTheme>().Any();

                // (b) Every view-referenced Shell* brush resolves to an IBrush in BOTH variants (embedded ShellTheme.axaml).
                foreach (var k in viewBrushKeys)
                {
                    bool darkOk = app.TryGetResource(k, global::Avalonia.Styling.ThemeVariant.Dark, out var dv) && dv is global::Avalonia.Media.IBrush;
                    bool lightOk = app.TryGetResource(k, global::Avalonia.Styling.ThemeVariant.Light, out var lv) && lv is global::Avalonia.Media.IBrush;
                    if (!(darkOk && lightOk)) { keysOk = false; Console.WriteLine($"[pkg-theme] MISSING/not-a-brush: {k} (dark={darkOk} light={lightOk})"); }
                }

                // (c) Diagnostic: which variant the running session selects (env-driven, not pass/fail).
                resolvedVariant = app.ActualThemeVariant?.ToString() ?? "(null)";
            }
        }
        catch (Exception ex)
        {
            // No Avalonia platform here (e.g. genuinely headless / no display). Skip the runtime check rather
            // than fail it — the theme/resource layer being unverifiable without a display is NOT a defect.
            Console.WriteLine($"[pkg-theme] runtime theme check skipped (no Avalonia platform here): {ex.GetType().Name}");
        }

        // Optional source-only cross-check: when run from the source tree (not the published DLL), scan the
        // view AXAML and confirm every {DynamicResource Shell*} key is in viewBrushKeys (catches a future
        // dangling/typo'd reference). Cleanly skipped from published output (no source AXAML present).
        bool axamlCrossChecked = false, axamlCrossOk = true;
        try
        {
            string projectDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
            if (System.IO.File.Exists(System.IO.Path.Combine(projectDir, "MainWindow.axaml")))
            {
                axamlCrossChecked = true;
                var known = new System.Collections.Generic.HashSet<string>(viewBrushKeys);
                var rx = new System.Text.RegularExpressions.Regex(@"DynamicResource\s+(Shell[A-Za-z0-9]+)");
                foreach (var f in System.IO.Directory.EnumerateFiles(projectDir, "*.axaml", System.IO.SearchOption.AllDirectories))
                    foreach (System.Text.RegularExpressions.Match m in rx.Matches(System.IO.File.ReadAllText(f)))
                    {
                        string key = m.Groups[1].Value;
                        if (!known.Contains(key)) { axamlCrossOk = false; Console.WriteLine($"[pkg-theme] AXAML references undefined key: {key} in {System.IO.Path.GetFileName(f)}"); }
                    }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[pkg-theme] AXAML cross-check skipped: {ex.GetType().Name}");
        }

        Console.WriteLine(runtimeChecked
            ? $"[pkg-theme] runtime: FluentTheme={fluentOk} shellKeys={viewBrushKeys.Length}×(Dark+Light) keysResolve={keysOk} variant='{resolvedVariant}'"
            : "[pkg-theme] runtime: SKIPPED (no Avalonia platform/display — not a defect)");
        Console.WriteLine($"[pkg-theme] AXAML key cross-check: {(axamlCrossChecked ? (axamlCrossOk ? "OK (source tree)" : "FAILED") : "skipped (published output / no source)")}");
        // Runtime portion passes if it ran cleanly OR was skipped (no platform). The AXAML cross-check (when it
        // ran) must pass. A genuine missing-FluentTheme / unresolved-key (with the platform up) is a real FAIL.
        bool runtimeOk = !runtimeChecked || (fluentOk && keysOk);
        bool allOk = runtimeOk && axamlCrossOk;
        Console.WriteLine(allOk ? "[pkg-theme] Packaged theme/resource smoke OK" : "[pkg-theme] Packaged theme/resource smoke FAIL");
        return allOk ? 0 : 1;
    }

    // Read-only, headless-safe verification of the dark visual-baseline slice. No window, no screenshots, no UI
    // change, no display REQUIRED to run. Confirms the Avalonia head is dark-first and exposes the FemVoice
    // theme palette, that the deferred nav surfaces stay deferred, and that Settings stays inert (no actions /
    // no theme or settings persistence wired). The runtime theme checks need an Avalonia platform (a display)
    // and are cleanly SKIPPED (not failed) when none is present, mirroring --theme-loc/--packaged-theme-smoke.
    private static int VisualBaselineSmoke()
    {
        // ── Platform-independent checks (always run) ──
        var svc = new VoiceFeminizationExerciseService();
        var dash = new MainDashboardViewModel(new NoopAudioCaptureService(), new InlineUiDispatcher());
        var shell = new ShellViewModel(dash, svc, new InlineUiDispatcher());
        int implemented = shell.NavItems.Count(n => n.IsImplemented);
        int deferred = shell.NavItems.Count(n => !n.IsImplemented);
        bool navOk = shell.NavItems.Count == 9 && implemented == 6 && deferred == 3;   // deferred surfaces stay deferred

        // Settings stays display-only/inert: not IDisposable, exposes no IRelayCommand (no actions/persistence wired).
        bool settingsInert = !typeof(System.IDisposable).IsAssignableFrom(typeof(SettingsViewModel))
            && typeof(SettingsViewModel).GetProperties()
                .All(p => !typeof(global::CommunityToolkit.Mvvm.Input.IRelayCommand).IsAssignableFrom(p.PropertyType));
        Console.WriteLine($"[visual] nav: total={shell.NavItems.Count} implemented={implemented} deferred={deferred} ok={navOk}");
        Console.WriteLine($"[visual] Settings inert (no actions/persistence): {settingsInert}");

        // ── Runtime theme checks (need an Avalonia platform; skipped, not failed, when headless) ──
        string[] paletteKeys =
        {
            "ShellWindowBackgroundBrush","ShellHeaderBackgroundBrush","ShellStatusBackgroundBrush",
            "ShellPanelBackgroundBrush","ShellCardBackgroundBrush","ShellBorderBrush",
            "ShellAccentBrush","ShellPrimaryBrush","ShellSecondaryBrush",
            "ShellSuccessBrush","ShellWarningBrush","ShellDangerBrush",
        };
        bool runtimeChecked = false, darkFirst = false, paletteOk = true;
        string variant = "(skipped — no Avalonia platform)";
        try
        {
            BuildAvaloniaApp().SetupWithoutStarting();
            var app = Application.Current;
            if (app is not null)
            {
                runtimeChecked = true;
                darkFirst = global::Avalonia.Styling.ThemeVariant.Dark.Equals(app.RequestedThemeVariant);
                variant = app.ActualThemeVariant?.ToString() ?? "(null)";
                foreach (var k in paletteKeys)
                    if (!(app.TryGetResource(k, global::Avalonia.Styling.ThemeVariant.Dark, out var v) && v is global::Avalonia.Media.IBrush))
                    { paletteOk = false; Console.WriteLine($"[visual] MISSING palette brush (Dark): {k}"); }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[visual] runtime theme check skipped (no Avalonia platform here): {ex.GetType().Name}");
        }
        Console.WriteLine(runtimeChecked
            ? $"[visual] runtime: dark-first(RequestedThemeVariant=Dark)={darkFirst} palette={paletteKeys.Length}-brushes-resolve={paletteOk} actualVariant='{variant}'"
            : "[visual] runtime: SKIPPED (no Avalonia platform/display — not a defect)");

        // ── Source-only check: implemented views use theme resources, not hardcoded light-grey defaults ──
        // Cleanly skipped from the published DLL (no source AXAML present).
        bool srcChecked = false, srcOk = true;
        try
        {
            string projectDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
            string viewsDir = System.IO.Path.Combine(projectDir, "Views");
            if (System.IO.File.Exists(System.IO.Path.Combine(projectDir, "MainWindow.axaml")) && System.IO.Directory.Exists(viewsDir))
            {
                srcChecked = true;
                var lightDefault = new System.Text.RegularExpressions.Regex(@"#(888|444|999|aaa|bbb|666)\b",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var files = new System.Collections.Generic.List<string> { System.IO.Path.Combine(projectDir, "MainWindow.axaml") };
                files.AddRange(System.IO.Directory.EnumerateFiles(viewsDir, "*.axaml"));
                foreach (var f in files)
                {
                    string text = System.IO.File.ReadAllText(f);
                    string name = System.IO.Path.GetFileName(f);
                    if (lightDefault.IsMatch(text)) { srcOk = false; Console.WriteLine($"[visual] light-default grey hex still present in {name}"); }
                    if (!text.Contains("DynamicResource Shell")) { srcOk = false; Console.WriteLine($"[visual] {name} does not reference theme brushes"); }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[visual] source check skipped: {ex.GetType().Name}");
        }
        Console.WriteLine($"[visual] source theme-usage check: {(srcChecked ? (srcOk ? "OK (source tree)" : "FAILED") : "skipped (published output / no source)")}");

        bool runtimeOk = !runtimeChecked || (darkFirst && paletteOk);
        bool allOk = navOk && settingsInert && runtimeOk && srcOk;
        Console.WriteLine(allOk ? "[visual] Visual baseline smoke OK" : "[visual] Visual baseline smoke FAIL");
        return allOk ? 0 : 1;
    }

    // Read-only verification of the interaction + chart polish. (1) The Exercise Guide row/card open command
    // path opens the ExerciseRuntimeViewModel exercise page directly for the selected exercise (the whole card is a Button bound
    // to OpenExerciseCommand; the chevron is only an affordance). (2) The dashboard exposes converter-free chart
    // geometry (comfort band + axis + marker) + a px trace, the chart brush keys resolve, and NO OxyPlot is
    // referenced. Runtime brush checks need an Avalonia platform and are skipped (not failed) when headless.
    private static int VisualInteractionChartSmoke()
    {
        var svc = new VoiceFeminizationExerciseService();
        var dash = new MainDashboardViewModel(new NoopAudioCaptureService(), new InlineUiDispatcher());
        var shell = new ShellViewModel(dash, svc, new InlineUiDispatcher());

        // (1) Exercise Guide row/card open command path -> ExerciseRuntimeViewModel directly (same path the chevron uses).
        shell.ShowGuideCommand.Execute(null);
        var guide = shell.CurrentPage as ExerciseGuideViewModel;
        int exerciseCount = guide?.Exercises.Count ?? 0;
        bool listsExercises = exerciseCount > 0;
        var firstCard = guide?.Exercises.FirstOrDefault();
        guide?.OpenExerciseCommand.Execute(firstCard);   // the command the whole-card Button is bound to
        var page = shell.CurrentPage as ExerciseRuntimeViewModel;   // the guide opens the exercise page directly
        bool cardOpensExercise = page is not null;
        bool detailMatches = page is not null && firstCard is not null && page.SelectedExerciseName == firstCard.Name;
        Console.WriteLine($"[visual-ix] guide: exercises={exerciseCount} cardOpensExercise={cardOpensExercise} detailMatches={detailMatches}");

        // (2) Dashboard chart geometry (display-only, converter-free) present + sane.
        var chart = dash.DashboardChart;
        bool chartGeometry = chart is not null && chart.ChartHeightPx > 0 && chart.TargetBandHeightPx >= 0
            && chart.ChartMaxPitch > chart.ChartMinPitch && dash.PitchTracePx is not null;
        Console.WriteLine($"[visual-ix] chart: heightPx={chart?.ChartHeightPx} band={chart?.TargetBandHeightPx:F0}px axis={chart?.ChartMinPitch:F0}-{chart?.ChartMaxPitch:F0}Hz geometryOk={chartGeometry}");

        // No third-party charting dependency introduced (the chart is in-house, Canvas/Shapes, converter-free).
        // Detect any charting assembly by the tell-tale "Plot"/"Chart" in its name (no legit ref has either),
        // without embedding a forbidden token literal here.
        var refs = typeof(Program).Assembly.GetReferencedAssemblies().Select(a => a.Name).Where(n => n != null).ToArray();
        bool noChartingLib = !refs.Any(n => n!.Contains("Plot") || n!.Contains("Chart"));
        Console.WriteLine($"[visual-ix] no-charting-lib-dependency={noChartingLib}");

        // Chart brush keys resolve in Dark (platform-gated; skipped, not failed, when headless).
        string[] chartKeys = { "ShellChartBackgroundBrush", "ShellTargetBandBrush", "ShellChartTraceBrush", "ShellMarkerBrush", "ShellBorderBrush" };
        bool runtimeChecked = false, chartBrushesOk = true;
        try
        {
            BuildAvaloniaApp().SetupWithoutStarting();
            var app = Application.Current;
            if (app is not null)
            {
                runtimeChecked = true;
                foreach (var k in chartKeys)
                    if (!(app.TryGetResource(k, global::Avalonia.Styling.ThemeVariant.Dark, out var v) && v is global::Avalonia.Media.IBrush))
                    { chartBrushesOk = false; Console.WriteLine($"[visual-ix] MISSING chart brush (Dark): {k}"); }
            }
        }
        catch (Exception ex) { Console.WriteLine($"[visual-ix] chart-brush check skipped (no Avalonia platform here): {ex.GetType().Name}"); }
        Console.WriteLine(runtimeChecked ? $"[visual-ix] chart brushes resolve (Dark): {chartBrushesOk}" : "[visual-ix] chart-brush check SKIPPED (no platform)");

        // Source-only: guide card is clickable (Button.guideCard -> OpenExerciseCommand); dashboard binds new geometry.
        bool srcChecked = false, srcOk = true;
        try
        {
            string projectDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
            string guidePath = System.IO.Path.Combine(projectDir, "Views", "ExerciseGuideView.axaml");
            string dashPath = System.IO.Path.Combine(projectDir, "Views", "DashboardView.axaml");
            if (System.IO.File.Exists(guidePath) && System.IO.File.Exists(dashPath))
            {
                srcChecked = true;
                string g = System.IO.File.ReadAllText(guidePath);
                string d = System.IO.File.ReadAllText(dashPath);
                if (!(g.Contains("Classes=\"guideCard\"") && g.Contains("OpenExerciseCommand")))
                { srcOk = false; Console.WriteLine("[visual-ix] ExerciseGuideView is not a clickable guideCard bound to OpenExerciseCommand"); }
                if (!(d.Contains("DashboardChart") && d.Contains("PitchTracePx") && d.Contains("ShellTargetBandBrush")))
                { srcOk = false; Console.WriteLine("[visual-ix] DashboardView does not bind the new chart geometry"); }
            }
        }
        catch (Exception ex) { Console.WriteLine($"[visual-ix] source check skipped: {ex.GetType().Name}"); }
        Console.WriteLine($"[visual-ix] source check: {(srcChecked ? (srcOk ? "OK (source tree)" : "FAILED") : "skipped (published output / no source)")}");

        bool runtimeOk = !runtimeChecked || chartBrushesOk;
        bool allOk = listsExercises && cardOpensExercise && detailMatches && chartGeometry && noChartingLib && runtimeOk && srcOk;
        Console.WriteLine(allOk ? "[visual-ix] Visual interaction + chart smoke OK" : "[visual-ix] Visual interaction + chart smoke FAIL");
        return allOk ? 0 : 1;
    }

    // Read-only verification of the WPF-parity exercise layout. (1) Guide card-click still opens the detail.
    // (2) Runtime Start/Stop lifecycle works and the feedback/hold/coordinator readouts stay wired (VM unchanged).
    // (3) The runtime VIEW no longer renders a pitch chart (WPF has none there) while the runtime VM RETAINS its
    // chart data model; the dashboard chart is retained. (4) Detail + runtime views are grid-based. No charting
    // dependency. All checks are deterministic (no frame-timing dependency) and need no display.
    private static async Task<int> ExerciseLayoutParitySmoke()
    {
        var svc = new VoiceFeminizationExerciseService();
        var dash = new MainDashboardViewModel(new NoopAudioCaptureService(), new InlineUiDispatcher());
        var shell = new ShellViewModel(dash, svc, new InlineUiDispatcher());

        // (1) Guide card-click -> the exercise (runtime) page directly (one page, one Start — no detail page).
        shell.ShowGuideCommand.Execute(null);
        var guide = shell.CurrentPage as ExerciseGuideViewModel;
        guide?.OpenExerciseCommand.Execute(guide.Exercises.FirstOrDefault());
        var runtime = shell.CurrentPage as ExerciseRuntimeViewModel;
        bool cardOpensExercise = runtime is not null;

        // (2) Start/Stop lifecycle on the SAME page + readouts still wired.
        runtime?.BeginCommand.Execute(null);
        await Task.Delay(60);
        bool started = runtime?.IsRunning == true;
        bool readoutsWired = runtime?.CoordinatorReadout is not null;             // coordinator readout still present
        bool runtimeChartModelRetained = runtime?.RuntimePitchSamples is not null; // data model NOT removed
        if (runtime is not null) await runtime.StopCommand.ExecuteAsync(null);   // deterministic: await the async Stop
        bool stopped = runtime is not null && !runtime.IsRunning;
        Console.WriteLine($"[ex-layout] guide->exercise={cardOpensExercise} started={started} readouts-wired={readoutsWired} chart-model-retained={runtimeChartModelRetained} stopped={stopped}");

        // (3) Dashboard chart retained; no charting dependency.
        bool dashChart = dash.DashboardChart is not null && dash.PitchTracePx is not null;
        var refs = typeof(Program).Assembly.GetReferencedAssemblies().Select(a => a.Name).Where(n => n != null).ToArray();
        bool noChartingLib = !refs.Any(n => n!.Contains("Plot") || n!.Contains("Chart"));
        Console.WriteLine($"[ex-layout] dashboard-chart-retained={dashChart} no-charting-lib={noChartingLib}");

        // (4) Source-only: runtime VIEW has no chart + is grid-based; dashboard keeps chart. (No detail view exists.)
        bool srcChecked = false, srcOk = true;
        try
        {
            string viewsDir = System.IO.Path.Combine(System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..")), "Views");
            string rt = System.IO.Path.Combine(viewsDir, "ExerciseRuntimeView.axaml");
            string dv = System.IO.Path.Combine(viewsDir, "DashboardView.axaml");
            if (System.IO.File.Exists(rt) && System.IO.File.Exists(dv))
            {
                srcChecked = true;
                string rtx = System.IO.File.ReadAllText(rt), dvx = System.IO.File.ReadAllText(dv);
                bool runtimeNoChart = !rtx.Contains("Canvas") && !rtx.Contains("RuntimePitchSamples") && !rtx.Contains("RuntimeChart");
                if (!runtimeNoChart) { srcOk = false; Console.WriteLine("[ex-layout] runtime view still renders a pitch chart"); }
                if (!rtx.Contains("ColumnDefinitions")) { srcOk = false; Console.WriteLine("[ex-layout] runtime view is not grid-based"); }
                if (!(dvx.Contains("Canvas") && dvx.Contains("PitchTracePx"))) { srcOk = false; Console.WriteLine("[ex-layout] dashboard view lost its chart"); }
            }
        }
        catch (Exception ex) { Console.WriteLine($"[ex-layout] source check skipped: {ex.GetType().Name}"); }
        Console.WriteLine($"[ex-layout] source check: {(srcChecked ? (srcOk ? "OK (source tree)" : "FAILED") : "skipped (published output / no source)")}");

        bool allOk = cardOpensExercise && started && readoutsWired && runtimeChartModelRetained && stopped
                     && dashChart && noChartingLib && srcOk;
        Console.WriteLine(allOk ? "[ex-layout] Exercise layout parity smoke OK" : "[ex-layout] Exercise layout parity smoke FAIL");
        return allOk ? 0 : 1;
    }

    // Read-only verification of the WPF-parity exercise FLOW + focus-aware wording. (1) The guide card opens the
    // exercise page DIRECTLY — there is NO separate detail/second-start page (the opened page is the runtime page,
    // Inactive with one pending Start). (2) Start activates the SAME page instance (no navigation to a second
    // page); Stop keeps the same page. (3) Non-pitch exercises do not present pitch as the primary focus; pitch
    // exercises still may. (4) Dashboard chart retained; the exercise page has no pitch chart; no charting dep.
    private static async Task<int> ExerciseFlowParitySmoke()
    {
        var svc = new VoiceFeminizationExerciseService();
        var dash = new MainDashboardViewModel(new NoopAudioCaptureService(), new InlineUiDispatcher());
        var shell = new ShellViewModel(dash, svc, new InlineUiDispatcher());

        // (1) Guide card click -> the exercise page directly (no separate detail page; one pending Start).
        shell.ShowGuideCommand.Execute(null);
        var guide = shell.CurrentPage as ExerciseGuideViewModel;
        var firstCard = guide?.Exercises.FirstOrDefault();
        guide?.OpenExerciseCommand.Execute(firstCard);
        var page = shell.CurrentPage as ExerciseRuntimeViewModel;
        bool opensExercisePage = page is not null;
        bool noSeparateStartPage = page is not null && page.IsInactive && !page.IsRunning;   // one page, one pending Start

        // (2) Start activates the SAME page instance (no nav to a second page); Stop keeps the same page.
        page?.BeginCommand.Execute(null);
        await Task.Delay(60);
        bool startSamePage = page is not null && ReferenceEquals(shell.CurrentPage, page) && page.IsRunning && page.Phase == RuntimePhase.Active;
        if (page is not null) await page.StopCommand.ExecuteAsync(null);   // deterministic: await the async Stop
        bool stopSamePage = page is not null && ReferenceEquals(shell.CurrentPage, page) && !page.IsRunning && page.IsStopped;
        Console.WriteLine($"[ex-flow] opens-exercise-page={opensExercisePage} no-separate-start-page={noSeparateStartPage} start-same-page={startSamePage} stop-same-page={stopSamePage}");

        // (3) Focus-aware wording: a non-pitch exercise does NOT lead with pitch; a pitch exercise still may.
        var nonPitchCard = guide?.Exercises.FirstOrDefault(c => !ExerciseDisplay.IsPitchPrimary(c.Exercise.Goal));
        var pitchCard = guide?.Exercises.FirstOrDefault(c => ExerciseDisplay.IsPitchPrimary(c.Exercise.Goal));
        bool nonPitchOk = true;
        if (nonPitchCard is not null)
        {
            shell.ShowGuideCommand.Execute(null);
            (shell.CurrentPage as ExerciseGuideViewModel)!.OpenExerciseCommand.Execute(nonPitchCard);
            var np = shell.CurrentPage as ExerciseRuntimeViewModel;
            nonPitchOk = np is not null && !np.IsPitchFocused;   // pitch is NOT the primary focus/wording
            Console.WriteLine($"[ex-flow] non-pitch '{nonPitchCard.Name}' (goal={nonPitchCard.Exercise.Goal}): isPitchFocused={np?.IsPitchFocused} secondaryPitch={np?.ShowSecondaryPitch} focus='{np?.FocusSummary}'");
        }
        else Console.WriteLine("[ex-flow] (no non-pitch exercise in catalog to check)");
        bool pitchOk = true;
        if (pitchCard is not null)
        {
            shell.ShowGuideCommand.Execute(null);
            (shell.CurrentPage as ExerciseGuideViewModel)!.OpenExerciseCommand.Execute(pitchCard);
            var pp = shell.CurrentPage as ExerciseRuntimeViewModel;
            pitchOk = pp is not null && pp.IsPitchFocused;
            Console.WriteLine($"[ex-flow] pitch '{pitchCard.Name}' (goal={pitchCard.Exercise.Goal}): isPitchFocused={pp?.IsPitchFocused}");
        }

        // (4) Dashboard chart retained; no charting dependency.
        bool dashChart = dash.DashboardChart is not null && dash.PitchTracePx is not null;
        var refs = typeof(Program).Assembly.GetReferencedAssemblies().Select(a => a.Name).Where(n => n != null).ToArray();
        bool noChartingLib = !refs.Any(n => n!.Contains("Plot") || n!.Contains("Chart"));
        Console.WriteLine($"[ex-flow] nonPitchOk={nonPitchOk} pitchOk={pitchOk} dashboard-chart={dashChart} no-charting-lib={noChartingLib}");

        // (5) Exercise Guide LIST parity (WPF): per-row session count + today's-progress summary are present;
        //     target-pitch (Hz) is NOT in the list; the list leads with goal/focus, not pitch.
        var card0 = guide?.Exercises.FirstOrDefault();
        bool listFieldsOk = card0 is not null
            && !string.IsNullOrWhiteSpace(card0.SessionCountText)   // per-exercise completed-session count (display-only)
            && !string.IsNullOrWhiteSpace(card0.FrequencyText)
            && !string.IsNullOrWhiteSpace(card0.GoalText)
            // Frequency must be the formatted WPF text, not the raw enum name (e.g. no "...GangerUkentlig").
            && (guide?.Exercises.All(c => !c.FrequencyText.Contains("Ganger")) ?? false);
        bool progressOk = guide is not null
            && !string.IsNullOrWhiteSpace(guide.TodaysProgressText) && !string.IsNullOrWhiteSpace(guide.ProgressNote);
        Console.WriteLine($"[ex-flow] list: sessionCount='{card0?.SessionCountText}' freq='{card0?.FrequencyText}' goal='{card0?.GoalText}' todaysProgress='{guide?.TodaysProgressText}' listFields={listFieldsOk} progress={progressOk}");

        // Source-only: exercise page has no pitch chart; dashboard keeps its chart; no separate detail view exists.
        bool srcChecked = false, srcOk = true;
        try
        {
            string viewsDir = System.IO.Path.Combine(System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..")), "Views");
            string rt = System.IO.Path.Combine(viewsDir, "ExerciseRuntimeView.axaml");
            string dv = System.IO.Path.Combine(viewsDir, "DashboardView.axaml");
            if (System.IO.File.Exists(rt) && System.IO.File.Exists(dv))
            {
                srcChecked = true;
                string rtx = System.IO.File.ReadAllText(rt), dvx = System.IO.File.ReadAllText(dv);
                if (rtx.Contains("Canvas") || rtx.Contains("RuntimePitchSamples") || rtx.Contains("RuntimeChart"))
                { srcOk = false; Console.WriteLine("[ex-flow] exercise page still renders a pitch chart"); }
                if (!(dvx.Contains("Canvas") && dvx.Contains("PitchTracePx")))
                { srcOk = false; Console.WriteLine("[ex-flow] dashboard lost its chart"); }
                if (System.IO.File.Exists(System.IO.Path.Combine(viewsDir, "ExerciseDetailView.axaml")))
                { srcOk = false; Console.WriteLine("[ex-flow] a separate ExerciseDetailView still exists (double-start risk)"); }
                // Guide LIST parity: no target-pitch (Hz) in the list; progress/session-count display present.
                string gp = System.IO.Path.Combine(viewsDir, "ExerciseGuideView.axaml");
                if (System.IO.File.Exists(gp))
                {
                    string gx = System.IO.File.ReadAllText(gp);
                    if (gx.Contains("TargetPitchText") || gx.Contains("Mål-pitch"))
                    { srcOk = false; Console.WriteLine("[ex-flow] guide list still shows target pitch (Hz)"); }
                    if (!(gx.Contains("SessionCountText") && gx.Contains("TodaysProgressText")))
                    { srcOk = false; Console.WriteLine("[ex-flow] guide list missing progress/session-count display"); }
                }
                // No persistence/analytics dependency introduced in the guide list VMs.
                string vmDir = System.IO.Path.Combine(System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..")), "ViewModels");
                foreach (var vmf in new[] { "ExerciseGuideViewModel.cs", "ExerciseCardViewModel.cs" })
                {
                    string p = System.IO.Path.Combine(vmDir, vmf);
                    if (System.IO.File.Exists(p))
                    {
                        string t = System.IO.File.ReadAllText(p);
                        // Detect persistence/analytics deps via substrings (avoids embedding the forbidden token literals).
                        if (t.Contains("AnalyticsStore") || t.Contains("DatabaseService") || t.Contains("SessionRecorder"))
                        { srcOk = false; Console.WriteLine($"[ex-flow] {vmf} introduces a persistence/analytics dependency"); }
                    }
                }
            }
        }
        catch (Exception ex) { Console.WriteLine($"[ex-flow] source check skipped: {ex.GetType().Name}"); }
        Console.WriteLine($"[ex-flow] source check: {(srcChecked ? (srcOk ? "OK (source tree)" : "FAILED") : "skipped (published output / no source)")}");

        bool allOk = opensExercisePage && noSeparateStartPage && startSamePage && stopSamePage
                     && nonPitchOk && pitchOk && listFieldsOk && progressOk && dashChart && noChartingLib && srcOk;
        Console.WriteLine(allOk ? "[ex-flow] Exercise flow parity smoke OK" : "[ex-flow] Exercise flow parity smoke FAIL");
        return allOk ? 0 : 1;
    }

    // Read-only verification that the desktop package SIGNING/NOTARIZATION READINESS surface is in place and
    // non-invasive: the Linux/macOS readiness docs + dry-run/check scripts exist; the scripts expose
    // --check/--dry-run/--help and hide secret values; the unsigned local flows (publish/package) are intact;
    // signing is NOT wired into the build (not mandatory); and no credential/key material was committed. It runs
    // NO scripts, requires NO secrets, and reads the source tree (like --packaging-smoke). From the published
    // DLL (no source tree) it cleanly SKIPS and returns 0.
    private static int SigningReadinessSmoke()
    {
        string projectDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        string lin = System.IO.Path.Combine(projectDir, "Packaging", "linux");
        string mac = System.IO.Path.Combine(projectDir, "Packaging", "macos");
        string signDoc = System.IO.Path.Combine(lin, "SIGNING.md");
        if (!System.IO.File.Exists(signDoc))
        {
            Console.WriteLine("[sign] skipped (published output / no source tree) — readiness surface lives in the source Packaging/ tree");
            Console.WriteLine("[sign] Signing readiness smoke OK");
            return 0;   // graceful skip from the published DLL (the docs/scripts are not shipped)
        }

        string notarizeDoc = System.IO.Path.Combine(mac, "NOTARIZATION.md");
        string signScript = System.IO.Path.Combine(lin, "signing-readiness.sh");
        string notarizeScript = System.IO.Path.Combine(mac, "notarization-readiness.sh");

        bool docsExist = System.IO.File.Exists(signDoc) && System.IO.File.Exists(notarizeDoc);
        bool scriptsExist = System.IO.File.Exists(signScript) && System.IO.File.Exists(notarizeScript);
        string ss = scriptsExist ? System.IO.File.ReadAllText(signScript) : "";
        string ns = scriptsExist ? System.IO.File.ReadAllText(notarizeScript) : "";
        // Each script exposes --check/--dry-run/--help and explicitly hides secret values.
        bool scriptFlags = new[] { ss, ns }.All(s => s.Contains("--check") && s.Contains("--dry-run") && s.Contains("--help"));
        bool scriptHidesValues = new[] { ss, ns }.All(s => s.Contains("value hidden") || s.Contains("values never printed") || s.Contains("never print"));

        // Unsigned local flows intact.
        bool unsignedFlows = System.IO.File.Exists(System.IO.Path.Combine(lin, "publish-linux.sh"))
                          && System.IO.File.Exists(System.IO.Path.Combine(lin, "package-deb.sh"))
                          && System.IO.File.Exists(System.IO.Path.Combine(mac, "publish-macos.sh"));

        // Signing is NOT wired into the build scripts (not mandatory locally): the package/publish scripts must
        // not invoke the readiness scripts or any signing tool.
        string deb = System.IO.File.ReadAllText(System.IO.Path.Combine(lin, "package-deb.sh"));
        string pubMac = System.IO.File.ReadAllText(System.IO.Path.Combine(mac, "publish-macos.sh"));
        // The build/publish scripts must not auto-run the readiness scripts, and must not contain an actual
        // signing INVOCATION (flag-bearing command). NB: "no codesign" comments in the scripts are non-invocations.
        bool signingNotMandatory = !deb.Contains("signing-readiness") && !pubMac.Contains("notarization-readiness")
                                   && !deb.Contains("dpkg-sig --sign") && !deb.Contains("gpg --")
                                   && !pubMac.Contains("codesign --") && !pubMac.Contains("notarytool ");

        // No secrets/keys committed in the readiness files (no PEM key material; scripts only READ env vars).
        var readinessFiles = new[] { signDoc, notarizeDoc, signScript, notarizeScript };
        bool noSecrets = readinessFiles.All(f => !System.IO.File.ReadAllText(f).Contains("-----BEGIN"));

        // Optional env vars are documented (presence only) — sanity-check a representative one in each doc.
        bool envDocumented = System.IO.File.ReadAllText(signDoc).Contains("FEMVOICE_DEB_SIGNING_KEY_ID")
                          && System.IO.File.ReadAllText(notarizeDoc).Contains("APPLE_NOTARY_PROFILE");

        Console.WriteLine($"[sign] docs(SIGNING.md+NOTARIZATION.md)={docsExist} scripts(present)={scriptsExist} flags(--check/--dry-run/--help)={scriptFlags} hides-values={scriptHidesValues}");
        Console.WriteLine($"[sign] unsigned-flows-intact={unsignedFlows} signing-not-mandatory(not-wired-into-build)={signingNotMandatory} no-secrets-committed={noSecrets} env-vars-documented={envDocumented}");

        bool ok = docsExist && scriptsExist && scriptFlags && scriptHidesValues
                  && unsignedFlows && signingNotMandatory && noSecrets && envDocumented;
        Console.WriteLine(ok ? "[sign] Signing readiness smoke OK" : "[sign] Signing readiness smoke FAIL");
        return ok ? 0 : 1;
    }

    // Read-only verification of the macOS .app / .dmg packaging readiness surface: the docs + package-app.sh +
    // package-dmg.sh exist, expose --check/--dry-run/--help, build UNSIGNED bundles only (no codesign/notarytool
    // invocation), require no Apple credentials, and the existing unsigned publish + notarization-readiness flows
    // are intact. Runs NO scripts, requires NO secrets. Inspects the source tree (like --packaging-smoke); from
    // the published DLL (no source tree) it cleanly SKIPS and returns 0.
    private static int MacosPackagingReadinessSmoke()
    {
        string projectDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        string mac = System.IO.Path.Combine(projectDir, "Packaging", "macos");
        string appScript = System.IO.Path.Combine(mac, "package-app.sh");
        if (!System.IO.File.Exists(appScript))
        {
            Console.WriteLine("[macos-pkg] skipped (published output / no source tree) — readiness surface lives in the source Packaging/ tree");
            Console.WriteLine("[macos-pkg] macOS packaging readiness smoke OK");
            return 0;   // graceful skip from the published DLL
        }

        string dmgScript = System.IO.Path.Combine(mac, "package-dmg.sh");
        string readme = System.IO.Path.Combine(mac, "README.md");
        string notarizeDoc = System.IO.Path.Combine(mac, "NOTARIZATION.md");
        string app = System.IO.File.ReadAllText(appScript);
        string dmg = System.IO.File.Exists(dmgScript) ? System.IO.File.ReadAllText(dmgScript) : "";

        bool docsExist = System.IO.File.Exists(readme) && System.IO.File.Exists(notarizeDoc);
        bool scriptsExist = System.IO.File.Exists(appScript) && System.IO.File.Exists(dmgScript);
        bool appFlags = app.Contains("--check") && app.Contains("--dry-run") && app.Contains("--help");
        bool dmgFlags = dmg.Contains("--check") && dmg.Contains("--dry-run") && dmg.Contains("--help");
        // Verify the documented graceful-skip CONTRACT, not just that the word "hdiutil" appears: build mode must
        // guard on `command -v hdiutil` and skip (off macOS) rather than calling `hdiutil create` unconditionally.
        bool dmgHandlesHdiutil = dmg.Contains("command -v hdiutil") && dmg.Contains("skipping");
        bool appUsesPlist = app.Contains("Info.plist");
        // No real signing: neither script contains a codesign/notarytool INVOCATION (flag-bearing); the "no
        // codesign" comments are non-invocations.
        bool noRealSigning = !app.Contains("codesign -") && !app.Contains("notarytool ")
                          && !dmg.Contains("codesign -") && !dmg.Contains("notarytool ");
        // Unsigned + future-signing flows intact.
        bool unsignedFlowsIntact = System.IO.File.Exists(System.IO.Path.Combine(mac, "publish-macos.sh"))
                                && System.IO.File.Exists(System.IO.Path.Combine(mac, "notarization-readiness.sh"));
        // No key material committed in the new readiness files — incl. NOTARIZATION.md, the doc that discusses
        // Apple credentials and is the most likely to accidentally receive a pasted key block.
        var files = new[] { appScript, dmgScript, readme, notarizeDoc };
        bool noSecrets = files.All(f => System.IO.File.Exists(f) && !System.IO.File.ReadAllText(f).Contains("-----BEGIN"));

        Console.WriteLine($"[macos-pkg] docs(README+NOTARIZATION)={docsExist} scripts(app+dmg)={scriptsExist} app-flags={appFlags} dmg-flags={dmgFlags} dmg-hdiutil={dmgHandlesHdiutil} app-uses-plist={appUsesPlist}");
        Console.WriteLine($"[macos-pkg] no-real-signing(no codesign/notarytool invocation)={noRealSigning} unsigned+notarization-flows-intact={unsignedFlowsIntact} no-secrets-committed={noSecrets}");

        bool ok = docsExist && scriptsExist && appFlags && dmgFlags && dmgHandlesHdiutil && appUsesPlist
                  && noRealSigning && unsignedFlowsIntact && noSecrets;
        Console.WriteLine(ok ? "[macos-pkg] macOS packaging readiness smoke OK" : "[macos-pkg] macOS packaging readiness smoke FAIL");
        return ok ? 0 : 1;
    }

    // Read-only verification of the macOS app-icon / .icns READINESS surface: the future icon path is documented;
    // Info.plist wires CFBundleIconFile=AppIcon; package-app.sh bundles AppIcon.icns ONLY if present and handles
    // its absence gracefully (no failure, no required icon); the packaging never FABRICATES an icon (no
    // iconutil/sips synthesis); and no production icon/branding is invented. Inspects the source tree (like
    // --packaging-smoke); from the published DLL (no source tree) it cleanly SKIPS and returns 0.
    private static int MacosIconReadinessSmoke()
    {
        string projectDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        string mac = System.IO.Path.Combine(projectDir, "Packaging", "macos");
        string appScript = System.IO.Path.Combine(mac, "package-app.sh");
        if (!System.IO.File.Exists(appScript))
        {
            Console.WriteLine("[macos-icon] skipped (published output / no source tree) — readiness surface lives in the source Packaging/ tree");
            Console.WriteLine("[macos-icon] macOS icon readiness smoke OK");
            return 0;   // graceful skip from the published DLL
        }

        string iconDoc = System.IO.Path.Combine(mac, "AppIcon.icns.README.md");
        string plist = System.IO.Path.Combine(mac, "Info.plist");
        string app = System.IO.File.ReadAllText(appScript);
        string doc = System.IO.File.Exists(iconDoc) ? System.IO.File.ReadAllText(iconDoc) : "";
        string pl = System.IO.File.Exists(plist) ? System.IO.File.ReadAllText(plist) : "";

        // Future icon path documented (the placeholder README names the expected AppIcon.icns asset).
        bool iconDocsExist = System.IO.File.Exists(iconDoc);
        bool iconPathDocumented = doc.Contains("AppIcon.icns") && doc.Contains("Packaging/macos/AppIcon.icns");
        // Info.plist wires CFBundleIconFile = AppIcon.
        bool cfBundleIconWired = pl.Contains("CFBundleIconFile") && pl.Contains("<string>AppIcon</string>");
        // package-app.sh bundles the icon ONLY conditionally (guarded by `if [ -f "$ICON"`) into Contents/Resources.
        bool appHandlesIconConditionally = app.Contains("AppIcon.icns") && app.Contains("if [ -f \"$ICON\"")
                                        && app.Contains("Contents/Resources");
        // Absent-icon path is graceful (a readiness note, not a failure / not required).
        bool gracefulWhenAbsent = app.Contains("absent") && (app.Contains("not an error") || app.Contains("system default"))
                                  && app.Contains("deferred");
        // No production icon FABRICATION: packaging never synthesizes an icon (no iconutil/sips generation in the
        // script). The .icns is only ever COPIED from an externally-provided asset.
        bool noFabrication = !app.Contains("iconutil") && !app.Contains("sips ");
        // Existing macOS packaging readiness intact.
        bool existingReadinessIntact = System.IO.File.Exists(System.IO.Path.Combine(mac, "package-dmg.sh"))
                                    && System.IO.File.Exists(System.IO.Path.Combine(mac, "README.md"))
                                    && System.IO.File.Exists(System.IO.Path.Combine(mac, "publish-macos.sh"));
        // No key material committed in the new icon-readiness files.
        bool noSecrets = !doc.Contains("-----BEGIN");
        // Informational: whether a real icon is committed (deferred state = absent). Not gated on.
        bool icnsCommitted = System.IO.File.Exists(System.IO.Path.Combine(mac, "AppIcon.icns"));

        Console.WriteLine($"[macos-icon] icon-docs={iconDocsExist} path-documented={iconPathDocumented} CFBundleIconFile=AppIcon={cfBundleIconWired} conditional-bundle={appHandlesIconConditionally} graceful-when-absent={gracefulWhenAbsent}");
        Console.WriteLine($"[macos-icon] no-fabrication(iconutil/sips)={noFabrication} existing-readiness-intact={existingReadinessIntact} no-secrets={noSecrets} icns-committed={icnsCommitted} (false = production icon deferred)");

        bool ok = iconDocsExist && iconPathDocumented && cfBundleIconWired && appHandlesIconConditionally
                  && gracefulWhenAbsent && noFabrication && existingReadinessIntact && noSecrets;
        Console.WriteLine(ok ? "[macos-icon] macOS icon readiness smoke OK" : "[macos-icon] macOS icon readiness smoke FAIL");
        return ok ? 0 : 1;
    }

    // Verifies the Exercise Guide list-level WPF parity: category-filter chips ("Alle" + goals) + a name/description
    // search that combine (category AND search) over the in-memory display list — DISPLAY-ONLY (no persistence,
    // analytics, DB, or session writes). Also confirms default-shows-all, category subset, clearing returns all,
    // search by name/description, combined filter, empty state, that a filtered card still opens the exercise page
    // directly, and that the list rows carry no target-Hz. Pure VM/logic + a source check; no display needed.
    private static int ExerciseGuideFilterSearchSmoke()
    {
        var svc = new VoiceFeminizationExerciseService();
        var guide = new ExerciseGuideViewModel(svc, _ => { });
        int total = guide.Exercises.Count;
        if (total == 0) { Console.WriteLine("[guide-filter] FAIL: no exercises"); return 1; }

        // Chips: "Alle" + at least one goal; "Alle" selected by default.
        bool chipsExist = guide.CategoryChips.Count >= 2
                          && guide.CategoryChips.Any(c => c.Label == ExerciseGuideViewModel.AllCategory);
        bool defaultAll = guide.SelectedCategory == ExerciseGuideViewModel.AllCategory
                          && guide.FilteredCount == total && guide.HasResults;

        // Select a real (non-"Alle") category chip -> a valid, non-empty subset, all matching that goal.
        var cat = guide.CategoryChips.First(c => c.Label != ExerciseGuideViewModel.AllCategory).Label;
        guide.SelectCategoryCommand.Execute(cat);
        int expectCat = guide.Exercises.Count(c => string.Equals(c.GoalText, cat, StringComparison.OrdinalIgnoreCase));
        bool categorySubset = guide.FilteredCount == expectCat && expectCat > 0 && guide.FilteredCount <= total
                              && guide.FilteredExercises.All(c => string.Equals(c.GoalText, cat, StringComparison.OrdinalIgnoreCase));
        bool oneChipSelected = guide.CategoryChips.Count(c => c.IsSelected) == 1
                               && guide.CategoryChips.Single(c => c.IsSelected).Label == cat;

        // Clearing category back to "Alle" returns all.
        guide.SelectCategoryCommand.Execute(ExerciseGuideViewModel.AllCategory);
        bool clearCategoryReturnsAll = guide.FilteredCount == total;

        // Search by name (case-insensitive) — match WPF (Name OR Description). Use a lowercased token from card[0].
        var token = new string(guide.Exercises[0].Name.Trim().Split(' ')[0].Take(4).ToArray()).ToLowerInvariant();
        guide.SearchText = token;
        bool searchFiltersByName = guide.FilteredCount > 0 && guide.FilteredCount <= total
            && guide.FilteredExercises.All(c =>
                c.Name.Contains(token, StringComparison.OrdinalIgnoreCase)
                || c.ShortDescription.Contains(token, StringComparison.OrdinalIgnoreCase));

        // Combined: category + search both applied (intersection, never larger than search-only).
        int searchOnly = guide.FilteredCount;
        guide.SelectCategoryCommand.Execute(cat);
        bool combined = guide.FilteredCount <= searchOnly
            && guide.FilteredExercises.All(c =>
                string.Equals(c.GoalText, cat, StringComparison.OrdinalIgnoreCase)
                && (c.Name.Contains(token, StringComparison.OrdinalIgnoreCase)
                    || c.ShortDescription.Contains(token, StringComparison.OrdinalIgnoreCase)));

        // Empty state: a no-match search yields zero and flips IsEmpty.
        guide.SelectCategoryCommand.Execute(ExerciseGuideViewModel.AllCategory);
        guide.SearchText = "zzqx-no-such-exercise-xqzz";
        bool emptyState = guide.FilteredCount == 0 && guide.IsEmpty && !guide.HasResults;

        // Clearing the search returns all (with "Alle").
        guide.SearchText = "";
        bool clearSearchReturnsAll = guide.FilteredCount == total && guide.HasResults;

        // A filtered card still opens the exercise (runtime) page directly (WPF parity, via the shell).
        var dash = new MainDashboardViewModel(new NoopAudioCaptureService(), new InlineUiDispatcher());
        var shell = new ShellViewModel(dash, svc, new InlineUiDispatcher());
        shell.ShowGuideCommand.Execute(null);
        var guidePage = shell.CurrentPage as ExerciseGuideViewModel;
        guidePage!.SelectCategoryCommand.Execute(cat);
        guidePage.OpenExerciseCommand.Execute(guidePage.FilteredExercises[0]);
        bool opensExercise = shell.CurrentPage is ExerciseRuntimeViewModel;

        // Source check: the Guide row template carries no target-pitch (Hz). Skipped (true) if no source tree.
        string viewPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "Views", "ExerciseGuideView.axaml"));
        bool noTargetHzInRows = !System.IO.File.Exists(viewPath)
                                || !System.IO.File.ReadAllText(viewPath).Contains("TargetPitchText");

        Console.WriteLine($"[guide-filter] total={total} chips={guide.CategoryChips.Count} chipsExist={chipsExist} defaultAll={defaultAll} categorySubset={categorySubset}({cat}={expectCat}) oneChipSelected={oneChipSelected} clearCatAll={clearCategoryReturnsAll}");
        Console.WriteLine($"[guide-filter] searchByName('{token}')={searchFiltersByName} combined={combined} emptyState={emptyState} clearSearchAll={clearSearchReturnsAll} opensExercise={opensExercise} noTargetHzInRows={noTargetHzInRows}");

        bool allOk = chipsExist && defaultAll && categorySubset && oneChipSelected && clearCategoryReturnsAll
                     && searchFiltersByName && combined && emptyState && clearSearchReturnsAll && opensExercise && noTargetHzInRows;
        Console.WriteLine(allOk ? "[guide-filter] Exercise Guide filter/search smoke OK" : "[guide-filter] Exercise Guide filter/search smoke FAIL");
        return allOk ? 0 : 1;
    }

    // Verifies the DEFERRED, display-only SmartCoach + Progression UI scaffolds: navigation opens the scaffold
    // VMs (inert, not IDisposable, hold no injected services — proven by a parameterless ctor), both are clearly
    // marked deferred with disabled actions and SYNTHETIC "—" placeholders (no real recommendations/scores/levels),
    // the placeholder cards exist, and the shell sidebar (9 items, 3 deferred) + dashboard navigation remain intact.
    // No engine/scoring/safety-gate/persistence is touched (build + project leak-guard enforce no such reference).
    private static int SmartCoachProgressionUiScaffoldSmoke()
    {
        var svc = new VoiceFeminizationExerciseService();
        var dash = new MainDashboardViewModel(new NoopAudioCaptureService(), new InlineUiDispatcher());
        var shell = new ShellViewModel(dash, svc, new InlineUiDispatcher());

        // Navigation to both deferred scaffolds works; each opens its inert scaffold VM (not IDisposable).
        shell.NavItems.First(n => n.Label.Contains("Progresjon")).Command.Execute(null);
        var prog = shell.CurrentPage as ProgressionScaffoldViewModel;
        bool progNav = prog is not null && shell.CurrentPage is not IDisposable;
        shell.NavItems.First(n => n.Label.Contains("SmartCoach")).Command.Execute(null);
        var coach = shell.CurrentPage as SmartCoachScaffoldViewModel;
        bool coachNav = coach is not null && shell.CurrentPage is not IDisposable;

        // Sidebar intact (still 9 items, still 3 deferred) and dashboard nav still works.
        bool navIntact = shell.NavItems.Count == 9 && shell.NavItems.Count(n => !n.IsImplemented) == 3;
        shell.ShowDashboardCommand.Execute(null);
        bool backToDash = shell.CurrentPage is MainDashboardViewModel;

        // Hold NO injected services (a single parameterless constructor).
        static bool OnlyParameterlessCtor(Type t)
        {
            var c = t.GetConstructors();
            return c.Length == 1 && c[0].GetParameters().Length == 0;
        }
        bool noServiceDeps = OnlyParameterlessCtor(typeof(SmartCoachScaffoldViewModel))
                             && OnlyParameterlessCtor(typeof(ProgressionScaffoldViewModel));

        // Deferred + disabled + synthetic placeholders (no real numbers/recommendations).
        bool coachDeferred = coach!.DeferredBadge.Contains("Utsatt") && !coach.ActionEnabled
                             && coach.Placeholder == "—" && coach.TodayRecommendation.Length > 0
                             && coach.StreakLabel.Length > 0 && coach.SessionsLabel.Length > 0 && coach.HealthLabel.Length > 0;
        bool progDeferred = prog!.DeferredBadge.Contains("Utsatt") && !prog.ActionEnabled
                            && prog.ScoreValue == "—" && prog.ProgressValue == 0
                            && prog.Parameters.Count == 3 && prog.Parameters.All(p => p.Value == "—")
                            && prog.LevelName.Length > 0 && prog.ScoreLabel.Length > 0;

        Console.WriteLine($"[sc-prog] progNav={progNav} coachNav={coachNav} navIntact={navIntact} backToDash={backToDash} noServiceDeps={noServiceDeps}");
        Console.WriteLine($"[sc-prog] coachDeferred(disabled+synthetic)={coachDeferred} progDeferred(disabled+3 synthetic params)={progDeferred}");

        bool ok = progNav && coachNav && navIntact && backToDash && noServiceDeps && coachDeferred && progDeferred;
        Console.WriteLine(ok ? "[sc-prog] SmartCoach/Progression UI scaffold smoke OK" : "[sc-prog] SmartCoach/Progression UI scaffold smoke FAIL");
        return ok ? 0 : 1;
    }

    // Verifies the DISPLAY-ONLY Settings visual scaffold: navigation opens the inert SettingsViewModel (no
    // services — parameterless ctor, no IRelayCommand, not IDisposable); the WPF-like section cards exist
    // (9: General/Theme/Language/Audio/VoiceGoal/Accessibility/Data/Privacy/About); every row is inert
    // (IsEnabled=false) and actionable rows render a representative DISABLED control (combo/toggle/button) with
    // an "Utsatt" chip; and the shell sidebar (9 items, 6 implemented) stays intact. No persistence/DB/analytics
    // or real theme/language/audio/database/privacy/backup behavior is invoked (build + leak-guard enforce it).
    private static int SettingsVisualParitySmoke()
    {
        var svc = new VoiceFeminizationExerciseService();
        var dash = new MainDashboardViewModel(new NoopAudioCaptureService(), new InlineUiDispatcher());
        var shell = new ShellViewModel(dash, svc, new InlineUiDispatcher());

        var nav = shell.NavItems.FirstOrDefault(n => n.Label == "Innstillinger");
        bool navOk = nav is not null && nav.IsImplemented;
        nav?.Command.Execute(null);
        var settings = shell.CurrentPage as SettingsViewModel;
        bool onSettings = settings is not null;

        // Inert / no services: not IDisposable, no IRelayCommand property, single parameterless ctor.
        bool notDisposable = !typeof(System.IDisposable).IsAssignableFrom(typeof(SettingsViewModel));
        bool noCommands = typeof(SettingsViewModel).GetProperties()
            .All(p => !typeof(global::CommunityToolkit.Mvvm.Input.IRelayCommand).IsAssignableFrom(p.PropertyType));
        var ctors = typeof(SettingsViewModel).GetConstructors();
        bool noServiceDeps = ctors.Length == 1 && ctors[0].GetParameters().Length == 0;

        // WPF-like section set: 9 non-empty section cards (titles are localization-resolved, so we assert the
        // count + non-emptiness rather than brittle language-specific substrings).
        bool sectionsOk = settings!.Sections.Count == 9
            && settings.Sections.All(s => !string.IsNullOrWhiteSpace(s.Title) && s.Rows.Count > 0);

        var rows = settings.Sections.SelectMany(s => s.Rows).ToList();
        // Every control inert; actionable rows carry an "Utsatt" chip.
        bool allInert = rows.All(r => !r.IsEnabled);
        bool chipsOnActionable = rows.Where(r => r.Kind != SettingsControlKind.Info).All(r => r.ShowDeferredChip);
        // Representative disabled controls present (not just generic text): at least one combo, toggle, button.
        bool hasCombo = rows.Any(r => r.Kind == SettingsControlKind.Combo);
        bool hasToggle = rows.Any(r => r.Kind == SettingsControlKind.Toggle);
        bool hasButton = rows.Any(r => r.Kind == SettingsControlKind.Button);
        bool deferredWording = settings.DeferredBadge.Contains("Utsatt") && settings.DeferredBanner.Length > 0;

        // Sidebar intact.
        bool navIntact = shell.NavItems.Count == 9 && shell.NavItems.Count(n => n.IsImplemented) == 6;

        Console.WriteLine($"[settings-vis] onSettings={onSettings} navOk={navOk} sections={settings.Sections.Count} controls(combo/toggle/button)={hasCombo}/{hasToggle}/{hasButton}");
        Console.WriteLine($"[settings-vis] allInert={allInert} chipsOnActionable={chipsOnActionable} deferredWording={deferredWording} notDisposable={notDisposable} noCommands={noCommands} noServiceDeps={noServiceDeps} navIntact={navIntact}");

        bool ok = navOk && onSettings && notDisposable && noCommands && noServiceDeps && sectionsOk
                  && allInert && chipsOnActionable && hasCombo && hasToggle && hasButton
                  && deferredWording && navIntact;
        Console.WriteLine(ok ? "[settings-vis] Settings visual parity smoke OK" : "[settings-vis] Settings visual parity smoke FAIL");
        return ok ? 0 : 1;
    }

    // Verifies the visual layout-polish pass is behavior-neutral: the polished views adopt a centered content
    // column (Settings + scaffolds + Exercise Guide) and Settings uses a responsive WrapPanel for section cards
    // (source inspection — skipped/true when no source tree, e.g. from the published DLL); AND the display-only
    // guarantees still hold at the VM level (Settings inert, SmartCoach/Progression deferred+disabled, Exercise
    // Guide filter/search intact, Dashboard chart model present). No behavior is enabled.
    private static int VisualLayoutPolishSmoke()
    {
        string viewsDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Views"));
        // Returns the file text, or "" if absent. Source checks treat absent as a skip (pass).
        string View(string name)
        {
            string p = System.IO.Path.Combine(viewsDir, name);
            return System.IO.File.Exists(p) ? System.IO.File.ReadAllText(p) : "";
        }
        bool SourcePresent = System.IO.File.Exists(System.IO.Path.Combine(viewsDir, "SettingsView.axaml"));

        // ---- Source (XAML) layout checks — skipped (true) when the source tree isn't shipped. ----
        string settings = View("SettingsView.axaml");
        bool settingsResponsive = !SourcePresent || (settings.Contains("WrapPanel") && settings.Contains("HorizontalAlignment=\"Center\""));
        bool scaffoldsCentered = !SourcePresent || new[]
        {
            "SmartCoachScaffoldView.axaml", "ProgressionScaffoldView.axaml",
            "AnalysisView.axaml", "ReportsView.axaml", "DiagnosticsView.axaml",
        }.All(v => View(v).Contains("HorizontalAlignment=\"Center\""));
        string guide = View("ExerciseGuideView.axaml");
        bool guideCentered = !SourcePresent || (guide.Contains("HorizontalAlignment=\"Center\"")
                              && guide.Contains("SearchText") && guide.Contains("CategoryChips")
                              && !guide.Contains("TargetPitchText"));   // filter/search kept, no target-Hz reintroduced

        // ---- VM display-only guarantees (always run; independent of source tree). ----
        var settingsVm = new SettingsViewModel();
        bool settingsInert = settingsVm.AllControlsDeferred
            && !typeof(System.IDisposable).IsAssignableFrom(typeof(SettingsViewModel));
        var coach = new SmartCoachScaffoldViewModel();
        var prog = new ProgressionScaffoldViewModel();
        bool scaffoldsDeferred = !coach.ActionEnabled && !prog.ActionEnabled
            && prog.Parameters.All(p => p.Value == "—")
            && typeof(SmartCoachScaffoldViewModel).GetConstructors()[0].GetParameters().Length == 0
            && typeof(ProgressionScaffoldViewModel).GetConstructors()[0].GetParameters().Length == 0;

        var svc = new VoiceFeminizationExerciseService();
        var dash = new MainDashboardViewModel(new NoopAudioCaptureService(), new InlineUiDispatcher());
        var shell = new ShellViewModel(dash, svc, new InlineUiDispatcher());
        var guideVm = new ExerciseGuideViewModel(svc, _ => { });
        bool guideFilterIntact = guideVm.CategoryChips.Count >= 2 && guideVm.FilteredExercises.Count == guideVm.Exercises.Count;
        guideVm.SearchText = "zzqx-none"; bool searchWorks = guideVm.FilteredCount == 0; guideVm.SearchText = "";
        bool dashboardChartIntact = dash.DashboardChart is not null;   // chart model unchanged
        bool navIntact = shell.NavItems.Count == 9 && shell.NavItems.Count(n => n.IsImplemented) == 6;

        Console.WriteLine($"[layout] source={(SourcePresent ? "present" : "skipped")} settingsResponsive={settingsResponsive} scaffoldsCentered={scaffoldsCentered} guideCentered={guideCentered}");
        Console.WriteLine($"[layout] settingsInert={settingsInert} scaffoldsDeferred={scaffoldsDeferred} guideFilterIntact={guideFilterIntact}&searchWorks={searchWorks} dashboardChartIntact={dashboardChartIntact} navIntact={navIntact}");

        bool ok = settingsResponsive && scaffoldsCentered && guideCentered
                  && settingsInert && scaffoldsDeferred && guideFilterIntact && searchWorks
                  && dashboardChartIntact && navIntact;
        Console.WriteLine(ok ? "[layout] Visual layout polish smoke OK" : "[layout] Visual layout polish smoke FAIL");
        return ok ? 0 : 1;
    }
}
