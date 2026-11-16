using System;
using AuraWallpaper.Core.Native;

namespace AuraWallpaper.Core.Models
{
    public enum AppTheme
    {
        System = 0,
        Dark = 1,
        Light = 2
    }

    public enum ScreensaverSource
    {
        ActiveWallpaper = 0,
        SpecificWallpaper = 1,
        Playlist = 2
    }

    public class AuraSettings
    {
        // General
        public string Language { get; set; } = "en";
        public AppTheme Theme { get; set; } = AppTheme.Dark;
        public bool LaunchOnStartup { get; set; } = true;
        public bool StartMinimized { get; set; } = false;
        public bool MinimizeToTray { get; set; } = true;
        public bool ShowNotifications { get; set; } = true;

        // Wallpaper defaults
        public int GlobalVolume { get; set; } = 80;
        public bool GlobalMute { get; set; } = false;
        public bool HardwareAcceleration { get; set; } = true;
        public WallpaperScaling DefaultScaling { get; set; } = WallpaperScaling.Fill;
        public int MaxFps { get; set; } = 60;

        // Performance
        public RuleAction FullscreenAction { get; set; } = RuleAction.Pause;
        public RuleAction MaximizedAction { get; set; } = RuleAction.Ignore;
        public RuleAction BatteryAction { get; set; } = RuleAction.Pause;
        public int BatteryPauseThreshold { get; set; } = 20; // percent
        public bool PauseOnLock { get; set; } = true;
        public bool PauseOnRemoteDesktop { get; set; } = true;

        // Taskbar
        public TaskbarThemeMode TaskbarMode { get; set; } = TaskbarThemeMode.Default;
        public int TaskbarTintColor { get; set; } = 0;

        // Screensaver
        public bool ScreensaverEnabled { get; set; } = true;
        public ScreensaverSource ScreensaverSourceMode { get; set; } = ScreensaverSource.ActiveWallpaper;
        public string ScreensaverWallpaperId { get; set; } = string.Empty;
        public string ScreensaverPlaylistId { get; set; } = string.Empty;
        public int ScreensaverTimeoutMinutes { get; set; } = 10;

        // Storage
        public string CustomLibraryPath { get; set; } = string.Empty;

        // Developer & Audio
        public bool EnableAudioVisualization { get; set; } = true;
        public double AudioSensitivity { get; set; } = 1.0;
        public bool EnableDevTools { get; set; } = false;
    }
}
