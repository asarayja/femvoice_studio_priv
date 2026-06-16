namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Health status indicator for voice analysis in real-time feedback
    /// </summary>
    public enum HealthStatus
    {
        /// <summary>Voice is within safe parameters</summary>
        Good,
        
        /// <summary>Warning - some parameters are elevated</summary>
        Warning,
        
        /// <summary>Critical - immediate stop recommended</summary>
        Critical
    }
}
