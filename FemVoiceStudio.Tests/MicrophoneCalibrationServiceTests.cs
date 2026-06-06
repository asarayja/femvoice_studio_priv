using FemVoiceStudio.Audio;
using Xunit;

namespace FemVoiceStudio.Tests
{
    public class MicrophoneCalibrationServiceTests
    {
        [Fact]
        public void BuildProfile_UsesNoiseAndSpeechLevels()
        {
            var service = new MicrophoneCalibrationService();
            var background = Enumerable.Repeat(0.002f, 2048).ToArray();
            var speech = Enumerable.Repeat(0.05f, 2048).ToArray();

            var profile = service.BuildProfile("USB Test Mic", background, speech);

            Assert.Equal("USB Test Mic", profile.DeviceName);
            Assert.True(profile.NoiseFloorRms > 0);
            Assert.True(profile.SpeechRms > profile.NoiseFloorRms);
            Assert.True(profile.NoiseGateThreshold >= 0.003);
            Assert.True(profile.VoicedRmsThreshold > profile.NoiseGateThreshold);
            Assert.True(profile.SignalToNoiseDb > 8);
            Assert.True(profile.PeakDbFs < 0);
        }

        [Fact]
        public void BuildProfile_AllowsQuietCleanMicrophones()
        {
            var service = new MicrophoneCalibrationService();
            var background = Enumerable.Repeat(0.0002f, 2048).ToArray();
            var speech = Enumerable.Repeat(0.004f, 2048).ToArray();

            var profile = service.BuildProfile("Quiet USB Mic", background, speech);

            Assert.True(profile.NoiseGateThreshold < 0.01);
            Assert.True(profile.VoicedRmsThreshold < 0.01);
            Assert.True(profile.VoicedRmsThreshold > profile.NoiseGateThreshold);
            Assert.True(profile.CompatibilityFlags.HasFlag(MicrophoneCompatibilityFlags.LowOutput));
        }

        [Fact]
        public void HasUsableVoiceSample_RejectsSilentVoicePhase()
        {
            var service = new MicrophoneCalibrationService();
            var background = Enumerable.Repeat(0.002f, 2048).ToArray();
            var silentVoice = Enumerable.Repeat(0.0021f, 2048).ToArray();

            Assert.False(service.HasUsableVoiceSample(background, silentVoice));
        }

        [Fact]
        public void AssessCalibrationQuality_ReportsVoiceTooCloseToNoise()
        {
            var service = new MicrophoneCalibrationService();
            var background = Enumerable.Repeat(0.02f, 2048).ToArray();
            var voiceNearNoise = Enumerable.Repeat(0.025f, 2048).ToArray();

            var quality = service.AssessCalibrationQuality(background, voiceNearNoise);

            Assert.False(quality.IsUsable);
            Assert.Equal(CalibrationQualityStatus.TooCloseToNoise, quality.Status);
        }

        [Fact]
        public void AssessCalibrationQuality_ReportsClippingRisk()
        {
            var service = new MicrophoneCalibrationService();
            var background = Enumerable.Repeat(0.002f, 2048).ToArray();
            var clippedVoice = Enumerable.Repeat(0.98f, 2048).ToArray();

            var quality = service.AssessCalibrationQuality(background, clippedVoice);

            Assert.False(quality.IsUsable);
            Assert.Equal(CalibrationQualityStatus.TooLoud, quality.Status);
            Assert.True(quality.CompatibilityFlags.HasFlag(MicrophoneCompatibilityFlags.ClippingRisk));
        }

        [Fact]
        public void AssessCalibrationQuality_FlagsHighNoiseFloorEvenWhenUsable()
        {
            var service = new MicrophoneCalibrationService();
            var background = Enumerable.Repeat(0.018f, 4096).ToArray();
            var voice = Enumerable.Repeat(0.08f, 4096).ToArray();

            var quality = service.AssessCalibrationQuality(background, voice);

            Assert.True(quality.IsUsable);
            Assert.True(quality.CompatibilityFlags.HasFlag(MicrophoneCompatibilityFlags.HighNoiseFloor));
        }

        [Fact]
        public void AssessCalibrationQuality_FlagsPossibleNoiseGate()
        {
            var service = new MicrophoneCalibrationService();
            var background = Enumerable.Repeat(0.001f, 8192).ToArray();
            var voice = new float[8192];
            for (var i = 0; i < voice.Length; i++)
            {
                voice[i] = i / 1024 % 3 == 0 ? 0f : 0.05f;
            }

            var quality = service.AssessCalibrationQuality(background, voice);

            Assert.True(quality.IsUsable);
            Assert.True(quality.CompatibilityFlags.HasFlag(MicrophoneCompatibilityFlags.PossibleNoiseGate));
        }

