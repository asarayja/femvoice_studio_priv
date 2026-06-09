using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FemVoiceStudio.Audio;

namespace FemVoiceStudio.Subsystems.Audio
{
    /// <summary>
    /// Audio Subsystem - wraps existing audio services behind interface
    /// Provides microphone capture, playback, and audio processing pipeline
    /// </summary>
    public class AudioSubsystem : IAudioSubsystem
    {
        private AudioCaptureService? _audioCapture;
        private AudioDevice? _currentDevice;
        private bool _isCapturing;
        private bool _hearOwnVoice;
        private bool _disposed;

        public bool IsCapturing => _isCapturing;

        public int CurrentLatencyMs => _audioCapture?.TargetLatencyMs ?? 0;

        public int SampleRate => _audioCapture?.SampleRate ?? 44100;

        public bool HearOwnVoice
        {
            get => _audioCapture?.HearOwnVoice ?? _hearOwnVoice;
            set
            {
                _hearOwnVoice = value;
                if (_audioCapture != null)
                {
                    _audioCapture.HearOwnVoice = value;
                }
            }
        }

        public event EventHandler<AudioSampleEventArgs>? AudioSampleAvailable;
        public event EventHandler<AudioErrorEventArgs>? ErrorOccurred;

        public Task<IEnumerable<AudioDevice>> GetAvailableDevicesAsync(CancellationToken ct = default)
        {
            return Task.Run(() =>
            {
                var devices = new List<AudioDevice>();

                try
                {
                    // Use NAudio WaveInEvent to enumerate devices
                    for (int i = 0; i < NAudio.Wave.WaveInEvent.DeviceCount; i++)
                    {
                        var caps = NAudio.Wave.WaveInEvent.GetCapabilities(i);
                        devices.Add(new AudioDevice
                        {
                            DeviceNumber = i,
                            Name = caps.ProductName,
                            Manufacturer = caps.ManufacturerGuid.ToString(),
                            Channels = caps.Channels,
                            SampleRate = 44100, // Default, NAudio doesn't expose this directly
                            IsDefault = i == 0
                        });
                    }
                }
                catch (Exception ex)
                {
                    ErrorOccurred?.Invoke(this, new AudioErrorEventArgs
                    {
                        ErrorMessage = $"Failed to enumerate devices: {ex.Message}",
                        Exception = ex
                    });
                }

                return (IEnumerable<AudioDevice>)devices;
            }, ct);
        }

        public Task StartCaptureAsync(AudioDevice device, CancellationToken ct = default)
        {
            return StartCaptureInternalAsync(device, ct);
        }

        public Task StartCaptureAsync(CancellationToken ct = default)
        {
            return StartCaptureAsync(new AudioDevice { DeviceNumber = 0 }, ct);
        }

        private async Task StartCaptureInternalAsync(AudioDevice device, CancellationToken ct)
        {
            if (_isCapturing)
            {
                await StopCaptureAsync();
            }

            await Task.Run(() =>
            {
                try
                {
                    _currentDevice = device;
                    _audioCapture = new AudioCaptureService(device.SampleRate > 0 ? device.SampleRate : 44100);
                    _audioCapture.HearOwnVoice = _hearOwnVoice;
                    
                    _audioCapture.AudioDataAvailable += OnAudioDataAvailable;
                    _audioCapture.ErrorOccurred += OnErrorOccurred;
                    
                    _audioCapture.Initialize();
                    _audioCapture.StartRecording();
                    if (!_audioCapture.IsRecording)
                        throw new InvalidOperationException("Audio capture startet ikke.");

                    _isCapturing = true;
                }
                catch (Exception ex)
                {
                    _isCapturing = false;
                    ErrorOccurred?.Invoke(this, new AudioErrorEventArgs
                    {
                        ErrorMessage = $"Failed to start capture: {ex.Message}",
                        Exception = ex
                    });
                    throw;
                }
            }, ct);
        }

        public Task StopCaptureAsync()
        {
            return Task.Run(() =>
            {
                if (_audioCapture != null && _isCapturing)
                {
                    try
                    {
                        _audioCapture.StopRecording();
                        _audioCapture.AudioDataAvailable -= OnAudioDataAvailable;
                        _audioCapture.ErrorOccurred -= OnErrorOccurred;
                    }
                    catch (Exception ex)
                    {
                        ErrorOccurred?.Invoke(this, new AudioErrorEventArgs
                        {
                            ErrorMessage = $"Error stopping capture: {ex.Message}",
                            Exception = ex
                        });
                    }
                    finally
                    {
                        _isCapturing = false;
                    }
                }
            });
        }

        public void SetBufferSize(int samples)
        {
            if (_audioCapture != null)
            {
                // Note: BufferSize is read-only in existing implementation
                // This could be exposed as a parameter in AudioCaptureService
            }
        }

        private void OnAudioDataAvailable(object? sender, float[] samples)
        {
            AudioSampleAvailable?.Invoke(this, new AudioSampleEventArgs
            {
                Samples = samples,
                SampleRate = _audioCapture?.SampleRate ?? 44100,
                Channels = _audioCapture?.Channels ?? 1,
                Timestamp = DateTime.Now
            });
        }

        private void OnErrorOccurred(object? sender, string error)
        {
            ErrorOccurred?.Invoke(this, new AudioErrorEventArgs
            {
                ErrorMessage = error
            });
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    StopCaptureAsync().Wait();
                    _audioCapture?.Dispose();
                }
                _disposed = true;
            }
        }
    }
}
