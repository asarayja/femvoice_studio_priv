using System.ComponentModel;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Interface for localization/language services.
    /// Allows for test implementations with mock strings.
    /// </summary>
    public interface ILocalizationService : INotifyPropertyChanged
    {
        /// <summary>
        /// Current language code (e.g., "nb", "en")
        /// </summary>
        string CurrentLanguage { get; }
        
        /// <summary>
        /// Indexer for getting localized string by key
        /// </summary>
        string this[string key] { get; }
        
        /// <summary>
        /// Get localized string by key
        /// </summary>
        string GetString(string key);
        
        /// <summary>
        /// Get localized string with format arguments
        /// </summary>
        string GetFormattedString(string key, params object[] args);
        
        /// <summary>
        /// Set the application language
        /// </summary>
        void SetLanguage(string languageCode);
        
        /// <summary>
        /// Check if current language is Norwegian
        /// </summary>
        bool IsNorwegian { get; }
        
        /// <summary>
        /// Check if current language is English
        /// </summary>
        bool IsEnglish { get; }
    }
}
