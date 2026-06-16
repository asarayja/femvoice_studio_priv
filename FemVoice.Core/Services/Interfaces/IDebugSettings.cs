namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Interface for debug settings persistence.
    /// </summary>
    public interface IDebugSettingsRepository
    {
        /// <summary>
        /// Enable pitch debug logging
        /// </summary>
        bool EnablePitchDebug { get; set; }
        
        /// <summary>
        /// Enable analyzer debug logging
        /// </summary>
        bool EnableAnalyzerDebug { get; set; }
        
        /// <summary>
        /// Load settings from storage
        /// </summary>
        void LoadSettings();
        
        /// <summary>
        /// Save settings to storage
        /// </summary>
        void SaveSettings();
    }
    
    /// <summary>
    /// Interface for debug logging.
    /// </summary>
    public interface IDebugLogger
    {
        /// <summary>
        /// Log pitch data during recording
        /// </summary>
        void LogPitchData(double pitch, double rms, bool isVoiced, double confidence, double targetMinPitch, double targetMaxPitch);
        
        /// <summary>
        /// Log analyzer data
        /// </summary>
        void LogAnalyzerData(string analysisType, double value, string result);
        
        /// <summary>
        /// Close log files
        /// </summary>
        void CloseLogs();
    }
}
