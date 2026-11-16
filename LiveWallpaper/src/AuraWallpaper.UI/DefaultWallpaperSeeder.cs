using System;
using System.IO;
using System.Linq;
using AuraWallpaper.Core.Models;
using AuraWallpaper.Core.Native;
using AuraWallpaper.Core.Storage;

namespace AuraWallpaper.UI
{
    public static class DefaultWallpaperSeeder
    {
        public static void SeedDefaultsIfEmpty()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string defaultsPath = Path.Combine(baseDir, "assets", "DefaultWallpapers");
                if (!Directory.Exists(defaultsPath))
                {
                    string devPath = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\..\assets\DefaultWallpapers"));
                    if (Directory.Exists(devPath)) defaultsPath = devPath;
                }

                var existing = AuraDatabase.Instance.GetAllWallpapers();

                // 1. Clean up stale/test entries and upgrade existing items with thumbnails
                foreach (var item in existing.ToList())
                {
                    // Remove transient test folders
                    if (!string.IsNullOrEmpty(item.FilePath) && item.FilePath.Contains("AuraTests_"))
                    {
                        AuraDatabase.Instance.DeleteWallpaper(item.Id);
                        continue;
                    }

                    bool changed = false;

                    // Clean title if it's "index"
                    if (string.Equals(item.Title, "index", StringComparison.OrdinalIgnoreCase))
                    {
                        string? folderName = Path.GetFileName(Path.GetDirectoryName(item.FilePath));
                        if (!string.IsNullOrEmpty(folderName) && folderName.Length > 2)
                        {
                            item.Title = folderName;
                            changed = true;
                        }
                    }

                    // Generate thumbnail and wrapper for videos if missing
                    if (item.Type == WallpaperType.Video && File.Exists(item.FilePath))
                    {
                        string? dir = Path.GetDirectoryName(item.FilePath);
                        if (!string.IsNullOrEmpty(dir))
                        {
                            string thumbPath = Path.Combine(dir, "thumbnail.png");
                            if (!File.Exists(thumbPath))
                            {
                                ShellThumbnailExtractor.TryExtractThumbnail(item.FilePath, thumbPath);
                            }

                            if (File.Exists(thumbPath) && item.ThumbnailPath != thumbPath)
                            {
                                item.ThumbnailPath = thumbPath;
                                item.PreviewPath = thumbPath;
                                changed = true;
                            }

                            // Ensure index.html player wrapper is ready
                            string indexHtml = Path.Combine(dir, "index.html");
                            if (!File.Exists(indexHtml))
                            {
                                string vName = Path.GetFileName(item.FilePath);
                                string htmlContent = $@"<!DOCTYPE html>
<html>
<head>
  <meta charset=""utf-8"">
  <style>
    * {{ margin: 0; padding: 0; box-sizing: border-box; }}
    html, body {{ width: 100vw; height: 100vh; overflow: hidden; background: #000; }}
    video {{ width: 100vw; height: 100vh; object-fit: cover; display: block; }}
  </style>
</head>
<body>
  <video id=""v"" src=""./{vName}"" autoplay loop muted playsinline></video>
  <script>
    const v = document.getElementById('v');
    v.play().catch(console.error);
  </script>
</body>
</html>";
                                try { File.WriteAllText(indexHtml, htmlContent); } catch { }
                            }
                        }
                    }
                    else if (item.Type == WallpaperType.Picture || item.Type == WallpaperType.Gif)
                    {
                        if (string.IsNullOrEmpty(item.ThumbnailPath) && File.Exists(item.FilePath))
                        {
                            item.ThumbnailPath = item.FilePath;
                            item.PreviewPath = item.FilePath;
                            changed = true;
                        }
                    }
                    else if (item.Type == WallpaperType.Web && File.Exists(item.FilePath))
                    {
                        string? dir = Path.GetDirectoryName(item.FilePath);
                        if (!string.IsNullOrEmpty(dir))
                        {
                            string thumb = Path.Combine(dir, "thumbnail.png");
                            if (File.Exists(thumb) && item.ThumbnailPath != thumb)
                            {
                                item.ThumbnailPath = thumb;
                                item.PreviewPath = thumb;
                                changed = true;
                            }
                        }
                    }

                    if (changed)
                    {
                        AuraDatabase.Instance.SaveWallpaper(item);
                    }
                }

                // 2. Ensure all default wallpapers exist in DB with their thumbnails
                if (Directory.Exists(defaultsPath))
                {
                    var currentWallpapers = AuraDatabase.Instance.GetAllWallpapers();
                    var wallpaperDirs = Directory.GetDirectories(defaultsPath);
                    foreach (var dir in wallpaperDirs)
                    {
                        string htmlPath = Path.Combine(dir, "index.html");
                        if (!File.Exists(htmlPath)) continue;

                        string dirName = Path.GetFileName(dir);
                        bool alreadyPresent = currentWallpapers.Any(w =>
                            string.Equals(w.FilePath, htmlPath, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(w.Title, dirName, StringComparison.OrdinalIgnoreCase));

                        string thumbPath = Path.Combine(dir, "thumbnail.png");
                        string infoPath = Path.Combine(dir, "LivelyInfo.json");
                        string propsPath = Path.Combine(dir, "LivelyProperties.json");

                        string title = dirName;
                        string author = "Aura Team";
                        string desc = "Interactive Live Wallpaper";
                        string tags = "interactive,web";

                        if (File.Exists(infoPath))
                        {
                            try
                            {
                                using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(infoPath));
                                var root = doc.RootElement;
                                if (root.TryGetProperty("Title", out var t)) title = t.GetString() ?? title;
                                if (root.TryGetProperty("Author", out var a)) author = a.GetString() ?? author;
                                if (root.TryGetProperty("Desc", out var d)) desc = d.GetString() ?? desc;
                            }
                            catch { }
                        }

                        if (!alreadyPresent)
                        {
                            string propsJson = File.Exists(propsPath) ? File.ReadAllText(propsPath) : "";
                            var item = new WallpaperItem
                            {
                                Id = Guid.NewGuid().ToString("N"),
                                Title = title,
                                Author = author,
                                Description = desc,
                                Type = WallpaperType.Web,
                                FilePath = htmlPath,
                                ThumbnailPath = File.Exists(thumbPath) ? thumbPath : "",
                                PreviewPath = File.Exists(thumbPath) ? thumbPath : "",
                                CustomPropertiesJson = propsJson,
                                Category = "Interactive",
                                Tags = tags,
                                DateAdded = DateTime.UtcNow,
                                LastUsed = DateTime.UtcNow
                            };
                            AuraDatabase.Instance.SaveWallpaper(item);
                        }
                        else
                        {
                            // Ensure thumbnail is linked if missing
                            var match = currentWallpapers.FirstOrDefault(w =>
                                string.Equals(w.FilePath, htmlPath, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(w.Title, dirName, StringComparison.OrdinalIgnoreCase));

                            if (match != null && string.IsNullOrEmpty(match.ThumbnailPath) && File.Exists(thumbPath))
                            {
                                match.ThumbnailPath = thumbPath;
                                match.PreviewPath = thumbPath;
                                match.Title = title;
                                AuraDatabase.Instance.SaveWallpaper(match);
                            }
                        }
                    }
                }
            }
            catch
            {
            }
        }
    }
}
