using System;
using System.Timers;
using Timer = System.Timers.Timer;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Håndterer stemmehelse: pauser og treningsgrenser
    /// </summary>
    public class VoiceHealthService : IDisposable
    {
        private readonly Timer _sessionTimer;
        private readonly Timer _breakTimer;
        private readonly ILocalizationService _localization;
        
        // Konfigurasjon
        private const int MaxSessionMinutes = 20;
        private const int BreakIntervalMinutes = 15;
        private const int BreakDurationSeconds = 30;
        
        public event EventHandler<SessionWarningEventArgs>? SessionWarning;
        public event EventHandler? BreakRequired;
        public event EventHandler? SessionEnded;
        
        public bool IsInBreak { get; private set; }
        public TimeSpan ElapsedTime { get; private set; }
        public TimeSpan TimeUntilBreak { get; private set; }
        
        private int _totalSessionSeconds;
        private int _consecutiveSpeakingSeconds;
        private bool _isSessionActive;
        
        /// <summary>
        /// Default constructor
        /// </summary>
        public VoiceHealthService()
            : this(null)
        {
        }
        
        /// <summary>
        /// Constructor with dependency injection
        /// </summary>
        public VoiceHealthService(ILocalizationService? localization)
        {
            _localization = localization ?? LocalizationService.Instance;
            _sessionTimer = new Timer(1000);
            _sessionTimer.Elapsed += OnSessionTick;
            
            _breakTimer = new Timer(BreakDurationSeconds * 1000);
            _breakTimer.Elapsed += OnBreakEnded;
            _breakTimer.AutoReset = false;
        }
        
        public void StartSession()
        {
            _isSessionActive = true;
            _totalSessionSeconds = 0;
            _consecutiveSpeakingSeconds = 0;
            ElapsedTime = TimeSpan.Zero;
            TimeUntilBreak = TimeSpan.FromMinutes(BreakIntervalMinutes);
            _sessionTimer.Start();
        }
        
        public void StopSession()
        {
            _isSessionActive = false;
            _sessionTimer.Stop();
            _breakTimer.Stop();
            IsInBreak = false;
        }
        
        /// <summary>
        /// Registrer taleaktivitet (kalles når brukeren snakker)
        /// </summary>
        public void RegisterSpeaking()
        {
            if (!IsInBreak && _isSessionActive)
            {
                _consecutiveSpeakingSeconds++;
                
                if (_consecutiveSpeakingSeconds >= BreakIntervalMinutes * 60)
                {
                    TriggerBreak();
                }
            }
        }
        
        /// <summary>
        /// Registrer stillhet
        /// </summary>
        public void RegisterSilence()
        {
            _consecutiveSpeakingSeconds = Math.Max(0, _consecutiveSpeakingSeconds - 2);
        }
        
        private void OnSessionTick(object? sender, ElapsedEventArgs e)
        {
            if (!IsInBreak && _isSessionActive)
            {
                _totalSessionSeconds++;
                ElapsedTime = TimeSpan.FromSeconds(_totalSessionSeconds);
                TimeUntilBreak = TimeSpan.FromSeconds(
                    Math.Max(0, (BreakIntervalMinutes * 60) - _consecutiveSpeakingSeconds));
                
                if (_totalSessionSeconds >= MaxSessionMinutes * 60)
                {
                    SessionEnded?.Invoke(this, EventArgs.Empty);
                    StopSession();
                }
                else if (_totalSessionSeconds >= MaxSessionMinutes * 60 * 0.8)
                {
                    var remaining = MaxSessionMinutes - (int)ElapsedTime.TotalMinutes;
                    SessionWarning?.Invoke(this, new SessionWarningEventArgs(
                        _localization.GetString("VoiceHealth_SessionAlmostDoneTitle"),
                        _localization.GetFormattedString("VoiceHealth_SessionTimeRemaining", remaining)));
                }
                else if (_consecutiveSpeakingSeconds >= BreakIntervalMinutes * 60 * 0.8)
                {
                    var secondsLeft = (BreakIntervalMinutes * 60) - _consecutiveSpeakingSeconds;
                    SessionWarning?.Invoke(this, new SessionWarningEventArgs(
                        _localization.GetString("VoiceHealth_TakeBreakTitle"),
                        _localization.GetFormattedString("VoiceHealth_BreakNeededFormat", BreakIntervalMinutes, BreakDurationSeconds)));
                }
            }
        }
        
        private void TriggerBreak()
        {
            IsInBreak = true;
            BreakRequired?.Invoke(this, EventArgs.Empty);
            _breakTimer.Start();
        }
        
        private void OnBreakEnded(object? sender, ElapsedEventArgs e)
        {
            IsInBreak = false;
            _consecutiveSpeakingSeconds = 0;
        }
        
        public void Dispose()
        {
            _sessionTimer.Stop();
            _sessionTimer.Dispose();
            _breakTimer.Stop();
            _breakTimer.Dispose();
        }
    }
    
    public class SessionWarningEventArgs : EventArgs
    {
        public string Title { get; }
        public string Message { get; }
        public SessionWarningEventArgs(string title, string message)
        {
            Title = title;
            Message = message;
        }
    }
}
