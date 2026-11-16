using System;
using System.Collections.Generic;

namespace AuraWallpaper.Core.Models
{
    public class Playlist
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "Default Playlist";
        public int RotationIntervalSeconds { get; set; } = 300; // 5 minutes
        public bool IsShuffle { get; set; }
        public bool IsRepeat { get; set; } = true;
        public bool IsActive { get; set; }
        public int TargetMonitorIndex { get; set; } = -1; // -1 for all monitors
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<PlaylistItem> Items { get; set; } = new List<PlaylistItem>();
    }

    public class PlaylistItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string PlaylistId { get; set; } = string.Empty;
        public string WallpaperId { get; set; } = string.Empty;
        public int OrderIndex { get; set; }
        public int CustomDurationSeconds { get; set; } // 0 for default playlist interval
    }

    public enum RuleAction
    {
        Pause = 0,
        Mute = 1,
        Stop = 2,
        Ignore = 3
    }

    public class ApplicationRule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string ProcessName { get; set; } = string.Empty;
        public RuleAction Action { get; set; } = RuleAction.Pause;
        public string Description { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
    }
}
