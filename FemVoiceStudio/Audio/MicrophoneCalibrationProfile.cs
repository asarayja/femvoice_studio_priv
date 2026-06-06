using System;

namespace FemVoiceStudio.Audio
{
    public sealed class MicrophoneCalibrationProfile
    {
        public string DeviceName { get; set; } = "";
        public double NoiseFloorRms { get; set; }
        public double SpeechRms { get; set; }
        public double NoiseGateThreshold { get; set; } = 0.01;
        public double VoicedRmsThreshold { get; set; } = 0.01;
        public double SignalToNoiseDb { get; set; }
        public double PeakDbFs { get; set; }
        public MicrophoneCompatibilityFlags CompatibilityFlags { get; set; } = MicrophoneCompatibilityFlags.None;
        public int CalibrationCount { get; set; } = 1;
        public DateTime CalibratedAt { get; set; } = DateTime.UtcNow;
    }

    [Flags]
    public enum MicrophoneCompatibilityFlags
    {
        None = 0,
        LowOutput = 1,
        HighNoiseFloor = 2,
        ClippingRisk = 4,
        PossibleNoiseGate = 8,
        PossibleAgcOrCompression = 16
    }
}
