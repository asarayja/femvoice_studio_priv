using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Service for managing debug settings in settings.json.
    /// Implements IDebugSettingsRepository and IDebugLogger for dependency injection.
    /// </summary>
    public class DebugSettingsService : IDebugSettingsRepository, IDebugLogger
    {
        private static DebugSettingsService? _instance;
        private static readonly object _lock = new object();
        
        public bool EnablePitchDebug { get; set; } = false;
        public bool EnableAnalyzerDebug { get; set; } = false;
        public bool EnableRc0Diagnostics { get; set; } = false;
        
        private readonly string _settingsPath;
        private readonly string _logsPath;
        private StreamWriter? _pitchLogWriter;
        private StreamWriter? _analyzerLogWriter;
        
        /// <summary>
        /// Static instance for backward compatibility
        /// </summary>
        public static DebugSettingsService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new DebugSettingsService();
                    }
                }
                return _instance;
            }
        }
        
        private DebugSettingsService()
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "FemVoiceStudio");

            _settingsPath = Path.Combine(appDataPath, "settings.json");
            _logsPath = Path.Combine(appDataPath, "logs");

            // Ctor må aldri kaste: Instance leses bl.a. fra audio-callbacks og
            // evidence-gating — en kastende singleton-ctor ville propagere dit.
            try
            {
                if (!Directory.Exists(appDataPath))
                    Directory.CreateDirectory(appDataPath);

                if (!Directory.Exists(_logsPath))
                    Directory.CreateDirectory(_logsPath);

                LoadSettings();
            }
            catch (Exception ex)
            {
                Rc0WriteFailureSink.Report("DebugSettingsService.ctor", _settingsPath, ex);
            }
        }
        
        /// <summary>
        /// Load settings from JSON file
        /// </summary>
        public void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var settings = SettingsMigrationService
                        .LoadOrRecover(_settingsPath, "DebugSettingsService.LoadSettings")
                        .Settings;

                    if (settings?.Debug == null)
                    {
                        // Add missing Debug section to existing settings
                        SaveSettings();
                    }
                    else
                    {
                        EnablePitchDebug = settings.Debug.EnablePitchDebug;
                        EnableAnalyzerDebug = settings.Debug.EnableAnalyzerDebug;
                        EnableRc0Diagnostics = settings.Debug.EnableRc0Diagnostics;
                    }
                }
                else
                {
                    // Create default settings file
                    SaveSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading debug settings: {ex.Message}");
                Rc0WriteFailureSink.Report("DebugSettingsService.LoadSettings", _settingsPath, ex);
                // Create default settings if loading fails
                SaveSettings();
            }
        }
        
        /// <summary>
        /// Save settings to JSON file (merges with existing settings)
        /// </summary>
        public void SaveSettings()
        {
            try
            {
                // Load existing settings first to preserve other sections
                var existingSettings = LoadExistingSettings();

                // Merge debug settings (mutate the existing object so unknown
                // hand-added keys in the Debug section survive via ExtensionData)
                existingSettings.Debug ??= new DebugSettings();
                existingSettings.Debug.EnablePitchDebug = EnablePitchDebug;
                existingSettings.Debug.EnableAnalyzerDebug = EnableAnalyzerDebug;
                existingSettings.Debug.EnableRc0Diagnostics = EnableRc0Diagnostics;

                SettingsMigrationService.Save(_settingsPath, existingSettings, "DebugSettingsService.SaveSettings");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving debug settings: {ex.Message}");
                Rc0WriteFailureSink.Report("DebugSettingsService.SaveSettings", _settingsPath, ex);
            }
        }
        
        /// <summary>
        /// Load existing settings without modifying Debug section
        /// </summary>
        private AppSettings LoadExistingSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    return SettingsMigrationService
                        .LoadOrRecover(_settingsPath, "DebugSettingsService.LoadExistingSettings")
                        .Settings;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading existing settings: {ex.Message}");
                Rc0WriteFailureSink.Report("DebugSettingsService.LoadExistingSettings", _settingsPath, ex);
            }
            
            return new AppSettings();
        }
        
        /// <summary>
        /// Ensure Debug section exists in settings file (call on initialization)
        /// </summary>
        public void EnsureDebugSection()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var settings = SettingsMigrationService
                        .LoadOrRecover(_settingsPath, "DebugSettingsService.EnsureDebugSection")
                        .Settings;

                    if (settings?.Debug == null)
                    {
                        // Add missing Debug section (with default false values)
                        if (settings == null)
                            settings = new AppSettings();

                        settings.Debug = new DebugSettings();

                        SettingsMigrationService.Save(_settingsPath, settings, "DebugSettingsService.EnsureDebugSection");
                    }
                    else
                    {
                        // Debug section exists - preserve existing values
                        EnablePitchDebug = settings.Debug.EnablePitchDebug;
                        EnableAnalyzerDebug = settings.Debug.EnableAnalyzerDebug;
                        EnableRc0Diagnostics = settings.Debug.EnableRc0Diagnostics;
                    }
                }
                else
                {
                    // Create new settings file
                    SaveSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error ensuring debug section: {ex.Message}");
                Rc0WriteFailureSink.Report("DebugSettingsService.EnsureDebugSection", _settingsPath, ex);
                SaveSettings();
            }
        }
        
        /// <summary>
        /// Log pitch data to file (call this during recording)
        /// </summary>
        public void LogPitchData(double pitch, double rms, bool isVoiced, double confidence, double targetMinPitch, double targetMaxPitch)
        {
            if (!EnablePitchDebug) return;
            
            try
            {
                if (_pitchLogWriter == null)
                {
                    var logFile = Path.Combine(_logsPath, $"pitch_debug_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                    _pitchLogWriter = new StreamWriter(logFile, false, Encoding.UTF8);
                    _pitchLogWriter.WriteLine("Timestamp,Pitch,RMS,IsVoiced,Confidence,TargetMin,TargetMax,InRange");
                }
                
                bool inRange = pitch >= targetMinPitch && pitch <= targetMaxPitch;
                _pitchLogWriter.WriteLine($"{DateTime.Now:HH:mm:ss.fff},{pitch:F1},{rms:F4},{isVoiced},{confidence:F2},{targetMinPitch:F0},{targetMaxPitch:F0},{inRange}");
                _pitchLogWriter.Flush();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error logging pitch data: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Log analyzer data to file
        /// </summary>
        public void LogAnalyzerData(string analysisType, double value, string result)
        {
            if (!EnableAnalyzerDebug) return;
            
            try
            {
                if (_analyzerLogWriter == null)
                {
                    var logFile = Path.Combine(_logsPath, $"analyzer_debug_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                    _analyzerLogWriter = new StreamWriter(logFile, false, Encoding.UTF8);
                    _analyzerLogWriter.WriteLine("Timestamp,AnalysisType,Value,Result");
                }
                
                _analyzerLogWriter.WriteLine($"{DateTime.Now:HH:mm:ss.fff},{analysisType},{value:F2},{result}");
                _analyzerLogWriter.Flush();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error logging analyzer data: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Close log files
        /// </summary>
        public void CloseLogs()
        {
            _pitchLogWriter?.Close();
            _pitchLogWriter = null;
            _analyzerLogWriter?.Close();
            _analyzerLogWriter = null;
        }
        
        /// <summary>
        /// Reload settings from disk
        /// </summary>
        public void Reload()
        {
            LoadSettings();
        }
    }
}
