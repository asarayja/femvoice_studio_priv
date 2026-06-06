using System;
using System.IO;
using System.Linq;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Threading;
using System.Threading.Tasks;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Audio
{
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
        
        // Buffer for akkumulering av audio data
        private readonly CircularBuffer<float> _audioBuffer;
        
        // Events for data og feilhåndtering
        public event EventHandler<float[]>? AudioDataAvailable;
        public event EventHandler<string>? ErrorOccurred;
        
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
        public int SampleRate => _sampleRate;
        public int InputDeviceNumber { get; private set; } = -1;
        public string InputDeviceName { get; private set; } = "Default microphone";
        public int Channels => _channels;
        public MicrophoneCalibrationProfile? CalibrationProfile { get; private set; }
        public bool ApplyInputProcessing { get; set; } = true;
        
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
                _waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(_sampleRate, _bitsPerSample, _channels),
                    BufferMilliseconds = (_bufferSize * 1000) / _sampleRate
                };
                
                InputDeviceNumber = SelectPreferredInputDevice();
                InputDeviceName = GetInputDeviceName(InputDeviceNumber);
                _waveIn.DeviceNumber = InputDeviceNumber;
                ApplyStoredCalibration();
                
                _waveIn.DataAvailable += OnDataAvailable;
                _waveIn.RecordingStopped += OnRecordingStopped;
                
                // Sjekk tilgjengelige enheter
                var deviceCount = WaveInEvent.DeviceCount;
                if (deviceCount == 0)
                {
                    ErrorOccurred?.Invoke(this, "Ingen mikrofon funnet. Koble til en mikrofon og prøv igjen.");
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Feil ved initialisering av mikrofon: {ex.Message}");
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
            if (_isRecording || _waveIn == null)
                return;
                
            try
            {
                _audioBuffer.Clear();
                if (!_hearOwnVoice)
                {
                    _waveOut?.Pause();
                    _playbackBuffer?.ClearBuffer();
                }
                _waveIn.BufferMilliseconds = (_bufferSize * 1000) / _sampleRate;
                _waveIn.StartRecording();
                _isRecording = true;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Feil ved start av opptak: {ex.Message}");
                _isRecording = false;
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
            
            // Legg til i circular buffer
            foreach (var sample in samples)
            {
                _audioBuffer.Write(sample);
            }
            
            // Send data til event subscribers
            AudioDataAvailable?.Invoke(this, samples);
            
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
            _isRecording = false;
            
            if (e.Exception != null)
            {
                ErrorOccurred?.Invoke(this, $"Opptak stoppet på grunn av feil: {e.Exception.Message}");
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

        private static string? GetDefaultCaptureDeviceName()
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                return enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console).FriendlyName;
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
