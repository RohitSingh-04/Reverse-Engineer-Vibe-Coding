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

        [Fact]
        public void MultiDisplayOptions_ValidateScalingAndModes()
        {
            var monitors = DisplayManager.Instance.GetMonitors();
            var vBounds = DisplayManager.Instance.GetVirtualScreenBounds();

            Assert.NotEmpty(monitors);
            Assert.True(vBounds.Width >= monitors[0].Bounds.Width);

            // Validate enum values
            Assert.Equal(0, (int)AuraWallpaper.Core.Models.AssignmentMode.PerMonitor);
            Assert.Equal(1, (int)AuraWallpaper.Core.Models.AssignmentMode.Span);
            Assert.Equal(2, (int)AuraWallpaper.Core.Models.AssignmentMode.Duplicate);

            Assert.Equal(0, (int)AuraWallpaper.Core.Models.WallpaperScaling.Fill);
            Assert.Equal(1, (int)AuraWallpaper.Core.Models.WallpaperScaling.Fit);
            Assert.Equal(2, (int)AuraWallpaper.Core.Models.WallpaperScaling.Stretch);
            Assert.Equal(3, (int)AuraWallpaper.Core.Models.WallpaperScaling.Center);
            Assert.Equal(4, (int)AuraWallpaper.Core.Models.WallpaperScaling.Span);
        }

        [Fact]
        public void DumpDisplayGeometry_Inspect()
        {
            var monitors = DisplayManager.Instance.GetMonitors();
            var vBounds = DisplayManager.Instance.GetVirtualScreenBounds();
            System.Console.WriteLine($"VIRTUAL SCREEN: X={vBounds.X}, Y={vBounds.Y}, W={vBounds.Width}, H={vBounds.Height}");
            foreach (var m in monitors)
            {
                System.Console.WriteLine($"MONITOR {m.Index}: Name={m.DeviceName}, Primary={m.IsPrimary}, Bounds=({m.Bounds.X},{m.Bounds.Y},{m.Bounds.Width},{m.Bounds.Height})");
            }

            DesktopBridge.Instance.InitializeWorkerW();
            var workerW = DesktopBridge.Instance.WorkerWHwnd;
            System.Console.WriteLine($"WorkerW: {workerW}");
            if (workerW != System.IntPtr.Zero)
            {
                NativeMethods.GetWindowRect(workerW, out var wRect);
                NativeMethods.GetClientRect(workerW, out var cRect);
                var pt = new NativeMethods.POINT { x = 0, y = 0 };
                NativeMethods.ClientToScreen(workerW, ref pt);
                System.Console.WriteLine($"WorkerW WindowRect: ({wRect.Left},{wRect.Top},{wRect.Right},{wRect.Bottom})");
                System.Console.WriteLine($"WorkerW ClientRect: ({cRect.Left},{cRect.Top},{cRect.Right},{cRect.Bottom})");
                System.Console.WriteLine($"WorkerW ClientToScreen(0,0): ({pt.x},{pt.y})");

                foreach (var m in monitors)
                {
                    var mPt = new NativeMethods.POINT { x = m.Bounds.X, y = m.Bounds.Y };
                    NativeMethods.ScreenToClient(workerW, ref mPt);
                    System.Console.WriteLine($"Monitor {m.Index} Screen({m.Bounds.X},{m.Bounds.Y}) -> WorkerW Client({mPt.x},{mPt.y})");
                }
            }
        }

        [Fact]
        public void AutoApplyDefaultMultiDisplayConfiguration_AdaptsToConnectedDisplays()
        {
            var monitors = DisplayManager.Instance.GetMonitors();
            var dummyWp = new AuraWallpaper.Core.Models.WallpaperItem
            {
                Id = "TEST_WP_AUTO",
                Title = "Auto Detected Wallpaper",
                Type = AuraWallpaper.Core.Models.WallpaperType.Web,
                FilePath = "test.html"
            };
            AuraWallpaper.Core.Storage.AuraDatabase.Instance.SaveWallpaper(dummyWp);

            // Verify auto configuration doesn't crash and adapts to monitor count
            AuraWallpaper.Core.Engine.WallpaperManager.Instance.AutoApplyDefaultMultiDisplayConfiguration(monitors);

            if (monitors.Count > 1)
            {
                Assert.True(AuraWallpaper.Core.Engine.WallpaperManager.Instance.IsDuplicateMode || AuraWallpaper.Core.Engine.WallpaperManager.Instance.IsSpanMode);
            }

            // Cleanup
            AuraWallpaper.Core.Storage.AuraDatabase.Instance.DeleteWallpaper("TEST_WP_AUTO");
        }
    }
}
