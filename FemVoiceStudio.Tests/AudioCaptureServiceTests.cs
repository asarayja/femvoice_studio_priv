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
    }
}
