using System;
using System.IO;
using System.Text.Json;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Service to manage first-time setup state.
    /// Implements ISettingsService for dependency injection.
    /// </summary>
    public class FirstTimeSetupService : ISettingsService
    {
        private static FirstTimeSetupService? _instance;
        private static readonly object _lock = new();
        
        private bool _isFirstTime = true;
        private AppSettings _settings = new();
        
        // Path to settings file
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "FemVoiceStudio", "settings.json");

        /// <summary>
        /// Static instance for backward compatibility (legacy code)
        /// </summary>
        public static FirstTimeSetupService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new FirstTimeSetupService();
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        public FirstTimeSetupService()
        {
            CheckFirstTimeStatus();
        }

        /// <summary>
        /// Check if this is the first time the app is run
        /// </summary>
        public bool IsFirstTime
        {
            get => _isFirstTime;
            private set => _isFirstTime = value;
        }

        /// <summary>
        /// Check first-time status by looking for settings file
        /// </summary>
        private void CheckFirstTimeStatus()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var settings = SettingsMigrationService
                        .LoadOrRecover(SettingsFilePath, "FirstTimeSetupService.CheckFirstTimeStatus")
                        .Settings;

                    // Check if first-time setup was completed
                    if (settings != null)
                    {
                        _settings = settings;
                        _isFirstTime = !settings.FirstTimeSetupCompleted;
                    }
                    else
                    {
                        _isFirstTime = true;
                    }
                }
                else
                {
                    _isFirstTime = true;
                }
            }
            catch
            {
                _isFirstTime = true;
            }
        }

        /// <summary>
        /// Mark first-time setup as completed
        /// </summary>
        public void MarkSetupCompleted()
        {
            try
            {
                AppSettings settings;
                
                if (File.Exists(SettingsFilePath))
                {
                    settings = SettingsMigrationService
                        .LoadOrRecover(SettingsFilePath, "FirstTimeSetupService.MarkSetupCompleted")
                        .Settings;
                }
                else
                {
                    settings = new AppSettings();
                }

                settings.FirstTimeSetupCompleted = true;

                SettingsMigrationService.Save(SettingsFilePath, settings, "FirstTimeSetupService.MarkSetupCompleted");
                _isFirstTime = false;
                _settings = settings;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error marking setup completed: {ex.Message}");
                Rc0WriteFailureSink.Report("FirstTimeSetupService.MarkSetupCompleted", SettingsFilePath, ex);
            }
        }

        /// <summary>
        /// Reset first-time status (for testing)
        /// </summary>
        public void ResetFirstTimeStatus()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var settings = SettingsMigrationService
                        .LoadOrRecover(SettingsFilePath, "FirstTimeSetupService.ResetFirstTimeStatus")
                        .Settings;
                    settings.FirstTimeSetupCompleted = false;

                    SettingsMigrationService.Save(SettingsFilePath, settings, "FirstTimeSetupService.ResetFirstTimeStatus");
                    _settings = settings;
                }
                _isFirstTime = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error resetting first-time status: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Load application settings
        /// </summary>
        public AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    _settings = SettingsMigrationService
                        .LoadOrRecover(SettingsFilePath, "FirstTimeSetupService.LoadSettings")
                        .Settings;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
            }
            return _settings;
        }
        
        /// <summary>
        /// Save application settings
        /// </summary>
        public void SaveSettings(AppSettings settings)
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                SettingsMigrationService.Save(SettingsFilePath, settings, "FirstTimeSetupService.SaveSettings");
                _settings = settings;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
                Rc0WriteFailureSink.Report("FirstTimeSetupService.SaveSettings", SettingsFilePath, ex);
            }
        }
    }
}
