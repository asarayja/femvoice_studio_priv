using System;
using System.IO;
using System.Linq;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;

// Eksponerer internal medlemmer (f.eks. HandleRecordingStopped) for enhetstesting
// av safety-stien uten en fysisk lydenhet.
[assembly: InternalsVisibleTo("FemVoiceStudio.Tests")]

namespace FemVoiceStudio.Audio
{
    public class AudioCaptureStartException : InvalidOperationException
    {
        public AudioCaptureStartException(string message) : base(message) { }
        public AudioCaptureStartException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Audio capture service som håndterer mikrofonopptak med NAudio.
    /// Implementerer lav-latens kontinuerlig opptak med buffer-håndtering.
    /// </summary>
    public class AudioCaptureService : IDisposable
    {
        private WaveInEvent? _waveIn;
        private WaveOutEvent? _waveOut;
        private BufferedWaveProvider? _playbackBuffer;
        private readonly int _sampleRate;
        private readonly int _channels;
        private readonly int _bitsPerSample;
        private bool _isRecording;
        private bool _isDisposed;
        private long _dataAvailableCount;
        private long _bytesReceived;
        private long _samplesReceived;
        private long _silenceDetectedCount;
        private long _droppedCallbackCount;
        private DateTime? _lastDataAvailableTime;
        private DateTime? _previousDataAvailableTime;
        private DateTime _lastDiagnosticLogTime = DateTime.MinValue;
        private double _lastCallbackIntervalMs;
        private double _lastRmsLevel;
        private double _lastPeakLevel;
        private double _lowestRmsLevel = double.MaxValue;
        private double _highestRmsLevel;
        private double _noiseFloorEstimate;
        private bool _lastSilenceDetected;
        private bool _lastSignalAccepted;
        private string _lastSignalRejectedReason = "";
        private string _defaultInputDeviceName = "";
        private string _defaultCommunicationsDeviceName = "";
        private string _sessionStartDeviceName = "";
        
        // Buffer for akkumulering av audio data
        private readonly CircularBuffer<float> _audioBuffer;
        
        // Events for data og feilhåndtering
        public event EventHandler<float[]>? AudioDataAvailable;
        public event EventHandler<string>? ErrorOccurred;

        /// <summary>
        /// Fyres når lydkilden går tapt under opptak (enhetstap/driverfeil).
        /// Safety-first: abonnenter skal STOPPE analysen og PAUSE økten — aldri
        /// fortsette stille på en ukjent kilde. Strengen er en kort årsaksbeskrivelse.
        /// </summary>
        public event EventHandler<string>? DeviceLost;

        /// <summary>
        /// Navnet på enheten som var aktiv ved siste StartRecording.
        /// Null inntil opptak er startet. Brukes for diagnostikk ved enhetstap.
        /// </summary>
        public string? ActiveDeviceName { get; private set; }
        
        // Konfigurasjon - lav latens
        private int _bufferSize = 1024; // Redusert for lav latens (~23ms ved 44100Hz)
        
        /// <summary>
        /// Buffer size in samples. Reducing from 4096 to 1024 achieves ~23ms latency.
        /// For ultra-low latency (<15ms), use 512 samples.
        /// </summary>
        public int BufferSize 
        { 
            get => _bufferSize;
            set
            {
                if (value >= 256 && value <= 8192)
                    _bufferSize = value;
            }
        }
        
        /// <summary>
        /// Current latency in milliseconds based on buffer size and sample rate.
        /// </summary>
        public int TargetLatencyMs => (_bufferSize * 1000) / _sampleRate;
        
        public bool IsRecording => _isRecording;
        public bool HasReceivedAudioData { get; private set; }
        public bool IsMonitoringActive =>
            _hearOwnVoice &&
            _isRecording &&
            HasReceivedAudioData &&
            _waveOut?.PlaybackState == PlaybackState.Playing;
        public int SampleRate => _sampleRate;
        public int BitsPerSample => _bitsPerSample;
        public int InputDeviceNumber { get; private set; } = -1;
        public string InputDeviceName { get; private set; } = "Default microphone";
        public int Channels => _channels;
        public MicrophoneCalibrationProfile? CalibrationProfile { get; private set; }
        public bool ApplyInputProcessing { get; set; } = true;
        public AudioCaptureDiagnosticsSnapshot DiagnosticsSnapshot => CreateDiagnosticsSnapshot();
        
