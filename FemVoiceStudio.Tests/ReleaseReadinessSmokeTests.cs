using System.Reflection;
using FemVoiceStudio;
using FemVoiceStudio.Data;
using FemVoiceStudio.Services;
using FemVoiceStudio.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace FemVoiceStudio.Tests
{
    public class ReleaseReadinessSmokeTests
    {
        [Fact]
        public void AppDependencyInjection_ResolvesCoreRuntimeServices()
        {
            var provider = BuildAppServiceProvider();

            Assert.NotNull(provider.GetRequiredService<IDatabaseService>());
            Assert.NotNull(provider.GetRequiredService<ILocalizationService>());
            Assert.NotNull(provider.GetRequiredService<SessionAnalyticsStore>());
            Assert.NotNull(provider.GetRequiredService<ProgressionOrchestrator>());
            Assert.NotNull(provider.GetRequiredService<VocalHealthSupervisor>());
            Assert.NotNull(provider.GetRequiredService<HydrationAdvisor>());
            Assert.NotNull(provider.GetRequiredService<SmartCoachEngine>());
            Assert.NotNull(provider.GetRequiredService<ExerciseIntelligenceCoordinator>());
        }

        [Fact]
        public void AppDependencyInjection_ResolvesRuntimeViewModels()
        {
            var provider = BuildAppServiceProvider();

            using var exerciseDetail = provider.GetRequiredService<ExerciseDetailViewModel>();
            var smartCoach = provider.GetRequiredService<SmartCoachViewModel>();

            Assert.NotNull(exerciseDetail);
            Assert.NotNull(smartCoach);
        }

        private static ServiceProvider BuildAppServiceProvider()
        {
            var services = new ServiceCollection();
            var configureServices = typeof(App).GetMethod(
                "ConfigureServices",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.NotNull(configureServices);
            configureServices!.Invoke(null, new object[] { services });
            ReplaceUserDatabaseWithTemporaryTestDatabase(services);

            return services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
        }

        private static void ReplaceUserDatabaseWithTemporaryTestDatabase(IServiceCollection services)
        {
            var databasePath = Path.Combine(
                Path.GetTempPath(),
                "FemVoiceStudio.Tests",
                $"{Guid.NewGuid():N}.db");

            Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

            services.RemoveAll<DatabaseService>();
            services.RemoveAll<IDatabaseService>();
            services.RemoveAll<ISessionAnalyticsRepository>();
            services.RemoveAll<IExerciseProfileStore>();
            services.RemoveAll<ExerciseDataService>();
            services.RemoveAll<SmartCoachMemoryStore>();
            services.RemoveAll<OutcomeProfileStore>();
            services.RemoveAll<ManualOverridesStore>();
            services.RemoveAll<ClinicalNotesStore>();
            services.RemoveAll<AuditTrailStore>();
            services.RemoveAll<CaseReviewsStore>();

            var connectionString = $"Data Source={databasePath}";

            services.AddSingleton(_ => new DatabaseService(databasePath));
            services.AddSingleton<IDatabaseService>(sp => sp.GetRequiredService<DatabaseService>());
            services.AddSingleton<ISessionAnalyticsRepository>(_ => new SqliteSessionAnalyticsRepository(connectionString));
            services.AddSingleton<IExerciseProfileStore>(_ => new SqliteExerciseProfileStore(connectionString));
            services.AddSingleton(_ => new ExerciseDataService(connectionString));
            services.AddSingleton(_ => new SmartCoachMemoryStore(new SqliteSmartCoachMemoryRepository(connectionString)));
            services.AddSingleton(_ => new OutcomeProfileStore(new SqliteOutcomeProfileRepository(connectionString)));
            services.AddSingleton(_ => new ManualOverridesStore(new SqliteManualOverridesRepository(connectionString)));
            services.AddSingleton(_ => new ClinicalNotesStore(new SqliteClinicalNotesRepository(connectionString)));
            services.AddSingleton(_ => new AuditTrailStore(new SqliteAuditTrailRepository(connectionString)));
            services.AddSingleton(_ => new CaseReviewsStore(new SqliteCaseReviewsRepository(connectionString)));
        }
    }
}
