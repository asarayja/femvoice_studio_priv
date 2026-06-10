using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;

namespace FemVoiceStudio.Audio
{
    /// <summary>
    /// Hoved-service for sanntids lydanalyse.
    /// Kombinerer AudioCapture og PitchDetection og håndterer buffer-håndtering.
    /// </summary>
    public class AudioAnalyzerService : IDisposable
    {
        private readonly AudioCaptureService _audioCapture;
        private readonly PitchDetectionService _pitchDetector;
        
        // Buffer for analyseresultater
        private readonly List<PitchAnalysisResult> _analysisHistory;
        private readonly object _historyLock = new();
        private long _pitchDetectorCalledCount;
        private long _pitchSamplesCount;
        private long _pitchRejectedCount;
        private DateTime? _lastPitchDetectedTime;
        private DateTime _lastPitchDiagnosticLogTime = DateTime.MinValue;
        
        // Analyse-parametere
        private readonly int _analysisWindowMs;
        private readonly int _analysisOverlapMs;
        
        // Events
        public event EventHandler<PitchAnalysisResult>? PitchAnalyzed;
        public event EventHandler<string>? ErrorOccurred;
        
        // Status
        public bool IsAnalyzing => _audioCapture.IsRecording;
        public int SampleRate => _audioCapture.SampleRate;
        public AudioCaptureDiagnosticsSnapshot CaptureDiagnostics => _audioCapture.DiagnosticsSnapshot;
        public long PitchDetectorCalledCount => _pitchDetectorCalledCount;
        public long PitchSamplesCount => _pitchSamplesCount;
        public long PitchRejectedCount => _pitchRejectedCount;
        public double PitchDetectionSuccessRate => _pitchDetectorCalledCount == 0 ? 0 : (double)_pitchSamplesCount / _pitchDetectorCalledCount;
        
        // Expose HearOwnVoice from AudioCapture
        public bool HearOwnVoice
        {
            get => _audioCapture.HearOwnVoice;
            set => _audioCapture.HearOwnVoice = value;
        }
        
        // FIFO buffers for history (begrenset størrelse)
        private const int MaxHistorySize = 10000;
        
        public AudioAnalyzerService(int sampleRate = 44100, int analysisWindowMs = 50, int overlapMs = 25)
        {
            // FrontPage-taggen skiller forside-monitorens capture-logglinjer fra
            // øvelsesvinduets i den delte RC0-runtimeloggen.
            _audioCapture = new AudioCaptureService(sampleRate) { PipelineLabel = "FrontPage" };
            _pitchDetector = new PitchDetectionService(sampleRate);
            _analysisWindowMs = analysisWindowMs;
            _analysisOverlapMs = overlapMs;
            _analysisHistory = new List<PitchAnalysisResult>();
            
            _audioCapture.AudioDataAvailable += OnAudioDataAvailable;
            _audioCapture.ErrorOccurred += (s, e) => ErrorOccurred?.Invoke(this, e);
        }
        
        /// <summary>
        /// Initialiserer audio analyzer
        /// </summary>
        public void Initialize()
        {
            _audioCapture.Initialize();
            if (_audioCapture.CalibrationProfile != null)
                _pitchDetector.VoicedRmsThreshold = _audioCapture.CalibrationProfile.VoicedRmsThreshold;
        }
        
        /// <summary>
        /// Starter sanntids-analyse
        /// </summary>
        public void StartAnalysis()
        {
            _analysisHistory.Clear();
            _pitchDetectorCalledCount = 0;
            _pitchSamplesCount = 0;
            _pitchRejectedCount = 0;
            _lastPitchDetectedTime = null;
            _lastPitchDiagnosticLogTime = DateTime.MinValue;
            Rc0RuntimeLog.Write("FrontPagePitchMonitor", "Start Økt requested; starting AudioAnalyzerService");
            _audioCapture.StartRecording();
            if (!_audioCapture.IsRecording)
                throw new InvalidOperationException("Audio capture startet ikke. Analyse ble ikke startet.");
        }
        
        /// <summary>
        /// Stopper analysen og returnerer aggregert resultat
        /// </summary>
        public SessionAnalysis StopAnalysis()
        {
            _audioCapture.StopRecording();
            Rc0RuntimeLog.Write("FrontPagePitchMonitor",
                $"StopAnalysis; PitchDetectorCalls={_pitchDetectorCalledCount}; PitchSamples={_pitchSamplesCount}; " +
                $"PitchRejected={_pitchRejectedCount}; SuccessRate={PitchDetectionSuccessRate:P1}");
            return CalculateSessionAnalysis();
        }
        