        // Noise filtering
        private float _noiseGateThreshold = 0.01f;
        private bool _highPassFilterEnabled = true;
        private float _highPassCutoff = 80f; // Hz
        
        /// <summary>
        /// Threshold for noise gate (0.0-1.0). Samples below this are silenced.
        /// </summary>
        public float NoiseGateThreshold
        {
            get => _noiseGateThreshold;
            set => _noiseGateThreshold = Math.Clamp(value, 0f, 1f);
        }
        
        /// <summary>
        /// Enable/disable high-pass filter to remove low frequency rumble.
        /// </summary>
        public bool HighPassFilterEnabled
        {
            get => _highPassFilterEnabled;
            set => _highPassFilterEnabled = value;
        }
        
        // Playback control
        private bool _hearOwnVoice = false;
        public bool HearOwnVoice
        {
            get => _hearOwnVoice;
            set
            {
                _hearOwnVoice = value;
                if (_hearOwnVoice)
                {
                    EnsurePlaybackInitialized();
                    StartPlayback();
                }
                else if (_waveOut != null)
                {
                    _waveOut.Pause();
                    _playbackBuffer?.ClearBuffer();
                }
            }
        }
        
        public AudioCaptureService(int sampleRate = 44100, int channels = 1, int bitsPerSample = 16)
        {
            _sampleRate = sampleRate;
            _channels = channels;
            _bitsPerSample = bitsPerSample;
            
            // Initialize circular buffer with enough capacity for ~2 seconds
            _audioBuffer = new CircularBuffer<float>(sampleRate * 2);
            
            // Playback is opt-in. It is initialized only when HearOwnVoice is enabled.
        }
        
