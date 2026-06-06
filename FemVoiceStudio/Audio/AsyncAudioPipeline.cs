using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Audio
{
    /// <summary>
    /// Asynkron audio pipeline med backpressure og cancellation
    /// </summary>
    public class AsyncAudioPipeline : IDisposable
    {
        private readonly CancellationTokenSource _cts;
        private readonly BlockingCollection<float[]> _audioQueue;
        private readonly Task _processingTask;
        
        public event EventHandler<PitchAnalysisResult>? PitchDetected;
        public event EventHandler<string>? ErrorOccurred;
        
        private readonly AdaptivePitchDetector _pitchDetector;
        private readonly VoiceActivityDetector _vad;
        private bool _isRunning;
        
        public bool IsRunning => _isRunning;
        
        public AsyncAudioPipeline(int maxQueueSize = 100)
        {
            _cts = new CancellationTokenSource();
            _audioQueue = new BlockingCollection<float[]>(new ConcurrentQueue<float[]>(), maxQueueSize);
            _pitchDetector = new AdaptivePitchDetector();
            _vad = new VoiceActivityDetector();
            
            _processingTask = Task.Run(ProcessAudioQueue);
        }
        
        public void EnqueueAudio(float[] samples)
        {
            if (!_isRunning || _cts.Token.IsCancellationRequested)
                return;
                
            try
            {
                if (!_audioQueue.IsAddingCompleted)
                {
                    var copy = new float[samples.Length];
                    Array.Copy(samples, copy, samples.Length);
                    _audioQueue.Add(copy, _cts.Token);
                }
            }
            catch (InvalidOperationException) { }
            catch (OperationCanceledException) { }
        }
        
        public void Start()
        {
            _isRunning = true;
        }
        
        public void Stop()
        {
            _isRunning = false;
            _audioQueue.CompleteAdding();
        }
        
        public void Calibrate(float[] backgroundNoise)
        {
            _pitchDetector.Calibrate(backgroundNoise);
            _vad.Calibrate(backgroundNoise);
        }
        
        private async Task ProcessAudioQueue()
        {
            try
            {
                foreach (var samples in _audioQueue.GetConsumingEnumerable(_cts.Token))
                {
                    if (!_isRunning) break;
                    
                    try
                    {
                        var result = await Task.Run(() => _pitchDetector.DetectPitch(samples));
                        
                        var vadResult = _vad.Detect(samples);
                        
                        PitchDetected?.Invoke(this, new Models.PitchAnalysisResult
                        {
                            Timestamp = result.Timestamp,
                            Pitch = result.Pitch,
                            RmsValue = result.RmsValue,
                            IsVoiced = result.IsVoiced && vadResult,
                            Confidence = result.Confidence
                        });
                    }
                    catch (Exception ex)
                    {
                        ErrorOccurred?.Invoke(this, $"Pitch detection error: {ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException) { }
        }
        
        public void Dispose()
        {
            _cts.Cancel();
            _audioQueue.CompleteAdding();
            _processingTask.Wait(TimeSpan.FromSeconds(2));
            _cts.Dispose();
            _audioQueue.Dispose();
        }
    }
}
