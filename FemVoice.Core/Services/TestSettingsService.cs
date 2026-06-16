using System;
using System.IO;
using System.Text.Json;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// In-memory test implementation of ISettingsService.
    /// No file system dependencies for unit testing.
    /// </summary>
    public class TestSettingsService : ISettingsService
    {
        private bool _isFirstTime = true;
        private AppSettings _settings = new();
        
        public bool IsFirstTime
        {
            get => _isFirstTime;
            private set => _isFirstTime = value;
        }
        
        public void MarkSetupCompleted()
        {
            _isFirstTime = false;
            _settings.FirstTimeSetupCompleted = true;
        }
        
        public void ResetFirstTimeStatus()
        {
            _isFirstTime = true;
            _settings.FirstTimeSetupCompleted = false;
        }
        
        public AppSettings LoadSettings()
        {
            return _settings;
        }
        
        public void SaveSettings(AppSettings settings)
        {
            _settings = settings;
        }
    }
}