        /// <summary>
        /// Initialiserer playback-enheten
        /// </summary>
        private void EnsurePlaybackInitialized()
        {
            if (_waveOut != null && _playbackBuffer != null)
                return;

            try
            {
                _playbackBuffer = new BufferedWaveProvider(WaveFormat.CreateIeeeFloatWaveFormat(_sampleRate, _channels))
                {
                    DiscardOnBufferOverflow = true,
                    BufferDuration = TimeSpan.FromMilliseconds(100)
                };
                
                _waveOut = new WaveOutEvent
                {
                    DesiredLatency = 50
                };
                _waveOut.Init(_playbackBuffer);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Playback init error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Starter playback (brukes ved oppstart for å teste)
        /// </summary>
        public void StartPlayback()
        {
            if (!_hearOwnVoice)
                return;

            EnsurePlaybackInitialized();

            if (_waveOut?.PlaybackState != PlaybackState.Playing)
            {
                try
                {
                    _waveOut?.Play();
                }
                catch { }
            }
        }
        
        /// <summary>
        /// Pauser playback
        /// </summary>
        public void PausePlayback()
        {
            if (_waveOut?.PlaybackState == PlaybackState.Playing)
            {
                try
                {
                    _waveOut?.Pause();
                }
                catch { }
            }
        }
        
        /// <summary>
        /// Initialiserer mikrofonenheten med lav latens innstillinger
        /// </summary>
        public void Initialize()
        {
            try
            {
                var deviceCount = WaveInEvent.DeviceCount;
                if (deviceCount == 0)
                {
                    const string message = "Ingen mikrofon funnet. Koble til en mikrofon og prøv igjen.";
                    ErrorOccurred?.Invoke(this, message);
                    throw new AudioCaptureStartException(message);
                }

                _waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(_sampleRate, _bitsPerSample, _channels),
                    BufferMilliseconds = (_bufferSize * 1000) / _sampleRate
                };
                
                _defaultInputDeviceName = GetDefaultCaptureDeviceName(Role.Console) ?? "";
                _defaultCommunicationsDeviceName = GetDefaultCaptureDeviceName(Role.Communications) ?? "";
                InputDeviceNumber = SelectPreferredInputDevice();
                InputDeviceName = GetInputDeviceName(InputDeviceNumber);
                _waveIn.DeviceNumber = InputDeviceNumber;
                ApplyStoredCalibration();
                Rc0RuntimeLog.Write("AudioCaptureService",
                    $"Initialized DeviceNumber={InputDeviceNumber}; DeviceName=\"{InputDeviceName}\"; " +
                    $"DefaultInput=\"{_defaultInputDeviceName}\"; DefaultCommunications=\"{_defaultCommunicationsDeviceName}\"; " +
                    $"SampleRate={_sampleRate}; Channels={_channels}; BitDepth={_bitsPerSample}; BufferMs={_waveIn.BufferMilliseconds}; Api=NAudio WaveInEvent");
                
                _waveIn.DataAvailable += OnDataAvailable;
                _waveIn.RecordingStopped += OnRecordingStopped;
            }
            catch (AudioCaptureStartException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var message = $"Feil ved initialisering av mikrofon: {ex.Message}";
                ErrorOccurred?.Invoke(this, message);
                throw new InvalidOperationException(message, ex);
            }
        }

        private void ApplyStoredCalibration()
        {
            CalibrationProfile = new MicrophoneCalibrationService().Load(InputDeviceName);
            if (CalibrationProfile == null)
                return;

            NoiseGateThreshold = (float)Math.Min(
                CalibrationProfile.NoiseGateThreshold,
                Math.Max(0.0015, CalibrationProfile.VoicedRmsThreshold * 0.75));
        }
        
        /// <summary>
        /// Initialiserer med lav latens for optimal yteevne
        /// </summary>
        /// <param name="ultraLowLatency">If true, uses 512 sample buffer (~12ms latency)</param>
        public void InitializeLowLatency(bool ultraLowLatency = false)
        {
            _bufferSize = ultraLowLatency ? 512 : 1024;
            Initialize();
        }
        
        /// <summary>
        /// Starter kontinuerlig opptak
        /// </summary>
        public void StartRecording()
        {
            if (_isRecording)
                return;

            if (_waveIn == null)
                throw new InvalidOperationException("Audio capture er ikke initialisert. Kall Initialize() før StartRecording().");
                
            try
            {
                _audioBuffer.Clear();
                ResetDiagnostics();
                HasReceivedAudioData = false;
                if (!_hearOwnVoice)
                {
                    _waveOut?.Pause();
                    _playbackBuffer?.ClearBuffer();
                }
                _waveIn.BufferMilliseconds = (_bufferSize * 1000) / _sampleRate;
                // Lagre aktiv enhet slik at vi kan rapportere hvilken kilde som gikk tapt.
                ActiveDeviceName = InputDeviceName;
                _sessionStartDeviceName = InputDeviceName;
                _waveIn.StartRecording();
                _isRecording = true;
                Rc0RuntimeLog.Write("AudioCaptureService",
                    $"StartRecording OK; DeviceName=\"{InputDeviceName}\"; IsRecording={_isRecording}; MonitoringRequested={_hearOwnVoice}");
            }
            catch (Exception ex)
            {
                var message = $"Feil ved start av opptak: {ex.Message}";
                ErrorOccurred?.Invoke(this, message);
                _isRecording = false;
                Rc0RuntimeLog.Write("AudioCaptureService", $"StartRecording FAILED; {message}");
                throw new InvalidOperationException(message, ex);
            }
        }
        
        /// <summary>
        /// Stopper opptaket
        /// </summary>
        public void StopRecording()
        {
            if (!_isRecording || _waveIn == null)
                return;
                
            try
            {
                _waveIn.StopRecording();
                _waveOut?.Pause();
                _playbackBuffer?.ClearBuffer();
                _isRecording = false;
                Rc0RuntimeLog.Write("AudioCaptureService", $"StopRecording; {FormatDiagnostics(CreateDiagnosticsSnapshot())}");
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Feil ved stopp av opptak: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Håndterer incoming audio data fra mikrofonen med støyfiltrering
        /// </summary>
        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded == 0)
                return;
                
            // Konverter byte data til float samples
            var samples = ConvertToFloatSamples(e.Buffer, e.BytesRecorded);
            var rawRms = CalculateRms(samples);
            var rawPeak = CalculatePeak(samples);
            _previousDataAvailableTime = _lastDataAvailableTime;
            _lastDataAvailableTime = DateTime.UtcNow;
            if (_previousDataAvailableTime.HasValue)
            {
                _lastCallbackIntervalMs = (_lastDataAvailableTime.Value - _previousDataAvailableTime.Value).TotalMilliseconds;
                var expectedMs = Math.Max(1, (_bufferSize * 1000.0) / _sampleRate);
                if (_lastCallbackIntervalMs > expectedMs * 3.5)
                    _droppedCallbackCount++;
            }
            _dataAvailableCount++;
            _bytesReceived += e.BytesRecorded;
            _samplesReceived += samples.Length;
            
            // Apply input processing for normal analysis. Calibration captures raw samples.
            if (ApplyInputProcessing && _noiseGateThreshold > 0)
            {
                samples = ApplyNoiseGate(samples);
            }
            
            // Apply high-pass filter if enabled
            if (ApplyInputProcessing && _highPassFilterEnabled)
            {
                samples = ApplyHighPassFilter(samples);
            }
            var processedRms = CalculateRms(samples);
            var processedPeak = CalculatePeak(samples);
            UpdateSignalDiagnostics(rawRms, rawPeak, processedRms, processedPeak);
            
            // Legg til i circular buffer
            foreach (var sample in samples)
            {
                _audioBuffer.Write(sample);
            }
            
            // Send data til event subscribers
            HasReceivedAudioData = true;
            AudioDataAvailable?.Invoke(this, samples);
            LogDiagnosticsEverySecond();
            
            // Spill av lyden hvis HearOwnVoice er aktivert
            if (_hearOwnVoice && _playbackBuffer != null && _isRecording)
            {
                try
                {
                    var byteData = new byte[samples.Length * 4];
                    Buffer.BlockCopy(samples, 0, byteData, 0, byteData.Length);
                    _playbackBuffer.AddSamples(byteData, 0, byteData.Length);
                }
                catch { }
            }
        }
        
        /// <summary>
        /// Apply noise gate to remove quiet background noise
        /// </summary>
        private float[] ApplyNoiseGate(float[] samples)
        {
            if (samples.Length == 0)
                return samples;

            var rms = Math.Sqrt(samples.Sum(sample => (double)sample * sample) / samples.Length);
            if (rms >= _noiseGateThreshold)
                return samples;

            var attenuation = rms < _noiseGateThreshold * 0.45 ? 0.15f : 0.45f;
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] *= attenuation;
            }
            return samples;
        }