        /// <summary>
        /// Håndterer innkommende audio data
        /// </summary>
        private void OnAudioDataAvailable(object? sender, float[] samples)
        {
            // Kjør pitch detection på buffer
            Task.Run(() =>
            {
                var result = _pitchDetector.DetectPitch(samples);
                Interlocked.Increment(ref _pitchDetectorCalledCount);
                if (result.IsVoiced && result.Pitch > 0)
                {
                    Interlocked.Increment(ref _pitchSamplesCount);
                    _lastPitchDetectedTime = DateTime.UtcNow;
                }
                else
                {
                    Interlocked.Increment(ref _pitchRejectedCount);
                }
                LogPitchDiagnosticsEverySecond(result);
                
                // Legg til historikk
                lock (_historyLock)
                {
                    _analysisHistory.Add(result);
                    if (_analysisHistory.Count > MaxHistorySize)
                    {
                        _analysisHistory.RemoveRange(0, _analysisHistory.Count - MaxHistorySize);
                    }
                }
                
                // Send resultat til subscribers
                PitchAnalyzed?.Invoke(this, result);
            });
        }

        private void LogPitchDiagnosticsEverySecond(PitchAnalysisResult result)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastPitchDiagnosticLogTime).TotalSeconds < 1)
                return;

            _lastPitchDiagnosticLogTime = now;
            var reason = result.IsVoiced
                ? ""
                : result.RmsValue < _pitchDetector.VoicedRmsThreshold
                    ? "rms below voiced threshold"
                    : "pitch detector confidence below acceptance";

            Rc0RuntimeLog.Write("PitchPipeline",
                $"PitchDetectorCalled=True; Pitch={result.Pitch:F1}; Confidence={result.Confidence:F3}; Rms={result.RmsValue:F5}; " +
                $"IsVoiced={result.IsVoiced}; PitchSamplesCount={_pitchSamplesCount}; PitchDetectionSuccessRate={PitchDetectionSuccessRate:P1}; " +
                $"PitchRejectedCount={_pitchRejectedCount}; RejectedReason=\"{reason}\"; LastPitchDetected={_lastPitchDetectedTime:O}");
        }
        
        /// <summary>
        /// Beregner aggregert analyse for hele økten
        /// </summary>
        public SessionAnalysis CalculateSessionAnalysis()
        {
            lock (_historyLock)
            {
                var validPitches = _analysisHistory
                    .Where(r => r.IsVoiced)
                    .Select(r => r.Pitch)
                    .ToList();
                    
                var analysis = new SessionAnalysis();
                
                if (validPitches.Count == 0)
                {
                    return analysis;
                }
                
                // Grunnleggende statistikk
                analysis.AveragePitch = validPitches.Average();
                analysis.MinPitch = validPitches.Min();
                analysis.MaxPitch = validPitches.Max();
                analysis.PitchVariationRange = analysis.MaxPitch - analysis.MinPitch;
                
                // Standard avvik
                double sumSquares = validPitches.Sum(p => Math.Pow(p - analysis.AveragePitch, 2));
                analysis.PitchStandardDeviation = Math.Sqrt(sumSquares / validPitches.Count);
                
                // Median
                var sorted = validPitches.OrderBy(p => p).ToList();
                analysis.MedianPitch = sorted[sorted.Count / 2];
                
                // Intensitet
                var intensities = _analysisHistory.Where(r => r.RmsValue > 0).Select(r => r.RmsValue).ToList();
                if (intensities.Count > 0)
                {
                    analysis.AverageIntensity = intensities.Average();
                }
                
                // Prosentandel med stemme
                analysis.VoicedPercentage = (double)validPitches.Count / _analysisHistory.Count * 100;
                
                // Intonasjon
                var intonation = PitchDetectionService.AnalyzeIntonation(validPitches.ToArray(), intensities.ToArray());
                analysis.IntonationRiseScore = intonation.RisingIntonationRatio;
                analysis.PitchVariationRange = intonation.PitchRangeSemitones;
                
                // Tid
                if (_analysisHistory.Count > 0)
                {
                    analysis.StartTime = _analysisHistory.First().Timestamp;
                    analysis.EndTime = _analysisHistory.Last().Timestamp;
                }
                
                return analysis;
            }
        }
        
        /// <summary>
        /// Hent nylige analyseresultater
        /// </summary>
        public List<PitchAnalysisResult> GetRecentResults(int count)
        {
            lock (_historyLock)
            {
                return _analysisHistory.TakeLast(count).ToList();
            }
        }
        
        public void Dispose()
        {
            _audioCapture.Dispose();
        }
    }
}
