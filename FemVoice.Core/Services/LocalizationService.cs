using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.IO;

namespace FemVoiceStudio.Services;

/// <summary>
/// Service for handling localization/language changes.
/// Implements ILocalizationService for dependency injection.
/// </summary>
public class LocalizationService : ILocalizationService
{
    private static LocalizationService? _instance;
    private static readonly object _lock = new();
    private readonly ISettingsService? _settingsService;
    private ResourceManager _resourceManager;
    private CultureInfo _currentCulture;
    
    /// <summary>
    /// Static instance for backward compatibility (legacy code)
    /// </summary>
    public static LocalizationService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new LocalizationService();
                }
            }
            return _instance;
        }
    }
    
    /// <summary>
    /// Event for property changes (for WPF binding)
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;
    
    /// <summary>
    /// Default constructor for production use (reads from file system)
    /// </summary>
    public LocalizationService() : this(null)
    {
    }
    
    /// <summary>
    /// Constructor with optional settings service for dependency injection
    /// </summary>
    public LocalizationService(ISettingsService? settingsService)
    {
        _settingsService = settingsService;
        _resourceManager = new ResourceManager("FemVoiceStudio.Resources.Strings", typeof(LocalizationService).Assembly);
        _currentCulture = CultureInfo.CurrentUICulture;
        
        // Load saved language preference
        LoadLanguagePreference();
    }
    
    public CultureInfo CurrentCulture
    {
        get => _currentCulture;
        private set
        {
            if (_currentCulture != value)
            {
                _currentCulture = value;
                OnPropertyChanged(nameof(CurrentCulture));
                OnPropertyChanged("Item[]");
            }
        }
    }
    
    /// <summary>
    /// Current language code (e.g., "nb", "en")
    /// </summary>
    public string CurrentLanguage => _currentCulture.TwoLetterISOLanguageName;
    
    /// <summary>
    /// Get localized string by key (indexer syntax)
    /// </summary>
    public string this[string key]
    {
        get
        {
            try
            {
                return _resourceManager.GetString(key, _currentCulture) ?? key;
            }
            catch
            {
                return key;
            }
        }
    }
    
    /// <summary>
    /// Get localized string (method syntax)
    /// </summary>
    public string GetString(string key)
    {
        return this[key];
    }
    
    /// <summary>
    /// Get localized string with format arguments
    /// </summary>
    public string GetFormattedString(string key, params object[] args)
    {
        var format = this[key];
        try
        {
            return string.Format(format, args);
        }
        catch
        {
            return format;
        }
    }
    
    /// <summary>
    /// Set the application language
    /// </summary>
    public void SetLanguage(string languageCode)
    {
        try
        {
            var culture = new CultureInfo(languageCode);
            Thread.CurrentThread.CurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;
            CurrentCulture = culture;
            
            // Save preference
            SaveLanguagePreference(languageCode);
            
            // Notify all bindings to refresh
            OnPropertyChanged("Item[]");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error setting language: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Check if current language is Norwegian
    /// </summary>
    public bool IsNorwegian => _currentCulture.TwoLetterISOLanguageName == "no";
    
    /// <summary>
    /// Check if current language is English
    /// </summary>
    public bool IsEnglish => _currentCulture.TwoLetterISOLanguageName == "en";
    
    private void LoadLanguagePreference()
    {
        try
        {
            var settingsPath = GetSettingsPath();
            if (File.Exists(settingsPath))
            {
                var language = File.ReadAllText(settingsPath, System.Text.Encoding.UTF8).Trim();
                if (!string.IsNullOrEmpty(language))
                {
                    SetLanguage(language);
                    return;
                }
            }
            
            // Default to Norwegian
            SetLanguage("nb");
        }
        catch
        {
            SetLanguage("nb");
        }
    }
    
    private void SaveLanguagePreference(string languageCode)
    {
        try
        {
            var settingsPath = GetSettingsPath();
            var directory = Path.GetDirectoryName(settingsPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(settingsPath, languageCode, System.Text.Encoding.UTF8);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving language preference: {ex.Message}");
        }
    }
    
    private string GetSettingsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "FemVoiceStudio", "language.txt");
    }
    
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