        private void ResetDiagnostics()
        {
            _dataAvailableCount = 0;
            _bytesReceived = 0;
            _samplesReceived = 0;
            _silenceDetectedCount = 0;
            _droppedCallbackCount = 0;
            _lastDataAvailableTime = null;
            _previousDataAvailableTime = null;
            _lastDiagnosticLogTime = DateTime.MinValue;
            _lastCallbackIntervalMs = 0;
            _lastRmsLevel = 0;
            _lastPeakLevel = 0;
            _lowestRmsLevel = double.MaxValue;
            _highestRmsLevel = 0;
            _noiseFloorEstimate = CalibrationProfile?.NoiseFloorRms ?? 0;
            _lastSilenceDetected = false;
            _lastSignalAccepted = false;
            _lastSignalRejectedReason = "";
        }

        private void UpdateSignalDiagnostics(double rawRms, double rawPeak, double processedRms, double processedPeak)
        {
            _lastRmsLevel = processedRms;
            _lastPeakLevel = processedPeak;
            _lowestRmsLevel = Math.Min(_lowestRmsLevel, processedRms);
            _highestRmsLevel = Math.Max(_highestRmsLevel, processedRms);

            if (rawRms > 0)
            {
                _noiseFloorEstimate = _noiseFloorEstimate <= 0
                    ? rawRms
                    : Math.Min(_noiseFloorEstimate * 0.995 + rawRms * 0.005, rawRms);
            }

            _lastSilenceDetected = processedRms < _noiseGateThreshold;
            _lastSignalAccepted = !_lastSilenceDetected && processedPeak > 0;
            if (_lastSilenceDetected)
            {
                _silenceDetectedCount++;
                _lastSignalRejectedReason = processedRms <= 0
                    ? "noise gate attenuated frame to near-zero"
                    : "rms below current silence threshold";
            }
            else
            {
                _lastSignalRejectedReason = "";
            }
        }

        private void LogDiagnosticsEverySecond()
        {
            var now = DateTime.UtcNow;
            if ((now - _lastDiagnosticLogTime).TotalSeconds < 1)
                return;

            _lastDiagnosticLogTime = now;
            Rc0RuntimeLog.Write("AudioCaptureHealth", FormatDiagnostics(CreateDiagnosticsSnapshot()));
        }