        [Fact]
        public void HasUsableVoiceSample_AcceptsQuietHummingAboveNoise()
        {
            var service = new MicrophoneCalibrationService();
            var background = Enumerable.Repeat(0.0002f, 2048).ToArray();
            var humming = Enumerable.Repeat(0.004f, 2048).ToArray();

            Assert.True(service.HasUsableVoiceSample(background, humming));
        }

        [Fact]
        public void SaveAndLoad_RoundTripsProfilePerDevice()
        {
            var directory = Path.Combine(Path.GetTempPath(), "FemVoiceStudio.Tests", Guid.NewGuid().ToString("N"));
            var service = new MicrophoneCalibrationService(directory);
            var profile = service.BuildProfile(
                "USB Test Mic",
                Enumerable.Repeat(0.002f, 512).ToArray(),
                Enumerable.Repeat(0.05f, 512).ToArray());

            service.Save(profile);

            var loaded = service.Load("USB Test Mic");

            Assert.NotNull(loaded);
            Assert.Equal(profile.DeviceName, loaded!.DeviceName);
            Assert.Equal(profile.NoiseGateThreshold, loaded.NoiseGateThreshold);
            Assert.Equal(profile.VoicedRmsThreshold, loaded.VoicedRmsThreshold);
        }

        [Fact]
        public void SaveAndLoad_IgnoresWaveInDeviceOrderPrefix()
        {
            var directory = Path.Combine(Path.GetTempPath(), "FemVoiceStudio.Tests", Guid.NewGuid().ToString("N"));
            var service = new MicrophoneCalibrationService(directory);
            var profile = service.BuildProfile(
                "0: Jack Microphone",
                Enumerable.Repeat(0.002f, 512).ToArray(),
                Enumerable.Repeat(0.05f, 512).ToArray());

            service.Save(profile);

            var loaded = service.Load("2: Jack Microphone");

            Assert.NotNull(loaded);
            Assert.Equal("Jack Microphone", loaded!.DeviceName);
            Assert.Equal(profile.NoiseGateThreshold, loaded.NoiseGateThreshold);
            Assert.Equal(profile.VoicedRmsThreshold, loaded.VoicedRmsThreshold);
        }

        [Fact]
        public void BuildAdaptiveProfile_BlendsNewCalibrationWithExistingProfile()
        {
            var directory = Path.Combine(Path.GetTempPath(), "FemVoiceStudio.Tests", Guid.NewGuid().ToString("N"));
            var service = new MicrophoneCalibrationService(directory);
            var first = service.BuildProfile(
                "USB Test Mic",
                Enumerable.Repeat(0.002f, 512).ToArray(),
                Enumerable.Repeat(0.05f, 512).ToArray());
            service.Save(first);

            var updated = service.BuildAdaptiveProfile(
                "USB Test Mic",
                Enumerable.Repeat(0.006f, 512).ToArray(),
                Enumerable.Repeat(0.08f, 512).ToArray());

            Assert.Equal(2, updated.CalibrationCount);
            Assert.True(updated.NoiseFloorRms > first.NoiseFloorRms);
            Assert.True(updated.SpeechRms > first.SpeechRms);
            Assert.True(updated.NoiseFloorRms < 0.006);
            Assert.True(updated.SpeechRms < 0.08);
        }

        [Fact]
        public void Save_WithSamples_UpdatesExistingProfileAdaptively()
        {
            var directory = Path.Combine(Path.GetTempPath(), "FemVoiceStudio.Tests", Guid.NewGuid().ToString("N"));
            var service = new MicrophoneCalibrationService(directory);

            service.Save(
                "USB Test Mic",
                Enumerable.Repeat(0.002f, 512).ToArray(),
                Enumerable.Repeat(0.05f, 512).ToArray());
            service.Save(
                "USB Test Mic",
                Enumerable.Repeat(0.004f, 512).ToArray(),
                Enumerable.Repeat(0.07f, 512).ToArray());

            var loaded = service.Load("USB Test Mic");

            Assert.NotNull(loaded);
            Assert.Equal(2, loaded!.CalibrationCount);
            Assert.True(loaded.NoiseFloorRms > 0.002);
            Assert.True(loaded.NoiseFloorRms < 0.004);
        }

        [Theory]
        [InlineData("0: USB Microphone", "USB Microphone")]
        [InlineData("12: Microphone Array", "Microphone Array")]
        [InlineData("Jack Microphone", "Jack Microphone")]
        public void NormalizeDeviceName_RemovesOnlyLeadingWaveInIndex(string input, string expected)
        {
            Assert.Equal(expected, MicrophoneCalibrationService.NormalizeDeviceName(input));
        }

        [Fact]
        public void PitchDetection_RespectsCalibratedVoicedThreshold()
        {
            var detector = new PitchDetectionService(voicedRmsThreshold: 0.05);
            var quietSignal = Enumerable.Repeat(0.01f, 2048).ToArray();

            var result = detector.DetectPitch(quietSignal);

            Assert.False(result.IsVoiced);
            Assert.Equal(0, result.Pitch);
        }
    }
}
