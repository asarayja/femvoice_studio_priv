using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FemVoiceStudio.Models;
using VoiceAnalysisMetrics = FemVoiceStudio.Subsystems.Analysis.VoiceMetrics;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Kjernesystemet som evaluerer ovelsesutfoersel i sanntid.
    /// </summary>
    public class ExerciseFeedbackEngine : IDisposable
    {
        private readonly ConcurrentQueue<VoiceMetrics> _metricsQueue = new();
        private CancellationTokenSource? _cts;
        private Task? _evaluationTask;
        private bool _isRunning;
        private bool _isPaused;
        private readonly int _evaluationIntervalMs;
        private readonly List<ExerciseEvaluationResult> _sessionResults = new();
        private readonly List<VoiceMetrics> _sessionMetrics = new();
        private int _consecutiveWarnings;
        private double _lastAmplitude;
        private DateTime _lastAmplitudeTime;
        private readonly object _resultsLock = new();
        
        public ExerciseDefinition? CurrentDefinition { get; private set; }
        public UserLevel CurrentUserLevel { get; private set; } = UserLevel.Middels;
        public bool IsRunning => _isRunning;
        public bool IsPaused => _isPaused;
        
        public event EventHandler<ExerciseEvaluationResult>? EvaluationCompleted;
        public event EventHandler<ExerciseEvaluationResult>? HealthWarning;
        public event EventHandler<ExerciseEvaluationResult>? HealthCritical;
        
        public ExerciseFeedbackEngine() : this(50) { }
        
        public ExerciseFeedbackEngine(int evaluationIntervalMs)
        {
            _evaluationIntervalMs = evaluationIntervalMs;
        }
        
        public void Start(ExerciseDefinition definition, UserLevel userLevel)
        {
            if (_isRunning) Stop();
            
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            
            CurrentDefinition = definition;
            CurrentUserLevel = userLevel;
            _sessionResults.Clear();
            _sessionMetrics.Clear();
            _consecutiveWarnings = 0;
            _lastAmplitude = 0;
            _isPaused = false;
            
            _cts = new CancellationTokenSource();
            _evaluationTask = Task.Run(() => EvaluationLoop(_cts.Token), _cts.Token);
            _isRunning = true;
        }
        
        public void Stop()
        {
            if (!_isRunning) return;
            _cts?.Cancel();
            try { _evaluationTask?.Wait(1000); }
            catch (AggregateException) { }
            _isRunning = false;
            _cts?.Dispose();
            _cts = null;
            _evaluationTask = null;
        }
        
        public void Pause() => _isPaused = true;
        public void Resume() => _isPaused = false;
        
        public void AddMetrics(VoiceMetrics metrics)
        {
            if (metrics == null || !_isRunning || _isPaused) return;
            _metricsQueue.Enqueue(metrics);
            lock (_resultsLock) { _sessionMetrics.Add(metrics); }
        }
        
        public SessionEvaluationSummary GetSessionSummary()
        {
            lock (_resultsLock) { return CalculateSessionSummary(); }
        }
        
        public List<ExerciseEvaluationResult> GetResults()
        {
            lock (_resultsLock) { return new List<ExerciseEvaluationResult>(_sessionResults); }
        }
        
        public ExerciseEvaluationResult EvaluateMetrics(VoiceMetrics metrics)
        {
            if (CurrentDefinition == null)
                return ExerciseEvaluationResult.Correct("ExerciseFeedback_NoDefinition");
            
            try
            {
                var healthResult = CheckHealth(metrics);
                if (healthResult != null) return healthResult;
                return EvaluateParameters(metrics);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in EvaluateMetrics: {ex.Message}");
                return ExerciseEvaluationResult.Correct("ExerciseFeedback_Error");
            }
        }
        
        private async Task EvaluationLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_evaluationIntervalMs, ct);
                    if (_isPaused || _metricsQueue.IsEmpty) continue;
                    
                    VoiceMetrics? latestMetrics = null;
                    while (_metricsQueue.TryDequeue(out var m)) latestMetrics = m;
                    if (latestMetrics == null) continue;
                    
                    var result = EvaluateMetrics(latestMetrics);
                    lock (_resultsLock) { _sessionResults.Add(result); }
                    EvaluationCompleted?.Invoke(this, result);
                    
                    if (result.HealthIndicator == HealthIndicator.Warning)
                    {
                        _consecutiveWarnings++;
                        HealthWarning?.Invoke(this, result);
                        if (CurrentDefinition != null && _consecutiveWarnings >= CurrentDefinition.HealthThresholds.ConsecutiveWarningLimit)
                            HealthCritical?.Invoke(this, result);
                    }
                    else if (result.HealthIndicator == HealthIndicator.Critical)
                    {
                        _consecutiveWarnings++;
                        HealthCritical?.Invoke(this, result);
                    }
                    else { _consecutiveWarnings = 0; }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error in evaluation loop: {ex.Message}"); }
            }
        }
        
        private ExerciseEvaluationResult? CheckHealth(VoiceMetrics metrics)
        {
            if (CurrentDefinition == null) return null;
            var thresholds = CurrentDefinition.HealthThresholds;
            var now = DateTime.Now;
            
            if (metrics.Jitter > thresholds.MaxJitterPercent)
                return ExerciseEvaluationResult.Stop("HealthWarning_Jitter", HealthIndicator.Critical);
            
            if (metrics.Shimmer > thresholds.MaxShimmerPercent)
                return ExerciseEvaluationResult.Stop("HealthWarning_Shimmer", HealthIndicator.Critical);
            
            if (_lastAmplitude > 0)
            {
                var amplitudeChange = Math.Abs((metrics.Intensity - _lastAmplitude) / _lastAmplitude) * 100;
                var timeSinceLast = (now - _lastAmplitudeTime).TotalMilliseconds;
                if (amplitudeChange > thresholds.MaxAmplitudeSpikePercent && timeSinceLast < 500)
                {
                    var indicator = _consecutiveWarnings > 0 ? HealthIndicator.Critical : HealthIndicator.Warning;
                    return ExerciseEvaluationResult.Stop("HealthWarning_Amplitude", indicator);
                }
            }
            
            _lastAmplitude = metrics.Intensity;
            _lastAmplitudeTime = now;
            
            if (CurrentDefinition != null)
            {
                var effectivePitchMax = CurrentDefinition.TargetPitchRange.Max + CurrentDefinition.GetEffectivePitchTolerance(CurrentUserLevel);
                if (metrics.Pitch > effectivePitchMax + thresholds.PitchPressThresholdHz && 
                    (metrics.Jitter > 1.5 || metrics.Shimmer > 2.0))
                    return ExerciseEvaluationResult.Warning("HealthWarning_PitchPress");
            }
            
            return null;
        }
        
        private ExerciseEvaluationResult EvaluateParameters(VoiceMetrics metrics)
        {
            if (CurrentDefinition == null) return ExerciseEvaluationResult.Correct();
            
            var details = new Dictionary<string, object>();
            var pitchStatus = EvaluatePitch(metrics, details);
            var resonanceStatus = EvaluateResonance(metrics, details);
            var stabilityStatus = EvaluateStability(metrics, details);
            var intonationStatus = EvaluateIntonation(metrics, details);
            
            var overallStatus = EvaluationStatus.Correct;
            var hintKey = "";
            
            if (pitchStatus == EvaluationStatus.Stop || resonanceStatus == EvaluationStatus.Stop || stabilityStatus == EvaluationStatus.Stop)
                overallStatus = EvaluationStatus.Stop;
            else if (pitchStatus == EvaluationStatus.Adjust || resonanceStatus == EvaluationStatus.Adjust || 
                     stabilityStatus == EvaluationStatus.Adjust || intonationStatus == EvaluationStatus.Adjust)
            {
                overallStatus = EvaluationStatus.Adjust;
                hintKey = GetAdjustmentHint(pitchStatus, resonanceStatus, stabilityStatus, intonationStatus);
            }
            else { hintKey = "ExerciseFeedback_Correct"; }
            
            return new ExerciseEvaluationResult
            {
                Status = overallStatus,
                ResonanceStatus = resonanceStatus,
                PitchStatus = pitchStatus,
                StabilityStatus = stabilityStatus,
                IntonationStatus = intonationStatus,
                HealthIndicator = HealthIndicator.Safe,
                CoachHintKey = hintKey,
                Timestamp = DateTime.Now,
                Details = details
            };
        }
        
        private EvaluationStatus EvaluatePitch(VoiceMetrics metrics, Dictionary<string, object> details)
        {
            if (CurrentDefinition == null || metrics.Pitch <= 0)
            {
                details["PitchStatus"] = "N/A";
                return EvaluationStatus.NotApplicable;
            }
            
            var effectiveMin = CurrentDefinition.TargetPitchRange.Min - CurrentDefinition.GetEffectivePitchTolerance(CurrentUserLevel);
            var effectiveMax = CurrentDefinition.TargetPitchRange.Max + CurrentDefinition.GetEffectivePitchTolerance(CurrentUserLevel);
            var isInRange = metrics.Pitch >= effectiveMin && metrics.Pitch <= effectiveMax;
            
            details["Pitch"] = Math.Round(metrics.Pitch, 1);
            details["PitchRange"] = $"{Math.Round(effectiveMin, 0)}-{Math.Round(effectiveMax, 0)}";
            details["PitchStatus"] = isInRange ? "Correct" : "Adjust";
            
            return isInRange ? EvaluationStatus.Correct : EvaluationStatus.Adjust;
        }
        
        private EvaluationStatus EvaluateResonance(VoiceMetrics metrics, Dictionary<string, object> details)
        {
            if (CurrentDefinition == null)
            {
                details["ResonanceStatus"] = "N/A";
                return EvaluationStatus.NotApplicable;
            }
            
            var effectiveF2Tolerance = CurrentDefinition.GetEffectiveResonanceToleranceF2(CurrentUserLevel);
            var effectiveF2Min = CurrentDefinition.TargetF2Range.Min - effectiveF2Tolerance;
            var effectiveF2Max = CurrentDefinition.TargetF2Range.Max + effectiveF2Tolerance;
            var isF2InRange = metrics.F2 >= effectiveF2Min && metrics.F2 <= effectiveF2Max;
            
            details["F1"] = Math.Round(metrics.F1, 0);
            details["F2"] = Math.Round(metrics.F2, 0);
            details["F3"] = Math.Round(metrics.F3, 0);
            details["F2Range"] = $"{Math.Round(effectiveF2Min, 0)}-{Math.Round(effectiveF2Max, 0)}";
            details["ResonanceStatus"] = isF2InRange ? "Correct" : "Adjust";
            
            return isF2InRange ? EvaluationStatus.Correct : EvaluationStatus.Adjust;
        }
        
        private EvaluationStatus EvaluateStability(VoiceMetrics metrics, Dictionary<string, object> details)
        {
            if (CurrentDefinition == null || metrics.Jitter < 0)
            {
                details["StabilityStatus"] = "N/A";
                return EvaluationStatus.NotApplicable;
            }
            
            var effectiveThreshold = CurrentDefinition.GetEffectiveStabilityThreshold(CurrentUserLevel);
            var isStable = metrics.Jitter <= effectiveThreshold;
            
            details["Jitter"] = Math.Round(metrics.Jitter, 2);
            details["JitterThreshold"] = effectiveThreshold;
            details["StabilityStatus"] = isStable ? "Correct" : "Adjust";
            
            return isStable ? EvaluationStatus.Correct : EvaluationStatus.Adjust;
        }
        
        private EvaluationStatus EvaluateIntonation(VoiceMetrics metrics, Dictionary<string, object> details)
        {
            if (CurrentDefinition == null || !CurrentDefinition.RequiresIntonation)
            {
                details["IntonationStatus"] = "N/A";
                return EvaluationStatus.NotApplicable;
            }
            
            var hasVariation = metrics.IntonationRange > 5;
            details["IntonationRange"] = Math.Round(metrics.IntonationRange, 1);
            details["IntonationStatus"] = hasVariation ? "Correct" : "Adjust";
            
            return hasVariation ? EvaluationStatus.Correct : EvaluationStatus.Adjust;
        }
        
        private string GetAdjustmentHint(EvaluationStatus pitch, EvaluationStatus resonance, 
                                        EvaluationStatus stability, EvaluationStatus intonation)
        {
            if (resonance == EvaluationStatus.Adjust) return "ExerciseFeedback_AdjustResonance";
            if (pitch == EvaluationStatus.Adjust) return "ExerciseFeedback_AdjustPitch";
            if (stability == EvaluationStatus.Adjust) return "ExerciseFeedback_AdjustStability";
            if (intonation == EvaluationStatus.Adjust) return "ExerciseFeedback_AdjustIntonation";
            return "ExerciseFeedback_Adjust";
        }
        
        private SessionEvaluationSummary CalculateSessionSummary()
        {
            var summary = new SessionEvaluationSummary();
            if (_sessionResults.Count == 0) return summary;
            
            int pitchCorrect = 0, resonanceCorrect = 0, stabilityCorrect = 0, intonationCorrect = 0;
            int pitchTotal = 0, resonanceTotal = 0, stabilityTotal = 0, intonationTotal = 0;
            
            foreach (var r in _sessionResults)
            {
                if (r.PitchStatus != EvaluationStatus.NotApplicable) { pitchTotal++; if (r.PitchStatus == EvaluationStatus.Correct) pitchCorrect++; }
                if (r.ResonanceStatus != EvaluationStatus.NotApplicable) { resonanceTotal++; if (r.ResonanceStatus == EvaluationStatus.Correct) resonanceCorrect++; }
                if (r.StabilityStatus != EvaluationStatus.NotApplicable) { stabilityTotal++; if (r.StabilityStatus == EvaluationStatus.Correct) stabilityCorrect++; }
                if (r.IntonationStatus != EvaluationStatus.NotApplicable) { intonationTotal++; if (r.IntonationStatus == EvaluationStatus.Correct) intonationCorrect++; }
                if (r.HealthIndicator == HealthIndicator.Critical || r.Status == EvaluationStatus.Stop) summary.HealthStopCount++;
            }
            
            summary.PitchCorrectPercent = pitchTotal > 0 ? pitchCorrect * 100.0 / pitchTotal : 100;
            summary.ResonanceCorrectPercent = resonanceTotal > 0 ? resonanceCorrect * 100.0 / resonanceTotal : 100;
            summary.StabilityCorrectPercent = stabilityTotal > 0 ? stabilityCorrect * 100.0 / stabilityTotal : 100;
            summary.IntonationCorrectPercent = intonationTotal > 0 ? intonationCorrect * 100.0 / intonationTotal : 100;
            
            double sumF1 = 0, sumF2 = 0, sumF3 = 0, sumPitch = 0, sumJitter = 0, sumShimmer = 0, sumStrain = 0;
            foreach (var m in _sessionMetrics)
            {
                sumF1 += m.F1; sumF2 += m.F2; sumF3 += m.F3;
                sumPitch += m.Pitch; sumJitter += m.Jitter; sumShimmer += m.Shimmer; sumStrain += m.StrainLevel;
            }
            int mCount = _sessionMetrics.Count > 0 ? _sessionMetrics.Count : 1;
            summary.AverageF1 = sumF1 / mCount;
            summary.AverageF2 = sumF2 / mCount;
            summary.AverageF3 = sumF3 / mCount;
            summary.AveragePitch = sumPitch / mCount;
            summary.AverageJitter = sumJitter / mCount;
            summary.AverageShimmer = sumShimmer / mCount;
            summary.AverageStrainLevel = sumStrain / mCount;
            
            summary.StrainLevel = SessionEvaluationSummary.CalculateStrainLevel(summary.AverageShimmer, summary.AverageStrainLevel);
            summary.OverallScore = SessionEvaluationSummary.CalculateOverallScore(
                summary.ResonanceCorrectPercent, summary.PitchCorrectPercent, summary.StabilityCorrectPercent, 1.0);
            
            if (_sessionResults.Count > 0)
            {
                summary.StartTime = _sessionResults[0].Timestamp;
                summary.EndTime = _sessionResults[^1].Timestamp;
            }
            
            return summary;
        }
        
        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }
    }
}