        private AudioCaptureDiagnosticsSnapshot CreateDiagnosticsSnapshot()
        {
            var now = DateTime.UtcNow;
            var timeSince = _lastDataAvailableTime.HasValue
                ? (now - _lastDataAvailableTime.Value).TotalSeconds
                : 0;
            var snr = _noiseFloorEstimate > 0
                ? MicrophoneCalibrationService.CalculateDbFs(_lastRmsLevel) - MicrophoneCalibrationService.CalculateDbFs(_noiseFloorEstimate)
                : 0;
            var levelCollapsed = _highestRmsLevel > 0.01 && _lastRmsLevel < _highestRmsLevel * 0.2;
            var classification = !_isRecording && _dataAvailableCount > 0
                ? AudioFailureClassification.CAPTURE_STOPS
                : levelCollapsed
                    ? AudioFailureClassification.SIGNAL_LEVEL_COLLAPSES
                    : _lastSilenceDetected && _dataAvailableCount > 0
                        ? AudioFailureClassification.SILENCE_GATE_REJECTS_SIGNAL
                        : AudioFailureClassification.UNKNOWN;

            return new AudioCaptureDiagnosticsSnapshot
            {
                DeviceName = InputDeviceName,
                DeviceId = InputDeviceNumber.ToString(),
                DefaultInputDeviceName = _defaultInputDeviceName,
                DefaultCommunicationsDeviceName = _defaultCommunicationsDeviceName,
                DeviceSelectedByFemVoice = InputDeviceName,
                DeviceChangedDuringSession = !string.IsNullOrEmpty(_sessionStartDeviceName)
                    && !string.Equals(_sessionStartDeviceName, InputDeviceName, StringComparison.OrdinalIgnoreCase),
                SampleRate = _sampleRate,
                Channels = _channels,
                BitDepth = _bitsPerSample,
                BufferMilliseconds = _waveIn?.BufferMilliseconds ?? TargetLatencyMs,
                IsRecording = _isRecording,
                DataAvailableCount = _dataAvailableCount,
                BytesReceived = _bytesReceived,
                SamplesReceived = _samplesReceived,
                CallbackIntervalMs = _lastCallbackIntervalMs,
                DroppedCallbackCount = _droppedCallbackCount,
                LastDataAvailableTime = _lastDataAvailableTime,
                TimeSinceLastAudioFrameSeconds = timeSince,
                RmsLevel = _lastRmsLevel,
                PeakLevel = _lastPeakLevel,
                InputLevelPercent = Math.Clamp(_lastRmsLevel / 0.05 * 100, 0, 100),
                NoiseFloorEstimate = _noiseFloorEstimate,
                SignalToNoiseEstimateDb = snr,
                LowestLevel = _lowestRmsLevel == double.MaxValue ? 0 : _lowestRmsLevel,
                HighestLevel = _highestRmsLevel,
                LevelCollapsed = levelCollapsed,
                SilenceDetected = _lastSilenceDetected,
                SilenceDetectedCount = _silenceDetectedCount,
                CurrentSilenceThreshold = _noiseGateThreshold,
                IsSignalAccepted = _lastSignalAccepted,
                IsSignalRejected = !_lastSignalAccepted && _dataAvailableCount > 0,
                SignalRejectedReason = _lastSignalRejectedReason,
                MonitoringActive = IsMonitoringActive,
                FailureClassification = classification
            };
        }

        private static string FormatDiagnostics(AudioCaptureDiagnosticsSnapshot snapshot)
        {
            return $"IsRecording={snapshot.IsRecording}; Device=\"{snapshot.DeviceName}\"; DataAvailableCount={snapshot.DataAvailableCount}; " +
                   $"BytesReceived={snapshot.BytesReceived}; SamplesReceived={snapshot.SamplesReceived}; CallbackIntervalMs={snapshot.CallbackIntervalMs:F1}; " +
                   $"DroppedCallbacks={snapshot.DroppedCallbackCount}; LastData={snapshot.LastDataAvailableTime:O}; TimeSinceLastFrame={snapshot.TimeSinceLastAudioFrameSeconds:F2}s; " +
                   $"Rms={snapshot.RmsLevel:F5}; Peak={snapshot.PeakLevel:F5}; InputLevel={snapshot.InputLevelPercent:F1}%; NoiseFloor={snapshot.NoiseFloorEstimate:F5}; " +
                   $"SnrDb={snapshot.SignalToNoiseEstimateDb:F1}; Low={snapshot.LowestLevel:F5}; High={snapshot.HighestLevel:F5}; LevelCollapsed={snapshot.LevelCollapsed}; " +
                   $"SilenceDetected={snapshot.SilenceDetected}; SilenceCount={snapshot.SilenceDetectedCount}; Threshold={snapshot.CurrentSilenceThreshold:F5}; " +
                   $"Accepted={snapshot.IsSignalAccepted}; Rejected={snapshot.IsSignalRejected}; RejectReason=\"{snapshot.SignalRejectedReason}\"; " +
                   $"MonitoringActive={snapshot.MonitoringActive}; Classification={snapshot.FailureClassification}";
        }

