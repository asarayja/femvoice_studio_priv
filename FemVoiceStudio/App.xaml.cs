using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FemVoiceStudio.Data;
using FemVoiceStudio.Audio;
using FemVoiceStudio.Services;
using FemVoiceStudio.Services.Progression;
using FemVoiceStudio.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FemVoiceStudio;

public partial class App : Application
{
    private Window? _splash;

    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            Services = serviceCollection.BuildServiceProvider();

            var isFirstTime = FirstTimeSetupService.Instance.IsFirstTime;
            ThemeManager.Instance.Initialize();
            DebugSettingsService.Instance.EnsureDebugSection();

            _splash = CreateSplash();
            _splash.Show();
            DispatcherHelper.DoEvents();

            if (isFirstTime)
            {
                var setupWindow = new Views.FirstTimeSetupWindow();
                var result = setupWindow.ShowDialog();
                if (result != true)
                {
                    Shutdown();
                    return;
                }
            }

            var mainWindow = new Views.MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_splash != null)
                {
                    _splash.Close();
                    _splash = null;
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Feil ved oppstart: {ex.Message}\n\nDetaljer:\n{ex.StackTrace}",
                "Feil", MessageBoxButton.OK, MessageBoxImage.Error);

            try
            {
                var mainWindow = new Views.MainWindow();
                MainWindow = mainWindow;
                mainWindow.Show();
                _splash?.Close();
            }
            catch
            {
                Shutdown(1);
            }
        }
    }

    /// <summary>
    /// Konfigurerer Dependency Injection-containeren.
    /// Legg til nye tjenester her etter behov.
    /// </summary>
    private static void ConfigureServices(IServiceCollection services)
    {
        // ── Data layer ────────────────────────────────────────────────────────────
        // DatabaseService is a singleton: schema initialization runs exactly once at
        // startup (guarded by the _initialized flag in DatabaseService). All ViewModels
        // receive the same already-initialized instance via IDatabaseService injection.
        services.AddSingleton<DatabaseService>();
        services.AddSingleton<IDatabaseService>(sp => sp.GetRequiredService<DatabaseService>());

        // ── Localization ──────────────────────────────────────────────────────────
        // LocalizationService.Instance is the app-wide singleton; wrap it so DI can
        // inject ILocalizationService into ExerciseDetailViewModel and SmartCoachEngine.
        services.AddSingleton<ILocalizationService>(_ => LocalizationService.Instance);

        // ── Audio / biofeedback engines ───────────────────────────────────────────
        // These are singletons: each engine owns audio hardware or long-lived state.
        services.AddSingleton<ResonanceProxyEngine>();
        services.AddSingleton<FemVoiceScoreEngine>();
        services.AddSingleton<ComfortZoneController>();
        // VoiceHealthMonitor fjernet: Analyze() ble aldri kalt i produksjon
        // (integrasjonsauditen) — helse drives av VocalHealthSupervisor-stien.

        // ── Coordinator ───────────────────────────────────────────────────────────
        // ExerciseIntelligenceCoordinator wires the five engines above; must be
        // singleton so ExerciseDetailViewModel and the coordinator share one instance.
        services.AddSingleton<ExerciseIntelligenceCoordinator>();

        // ── Service layer ─────────────────────────────────────────────────────────
        services.AddSingleton<InMemoryExerciseRepository>();
        services.AddSingleton<IUserRepository>(sp => sp.GetRequiredService<InMemoryExerciseRepository>());
        services.AddSingleton<IScoreRepository>(sp => sp.GetRequiredService<InMemoryExerciseRepository>());
        services.AddSingleton<ISessionAnalyticsRepository>(_ =>
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "FemVoiceStudio");
            Directory.CreateDirectory(appDataPath);
            var databasePath = Path.Combine(appDataPath, "femvoice.db");
            return new SqliteSessionAnalyticsRepository($"Data Source={databasePath}");
        });
        services.AddSingleton<IExerciseProfileStore>(_ =>
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "FemVoiceStudio");
            Directory.CreateDirectory(appDataPath);
            var databasePath = Path.Combine(appDataPath, "femvoice.db");
            return new SqliteExerciseProfileStore($"Data Source={databasePath}");
        });
        services.AddSingleton<SessionAnalyticsStore>();
        services.AddSingleton<ProgressionOrchestrator>();

        // ── Klinisk øktscoring og gating ──────────────────────────────────────────
        // ExerciseSessionRecorder abonnerer på koordinatorens live-state-strøm,
        // vekker VocalHealthSupervisor (strain/fatigue) og persisterer øktresultater
        // til SessionAnalyticsStore. MasteryEvaluator og ProgressionSafetyGate leser
        // den persisterte historikken for å gate mastery og vanskelighetspromotering.
        services.AddSingleton<ExerciseSessionRecorder>();
        services.AddSingleton<MasteryEvaluator>();
        services.AddSingleton<ProgressionSafetyGate>();
        services.AddSingleton<TargetProfileAdapter>();

        // ── Tilgjengelighet (StressSensitiveMode / ReducedVisualFeedback) ──────────
        // Singleton: laster UserVoiceProfile lazy og caches. SettingsWindow kaller
        // Refresh() etter lagring (SaveUserVoiceProfile) slik at endrede flagg slår
        // igjennom uten omstart. Dempingen endrer KUN presentasjon, aldri om
        // safety/helse-informasjon vises (Safety > Health > ... > UI).
        services.AddSingleton(sp => new StressSensitiveExperience(
            sp.GetRequiredService<IDatabaseService>()));

        // ExerciseDataService brukes av ExerciseDetailViewModel for å lese øvelses-
        // progresjon (mastery). Var tidligere aldri registrert — GetService returnerte
        // null og mastery-badgen viste alltid «Beginner».
        services.AddSingleton(_ =>
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "FemVoiceStudio");
            Directory.CreateDirectory(appDataPath);
            var databasePath = Path.Combine(appDataPath, "femvoice.db");
            return new Data.ExerciseDataService($"Data Source={databasePath}");
        });
        services.AddSingleton<FeedbackConsistencyGuard>();
        services.AddSingleton<FeedbackPipeline>();
        services.AddSingleton<ProgressionFeedbackMapper>();
        services.AddSingleton<SmartCoachFeedbackMapper>();
        services.AddSingleton<InlineCoachFeedbackMapper>();
        services.AddSingleton<VocalHealthBaselineProvider>();
        services.AddSingleton(sp => sp.GetRequiredService<VocalHealthBaselineProvider>().CreateVocalHealthOptions());
        services.AddSingleton(sp => sp.GetRequiredService<VocalHealthBaselineProvider>().CreateHydrationOptions());
        services.AddSingleton(sp => new VocalHealthSupervisor(sp.GetRequiredService<VocalHealthSupervisorOptions>()));
        services.AddSingleton<VocalHealthFeedbackMapper>();
        services.AddSingleton(sp => new HydrationAdvisor(sp.GetRequiredService<HydrationAdvisorOptions>()));
        services.AddSingleton<HydrationFeedbackMapper>();
        services.AddSingleton<IVoiceGoalProfileProvider, LocalVoiceGoalProfileStore>();
        services.AddSingleton(sp => new SmartCoachEngine(
            sp.GetRequiredService<IDatabaseService>(),
            sp.GetRequiredService<ILocalizationService>(),
            sp.GetRequiredService<FeedbackPipeline>(),
            sp.GetRequiredService<SmartCoachFeedbackMapper>(),
            sp.GetRequiredService<IVoiceGoalProfileProvider>()));
        services.AddSingleton<IExerciseProfileFactory, ExerciseProfileFactory>();

        // ── ViewModels ────────────────────────────────────────────────────────────
        // SmartCoachViewModel is Transient: a new instance per tab navigation is fine
        // because it holds no long-lived state of its own — all state lives in the DB.
        services.AddTransient<SmartCoachViewModel>();

        // ExerciseDetailViewModel is Transient: one per ExerciseWindow.
        // Constructor injection resolves ExerciseIntelligenceCoordinator,
        // ILocalizationService, and IExerciseProfileFactory automatically.
        services.AddTransient<ExerciseDetailViewModel>();
    }

    private Window CreateSplash()
    {
        var splash = new Window
        {
            Width = 500,
            Height = 500,
            WindowStyle = WindowStyle.None,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = new SolidColorBrush(Color.FromRgb(255, 240, 245)),
            ShowInTaskbar = false,
            ResizeMode = ResizeMode.NoResize
        };

        try
        {
            var grid = new Grid();
            var logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "logo.png");
            if (!File.Exists(logoPath))
                logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.png");

            var image = new Image
            {
                Source = new BitmapImage(new Uri(logoPath, UriKind.Absolute)),
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.Children.Add(image);
            splash.Content = grid;
        }
        catch
        {
            var text = new TextBlock
            {
                Text = "FemVoice Studio",
                FontSize = 32,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(233, 30, 99)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            splash.Content = new Grid { Children = { text } };
        }

        return splash;
    }
}

public static class DispatcherHelper
{
    public static void DoEvents()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
            new DispatcherOperationCallback(ExitFrame), frame);
        Dispatcher.PushFrame(frame);
    }

    private static object? ExitFrame(object f)
    {
        ((DispatcherFrame)f).Continue = false;
        return null;
    }
}
