using Xunit;
using FemVoiceStudio.Services;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Tests
{
    public class IconMappingTests
    {
        [Fact]
        public void Map_LegacyEmoji_ReturnsExpectedKey()
        {
            Assert.Equal(IconKey.Microphone, IconMapping.Map("🎤"));
            Assert.Equal(IconKey.MusicNote, IconMapping.Map("🎵"));
            Assert.Equal(IconKey.TrendingUp, IconMapping.Map("📈"));
            Assert.Equal(IconKey.TrendingDown, IconMapping.Map("📉"));
            Assert.Equal(IconKey.Volume, IconMapping.Map("🔊"));
            Assert.Equal(IconKey.Breath, IconMapping.Map("💨"));
            Assert.Equal(IconKey.Chat, IconMapping.Map("💬"));
            Assert.Equal(IconKey.Book, IconMapping.Map("📖"));
        }

        [Fact]
        public void Map_MojibakeVariants_ReturnsExpectedKey()
        {
            Assert.Equal(IconKey.Microphone, IconMapping.Map("ðŸŽµ"));
            Assert.Equal(IconKey.MusicNote, IconMapping.Map("ðŸ“Š"));
            Assert.Equal(IconKey.TrendingUp, IconMapping.Map("ðŸ“ˆ"));
            Assert.Equal(IconKey.TrendingDown, IconMapping.Map("ðŸ“‰"));
        }
    }
}