        private static double CalculateRms(float[] samples)
            => samples.Length == 0 ? 0 : Math.Sqrt(samples.Sum(sample => (double)sample * sample) / samples.Length);

        private static double CalculatePeak(float[] samples)
            => samples.Length == 0 ? 0 : samples.Max(sample => Math.Abs((double)sample));
        
        /// <summary>
        /// Simple high-pass filter using difference equation
        /// y[n] = x[n] - x[n-1] + alpha * y[n-1]
        /// This is a first-order IIR high-pass filter
        /// </summary>
        private float[] ApplyHighPassFilter(float[] samples)
        {
            if (samples.Length < 2)
                return samples;
                
            // Calculate filter coefficient based on cutoff frequency
            // alpha = RC / (RC + dt) where RC = 1/(2*pi*cutoff)
            float rc = 1f / (2f * (float)Math.PI * _highPassCutoff);
            float dt = 1f / _sampleRate;
            float alpha = rc / (rc + dt);
            
            float[] output = new float[samples.Length];
            float yPrev = 0;
            
            for (int i = 0; i < samples.Length; i++)
            {
                output[i] = alpha * (yPrev + samples[i] - (i > 0 ? samples[i-1] : 0));
                yPrev = output[i];
            }
            
            return output;
        }
        
        /// <summary>
        /// Konverterer byte-array til normaliserte float verdier (-1.0 til 1.0)
        /// </summary>
        private float[] ConvertToFloatSamples(byte[] buffer, int bytesRecorded)
        {
            var sampleCount = bytesRecorded / (_bitsPerSample / 8);
            var samples = new float[sampleCount];
            
            if (_bitsPerSample == 16)
            {
                for (int i = 0; i < sampleCount; i++)
                {
                    short sample = BitConverter.ToInt16(buffer, i * 2);
                    samples[i] = sample / 32768f;
                }
            }
            else if (_bitsPerSample == 32)
            {
                for (int i = 0; i < sampleCount; i++)
                {
                    samples[i] = BitConverter.ToSingle(buffer, i * 4);
                }
            }
            
            return samples;
        }
        
        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            HandleRecordingStopped(e.Exception);
        }

        /// <summary>
        /// Felles håndtering av at opptaket stoppet. Skilt ut fra NAudio-eventet slik at
        /// enhetstap-logikken kan verifiseres i test uten en fysisk lydenhet.
        /// Ved exception (enhetstap/driverfeil manifesterer her i NAudio) fyres BÅDE
        /// ErrorOccurred (eksisterende oppførsel) OG DeviceLost (safety-stopp).
        /// </summary>
        internal void HandleRecordingStopped(Exception? exception)
        {
            _isRecording = false;

            if (exception != null)
            {
                var reason = exception.Message;
                ErrorOccurred?.Invoke(this, $"Opptak stoppet på grunn av feil: {reason}");
                // Safety: signaler tapt lydkilde slik at økten kan pauses trygt.
                DeviceLost?.Invoke(this, reason);
            }
        }
        
        /// <summary>
        /// Hent de siste N millisekunder med audio data
        /// </summary>
        public float[] GetRecentSamples(int milliseconds)
        {
            var sampleCount = (_sampleRate * milliseconds) / 1000;
            var samples = new float[Math.Min(sampleCount, _audioBuffer.Count)];
            
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = _audioBuffer.Read();
            }
            
