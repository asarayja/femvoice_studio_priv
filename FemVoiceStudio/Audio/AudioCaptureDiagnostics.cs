using System;

namespace FemVoiceStudio.Audio
{
    public enum AudioFailureClassification
    {
        UNKNOWN,
        CAPTURE_STOPS,
        SIGNAL_LEVEL_COLLAPSES,
        SILENCE_GATE_REJECTS_SIGNAL,
        PITCH_DETECTOR_REJECTS_SIGNAL,
        GRAPH_UPDATE_STOPS,
        SCORE_USES_LOW_INPUT,
        SCORE_FALLBACK_VALUE,
        DEVICE_SELECTION_ERROR,
        WINDOWS_OR_DRIVER_LEVEL_ISSUE
    }

    public sealed record AudioCaptureDiagnosticsSnapshot
    {
        public string DeviceName { get; init; } = "";
        public string DeviceId { get; init; } = "";
        public string DefaultInputDeviceName { get; init; } = "";
        public string DefaultCommunicationsDeviceName { get; init; } = "";
        public string DeviceSelectedByFemVoice { get; init; } = "";
        public bool DeviceChangedDuringSession { get; init; }
        public int SampleRate { get; init; }
        public int Channels { get; init; }
        public int BitDepth { get; init; }
        public int BufferMilliseconds { get; init; }
        public string AudioApi { get; init; } = "NAudio WaveInEvent";
        public bool IsRecording { get; init; }
        public long DataAvailableCount { get; init; }
        public long BytesReceived { get; init; }
        public long SamplesReceived { get; init; }
        public double CallbackIntervalMs { get; init; }
        public long DroppedCallbackCount { get; init; }
        public DateTime? LastDataAvailableTime { get; init; }
        public double TimeSinceLastAudioFrameSeconds { get; init; }
        public double RmsLevel { get; init; }
        public double PeakLevel { get; init; }
        public double InputLevelPercent { get; init; }
        public double NoiseFloorEstimate { get; init; }
        public double SignalToNoiseEstimateDb { get; init; }
        public double LowestLevel { get; init; }
        public double HighestLevel { get; init; }
        public bool LevelCollapsed { get; init; }
        public bool SilenceDetected { get; init; }
        public long SilenceDetectedCount { get; init; }
        public double CurrentSilenceThreshold { get; init; }
        public bool IsSignalAccepted { get; init; }
        public bool IsSignalRejected { get; init; }
        public string SignalRejectedReason { get; init; } = "";
        public bool MonitoringActive { get; init; }
        public AudioFailureClassification FailureClassification { get; init; } = AudioFailureClassification.UNKNOWN;
    }
}
