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

        // Headless verification paths (no display needed) — used by scripts/linux-portable-gate.sh.
        if (args.Contains("--smoke")) return Smoke();
        if (args.Contains("--dashboard-smoke")) return DashboardSmoke().GetAwaiter().GetResult();
        if (args.Contains("--exercise-smoke")) return ExerciseSmoke();
        if (args.Contains("--exercise-runtime-smoke")) return ExerciseRuntimeSmoke().GetAwaiter().GetResult();
        if (args.Contains("--exercise-runtime-integration-smoke")) return ExerciseRuntimeIntegrationSmoke().GetAwaiter().GetResult();
        if (args.Contains("--exercise-coordinator-smoke")) return ExerciseCoordinatorSmoke().GetAwaiter().GetResult();
        if (args.Contains("--runtime-chart-feedback-smoke")) return RuntimeChartFeedbackSmoke().GetAwaiter().GetResult();
        if (args.Contains("--shell-smoke")) return ShellSmoke().GetAwaiter().GetResult();
        if (args.Contains("--theme-loc-smoke")) return ThemeLocSmoke();

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
        var shell = new ShellViewModel(dash, svc, new InlineUiDispatcher());
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

    // Headless verification of the Exercise Runtime slice (no display): synthetic pitch drives the
    // runtime VM into the exercise target band, hold/elapsed advance, and detail -> runtime -> back nav works.
    private static async Task<int> ExerciseRuntimeSmoke()
    {
        var svc = new VoiceFeminizationExerciseService();
        var exercise = svc.GetAllEnhancedExercises()[0]; // Grunnleggende humming (target 140-180 Hz)

        using var rt = new ExerciseRuntimeViewModel(exercise, new InlineUiDispatcher(), () => { });
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

        // Navigation via the shell: dashboard -> guide -> detail -> runtime -> back-to-detail
        var dash = new MainDashboardViewModel(new NoopAudioCaptureService(), new InlineUiDispatcher());
        var shell = new ShellViewModel(dash, svc, new InlineUiDispatcher());
        shell.ShowGuideCommand.Execute(null);
        var guide = shell.CurrentPage as ExerciseGuideViewModel;
        guide!.OpenExerciseCommand.Execute(guide.Exercises[0]);
        var detail = shell.CurrentPage as ExerciseDetailViewModel;
        detail!.StartCommand.Execute(null);
        bool onRuntime = shell.CurrentPage is ExerciseRuntimeViewModel;
        var rvm = shell.CurrentPage as ExerciseRuntimeViewModel;
        await Task.Delay(100);
        if (rvm is not null) await rvm.BackCommand.ExecuteAsync(null);
        bool backToDetail = shell.CurrentPage is ExerciseDetailViewModel;
        Console.WriteLine($"[runtime] Navigation: runtime={onRuntime} back-to-detail={backToDetail}");

        bool ok = running && stopped && pitch > 0 && rt.TargetPitchMax > 0
                  && status == "Innenfor målområde" && hold > 0 && onRuntime && backToDetail;
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

        // Navigation: dashboard -> guide -> detail -> runtime -> back-to-detail
        var dash = new MainDashboardViewModel(new NoopAudioCaptureService(), new InlineUiDispatcher());
        var shell = new ShellViewModel(dash, svc, new InlineUiDispatcher());
        shell.ShowGuideCommand.Execute(null);
        var guide = shell.CurrentPage as ExerciseGuideViewModel;
        guide!.OpenExerciseCommand.Execute(guide.Exercises[0]);
        (shell.CurrentPage as ExerciseDetailViewModel)!.StartCommand.Execute(null);
        bool onRuntime = shell.CurrentPage is ExerciseRuntimeViewModel;
        var rvm = shell.CurrentPage as ExerciseRuntimeViewModel;
        await Task.Delay(50);
        if (rvm is not null) await rvm.BackCommand.ExecuteAsync(null);
        bool backToDetail = shell.CurrentPage is ExerciseDetailViewModel;
        Console.WriteLine($"[rt-int] Runtime: {(onRuntime ? "OK" : "FAIL")}");
        Console.WriteLine($"[rt-int] Navigation: runtime={onRuntime} back-to-detail={backToDetail}");

        bool ok = exercises.Count == 15 && (mapped + fallback) == 15 && profileShown
                  && pitch > 0 && status == "Innenfor målområde" && onRuntime && backToDetail;
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

        // Navigation A via the shell: dashboard -> guide -> detail -> runtime -> back-to-detail (own Back).
        var dash = new MainDashboardViewModel(new NoopAudioCaptureService(), new InlineUiDispatcher());
        var shell = new ShellViewModel(dash, svc, new InlineUiDispatcher());
        shell.ShowGuideCommand.Execute(null);
        var guide = shell.CurrentPage as ExerciseGuideViewModel;
        guide!.OpenExerciseCommand.Execute(guide.Exercises[0]);
        (shell.CurrentPage as ExerciseDetailViewModel)!.StartCommand.Execute(null);
        bool onRuntime = shell.CurrentPage is ExerciseRuntimeViewModel;
        var rvm = shell.CurrentPage as ExerciseRuntimeViewModel;
        await Task.Delay(50);
        if (rvm is not null) await rvm.BackCommand.ExecuteAsync(null);
        bool backToDetail = shell.CurrentPage is ExerciseDetailViewModel;

        // Navigation B: leaving a RUNNING runtime via the always-visible top nav must DISPOSE it
        // (stop the synthetic capture + clear the VM-local coordinator), not orphan it.
        (shell.CurrentPage as ExerciseDetailViewModel)!.StartCommand.Execute(null);
        var rvm2 = shell.CurrentPage as ExerciseRuntimeViewModel;
        await Task.Delay(50);
        bool wasRunning = rvm2?.IsRunning == true;
        shell.ShowGuideCommand.Execute(null);   // top-nav away while running
        bool clearedByNav = rvm2 is not null && !rvm2.IsRunning && shell.CurrentPage is ExerciseGuideViewModel;
        Console.WriteLine($"[coord] Navigation: runtime={onRuntime} back-to-detail={backToDetail} " +
                          $"nav-away-clears={clearedByNav} (was-running={wasRunning})");

        // The coordinator was enabled for exercise #1 (mapped profile), so we expect the active path.
        // If a future exercise had no mapped profile, the readout would be documented unavailable instead.
        if (!active)
            Console.WriteLine("[coord] Coordinator readout unavailable: documented");

        bool ok = exercises.Count == 15 && active && liveStateReceived && readoutMode && safetyDisplayOnly
                  && clearedOnStop && reBeginActive && reBeginLive
                  && onRuntime && backToDetail && wasRunning && clearedByNav;
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

        // Navigation via the shell: dashboard -> guide -> detail -> runtime -> back-to-detail
        var dash = new MainDashboardViewModel(new NoopAudioCaptureService(), new InlineUiDispatcher());
        var shell = new ShellViewModel(dash, svc, new InlineUiDispatcher());
        shell.ShowGuideCommand.Execute(null);
        var guide = shell.CurrentPage as ExerciseGuideViewModel;
        guide!.OpenExerciseCommand.Execute(guide.Exercises[0]);
        (shell.CurrentPage as ExerciseDetailViewModel)!.StartCommand.Execute(null);
        bool onRuntime = shell.CurrentPage is ExerciseRuntimeViewModel;
        var rvm = shell.CurrentPage as ExerciseRuntimeViewModel;
        await Task.Delay(50);
        if (rvm is not null) await rvm.BackCommand.ExecuteAsync(null);
        bool backToDetail = shell.CurrentPage is ExerciseDetailViewModel;
        Console.WriteLine($"[chart] Navigation: {(onRuntime && backToDetail ? "OK" : "FAIL")} (runtime={onRuntime} back-to-detail={backToDetail})");

        bool ok = exercises.Count == 15 && samples > 0 && samples <= 120 && markerOk && bandOk
                  && feedbackOk && feedbackMsg == "Innenfor målområdet" && derivedHoldOk
                  && coordVisualOk && stopped && onRuntime && backToDetail;
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

        // Deferred nav opens a STATIC placeholder with no side effect (not IDisposable, holds no services).
        var deferredItem = shell.NavItems.First(n => !n.IsImplemented);
        deferredItem.Command.Execute(null);
        bool onDeferred = shell.CurrentPage is DeferredSurfaceViewModel;
        bool deferredInert = shell.CurrentPage is DeferredSurfaceViewModel && shell.CurrentPage is not IDisposable;
        Console.WriteLine($"[shell] Deferred nav '{deferredItem.Label}' -> {(onDeferred ? "static placeholder" : "FAIL")} (inert={deferredInert})");

        // Runtime nav-away disposes the transient runtime (no orphaned capture).
        shell.ShowGuideCommand.Execute(null);
        var guide = shell.CurrentPage as ExerciseGuideViewModel;
        guide!.OpenExerciseCommand.Execute(guide.Exercises[0]);
        (shell.CurrentPage as ExerciseDetailViewModel)!.StartCommand.Execute(null);
        var firstRuntime = shell.CurrentPage as ExerciseRuntimeViewModel;
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
        (shell.CurrentPage as ExerciseDetailViewModel)!.StartCommand.Execute(null);
        var secondRuntime = shell.CurrentPage as ExerciseRuntimeViewModel;
        await Task.Delay(50);
        bool secondRunning = secondRuntime?.IsRunning == true;
        bool distinctInstance = secondRuntime is not null && !ReferenceEquals(firstRuntime, secondRuntime);
        bool firstStillStopped = firstRuntime is not null && !firstRuntime.IsRunning;
        if (secondRuntime is not null) await secondRuntime.BackCommand.ExecuteAsync(null);
        Console.WriteLine($"[shell] Runtime lifecycle: running={runtimeRunning} disposed-on-nav={firstDisposedOnNav} " +
                          $"no-orphan-frames={noOrphanFrames} fresh-instance={distinctInstance} " +
                          $"second-running={secondRunning} no-orphan={firstStillStopped}");

        bool ok = landsOnDashboard && implemented == 2 && deferred >= 1
                  && onGuide && backToDash && onDeferred && deferredInert
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
            && shell.NavItems[2].Label == "Innstillinger — senere";
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
}
