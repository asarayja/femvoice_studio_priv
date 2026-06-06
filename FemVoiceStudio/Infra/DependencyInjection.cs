using System;
using Microsoft.Extensions.DependencyInjection;
using FemVoiceStudio.Subsystems.Audio;
using FemVoiceStudio.Subsystems.Data;
using FemVoiceStudio.Subsystems.Progression;
using FemVoiceStudio.Subsystems.SmartCoach;
using FemVoiceStudio.Subsystems.Analysis;
using FemVoiceStudio.Services;
using FemVoiceStudio.Services.Progression;

namespace FemVoiceStudio.Infra
{
    /// <summary>
    /// Dependency Injection container setup for FemVoice Studio
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Add all FemVoice Studio subsystems to the service collection
        /// </summary>
        public static IServiceCollection AddFemVoiceStudio(this IServiceCollection services)
        {
            // Register existing services
            services.AddSingleton<LocalizationService>();
            services.AddSingleton<ThemeManager>();
            
            // Register Progression Engines
            services.AddSingleton<ProgressionEngine>();
            services.AddSingleton<WeeklyPlannerEngine>();
            
            // Register Data Subsystem (singleton - wraps DatabaseService)
            services.AddSingleton<IDataSubsystem, DataSubsystem>();
            
            // Register Progression Subsystem
            services.AddSingleton<IProgressionSubsystem, ProgressionSubsystem>();
            
            // Register Audio Subsystem (singleton for microphone state)
            services.AddSingleton<IAudioSubsystem, AudioSubsystem>();
            
            // Register Analysis Subsystem
            services.AddTransient<IAnalysisSubsystem, AnalysisSubsystem>();
            
            // Register SmartCoach Subsystem
            services.AddSingleton<ISmartCoachSubsystem, SmartCoachSubsystem>();
            
            // Register Exercise Intelligence Coordinator (singleton - maintains state across sessions)
            services.AddSingleton<ExerciseIntelligenceCoordinator>();
            
            return services;
        }
    }

    /// <summary>
    /// Service provider extensions for accessing subsystems
    /// </summary>
    public static class ServiceProviderExtensions
    {
        /// <summary>
        /// Get the Data Subsystem
        /// </summary>
        public static IDataSubsystem GetDataSubsystem(this IServiceProvider provider)
        {
            return provider.GetRequiredService<IDataSubsystem>();
        }

        /// <summary>
        /// Get the Progression Subsystem
        /// </summary>
        public static IProgressionSubsystem GetProgressionSubsystem(this IServiceProvider provider)
        {
            return provider.GetRequiredService<IProgressionSubsystem>();
        }

        /// <summary>
        /// Get the Audio Subsystem
        /// </summary>
        public static IAudioSubsystem GetAudioSubsystem(this IServiceProvider provider)
        {
            return provider.GetRequiredService<IAudioSubsystem>();
        }

        /// <summary>
        /// Get the Analysis Subsystem
        /// </summary>
        public static IAnalysisSubsystem GetAnalysisSubsystem(this IServiceProvider provider)
        {
            return provider.GetRequiredService<IAnalysisSubsystem>();
        }

        /// <summary>
        /// Get the SmartCoach Subsystem
        /// </summary>
        public static ISmartCoachSubsystem GetSmartCoachSubsystem(this IServiceProvider provider)
        {
            return provider.GetRequiredService<ISmartCoachSubsystem>();
        }
    }

    /// <summary>
    /// Factory for creating scoped analysis services
    /// </summary>
    public class AnalysisSubsystemFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public AnalysisSubsystemFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Create a new Analysis Subsystem instance (for scoped use)
        /// </summary>
        public IAnalysisSubsystem Create()
        {
            return new AnalysisSubsystem();
        }
    }
}