            return samples;
        }
        
        /// <summary>
        /// Hent tilgjengelige mikrofonenheter
        /// </summary>
        public static string[] GetAvailableDevices()
        {
            var devices = new string[WaveInEvent.DeviceCount];
            for (int i = 0; i < WaveInEvent.DeviceCount; i++)
            {
                var capabilities = WaveInEvent.GetCapabilities(i);
                devices[i] = $"{i}: {capabilities.ProductName}";
            }
            return devices;
        }

        private static int SelectPreferredInputDevice()
        {
            var deviceCount = WaveInEvent.DeviceCount;
            if (deviceCount <= 0)
                return -1;

            var defaultCaptureName = GetDefaultCaptureDeviceName();
            if (!string.IsNullOrWhiteSpace(defaultCaptureName))
            {
                for (int i = 0; i < deviceCount; i++)
                {
                    var waveInName = WaveInEvent.GetCapabilities(i).ProductName;
                    if (DeviceNamesMatch(defaultCaptureName, waveInName))
                        return i;
                }
            }

            for (int i = 0; i < deviceCount; i++)
            {
                var name = WaveInEvent.GetCapabilities(i).ProductName;
                if (name.Contains("Microphone", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Mic", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Mikrofon", StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return 0;
        }

        private static string? GetDefaultCaptureDeviceName(Role role = Role.Console)
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                return enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, role).FriendlyName;
            }
            catch
            {
                return null;
            }
        }

        private static bool DeviceNamesMatch(string defaultCaptureName, string waveInName)
        {
            var defaultName = NormalizeDeviceName(defaultCaptureName);
            var inputName = NormalizeDeviceName(waveInName);

            return defaultName.Contains(inputName, StringComparison.OrdinalIgnoreCase)
                || inputName.Contains(defaultName, StringComparison.OrdinalIgnoreCase)
                || defaultName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Any(part => IsSpecificDeviceNamePart(part)
                        && inputName.Contains(part, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsSpecificDeviceNamePart(string part)
        {
            if (part.Length < 4)
                return false;

            return !part.Equals("Microphone", StringComparison.OrdinalIgnoreCase)
                && !part.Equals("Mikrofon", StringComparison.OrdinalIgnoreCase)
                && !part.Equals("Audio", StringComparison.OrdinalIgnoreCase)
                && !part.Equals("Input", StringComparison.OrdinalIgnoreCase)
                && !part.Equals("Device", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeDeviceName(string name)
        {
            var normalized = name
                .Replace("(", " ", StringComparison.Ordinal)
                .Replace(")", " ", StringComparison.Ordinal)
                .Replace("-", " ", StringComparison.Ordinal)
                .Replace("_", " ", StringComparison.Ordinal)
                .Trim();

            return string.Join(" ", normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        private static string GetInputDeviceName(int deviceNumber)
        {
            if (deviceNumber >= 0 && deviceNumber < WaveInEvent.DeviceCount)
                return WaveInEvent.GetCapabilities(deviceNumber).ProductName;

            return "Default microphone";
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;
                
            StopRecording();
            _waveIn?.Dispose();
            _waveOut?.Stop();
            _waveOut?.Dispose();
            _isDisposed = true;
        }
    }
    
    /// <summary>
    /// Enkel sirkulær buffer for audio samples
    /// </summary>
    public class CircularBuffer<T>
    {
        private readonly T[] _buffer;
        private int _readIndex;
        private int _writeIndex;
        private int _count;
        private readonly object _lock = new();
        
        public CircularBuffer(int capacity)
        {
            _buffer = new T[capacity];
        }
        
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _count;
                }
            }
        }
        
        public void Write(T item)
        {
            lock (_lock)
            {
                _buffer[_writeIndex] = item;
                _writeIndex = (_writeIndex + 1) % _buffer.Length;
                
                if (_count < _buffer.Length)
                    _count++;
                else
                    _readIndex = (_readIndex + 1) % _buffer.Length;
            }
        }
        
        public T Read()
        {
            lock (_lock)
            {
                if (_count == 0)
                    throw new InvalidOperationException("Buffer is empty");
                    
                var item = _buffer[_readIndex];
                _readIndex = (_readIndex + 1) % _buffer.Length;
                _count--;
                return item;
            }
        }
        
        public void Clear()
        {
            lock (_lock)
            {
                _readIndex = 0;
                _writeIndex = 0;
                _count = 0;
            }
        }
    }
}
