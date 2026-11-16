using System;
using System.Text.Json.Serialization;

namespace AuraWallpaper.Core.Models
{
    public class MonitorAssignment
    {
        public string MonitorDeviceId { get; set; } = string.Empty;
        public int MonitorIndex { get; set; }
        public string WallpaperId { get; set; } = string.Empty;
        public AssignmentMode Mode { get; set; } = AssignmentMode.PerMonitor;
        public PlaybackState State { get; set; } = PlaybackState.Playing;
        public int Volume { get; set; } = 80;
        public bool IsMuted { get; set; }
        public double PlaybackSpeed { get; set; } = 1.0;
        public WallpaperScaling Scaling { get; set; } = WallpaperScaling.Fill;
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

        // Process ID of the isolated player host
        public int PlayerProcessId { get; set; }

        [JsonIgnore]
        public IntPtr PlayerWindowHwnd { get; set; } = IntPtr.Zero;

        [JsonPropertyName("PlayerWindowHwnd")]
        public long PlayerWindowHwndValue => PlayerWindowHwnd.ToInt64();
    }
}
