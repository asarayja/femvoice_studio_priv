using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FemVoiceStudio.Audio
{
    public sealed class MicrophoneCalibrationService
    {
        private const double MinimumNoiseGate = 0.0015;
        private const double MinimumVoicedThreshold = 0.0025;
        private const double MinimumSignalToNoiseDb = 8.0;
        private const double ClippingPeakThreshold = 0.95;
        private const double ClippingWarningPeakThreshold = 0.85;
        private const double HighNoiseFloorThreshold = 0.015;
        private const double LowOutputSpeechThreshold = 0.01;
        private const double NewCalibrationWeight = 0.35;
        private const int CompatibilityFrameSize = 1024;
        private readonly string _profileDirectory;

        public MicrophoneCalibrationService(string? profileDirectory = null)
        {
            _profileDirectory = profileDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FemVoiceStudio",
                "MicrophoneCalibration");
        }

        public MicrophoneCalibrationProfile BuildProfile(
            string deviceName,
            float[] backgroundSamples,
            float[] comfortableVoiceSamples)
        {
            var noise = CalculateRms(backgroundSamples);
            var speech = CalculateRms(comfortableVoiceSamples);
            var quality = AssessCalibrationQuality(backgroundSamples, comfortableVoiceSamples);

            var gate = Math.Clamp(Math.Max(noise * 2.8, noise + 0.001), MinimumNoiseGate, 0.08);
            var voiced = Math.Clamp(Math.Max(gate * 1.35, speech * 0.22), MinimumVoicedThreshold, 0.12);

            return new MicrophoneCalibrationProfile
            {
                DeviceName = NormalizeDeviceName(deviceName),
                NoiseFloorRms = noise,
                SpeechRms = speech,
                NoiseGateThreshold = gate,
                VoicedRmsThreshold = voiced,
                SignalToNoiseDb = quality.SignalToNoiseDb,
                PeakDbFs = quality.PeakDbFs,
                CompatibilityFlags = quality.CompatibilityFlags,
                CalibrationCount = 1,
                CalibratedAt = DateTime.UtcNow
            };
        }

        public MicrophoneCalibrationProfile BuildAdaptiveProfile(
            string deviceName,
            float[] backgroundSamples,
            float[] comfortableVoiceSamples)
        {
            var current = BuildProfile(deviceName, backgroundSamples, comfortableVoiceSamples);
            var existing = Load(deviceName);
            if (existing == null)
                return current;

            return Blend(existing, current);
        }

        public MicrophoneCalibrationProfile? Load(string deviceName)
        {
            var path = GetProfilePath(deviceName);
            if (!File.Exists(path))
                return null;

            try
            {
                return JsonSerializer.Deserialize<MicrophoneCalibrationProfile>(
                    File.ReadAllText(path));
            }
            catch
            {
                return null;
            }
        }

        public void Save(MicrophoneCalibrationProfile profile)
        {
            Directory.CreateDirectory(_profileDirectory);
            var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(GetProfilePath(profile.DeviceName), json);
        }

        public void Save(string deviceName, float[] backgroundSamples, float[] comfortableVoiceSamples)
            => Save(BuildAdaptiveProfile(deviceName, backgroundSamples, comfortableVoiceSamples));

        public bool HasUsableVoiceSample(float[] backgroundSamples, float[] comfortableVoiceSamples)
            => AssessCalibrationQuality(backgroundSamples, comfortableVoiceSamples).IsUsable;

        public CalibrationQualityReport AssessCalibrationQuality(
            float[] backgroundSamples,
            float[] comfortableVoiceSamples)
        {
            var noise = CalculateRms(backgroundSamples);
            var speech = CalculateRms(comfortableVoiceSamples);
            var peak = CalculatePeak(comfortableVoiceSamples);
            var noiseDb = CalculateDbFs(noise);
            var speechDb = CalculateDbFs(speech);
            var peakDb = CalculateDbFs(peak);
            var signalToNoiseDb = speechDb - noiseDb;
            var minimumSeparation = Math.Max(noise * 1.8, noise + 0.001);
            var compatibilityFlags = DetectCompatibilityFlags(
                backgroundSamples,
                comfortableVoiceSamples,
                noise,
                speech,
                peak,
                signalToNoiseDb);

            CalibrationQualityStatus status;
            if (backgroundSamples.Length == 0 || comfortableVoiceSamples.Length == 0)
            {
                status = CalibrationQualityStatus.NoSamples;
            }
            else if (peak >= ClippingPeakThreshold)
            {
                status = CalibrationQualityStatus.TooLoud;
            }
            else if (speech < MinimumVoicedThreshold)
            {
                status = CalibrationQualityStatus.TooQuiet;
            }
            else if (speech < minimumSeparation || signalToNoiseDb < MinimumSignalToNoiseDb)
            {
                status = CalibrationQualityStatus.TooCloseToNoise;
            }
            else
            {
                status = CalibrationQualityStatus.Good;
            }

            return new CalibrationQualityReport(
                status,
                noise,
                speech,
                signalToNoiseDb,
                peakDb,
                compatibilityFlags);
        }

        public static double CalculateRms(float[] samples)
        {
            if (samples.Length == 0)
                return 0;

            return Math.Sqrt(samples.Sum(sample => (double)sample * sample) / samples.Length);
        }

        public static double CalculatePeak(float[] samples)
            => samples.Length == 0 ? 0 : samples.Max(sample => Math.Abs((double)sample));

        public static double CalculateDbFs(double linearValue)
            => linearValue <= 0 ? -120 : 20 * Math.Log10(Math.Min(1.0, linearValue));

        private string GetProfilePath(string deviceName)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(NormalizeDeviceName(deviceName)));
            var fileName = Convert.ToHexString(hash).ToLowerInvariant() + ".json";
            return Path.Combine(_profileDirectory, fileName);
        }

        public static string NormalizeDeviceName(string deviceName)
        {
            var normalized = deviceName.Trim();
            var colonIndex = normalized.IndexOf(':');

            if (colonIndex > 0
                && normalized[..colonIndex].All(char.IsDigit)
                && colonIndex + 1 < normalized.Length)
            {
                normalized = normalized[(colonIndex + 1)..].Trim();
            }

            return normalized;
        }

        private static MicrophoneCalibrationProfile Blend(
            MicrophoneCalibrationProfile existing,
            MicrophoneCalibrationProfile current)
        {
            var oldWeight = 1.0 - NewCalibrationWeight;
            return new MicrophoneCalibrationProfile
            {
                DeviceName = current.DeviceName,
                NoiseFloorRms = Weighted(existing.NoiseFloorRms, current.NoiseFloorRms, oldWeight),
                SpeechRms = Weighted(existing.SpeechRms, current.SpeechRms, oldWeight),
                NoiseGateThreshold = Weighted(existing.NoiseGateThreshold, current.NoiseGateThreshold, oldWeight),
                VoicedRmsThreshold = Weighted(existing.VoicedRmsThreshold, current.VoicedRmsThreshold, oldWeight),
                SignalToNoiseDb = Weighted(existing.SignalToNoiseDb, current.SignalToNoiseDb, oldWeight),
                PeakDbFs = current.PeakDbFs,
                CompatibilityFlags = current.CompatibilityFlags,
                CalibrationCount = Math.Max(1, existing.CalibrationCount) + 1,
                CalibratedAt = DateTime.UtcNow
            };
        }

        private static double Weighted(double oldValue, double newValue, double oldWeight)
            => oldValue <= 0 ? newValue : (oldValue * oldWeight) + (newValue * NewCalibrationWeight);

        private static MicrophoneCompatibilityFlags DetectCompatibilityFlags(
            float[] backgroundSamples,
            float[] voiceSamples,
            double noise,
            double speech,
            double peak,
            double signalToNoiseDb)
        {
            var flags = MicrophoneCompatibilityFlags.None;

            if (speech > 0 && speech < LowOutputSpeechThreshold)
                flags |= MicrophoneCompatibilityFlags.LowOutput;

            if (noise >= HighNoiseFloorThreshold || signalToNoiseDb < 12)
                flags |= MicrophoneCompatibilityFlags.HighNoiseFloor;

            if (peak >= ClippingWarningPeakThreshold)
                flags |= MicrophoneCompatibilityFlags.ClippingRisk;

            var voiceFrames = CalculateFrameRmsValues(voiceSamples, CompatibilityFrameSize);
            if (voiceFrames.Length >= 8)
            {
                var activeThreshold = Math.Max(noise * 2.2, MinimumVoicedThreshold);
                var nearZeroFrames = voiceFrames.Count(value => value < Math.Max(0.0002, noise * 0.35));
                var activeFrames = voiceFrames.Count(value => value >= activeThreshold);
                if (activeFrames >= 3 && nearZeroFrames >= Math.Max(2, voiceFrames.Length / 5))
                    flags |= MicrophoneCompatibilityFlags.PossibleNoiseGate;

                var maxFrame = voiceFrames.Max();
                var minActiveFrame = voiceFrames.Where(value => value >= activeThreshold).DefaultIfEmpty(0).Min();
                if (activeFrames >= 6
                    && peak >= 0.25
                    && minActiveFrame > 0
                    && CalculateDbFs(maxFrame) - CalculateDbFs(minActiveFrame) < 2.0)
                {
                    flags |= MicrophoneCompatibilityFlags.PossibleAgcOrCompression;
                }
            }

            return flags;
        }

        private static double[] CalculateFrameRmsValues(float[] samples, int frameSize)
        {
            if (samples.Length < frameSize || frameSize <= 0)
                return Array.Empty<double>();

            var frameCount = samples.Length / frameSize;
            var values = new double[frameCount];
            for (var frame = 0; frame < frameCount; frame++)
            {
                var sum = 0.0;
                var offset = frame * frameSize;
                for (var i = 0; i < frameSize; i++)
                {
                    var sample = samples[offset + i];
                    sum += sample * sample;
                }

                values[frame] = Math.Sqrt(sum / frameSize);
            }

            return values;
        }
    }

    public enum CalibrationQualityStatus
    {
        Good,
        NoSamples,
        TooQuiet,
        TooCloseToNoise,
        TooLoud
    }

    public sealed record CalibrationQualityReport(
        CalibrationQualityStatus Status,
        double NoiseFloorRms,
        double SpeechRms,
        double SignalToNoiseDb,
        double PeakDbFs,
        MicrophoneCompatibilityFlags CompatibilityFlags)
    {
        public bool IsUsable => Status == CalibrationQualityStatus.Good;
    }
}
