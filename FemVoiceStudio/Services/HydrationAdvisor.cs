using System;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    public sealed record HydrationAdvisorOptions
    {
        public double SpikeLimit { get; init; } = 0.35;
        public double NoiseFloor { get; init; } = 0.015;
        public double MicroAlpha { get; init; } = 0.35;
        public double MesoAlpha { get; init; } = 0.08;
        public double BaselineResonance { get; init; } = 0.70;
        public double BaselineStability { get; init; } = 0.70;
        public double ResonanceDriftThreshold { get; init; } = 0.06;
        public double StabilityVarianceThreshold { get; init; } = 0.03;
        public double AccumulatedLoadThreshold { get; init; } = 0.55;
        public double SuggestionThreshold { get; init; } = 0.65;
        public double LoadDecay { get; init; } = 0.96;
        public TimeSpan MinimumSuggestionInterval { get; init; } = TimeSpan.FromMinutes(2);
    }

    public sealed record HydrationAdvice
    {
        public bool Suggested { get; init; }
        public string ReasonCode { get; init; } = "HYDRATION_NORMAL";
        public double Score { get; init; }
        public double ResonanceDrift { get; init; }
        public double StabilityVariance { get; init; }
        public double AccumulatedLoad { get; init; }
        public DateTime Timestamp { get; init; }
    }

    public sealed class HydrationAdvisor
    {
        private readonly object _sync = new();
        private readonly HydrationAdvisorOptions _options;
        private bool _initialized;
        private double _microResonance;
        private double _microStability;
        private double _mesoResonance;
        private double _mesoStability;
        private double _previousMesoResonance;
        private double _previousMesoStability;
        private double _stabilityVariance;
        private double _accumulatedLoad;
        private DateTime? _lastSuggestionAt;

        public HydrationAdvisor(HydrationAdvisorOptions? options = null)
        {
            _options = options ?? new HydrationAdvisorOptions();
        }

        public event EventHandler<HydrationAdvice>? HydrationSuggested;

        public HydrationAdvice Evaluate(ExerciseLiveState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            HydrationAdvice advice;
            lock (_sync)
            {
                UpdateTrend(state);

                var resonanceDrift = Math.Max(0, _options.BaselineResonance - _mesoResonance);
                var stabilityDrift = Math.Max(0, _options.BaselineStability - _mesoStability);
                var resonanceSlope = _mesoResonance - _previousMesoResonance;
                var stabilitySlope = _mesoStability - _previousMesoStability;

                var score = 0.0;
                if (resonanceDrift >= _options.ResonanceDriftThreshold) score += 0.40;
                if (_stabilityVariance >= _options.StabilityVarianceThreshold) score += 0.25;
                if (_accumulatedLoad >= _options.AccumulatedLoadThreshold) score += 0.25;
                if (stabilityDrift >= _options.ResonanceDriftThreshold) score += 0.10;
                if (resonanceSlope < -0.02 || stabilitySlope < -0.02) score += 0.10;

                score = Math.Clamp(score, 0, 1);
                var timestamp = state.Timestamp == default ? DateTime.Now : state.Timestamp;
                var eligibleForSuggestion = !_lastSuggestionAt.HasValue
                    || timestamp - _lastSuggestionAt.Value >= _options.MinimumSuggestionInterval;
                var suggested = score >= _options.SuggestionThreshold
                    && !state.IsSafetyLocked
                    && eligibleForSuggestion;

                if (suggested)
                    _lastSuggestionAt = timestamp;

                advice = new HydrationAdvice
                {
                    Suggested = suggested,
                    ReasonCode = suggested ? DetermineReasonCode(resonanceDrift) : "HYDRATION_NORMAL",
                    Score = score,
                    ResonanceDrift = resonanceDrift,
                    StabilityVariance = _stabilityVariance,
                    AccumulatedLoad = _accumulatedLoad,
                    Timestamp = timestamp
                };
            }

            if (advice.Suggested)
                HydrationSuggested?.Invoke(this, advice);

            return advice;
        }

        public void Reset()
        {
            lock (_sync)
            {
                _initialized = false;
                _microResonance = 0;
                _microStability = 0;
                _mesoResonance = 0;
                _mesoStability = 0;
                _previousMesoResonance = 0;
                _previousMesoStability = 0;
                _stabilityVariance = 0;
                _accumulatedLoad = 0;
                _lastSuggestionAt = null;
            }
        }

        private void UpdateTrend(ExerciseLiveState state)
        {
            var resonance = ClampMetric(state.PrimaryMetricScore);
            var stability = ClampMetric(state.StabilityScore);

            if (!_initialized)
            {
                _microResonance = resonance;
                _microStability = stability;
                _mesoResonance = resonance;
                _mesoStability = stability;
                _previousMesoResonance = resonance;
                _previousMesoStability = stability;
                _initialized = true;
            }

            resonance = FilterSample(resonance, _microResonance);
            stability = FilterSample(stability, _microStability);

            _microResonance = Smooth(_microResonance, resonance, _options.MicroAlpha);
            _microStability = Smooth(_microStability, stability, _options.MicroAlpha);
            _previousMesoResonance = _mesoResonance;
            _previousMesoStability = _mesoStability;
            _mesoResonance = Smooth(_mesoResonance, _microResonance, _options.MesoAlpha);
            _mesoStability = Smooth(_mesoStability, _microStability, _options.MesoAlpha);

            var stabilityDeviation = Math.Abs(stability - _microStability);
            _stabilityVariance = Smooth(_stabilityVariance, stabilityDeviation, 0.20);

            var resonanceDrift = Math.Max(0, _options.BaselineResonance - _mesoResonance);
            var stabilityDrift = Math.Max(0, _options.BaselineStability - _mesoStability);
            var sampleLoad = 0.02
                + (state.IsHoldingCorrectly ? 0.04 : 0)
                + Math.Min(0.08, resonanceDrift)
                + Math.Min(0.08, stabilityDrift)
                + Math.Min(0.08, _stabilityVariance);
            _accumulatedLoad = Math.Clamp((_accumulatedLoad * _options.LoadDecay) + sampleLoad, 0, 1);
        }

        private double FilterSample(double value, double previous)
        {
            if (Math.Abs(value - previous) > _options.SpikeLimit)
                return previous;

            if (Math.Abs(value - previous) < _options.NoiseFloor)
                return previous;

            return value;
        }

        private static double ClampMetric(double value)
            => Math.Clamp(double.IsNaN(value) ? 0 : value, 0, 1);

        private static double Smooth(double previous, double current, double alpha)
            => previous + (current - previous) * Math.Clamp(alpha, 0, 1);

        private static string DetermineReasonCode(double resonanceDrift)
            => resonanceDrift > 0
                ? "HYDRATION_RESONANCE_DRIFT"
                : "HYDRATION_LOAD";
    }
}
