using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Theme options available in the application
    /// </summary>
    public enum AppTheme
    {
        System,  // Follow system theme
        Light,   // Always use light theme
        Dark     // Always use dark theme
    }

    /// <summary>
    /// Application settings model for JSON serialization
    /// </summary>
    public class AppSettings
    {
        public string Language { get; set; } = "nb";
        public AppTheme Theme { get; set; } = AppTheme.System;
        public bool HearOwnVoice { get; set; } = false;
        public bool FirstTimeSetupCompleted { get; set; } = false;
        public DebugSettings? Debug { get; set; }

        // Hand-edited keys this model doesn't know about must survive the
        // load-modify-save round-trips done by ThemeManager, DebugSettingsService
        // and FirstTimeSetupService.
        [System.Text.Json.Serialization.JsonExtensionData]
        public System.Collections.Generic.Dictionary<string, System.Text.Json.JsonElement>? ExtensionData { get; set; }
    }

    /// <summary>
    /// Debug settings model
    /// </summary>
    public class DebugSettings
    {
        public bool EnablePitchDebug { get; set; } = false;
        public bool EnableAnalyzerDebug { get; set; } = false;
        public bool EnableRc0Diagnostics { get; set; } = false;

        [System.Text.Json.Serialization.JsonExtensionData]
        public System.Collections.Generic.Dictionary<string, System.Text.Json.JsonElement>? ExtensionData { get; set; }
    }

    /// <summary>
    /// Shared serializer options for settings.json. All three settings writers
    /// (ThemeManager, DebugSettingsService, FirstTimeSetupService) must use the same
    /// options so a hand-edited file (e.g. "Theme": "Dark" as a string) never fails
    /// deserialization and gets reset to defaults.
    /// </summary>
    public static class AppSettingsJson
    {
        public static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            // Håndredigert fil er den dokumenterte måten å skru på debug-flaggene —
            // feil casing skal ikke gjøre et flagg stille false.
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };
    }

    /// <summary>
    /// ThemeManager - Handles theme loading, switching, and persistence.
    /// Implements ISettingsService for dependency injection while keeping singleton for backward compatibility.
    /// 
    /// Design decisions:
    /// - Uses MergedDictionaries to swap theme resources at runtime without restart
    /// - Stores settings in %USERPROFILE%\Documents\FemVoiceStudio\settings.json
    /// - Implements INotifyPropertyChanged for MVVM compatibility
    /// - Supports runtime theme switching for open windows
    /// </summary>
    public class ThemeManager : ISettingsService, INotifyPropertyChanged
    {
        private static ThemeManager? _instance;
        private static readonly object _lock = new();
        
        private AppTheme _currentThemeMode = AppTheme.System;
        private bool _isDarkTheme = false;
        private bool _isFirstTime = true;
        private AppSettings _settings = new();
        
        // Path to settings file in Documents\FemVoiceStudio folder
        private static readonly string AppFolderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "FemVoiceStudio");
        
        private static readonly string SettingsFilePath = Path.Combine(
            AppFolderPath, "settings.json");

        /// <summary>
        /// Singleton instance for backward compatibility
        /// </summary>
        public static ThemeManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new ThemeManager();
                    }
                }
                return _instance;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? ThemeChanged;

        /// <summary>
        /// ISettingsService.IsFirstTime property
        /// </summary>
        public bool IsFirstTime => _isFirstTime;

        /// <summary>
        /// Current theme mode (System, Light, or Dark)
        /// </summary>
        public AppTheme CurrentThemeMode
        {
            get => _currentThemeMode;
            set
            {
                if (_currentThemeMode != value)
                {
                    _currentThemeMode = value;
                    OnPropertyChanged(nameof(CurrentThemeMode));
                    ApplyTheme();
                    SaveSettings();
                }
            }
        }

        /// <summary>
        /// Is dark theme currently active
        /// </summary>
        public bool IsDarkTheme
        {
            get => _isDarkTheme;
            private set
            {
                if (_isDarkTheme != value)
                {
                    _isDarkTheme = value;
                    OnPropertyChanged(nameof(IsDarkTheme));
                }
            }
        }

        /// <summary>
        /// Private constructor for singleton
        /// </summary>
        private ThemeManager()
        {
            // Ensure the FemVoiceStudio folder exists
            EnsureAppFolderExists();
            
            // Load settings
            _settings = LoadSettings();
            _currentThemeMode = _settings.Theme;
            _isFirstTime = !_settings.FirstTimeSetupCompleted;
        }

        /// <summary>
        /// Initialize theme at application startup
        /// Loads settings and applies appropriate theme
        /// </summary>
        public void Initialize()
        {
            // Apply theme based on loaded settings
            ApplyTheme();
        }

        /// <summary>
        /// Apply theme based on current theme mode
        /// Handles system theme detection if mode is System
        /// </summary>
        private void ApplyTheme()
        {
            bool useDarkTheme = _currentThemeMode switch
            {
                AppTheme.Light => false,
                AppTheme.Dark => true,
                AppTheme.System => !IsSystemLightTheme(),
                _ => false
            };

            IsDarkTheme = useDarkTheme;
            LoadThemeResourceDictionary(useDarkTheme);
            
            // Notify windows to update their title bar
            ThemeChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Load the appropriate theme ResourceDictionary into Application.Resources
        /// Replaces existing theme dictionary while preserving other resources
        /// </summary>
        private void LoadThemeResourceDictionary(bool isDark)
        {
            var app = Application.Current;
            if (app == null) return;

            // Find and remove existing theme dictionary
            ResourceDictionary? existingTheme = null;
            foreach (var dict in app.Resources.MergedDictionaries)
            {
                if (dict.Source != null && 
                    (dict.Source.OriginalString.Contains("LightTheme") || 
                     dict.Source.OriginalString.Contains("DarkTheme")))
                {
                    existingTheme = dict;
                    break;
                }
            }

            // Remove existing theme if found
            if (existingTheme != null)
            {
                app.Resources.MergedDictionaries.Remove(existingTheme);
            }

            // Load new theme
            var themeUri = isDark 
                ? new Uri("pack://application:,,,/Themes/DarkTheme.xaml", UriKind.Absolute)
                : new Uri("pack://application:,,,/Themes/LightTheme.xaml", UriKind.Absolute);

            var newTheme = new ResourceDictionary { Source = themeUri };
            app.Resources.MergedDictionaries.Add(newTheme);
        }

        /// <summary>
        /// Switch to a specific theme immediately
        /// Called when user changes theme from settings
        /// </summary>
        public void SwitchTheme(AppTheme mode)
        {
            CurrentThemeMode = mode;
        }

        /// <summary>
        /// Toggle between light and dark theme
        /// </summary>
        public void ToggleTheme()
        {
            CurrentThemeMode = IsDarkTheme ? AppTheme.Light : AppTheme.Dark;
        }

        /// <summary>
        /// Detect Windows system theme using Registry
        /// Returns true if system is using light theme, false for dark theme
        /// </summary>
        public static bool IsSystemLightTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                
                var value = key?.GetValue("AppsUseLightTheme");
                
                if (value is int intValue)
                {
                    return intValue > 0;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading system theme: {ex.Message}");
                return true;
            }
        }

        /// <summary>
        /// Ensure the FemVoiceStudio folder exists in Documents
        /// </summary>
        private static void EnsureAppFolderExists()
        {
            try
            {
                if (!Directory.Exists(AppFolderPath))
                {
                    Directory.CreateDirectory(AppFolderPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating app folder: {ex.Message}");
            }
        }

        /// <summary>
        /// Load application settings from JSON file
        /// Returns default settings if file doesn't exist or is corrupted
        /// </summary>
        public AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json, AppSettingsJson.Options);
                    _settings = settings ?? new AppSettings();
                    return _settings;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
                Rc0WriteFailureSink.Report("ThemeManager.LoadSettings", SettingsFilePath, ex);
            }
            
            _settings = new AppSettings();
            return _settings;
        }

        /// <summary>
        /// Save application settings to JSON file
        /// Settings are persisted to %USERPROFILE%\Documents\FemVoiceStudio\settings.json
        /// Preserves Debug section if it exists
        /// </summary>
        public void SaveSettings()
        {
            SaveSettings(LoadSettings());
        }
        
        /// <summary>
        /// Save application settings to JSON file (ISettingsService implementation)
        /// </summary>
        public void SaveSettings(AppSettings settings)
        {
            try
            {
                EnsureAppFolderExists();
                
                // Update only ThemeManager-related fields
                try
                {
                    settings.Language = LocalizationService.Instance.CurrentLanguage;
                }
                catch
                {
                    settings.Language = "nb";
                }
                settings.Theme = _currentThemeMode;
                settings.HearOwnVoice = GetHearOwnVoiceSetting();

                var json = JsonSerializer.Serialize(settings, AppSettingsJson.Options);

                File.WriteAllText(SettingsFilePath, json);
                _settings = settings;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
                Rc0WriteFailureSink.Report("ThemeManager.SaveSettings", SettingsFilePath, ex);
            }
        }

        /// <summary>
        /// Get HearOwnVoice setting
        /// </summary>
        private bool GetHearOwnVoiceSetting()
        {
            return _settings.HearOwnVoice;
        }

        /// <summary>
        /// Load language preference from settings
        /// </summary>
        public string LoadLanguagePreference()
        {
            return _settings.Language;
        }

        /// <summary>
        /// Get the path to the settings file
        /// </summary>
        public static string SettingsPath => SettingsFilePath;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        #region ISettingsService Implementation
        
        /// <summary>
        /// Mark first-time setup as completed
        /// </summary>
        public void MarkSetupCompleted()
        {
            _settings.FirstTimeSetupCompleted = true;
            _isFirstTime = false;
            SaveSettings();
        }
        
        /// <summary>
        /// Reset first-time status (for testing)
        /// </summary>
        public void ResetFirstTimeStatus()
        {
            _settings.FirstTimeSetupCompleted = false;
            _isFirstTime = true;
            SaveSettings();
        }
        
        #endregion
    }
}
