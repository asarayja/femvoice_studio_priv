using System;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    public enum HealthSafetyState
    {
        Normal,
        Caution,
        Restrict,
        Lock
    }

    public sealed record VocalHealthSupervisorOptions
    {
        public double SpikeLimit { get; init; } = 0.35;
        public double NoiseFloor { get; init; } = 0.015;
        public double MicroAlpha { get; init; } = 0.35;
        public double MesoAlpha { get; init; } = 0.08;
        public int StrainSamplesForRestrict { get; init; } = 3;
        public int RestrictCyclesForLock { get; init; } = 3;
        public int StableSamplesForRecovery { get; init; } = 5;
        public double StabilityDropForStrain { get; init; } = 0.18;
        public double StabilityDriftForFatigue { get; init; } = 0.09;
        public double ResonanceDriftForFatigue { get; init; } = 0.09;
        public double HydrationResonanceDrift { get; init; } = 0.06;
        public double BaselineResonance { get; init; } = 0.70;
        public double BaselineStability { get; init; } = 0.70;
    }

    public sealed record VocalHealthDecision
    {
        public HealthSafetyState State { get; init; }
        public string ReasonCode { get; init; } = "NORMAL";
        public bool StrainDetected { get; init; }
        public bool FatigueDetected { get; init; }
        public bool PauseRecommended { get; init; }
        public bool HydrationSuggested { get; init; }
        public double StrainScore { get; init; }
        public double FatigueScore { get; init; }
        public double HydrationScore { get; init; }
        public DateTime Timestamp { get; init; }
    }

    public sealed class VocalHealthSupervisor
    {
        private readonly object _sync = new();
        private readonly VocalHealthSupervisorOptions _options;
        private readonly VocalHealthTrendEngine _trendEngine;
        private HealthSafetyState _currentState = HealthSafetyState.Normal;
        private int _strainSamples;
        private int _fatigueSamples;
        private int _restrictCycles;
        private int _stableSamples;
        private int _comfortBreaches;
        private double _lastHoldProgress;

        public VocalHealthSupervisor(VocalHealthSupervisorOptions? options = null)
        {
            _options = options ?? new VocalHealthSupervisorOptions();
            _trendEngine = new VocalHealthTrendEngine(_options);
        }

        public HealthSafetyState CurrentState
        {
            get { lock (_sync) return _currentState; }
        }

        public event EventHandler<VocalHealthDecision>? HealthStateUpdated;
        public event EventHandler<VocalHealthDecision>? StrainDetected;
        public event EventHandler<VocalHealthDecision>? FatigueDetected;
        public event EventHandler<VocalHealthDecision>? PauseRecommended;
        public event EventHandler<VocalHealthDecision>? HydrationSuggested;
        public event EventHandler<VocalHealthDecision>? RestrictTriggered;
        public event EventHandler<VocalHealthDecision>? LockTriggered;

        public VocalHealthDecision Evaluate(ExerciseLiveState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            VocalHealthDecision decision;
            lock (_sync)
            {
                var trend = _trendEngine.Update(state);
                var strainScore = EvaluateStrain(state, trend);
                var fatigueScore = EvaluateFatigue(trend);
                var hydrationScore = EvaluateHydration(trend, fatigueScore);

                var strainDetected = strainScore >= 0.65;
                var fatigueDetected = fatigueScore >= 0.65;
                var pauseRecommended = fatigueDetected || _strainSamples >= _options.StrainSamplesForRestrict;
                var hydrationSuggested = hydrationScore >= 0.65 && _currentState != HealthSafetyState.Lock;

                _strainSamples = strainDetected ? _strainSamples + 1 : Math.Max(0, _strainSamples - 1);
                _fatigueSamples = fatigueDetected ? _fatigueSamples + 1 : Math.Max(0, _fatigueSamples - 1);

                var nextState = EvaluateState(state, strainDetected, fatigueDetected);
                var reasonCode = DetermineReasonCode(nextState, strainDetected, fatigueDetected, pauseRecommended, hydrationSuggested);

                var stable = !state.IsSafetyLocked
                    && ((!strainDetected && !fatigueDetected)
                        || (state.PrimaryMetricScore >= _options.BaselineResonance
                            && state.StabilityScore >= _options.BaselineStability));
                _stableSamples = stable ? _stableSamples + 1 : 0;
                if (_stableSamples >= _options.StableSamplesForRecovery)
                {
                    nextState = Deescalate(nextState);
                    _restrictCycles = Math.Max(0, _restrictCycles - 1);
                    _stableSamples = 0;
                }

                _currentState = nextState;
                _lastHoldProgress = state.HoldProgress;

                decision = new VocalHealthDecision
                {
                    State = _currentState,
                    ReasonCode = reasonCode,
                    StrainDetected = strainDetected,
                    FatigueDetected = fatigueDetected,
                    PauseRecommended = pauseRecommended,
                    HydrationSuggested = hydrationSuggested,
                    StrainScore = strainScore,
                    FatigueScore = fatigueScore,
                    HydrationScore = hydrationScore,
                    Timestamp = state.Timestamp == default ? DateTime.Now : state.Timestamp
                };
            }

            Publish(decision);
            return decision;
        }

        public void Reset()
        {
            lock (_sync)
            {
                _trendEngine.Reset();
                _currentState = HealthSafetyState.Normal;
                _strainSamples = 0;
                _fatigueSamples = 0;
                _restrictCycles = 0;
                _stableSamples = 0;
                _comfortBreaches = 0;
                _lastHoldProgress = 0;
            }
        }

        private double EvaluateStrain(ExerciseLiveState state, VocalHealthTrend trend)
        {
            _comfortBreaches = !state.IsInComfortZone ? _comfortBreaches + 1 : Math.Max(0, _comfortBreaches - 1);
            var suddenHoldCollapse = _lastHoldProgress - state.HoldProgress >= 0.35;
            var microInstability = Math.Max(0, _options.BaselineStability - trend.MicroStability);
            var acuteDrop = Math.Max(0, trend.PreviousMicroStability - trend.MicroStability);

            var score = 0.0;
            if (state.IsSafetyLocked) score += 0.80;
            if (suddenHoldCollapse) score += 0.65;
            if (_comfortBreaches >= 3) score += 0.30;
            if (microInstability >= _options.StabilityDropForStrain) score += 0.35;
            if (acuteDrop >= _options.StabilityDropForStrain) score += 0.30;

            return Math.Clamp(score, 0, 1);
        }

        private double EvaluateFatigue(VocalHealthTrend trend)
        {
            var stabilityDrift = Math.Max(0, _options.BaselineStability - trend.MesoStability);
            var resonanceDrift = Math.Max(0, _options.BaselineResonance - trend.MesoResonance);
            var score = 0.0;

            if (stabilityDrift >= _options.StabilityDriftForFatigue) score += 0.45;
            if (resonanceDrift >= _options.ResonanceDriftForFatigue) score += 0.35;
            if (trend.MesoStabilitySlope < -0.03) score += 0.20;
            if (trend.MesoResonanceSlope < -0.03) score += 0.20;

            return Math.Clamp(score, 0, 1);
        }

        private double EvaluateHydration(VocalHealthTrend trend, double fatigueScore)
        {
            var resonanceDrift = Math.Max(0, _options.BaselineResonance - trend.MesoResonance);
            var stabilityVariance = trend.MicroStabilityVariance;
            var score = 0.0;

            if (resonanceDrift >= _options.HydrationResonanceDrift) score += 0.45;
            if (stabilityVariance >= 0.03) score += 0.25;
            if (fatigueScore >= 0.45) score += 0.25;

            return Math.Clamp(score, 0, 1);
        }

        private HealthSafetyState EvaluateState(ExerciseLiveState state, bool strainDetected, bool fatigueDetected)
        {
            if (state.IsSafetyLocked)
            {
                _restrictCycles++;
                return _restrictCycles >= _options.RestrictCyclesForLock ? HealthSafetyState.Lock : HealthSafetyState.Restrict;
            }

            if (_currentState == HealthSafetyState.Restrict && strainDetected)
            {
                _restrictCycles++;
            }

            if (_restrictCycles >= _options.RestrictCyclesForLock)
            {
                return HealthSafetyState.Lock;
            }

            if (_strainSamples >= _options.StrainSamplesForRestrict || (strainDetected && fatigueDetected))
            {
                _restrictCycles++;
                return HealthSafetyState.Restrict;
            }

            if (strainDetected || fatigueDetected)
            {
                return HealthSafetyState.Caution;
            }

            return _currentState switch
            {
                HealthSafetyState.Lock => HealthSafetyState.Lock,
                HealthSafetyState.Restrict => HealthSafetyState.Restrict,
                _ => HealthSafetyState.Normal
            };
        }

        private static HealthSafetyState Deescalate(HealthSafetyState state)
        {
            return state switch
            {
                HealthSafetyState.Lock => HealthSafetyState.Restrict,
                HealthSafetyState.Restrict => HealthSafetyState.Caution,
                HealthSafetyState.Caution => HealthSafetyState.Normal,
                _ => HealthSafetyState.Normal
            };
        }

        private static string DetermineReasonCode(
            HealthSafetyState state,
            bool strainDetected,
            bool fatigueDetected,
            bool pauseRecommended,
            bool hydrationSuggested)
        {
            if (state == HealthSafetyState.Lock) return "HEALTH_LOCK";
            if (state == HealthSafetyState.Restrict) return "HEALTH_RESTRICT";
            if (strainDetected) return "STRAIN_DETECTED";
            if (pauseRecommended) return "PAUSE_RECOMMENDED";
            if (fatigueDetected) return "FATIGUE_DETECTED";
            if (hydrationSuggested) return "HYDRATION_SUGGESTED";
            return "NORMAL";
        }

        private void Publish(VocalHealthDecision decision)
        {
            HealthStateUpdated?.Invoke(this, decision);
            if (decision.StrainDetected) StrainDetected?.Invoke(this, decision);
            if (decision.FatigueDetected) FatigueDetected?.Invoke(this, decision);
            if (decision.PauseRecommended) PauseRecommended?.Invoke(this, decision);
            if (decision.HydrationSuggested) HydrationSuggested?.Invoke(this, decision);
            if (decision.State == HealthSafetyState.Restrict) RestrictTriggered?.Invoke(this, decision);
            if (decision.State == HealthSafetyState.Lock) LockTriggered?.Invoke(this, decision);
        }
    }

    public sealed record VocalHealthTrend
    {
        public double MicroResonance { get; init; }
        public double MicroStability { get; init; }
        public double MesoResonance { get; init; }
        public double MesoStability { get; init; }
        public double PreviousMicroStability { get; init; }
        public double MesoResonanceSlope { get; init; }
        public double MesoStabilitySlope { get; init; }
        public double MicroStabilityVariance { get; init; }
        public int SampleCount { get; init; }
    }

    public sealed class VocalHealthTrendEngine
    {
        private readonly VocalHealthSupervisorOptions _options;
        private double _microResonance;
        private double _microStability;
        private double _mesoResonance;
        private double _mesoStability;
        private double _previousMicroStability;
        private double _previousMesoResonance;
        private double _previousMesoStability;
        private double _stabilityVarianceEma;
        private int _sampleCount;

        public VocalHealthTrendEngine(VocalHealthSupervisorOptions? options = null)
        {
            _options = options ?? new VocalHealthSupervisorOptions();
        }

        public VocalHealthTrend Update(ExerciseLiveState state)
        {
            var resonance = Clean(state.PrimaryMetricScore);
            var stability = Clean(state.StabilityScore);

            if (_sampleCount == 0)
            {
                _microResonance = _mesoResonance = resonance;
                _microStability = _mesoStability = stability;
            }
            else
            {
                resonance = LimitSpike(resonance, _microResonance);
                stability = LimitSpike(stability, _microStability);

                _previousMicroStability = _microStability;
                _previousMesoResonance = _mesoResonance;
                _previousMesoStability = _mesoStability;

                _microResonance = Ema(_microResonance, resonance, _options.MicroAlpha);
                _microStability = Ema(_microStability, stability, _options.MicroAlpha);
                _mesoResonance = Ema(_mesoResonance, resonance, _options.MesoAlpha);
                _mesoStability = Ema(_mesoStability, stability, _options.MesoAlpha);
                _stabilityVarianceEma = Ema(_stabilityVarianceEma, Math.Abs(stability - _microStability), 0.20);
            }

            _sampleCount++;

            return new VocalHealthTrend
            {
                MicroResonance = _microResonance,
                MicroStability = _microStability,
                MesoResonance = _mesoResonance,
                MesoStability = _mesoStability,
                PreviousMicroStability = _previousMicroStability,
                MesoResonanceSlope = _mesoResonance - _previousMesoResonance,
                MesoStabilitySlope = _mesoStability - _previousMesoStability,
                MicroStabilityVariance = _stabilityVarianceEma,
                SampleCount = _sampleCount
            };
        }

        public void Reset()
        {
            _microResonance = 0;
            _microStability = 0;
            _mesoResonance = 0;
            _mesoStability = 0;
            _previousMicroStability = 0;
            _previousMesoResonance = 0;
            _previousMesoStability = 0;
            _stabilityVarianceEma = 0;
            _sampleCount = 0;
        }

        private double Clean(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return 0;
            return Math.Clamp(value, 0, 1);
        }

        private double LimitSpike(double value, double previous)
        {
            if (Math.Abs(value - previous) <= _options.SpikeLimit)
            {
                return Math.Abs(value - previous) < _options.NoiseFloor ? previous : value;
            }

            return previous + Math.Sign(value - previous) * _options.SpikeLimit;
        }

        private static double Ema(double previous, double value, double alpha)
            => previous + alpha * (value - previous);
    }
}
