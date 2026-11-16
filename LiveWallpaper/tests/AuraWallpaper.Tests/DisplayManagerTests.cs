using AuraWallpaper.Core.Native;
using Xunit;

namespace AuraWallpaper.Tests
{
    public class DisplayManagerTests
    {
        [Fact]
        public void GetMonitors_ReturnsAtLeastOneDisplay()
        {
            var monitors = DisplayManager.Instance.GetMonitors();
            Assert.NotEmpty(monitors);

            var primary = monitors.Find(m => m.IsPrimary);
            Assert.NotNull(primary);
            Assert.True(primary.Bounds.Width > 0);
            Assert.True(primary.Bounds.Height > 0);
        }

        [Fact]
        public void GetVirtualScreenBounds_ReturnsPositiveDimensions()
        {
            var bounds = DisplayManager.Instance.GetVirtualScreenBounds();
            Assert.True(bounds.Width > 0);
            Assert.True(bounds.Height > 0);
        }
    }
}
