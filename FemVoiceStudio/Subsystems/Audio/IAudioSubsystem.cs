using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FemVoiceStudio.Subsystems.Audio
{
    /// <summary>
    /// Represents an audio input device
    /// </summary>
    public class AudioDevice
    {
        public int DeviceNumber { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty;
        public int SampleRate { get; set; }
        public int Channels { get; set; }
        public bool IsDefault { get; set; }
    }

    /// <summary>
    /// Event args for audio sample data
    /// </summary>
    public class AudioSampleEventArgs : EventArgs
    {
        public float[] Samples { get; set; } = Array.Empty<float>();
        public int SampleRate { get; set; }
        public int Channels { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Event args for audio errors
    /// </summary>
    public class AudioErrorEventArgs : EventArgs
    {
        public string ErrorMessage { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
    }

    /// <summary>
    /// Audio subsystem interface - handles microphone capture and audio processing
    /// </summary>
    public interface IAudioSubsystem : IDisposable
    {
        /// <summary>
        /// Whether currently capturing audio
        /// </summary>
        bool IsCapturing { get; }

        /// <summary>
        /// Current audio latency in milliseconds
        /// </summary>
        int CurrentLatencyMs { get; }

        /// <summary>
        /// Current sample rate
        /// </summary>
        int SampleRate { get; }

        /// <summary>
        /// Get available audio input devices
        /// </summary>
        Task<IEnumerable<AudioDevice>> GetAvailableDevicesAsync(CancellationToken ct = default);

        /// <summary>
        /// Start audio capture from specified device
        /// </summary>
        Task StartCaptureAsync(AudioDevice device, CancellationToken ct = default);

        /// <summary>
        /// Start capture from default device
        /// </summary>
        Task StartCaptureAsync(CancellationToken ct = default);

        /// <summary>
        /// Stop audio capture
        /// </summary>
        Task StopCaptureAsync();

        /// <summary>
        /// Set audio buffer size
        /// </summary>
        void SetBufferSize(int samples);

        /// <summary>
        /// Whether user can hear their own voice during recording
        /// </summary>
        bool HearOwnVoice { get; set; }

        /// <summary>
        /// Event raised when new audio sample is available
        /// </summary>
        event EventHandler<AudioSampleEventArgs>? AudioSampleAvailable;

        /// <summary>
        /// Event raised when an error occurs
        /// </summary>
        event EventHandler<AudioErrorEventArgs>? ErrorOccurred;
    }
}
