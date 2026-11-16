using System;

namespace AuraWallpaper.Core.Models
{
    public struct MonitorRect
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public MonitorRect(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public override string ToString() => $"{Width}x{Height} at ({X},{Y})";
    }

    public class MonitorDevice
    {
        public int Index { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public IntPtr HMonitor { get; set; }
        public MonitorRect Bounds { get; set; }
        public MonitorRect WorkingArea { get; set; }
        public bool IsPrimary { get; set; }
        public double ScalingFactor { get; set; } = 1.0;
        public int RefreshRate { get; set; } = 60;
    }
}
