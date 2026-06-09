using FemVoiceStudio.Audio;
using Xunit;

namespace FemVoiceStudio.Tests
{
    public class AudioCaptureServiceTests
    {
        [Fact]
        public void HearOwnVoice_DefaultsToFalse()
        {
            using var service = new AudioCaptureService();

            Assert.False(service.HearOwnVoice);
        }

        [Fact]
        public void StartRecording_WithoutInitialize_ThrowsAndDoesNotRecord()
        {
            using var service = new AudioCaptureService();

            var ex = Assert.Throws<InvalidOperationException>(() => service.StartRecording());

            Assert.Contains("initialisert", ex.Message);
            Assert.False(service.IsRecording);
            Assert.False(service.HasReceivedAudioData);
        }

        [Fact]
        public void Monitoring_WithoutCallback_IsInactive()
        {
            using var service = new AudioCaptureService();

            service.HearOwnVoice = true;

            Assert.True(service.HearOwnVoice);
            Assert.False(service.HasReceivedAudioData);
            Assert.False(service.IsMonitoringActive);
        }
    }

    public class AudioAnalyzerServiceTests
    {
        [Fact]
        public void StartAnalysis_WhenCaptureNotInitialized_ThrowsAndDoesNotAnalyze()
        {
            using var service = new AudioAnalyzerService();

            Assert.Throws<InvalidOperationException>(() => service.StartAnalysis());

            Assert.False(service.IsAnalyzing);
            Assert.Empty(service.GetRecentResults(10));
        }

        [Fact]
        public void NoDataAvailable_ProducesNoAnalysisResults()
        {
            using var service = new AudioAnalyzerService();

            Assert.Empty(service.GetRecentResults(10));
            Assert.False(service.IsAnalyzing);
        }
    }
}
