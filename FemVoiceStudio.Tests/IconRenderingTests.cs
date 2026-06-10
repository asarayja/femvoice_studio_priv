using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;
using FemVoiceStudio.Services;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Tests
{
    public class IconRenderingTests
    {
        [Fact]
        public void When_ResourceExists_IconProviderReturnsImage_And_NullToVisibilityShowsVisible()
        {
            // Arrange: create a test resource dictionary with a DrawingImage for Microphone
            var rd = new ResourceDictionary();
            var drawing = new GeometryDrawing(Brushes.Black, null, Geometry.Empty);
            var img = new DrawingImage(drawing);
            rd["Icon.Microphone"] = img;
            IconProvider.TestResources = rd;

            // Act
            var result = IconProvider.GetImageForValue("🎤");
            var converter = new FemVoiceStudio.Converters.NullToVisibilityConverter();
            var vis = (Visibility)converter.Convert("🎤", typeof(Visibility), null!, System.Globalization.CultureInfo.InvariantCulture);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(Visibility.Visible, vis);
        }

        [Fact]
        public void When_ResourceMissing_IconProviderReturnsNull_And_NullToVisibilityShowsCollapsed()
        {
            // Arrange: ensure no test resources
            IconProvider.TestResources = null;

            // Act
            var result = IconProvider.GetImageForValue("unknown_icon_value");
            var converter = new FemVoiceStudio.Converters.NullToVisibilityConverter();
            var vis = (Visibility)converter.Convert("unknown_icon_value", typeof(Visibility), null!, System.Globalization.CultureInfo.InvariantCulture);

            // Assert
            Assert.Null(result);
            Assert.Equal(Visibility.Collapsed, vis);
        }
    }
}
