namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Interface for settings service.
    /// Manages first-time setup state and application settings.
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        /// Check if this is the first time the app is run
        /// </summary>
        bool IsFirstTime { get; }
        
        /// <summary>
        /// Mark first-time setup as completed
        /// </summary>
        void MarkSetupCompleted();
        
        /// <summary>
        /// Reset first-time status (for testing)
        /// </summary>
        void ResetFirstTimeStatus();
        
        /// <summary>
        /// Load application settings
        /// </summary>
        AppSettings LoadSettings();
        
        /// <summary>
        /// Save application settings
        /// </summary>
        void SaveSettings(AppSettings settings);
    }
}
