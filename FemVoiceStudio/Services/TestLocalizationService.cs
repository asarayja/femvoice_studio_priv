using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// In-memory test implementation of ILocalizationService.
    /// Returns mock strings for unit testing without file dependencies.
    /// </summary>
    public class TestLocalizationService : ILocalizationService
    {
        private readonly Dictionary<string, string> _strings = new();
        private string _currentLanguage = "nb";
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        public TestLocalizationService()
        {
            // Add common test strings
            AddDefaultStrings();
        }
        
        private void AddDefaultStrings()
        {
            // Progression strings
            _strings["Progression_LevelUp"] = "Gratulerer! Du har nådd nivå {0} etter {1} økter med score over {2}%";
            _strings["Progression_SessionsToNext"] = "Du har hatt {0} av {1} økter for å gå videre";
            _strings["Progression_FirstSession"] = "Velkommen til din første økt!";
            _strings["Progression_TenSessions"] = "10 økter fullført!";
            _strings["Progression_FiftySessions"] = "50 økter fullført!";
            _strings["Progression_ExcellentScore"] = "Fantastisk score!";
            _strings["Progression_TotalSessions"] = "Totalt {0} økter";
            
            // SmartCoach strings
            _strings["SmartCoach_Health_PitchPress"] = "Unngå å presse stemmen. Fokus på avslapping.";
            _strings["SmartCoach_Health_Noise"] = "Reduser stemmestøy. Prøv mykere stemme.";
            _strings["SmartCoach_Health_Fatigue"] = "Ta en pause. Stemmen trenger hvile.";
            _strings["SmartCoach_Health_Default"] = "Ta det rolig og pust dypt.";
            _strings["SmartCoach_HealthWarning"] = "Helseadvarsel";
            
            // Coach messages
            _strings["Coach_WeeklyAverage"] = "Ukens gjennomsnitt: {0}%";
            _strings["Coach_SessionsThisWeek"] = "Du har hatt {0} økter denne uken!";
            _strings["Coach_QualityOverQuantity"] = "Kvalitet er viktigere enn kvantitet.";
        }
        
        /// <summary>
        /// Add a custom string for testing
        /// </summary>
        public void AddString(string key, string value)
        {
            _strings[key] = value;
        }
        
        /// <summary>
        /// Clear all strings (for testing)
        /// </summary>
        public void ClearStrings()
        {
            _strings.Clear();
        }
        
        public string CurrentLanguage
        {
            get => _currentLanguage;
            private set
            {
                if (_currentLanguage != value)
                {
                    _currentLanguage = value;
                    OnPropertyChanged(nameof(CurrentLanguage));
                }
            }
        }
        
        /// <summary>
        /// Indexer for getting localized string by key
        /// </summary>
        public string this[string key] => GetString(key);
        
        public string GetString(string key)
        {
            if (_strings.TryGetValue(key, out var value))
            {
                return value;
            }
            // Return mock format for unknown keys
            return $"[{key}]";
        }
        
        public string GetFormattedString(string key, params object[] args)
        {
            var format = GetString(key);
            try
            {
                return string.Format(format, args);
            }
            catch
            {
                return format;
            }
        }
        
        public void SetLanguage(string languageCode)
        {
            CurrentLanguage = languageCode;
        }
        
        public bool IsNorwegian => _currentLanguage == "nb" || _currentLanguage == "no";
        
        public bool IsEnglish => _currentLanguage == "en";
        
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
