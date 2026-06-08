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

        // ── Adaptiv coach-lag (Sprint C, Bølge 2) ──────────────────────────────────
        // Rene/lese-tjenester som SmartCoach konsumerer for GOAL-/STAGE-/RECOVERY-
        // bevisst coaching. Alle er additive: SmartCoach faller tilbake til dagens
        // oppførsel dersom noen mangler (valgfrie ctor-params).
        services.AddSingleton<RecoveryScorer>();
        services.AddSingleton(sp => new RecoveryIntelligenceService(sp.GetRequiredService<RecoveryScorer>()));
        services.AddSingleton<LearningPathProfileBuilder>();
        services.AddSingleton(sp => new Services.Progression.ComplexityEngine(
            sp.GetRequiredService<DatabaseService>()));

        // Sprint C.2, Agent 7 (Recommendation Data Provider): per-øvelse-effektivitet
        // over den persisterte analytics-historikken. SmartCoach konsumerer den (valgfritt)
        // for å lede LearningPath-anbefalingene mot det som faktisk har fungert. Additivt —
        // null-motor ville gitt dagens bånd-logikk. Bygger på SessionAnalyticsStore, ved
        // siden av RecoveryIntelligenceService/LearningPathProfileBuilder.
        services.AddSingleton(sp => new ExerciseEffectivenessEngine(
            sp.GetRequiredService<SessionAnalyticsStore>()));

        // ── Bølge 1/2: longitudinell intelligens (additive, alle valgfrie) ─────────
        // TrendEngineService drives trendvindu-beregninger over SessionAnalyticsStore.
        services.AddSingleton(sp => new TrendEngineService(
            sp.GetRequiredService<SessionAnalyticsStore>()));
        // VoicePatternDetector er en ren, tilstandsløs tjeneste — ingen ctor-avhengigheter.
        services.AddSingleton<VoicePatternDetector>();
        // LongitudinalInsightEngine bruker parameterløs ctor (bruker LocalizationService.Instance internt).
        services.AddSingleton<LongitudinalInsightEngine>();
        // RecommendationExplanationEngine er ren og trenger kun lokalisering.
        services.AddSingleton(sp => new RecommendationExplanationEngine(
            sp.GetRequiredService<ILocalizationService>()));
        // SmartCoachMemoryStore persisterer coach-råd og utfall i femvoice.db.
        services.AddSingleton(_ =>
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "FemVoiceStudio");
            Directory.CreateDirectory(appDataPath);
            var databasePath = Path.Combine(appDataPath, "femvoice.db");
            ISmartCoachMemoryRepository repo = new SqliteSmartCoachMemoryRepository($"Data Source={databasePath}");
            return new SmartCoachMemoryStore(repo);
        });
        // VoiceKnowledgeGraphBuilder er en ren, tilstandsløs tjeneste — ingen ctor-avhengigheter.
        services.AddSingleton<VoiceKnowledgeGraphBuilder>();

        services.AddSingleton(sp => new SmartCoachEngine(
            sp.GetRequiredService<IDatabaseService>(),
            sp.GetRequiredService<ILocalizationService>(),
            sp.GetRequiredService<FeedbackPipeline>(),
            sp.GetRequiredService<SmartCoachFeedbackMapper>(),
            sp.GetRequiredService<IVoiceGoalProfileProvider>(),
            // Bølge 2: gi SmartCoach lesetilgang til VoiceMetrics-trenden slik at
            // daglig-anbefalingen kan velge coaching-akse på den svakeste dimensjonen
            // (etter health-gaten). Valgfri param — null ville gitt dagens oppførsel.
            sp.GetRequiredService<SessionAnalyticsStore>(),
            // Sprint C, Bølge 2: prediktiv restitusjon + LearningPath-stage + complexity.
            // Recovery > Goals: en forecast med Severity >= Recommend (eller RestRecommended)
            // gir restitusjons-coaching FØR mål, og recovery/konsistens skalerer varigheten.
            sp.GetRequiredService<RecoveryIntelligenceService>(),
            sp.GetRequiredService<LearningPathProfileBuilder>(),
            sp.GetRequiredService<Services.Progression.ComplexityEngine>(),
            // masteryLevelProvider: foreløpig null i produksjon. MasteryEvaluator er
            // øvelses-/profil-scoped og async; en trygg per-bruker-bro krever mer
            // plumbing enn denne bølgen rører. Den feirende mestrings-grenen er fullt
            // dekket av delegat-sømmen i testene; produksjonswiring kommer i en senere
            // bølge. null ⇒ ingen mestrings-melding (dagens oppførsel).
            masteryLevelProvider: null,
            // Sprint C.2, Agent 7: effektivitets-intelligensen som data-provider. SmartCoach
            // leder LearningPath-anbefalingene mot observert effektivitet (mest effektive
            // først). Valgfri — null ville gitt dagens bånd-baserte anbefalinger.
            effectivenessEngine: sp.GetRequiredService<ExerciseEffectivenessEngine>(),
            // Bølge 1/2: longitudinell intelligens. Alle er additive — null ⇒ dagens oppførsel.
            insightEngine: sp.GetRequiredService<LongitudinalInsightEngine>(),
            explanationEngine: sp.GetRequiredService<RecommendationExplanationEngine>(),
            memory: sp.GetRequiredService<SmartCoachMemoryStore>(),
            knowledgeGraphBuilder: sp.GetRequiredService<VoiceKnowledgeGraphBuilder>(),
            trendEngine: sp.GetRequiredService<TrendEngineService>(),
            patternDetector: sp.GetRequiredService<VoicePatternDetector>()));
        services.AddSingleton<IExerciseProfileFactory, ExerciseProfileFactory>();

        // ── Sprint E (Professional / Research Edition): persistens + motorer ───────
        // Profesjonell/forsknings-edisjon. Rene lese-/montasje-tjenester og fem SQLite-
        // backede stores mot SAMME femvoice.db som SmartCoachMemoryStore og
        // SqliteSessionAnalyticsRepository bruker. Alt er additivt: ingen av disse rører
        // safety/health/recovery-gatene — de LESER kun den persisterte historikken og
        // monterer rapporter/utfallsprofiler/case-reviews. VM-ene (Clinician/Coach/
        // Report/Override/CaseReview) selv-resolver disse via App.Services og er derfor
        // bevisst IKKE registrert her.

        // De fem stores: hver konstrueres med sin SQLite-repo mot femvoice.db, speilet
        // EKSAKT etter mønsteret over (SmartCoachMemoryStore ~l.214-223). Hver repo tar
        // en enkelt "Data Source=…"-streng; stores tar kun sin repository.
        services.AddSingleton(_ =>
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "FemVoiceStudio");
            Directory.CreateDirectory(appDataPath);
            var databasePath = Path.Combine(appDataPath, "femvoice.db");
            return new OutcomeProfileStore(
                new SqliteOutcomeProfileRepository($"Data Source={databasePath}"));
        });
        services.AddSingleton(_ =>
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "FemVoiceStudio");
            Directory.CreateDirectory(appDataPath);
            var databasePath = Path.Combine(appDataPath, "femvoice.db");
            return new ManualOverridesStore(
                new SqliteManualOverridesRepository($"Data Source={databasePath}"));
        });
        services.AddSingleton(_ =>
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "FemVoiceStudio");
            Directory.CreateDirectory(appDataPath);
            var databasePath = Path.Combine(appDataPath, "femvoice.db");
            return new ClinicalNotesStore(
                new SqliteClinicalNotesRepository($"Data Source={databasePath}"));
        });
        services.AddSingleton(_ =>
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "FemVoiceStudio");
            Directory.CreateDirectory(appDataPath);
            var databasePath = Path.Combine(appDataPath, "femvoice.db");
            return new AuditTrailStore(
                new SqliteAuditTrailRepository($"Data Source={databasePath}"));
        });
        services.AddSingleton(_ =>
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "FemVoiceStudio");
            Directory.CreateDirectory(appDataPath);
            var databasePath = Path.Combine(appDataPath, "femvoice.db");
            return new CaseReviewsStore(
                new SqliteCaseReviewsRepository($"Data Source={databasePath}"));
        });

        // Motorene / montasje-tjenestene. Per FAKTISK ctor:
        // OutcomeProfileBuilder LESER gjennom tre alt-registrerte, live engines
        // (SmartCoachEngine, ExerciseEffectivenessEngine, LongitudinalInsightEngine) —
        // den muterer aldri state, kun monterer en OutcomeProfile fra det de eksponerer.
        services.AddSingleton(sp => new OutcomeProfileBuilder(
            sp.GetRequiredService<SmartCoachEngine>(),
            sp.GetRequiredService<ExerciseEffectivenessEngine>(),
            sp.GetRequiredService<LongitudinalInsightEngine>()));
        // ManualOverrideEngine, ReportAssembler, ResearchAnonymizer, ResearchAggregator,
        // CaseReviewAssembler er rene/tilstandsløse — ingen ctor-avhengigheter. De
        // opererer udelukkende på inn-argumenter (Evaluate/Build/Anonymize/Assemble).
        services.AddSingleton<ManualOverrideEngine>();
        services.AddSingleton<ReportAssembler>();
        services.AddSingleton<ResearchAnonymizer>();
        services.AddSingleton<ResearchAggregator>();
        services.AddSingleton<CaseReviewAssembler>();
        // ExportWriter har kun en statisk ctor (setter QuestPDF Community-lisens én gang)
        // og en parameterløs instans-ctor — registreres direkte.
        services.AddSingleton<ExportWriter>();
        // ParticipantTokenProvider: begge ctor-params er valgfrie (directory == null ⇒
        // LocalApplicationData/FemVoiceStudio/Research; tokenFactory ⇒ tilfeldig UUID).
        // Produksjon bruker standardene — vi registrerer den parameterløse formen.
        services.AddSingleton(_ => new ParticipantTokenProvider());

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
