using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FemVoiceStudio.Audio;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Subsystems.Analysis
{
    /// <summary>
    /// Analysis Subsystem - handles voice analysis and metrics calculation
    /// Provides real-time analysis of pitch, resonance, intonation, and health indicators
    /// </summary>
    public class AnalysisSubsystem : IAnalysisSubsystem
    {
        private readonly PitchDetectionService _pitchDetector;
        private readonly FormantDetectionService _formantDetector;
        private readonly VoiceActivityDetector _vad;
        private readonly VoiceStrainDetector _strainDetector;
        
        private readonly Queue<VoiceMetrics> _metricsHistory;
        private VoiceMetrics? _baseline;
        
        private const int MaxHistorySize = 500;

        public AnalysisSubsystem(int sampleRate = 44100)
        {
            _pitchDetector = new PitchDetectionService(sampleRate);
            _formantDetector = new FormantDetectionService(sampleRate);
            _vad = new VoiceActivityDetector(sampleRate);
            _strainDetector = new VoiceStrainDetector();
            _metricsHistory = new Queue<VoiceMetrics>();
        }

        public VoiceMetrics? CurrentMetrics { get; private set; }

        public async Task<VoiceMetrics> AnalyzeAsync(float[] samples, CancellationToken ct = default)
        {
            return await Task.Run(() =>
            {
                var metrics = new VoiceMetrics
                {
                    Timestamp = DateTime.Now
                };

                // Pitch detection
                var pitchResult = _pitchDetector.DetectPitch(samples);
                metrics.Pitch = pitchResult.Pitch;
                metrics.PitchConfidence = pitchResult.Confidence;
                metrics.IsVoiced = pitchResult.IsVoiced;
                metrics.RmsValue = pitchResult.RmsValue;
                metrics.Intensity = pitchResult.Intensity;

                // Formant detection
                var formantResult = _formantDetector.ExtractFormants(samples);
                metrics.F1 = formantResult.F1;
                metrics.F2 = formantResult.F2;
                metrics.F3 = formantResult.F3;
                metrics.SmoothedF1 = formantResult.SmoothedF1;
                metrics.SmoothedF2 = formantResult.SmoothedF2;

                // Resonance scoring
                if (metrics.F1 > 0 && metrics.F2 > 0)
                {
                    metrics.ResonanceScore = CalculateResonanceScore(metrics.F1, metrics.F2, formantResult.SpectralCentroid);
                    metrics.ResonanceCategory = ClassifyResonance(metrics.ResonanceScore);
                }

                // Voice activity detection
                metrics.IsSpeaking = _vad.Detect(samples);

                // Voice strain detection
                var strainResult = _strainDetector.Analyze(samples, metrics.Pitch);
                metrics.StrainLevel = strainResult.IsHighAmplitude || strainResult.IsPitchUnstable ? 
                    (strainResult.IsHighAmplitude && strainResult.IsPitchUnstable ? 0.8 : 0.5) : 0;
                metrics.Jitter = strainResult.JitterValue;
                metrics.Shimmer = strainResult.ShimmerValue;

                // Update current metrics
                CurrentMetrics = metrics;
                
                // Add to history
                _metricsHistory.Enqueue(metrics);
                if (_metricsHistory.Count > MaxHistorySize)
                {
                    _metricsHistory.Dequeue();
                }

                return metrics;
            }, ct);
        }

        public Task<VoiceMetrics> AnalyzeSessionAsync(TrainingSession session)
        {
            // For session-level analysis, create aggregate metrics
            return Task.Run(() =>
            {
                var metrics = new VoiceMetrics
                {
                    Timestamp = session.StartTime,
                    Pitch = session.AveragePitch,
                    MinPitch = session.MinPitch,
                    MaxPitch = session.MaxPitch,
                    PitchVariation = session.PitchVariation,
                    F1 = session.AverageF1,
                    F2 = session.AverageF2,
                    F3 = session.AverageF3,
                    ResonanceScore = session.ResonanceScore,
                    ResonanceCategory = session.ResonanceCategory,
                    SpectralCentroid = session.SpectralCentroid,
                    IntonationRiseScore = session.IntonationScore
                };
                
                return metrics;
            });
        }

        public TargetZone GetTargetZone(VoiceParameter parameter, DifficultyLevel level)
        {
            return parameter switch
            {
                VoiceParameter.Pitch => level switch
                {
                    DifficultyLevel.Nybegynner => new TargetZone { Parameter = parameter, MinValue = 140, MaxValue = 220, OptimalValue = 180 },
                    DifficultyLevel.Middels => new TargetZone { Parameter = parameter, MinValue = 155, MaxValue = 235, OptimalValue = 195 },
                    DifficultyLevel.Avansert => new TargetZone { Parameter = parameter, MinValue = 165, MaxValue = 255, OptimalValue = 210 },
                    _ => new TargetZone { Parameter = parameter, MinValue = 140, MaxValue = 220, OptimalValue = 180 }
                },
                VoiceParameter.Resonance => level switch
                {
                    DifficultyLevel.Nybegynner => new TargetZone { Parameter = parameter, MinValue = 30, MaxValue = 70, OptimalValue = 50 },
                    DifficultyLevel.Middels => new TargetZone { Parameter = parameter, MinValue = 45, MaxValue = 80, OptimalValue = 60 },
                    DifficultyLevel.Avansert => new TargetZone { Parameter = parameter, MinValue = 60, MaxValue = 90, OptimalValue = 75 },
                    _ => new TargetZone { Parameter = parameter, MinValue = 30, MaxValue = 70, OptimalValue = 50 }
                },
                VoiceParameter.Intonation => new TargetZone { Parameter = parameter, MinValue = 20, MaxValue = 80, OptimalValue = 50 },
                VoiceParameter.VoiceHealth => new TargetZone { Parameter = parameter, MinValue = 0, MaxValue = 30, OptimalValue = 0, IsPercentage = true },
                _ => new TargetZone { Parameter = parameter, MinValue = 0, MaxValue = 100, OptimalValue = 50 }
            };
        }

        public bool IsInTargetZone(VoiceMetrics metrics)
        {
            // Check pitch zone
            var pitchZone = GetTargetZone(VoiceParameter.Pitch, DifficultyLevel.Nybegynner);
            bool pitchInZone = metrics.Pitch >= pitchZone.MinValue && metrics.Pitch <= pitchZone.MaxValue;

            // Check resonance zone
            var resonanceZone = GetTargetZone(VoiceParameter.Resonance, DifficultyLevel.Nybegynner);
            bool resonanceInZone = metrics.ResonanceScore >= resonanceZone.MinValue && 
                                  metrics.ResonanceScore <= resonanceZone.MaxValue;

            // Check health
            bool healthy = metrics.StrainLevel < 0.5;

            return pitchInZone && resonanceInZone && healthy;
        }

        public HealthIndicators GetHealthIndicators(VoiceMetrics metrics)
        {
            var indicators = new HealthIndicators
            {
                StrainLevel = metrics.StrainLevel,
                Jitter = metrics.Jitter,
                Shimmer = metrics.Shimmer
            };

            if (metrics.StrainLevel >= 0.7)
            {
                indicators.IsHealthy = false;
                indicators.RequiresBreak = true;
                indicators.WarningMessage = "High vocal strain detected. Please take a break.";
            }
            else if (metrics.StrainLevel >= 0.5)
            {
                indicators.IsHealthy = false;
                indicators.RequiresBreak = false;
                indicators.WarningMessage = "Moderate strain detected. Consider reducing intensity.";
            }
            else if (metrics.Jitter > 2.0 || metrics.Shimmer > 0.5)
            {
                indicators.IsHealthy = false;
                indicators.WarningMessage = "Voice instability detected. Focus on relaxed production.";
            }
            else
            {
                indicators.IsHealthy = true;
            }

            return indicators;
        }

        public void UpdateBaseline(VoiceMetrics baseline)
        {
            _baseline = baseline;
        }

        public VoiceMetrics GetSmoothedMetrics(int windowSizeMs = 500)
        {
            if (_metricsHistory.Count == 0)
                return new VoiceMetrics();

            // Estimate samples per window (assuming ~50ms analysis windows)
            int windowSize = windowSizeMs / 50;
            var recent = _metricsHistory.TakeLast(Math.Min(windowSize, _metricsHistory.Count)).ToList();

            if (recent.Count == 0)
                return new VoiceMetrics();

            return new VoiceMetrics
            {
                Timestamp = DateTime.Now,
                Pitch = recent.Average(m => m.Pitch),
                F1 = recent.Average(m => m.F1),
                F2 = recent.Average(m => m.F2),
                F3 = recent.Average(m => m.F3),
                ResonanceScore = recent.Average(m => m.ResonanceScore),
                StrainLevel = recent.Average(m => m.StrainLevel),
                RmsValue = recent.Average(m => m.RmsValue)
            };
        }

        private double CalculateResonanceScore(double f1, double f2, double spectralCentroid)
        {
            double score = 0;

            // F1 scoring: target 280-450 Hz for feminine
            if (f1 >= 280 && f1 <= 450)
            {
                double optimalDistance = Math.Abs(f1 - 330);
                score += 40 * (1 - optimalDistance / 170);
            }

            // F2 scoring: target 1800-2600 Hz for feminine
            if (f2 >= 1800 && f2 <= 2600)
            {
                double optimalDistance = Math.Abs(f2 - 2200);
                score += 40 * (1 - optimalDistance / 400);
            }

            // Spectral centroid bonus
            if (spectralCentroid > 2000)
            {
                score += 20 * Math.Min(1, (spectralCentroid - 2000) / 1500);
            }

            return Math.Min(100, Math.Max(0, score));
        }

        private ResonanceCategory ClassifyResonance(double resonanceScore)
        {
            return resonanceScore switch
            {
                >= 60 => ResonanceCategory.Forward,
                >= 40 => ResonanceCategory.Neutral,
                _ => ResonanceCategory.Back
            };
        }
    }
}
