using System;
using System.IO;

namespace AuraWallpaper.Core.Models
{
    public class WallpaperItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = "Untitled Wallpaper";
        public string Description { get; set; } = string.Empty;
        public string Author { get; set; } = "Unknown";
        public string Version { get; set; } = "1.0.0";
        public string Contact { get; set; } = string.Empty;
        public string License { get; set; } = string.Empty;
        public WallpaperType Type { get; set; } = WallpaperType.Video;

        // Path to the main file (e.g. index.html, video.mp4, scene.gif, app.exe)
        public string FilePath { get; set; } = string.Empty;
        // Preview gif or mp4
        public string PreviewPath { get; set; } = string.Empty;
        // Thumbnail image (jpg/png)
        public string ThumbnailPath { get; set; } = string.Empty;

        public string Tags { get; set; } = string.Empty;
        public string Category { get; set; } = "General";
        public bool IsFavorite { get; set; }
        public DateTime DateAdded { get; set; } = DateTime.UtcNow;
        public DateTime LastUsed { get; set; } = DateTime.UtcNow;
        public string SourceUrl { get; set; } = string.Empty;

        // Command line arguments for App wallpapers
        public string Arguments { get; set; } = string.Empty;

        // Raw custom properties JSON schema (LivelyProperties.json parity)
        public string CustomPropertiesJson { get; set; } = string.Empty;
        // Saved property values
        public string PropertyValuesJson { get; set; } = string.Empty;

        // Lively package compatibility metadata
        public string LivelyInfoMetadataJson { get; set; } = string.Empty;

        public bool FileExists => !string.IsNullOrEmpty(FilePath) && (File.Exists(FilePath) || Directory.Exists(FilePath) || FilePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || FilePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
    }
}
