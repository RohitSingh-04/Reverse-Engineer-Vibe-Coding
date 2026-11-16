using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using AuraWallpaper.Core.Models;
using AuraWallpaper.Core.Native;

namespace AuraWallpaper.Core.Storage
{
    public class WallpaperPackageService
    {
        private static WallpaperPackageService? _instance;
        public static WallpaperPackageService Instance => _instance ??= new WallpaperPackageService();

        private string _libraryDirectory;
        public string LibraryDirectory => _libraryDirectory;

        public WallpaperPackageService(string? customLibDir = null)
        {
            if (!string.IsNullOrEmpty(customLibDir) && Directory.Exists(customLibDir))
            {
                _libraryDirectory = customLibDir;
            }
            else
            {
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                _libraryDirectory = Path.Combine(localApp, "AuraWallpaper", "Library");
            }

            Directory.CreateDirectory(_libraryDirectory);
        }

        public void SetLibraryDirectory(string newPath)
        {
            if (string.IsNullOrWhiteSpace(newPath)) return;
            Directory.CreateDirectory(newPath);
            _libraryDirectory = newPath;
        }

        #region Import Methods

        public WallpaperItem ImportFile(string sourceFilePath)
        {
            if (string.IsNullOrEmpty(sourceFilePath))
                throw new ArgumentException("Source file path cannot be empty.");

            string ext = Path.GetExtension(sourceFilePath).ToLowerInvariant();

            if (ext == ".zip" || ext == ".livelypkg" || ext == ".aurapkg")
            {
                return ImportPackage(sourceFilePath);
            }

            string wallpaperId = Guid.NewGuid().ToString("N");
            string itemDir = Path.Combine(_libraryDirectory, wallpaperId);
            Directory.CreateDirectory(itemDir);

            string fileName = Path.GetFileName(sourceFilePath);
            string destFile = Path.Combine(itemDir, fileName);
            File.Copy(sourceFilePath, destFile, true);

            var type = DetectWallpaperType(destFile);
            string title = Path.GetFileNameWithoutExtension(sourceFilePath);

            var item = new WallpaperItem
            {
                Id = wallpaperId,
                Title = CleanTitle(title),
                Type = type,
                FilePath = destFile,
                DateAdded = DateTime.UtcNow,
                LastUsed = DateTime.UtcNow,
                Category = type.ToString()
            };

            // Generate thumbnail and playback resources
            if (type == WallpaperType.Picture || type == WallpaperType.Gif)
            {
                item.ThumbnailPath = destFile;
                item.PreviewPath = destFile;
            }
            else if (type == WallpaperType.Video)
            {
                string thumbPath = Path.Combine(itemDir, "thumbnail.png");
                if (ShellThumbnailExtractor.TryExtractThumbnail(destFile, thumbPath))
                {
                    item.ThumbnailPath = thumbPath;
                    item.PreviewPath = thumbPath;
                }

                // Generate HTML5 video player wrapper in item directory
                string indexHtml = Path.Combine(itemDir, "index.html");
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
  <video id=""v"" src=""./{fileName}"" autoplay loop muted playsinline></video>
  <script>
    const v = document.getElementById('v');
    v.play().catch(console.error);
  </script>
</body>
</html>";
                try { File.WriteAllText(indexHtml, htmlContent); } catch { }
            }
            else if (type == WallpaperType.Web)
            {
                string thumbPath = Path.Combine(itemDir, "thumbnail.png");
                if (File.Exists(thumbPath))
                {
                    item.ThumbnailPath = thumbPath;
                }
            }

            AuraDatabase.Instance.SaveWallpaper(item);
            return item;
        }

        public WallpaperItem ImportUrl(string url, string? title = null)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("URL cannot be empty.");

            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }

            string wallpaperId = Guid.NewGuid().ToString("N");
            string effectiveTitle = !string.IsNullOrWhiteSpace(title) ? title : new Uri(url).Host;

            var item = new WallpaperItem
            {
                Id = wallpaperId,
                Title = effectiveTitle,
                Type = WallpaperType.Web,
                FilePath = url,
                SourceUrl = url,
                Category = "Web",
                DateAdded = DateTime.UtcNow,
                LastUsed = DateTime.UtcNow
            };

            AuraDatabase.Instance.SaveWallpaper(item);
            return item;
        }

        public WallpaperItem ImportPackage(string packageFilePath)
        {
            if (!File.Exists(packageFilePath))
                throw new FileNotFoundException("Package file not found.", packageFilePath);

            string wallpaperId = Guid.NewGuid().ToString("N");
            string destDir = Path.Combine(_libraryDirectory, wallpaperId);
            Directory.CreateDirectory(destDir);

            // Extract with zip-slip path traversal prevention
            using (var archive = ZipFile.OpenRead(packageFilePath))
            {
                string canonicalDestDir = Path.GetFullPath(destDir);
                if (!canonicalDestDir.EndsWith(Path.DirectorySeparatorChar.ToString()))
                {
                    canonicalDestDir += Path.DirectorySeparatorChar;
                }

                foreach (var entry in archive.Entries)
                {
                    string targetPath = Path.GetFullPath(Path.Combine(destDir, entry.FullName));
                    if (!targetPath.StartsWith(canonicalDestDir, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException($"Security violation: Zip entry '{entry.FullName}' attempts directory traversal outside destination.");
                    }

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        // Directory entry
                        Directory.CreateDirectory(targetPath);
                    }
                    else
                    {
                        string? parent = Path.GetDirectoryName(targetPath);
                        if (!string.IsNullOrEmpty(parent))
                            Directory.CreateDirectory(parent);

                        entry.ExtractToFile(targetPath, true);
                    }
                }
            }

            // Look for LivelyInfo.json or AuraInfo.json
            string infoPath = Path.Combine(destDir, "LivelyInfo.json");
            if (!File.Exists(infoPath))
            {
                infoPath = Path.Combine(destDir, "AuraInfo.json");
            }

            var item = new WallpaperItem
            {
                Id = wallpaperId,
                DateAdded = DateTime.UtcNow,
                LastUsed = DateTime.UtcNow
            };

            if (File.Exists(infoPath))
            {
                string json = File.ReadAllText(infoPath);
                item.LivelyInfoMetadataJson = json;
                ParseManifest(json, destDir, item);
            }
            else
            {
                // Auto-detect entry file in unpacked directory
                FindEntryFileInDirectory(destDir, item);
            }

            // Check for LivelyProperties.json
            string propsPath = Path.Combine(destDir, "LivelyProperties.json");
            if (File.Exists(propsPath))
            {
                item.CustomPropertiesJson = File.ReadAllText(propsPath);
            }

            AuraDatabase.Instance.SaveWallpaper(item);
            return item;
        }

        public void ExportPackage(string wallpaperId, string outputZipPath)
        {
            var item = AuraDatabase.Instance.GetWallpaperById(wallpaperId);
            if (item == null)
                throw new InvalidOperationException("Wallpaper not found.");

            string? itemDir = Path.GetDirectoryName(item.FilePath);
            if (string.IsNullOrEmpty(itemDir) || !Directory.Exists(itemDir))
                throw new InvalidOperationException("Wallpaper directory not found.");

            // Create AuraInfo.json manifest if not present
            string infoPath = Path.Combine(itemDir, "AuraInfo.json");
            var manifestObj = new
            {
                Title = item.Title,
                Description = item.Description,
                Author = item.Author,
                Version = item.Version,
                Contact = item.Contact,
                License = item.License,
                Type = (int)item.Type,
                FileName = Path.GetFileName(item.FilePath),
                Thumbnail = !string.IsNullOrEmpty(item.ThumbnailPath) ? Path.GetFileName(item.ThumbnailPath) : "",
                Preview = !string.IsNullOrEmpty(item.PreviewPath) ? Path.GetFileName(item.PreviewPath) : "",
                Tags = item.Tags,
                Arguments = item.Arguments
            };
            File.WriteAllText(infoPath, JsonSerializer.Serialize(manifestObj, new JsonSerializerOptions { WriteIndented = true }));

            if (File.Exists(outputZipPath))
                File.Delete(outputZipPath);

            ZipFile.CreateFromDirectory(itemDir, outputZipPath);
        }

        #endregion

        #region Helpers

        private static void ParseManifest(string json, string baseDir, WallpaperItem item)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("Title", out var titleProp))
                item.Title = titleProp.GetString() ?? item.Title;

            if (root.TryGetProperty("Desc", out var descProp) || root.TryGetProperty("Description", out descProp))
                item.Description = descProp.GetString() ?? "";

            if (root.TryGetProperty("Author", out var authorProp))
                item.Author = authorProp.GetString() ?? "Unknown";

            if (root.TryGetProperty("Contact", out var contactProp))
                item.Contact = contactProp.GetString() ?? "";

            if (root.TryGetProperty("Version", out var verProp))
                item.Version = verProp.GetString() ?? "1.0.0";

            if (root.TryGetProperty("License", out var licProp))
                item.License = licProp.GetString() ?? "";

            if (root.TryGetProperty("Arguments", out var argProp))
                item.Arguments = argProp.GetString() ?? "";

            if (root.TryGetProperty("Type", out var typeProp))
            {
                if (typeProp.ValueKind == JsonValueKind.Number)
                {
                    item.Type = (WallpaperType)typeProp.GetInt32();
                }
                else if (typeProp.ValueKind == JsonValueKind.String)
                {
                    if (Enum.TryParse<WallpaperType>(typeProp.GetString(), true, out var parsedType))
                        item.Type = parsedType;
                }
            }

            if (root.TryGetProperty("FileName", out var fnProp))
            {
                string? fileName = fnProp.GetString();
                if (!string.IsNullOrEmpty(fileName))
                {
                    item.FilePath = Path.Combine(baseDir, fileName);
                }
            }

            if (root.TryGetProperty("Thumbnail", out var thumbProp))
            {
                string? thumbName = thumbProp.GetString();
                if (!string.IsNullOrEmpty(thumbName))
                {
                    item.ThumbnailPath = Path.Combine(baseDir, thumbName);
                }
            }

            if (root.TryGetProperty("Preview", out var prevProp))
            {
                string? prevName = prevProp.GetString();
                if (!string.IsNullOrEmpty(prevName))
                {
                    item.PreviewPath = Path.Combine(baseDir, prevName);
                }
            }

            if (root.TryGetProperty("Tags", out var tagsProp))
            {
                if (tagsProp.ValueKind == JsonValueKind.Array)
                {
                    var tags = new List<string>();
                    foreach (var elem in tagsProp.EnumerateArray())
                    {
                        var t = elem.GetString();
                        if (!string.IsNullOrEmpty(t)) tags.Add(t);
                    }
                    item.Tags = string.Join(",", tags);
                }
                else if (tagsProp.ValueKind == JsonValueKind.String)
                {
                    item.Tags = tagsProp.GetString() ?? "";
                }
            }
        }

        private static void FindEntryFileInDirectory(string dir, WallpaperItem item)
        {
            // Check for index.html
            string htmlPath = Path.Combine(dir, "index.html");
            if (File.Exists(htmlPath))
            {
                item.FilePath = htmlPath;
                item.Type = WallpaperType.Web;
                item.Title = "Interactive Web Wallpaper";
                return;
            }

            // Look for video or gif or exe
            var files = Directory.GetFiles(dir);
            foreach (var f in files)
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext == ".mp4" || ext == ".webm" || ext == ".mkv" || ext == ".mov" || ext == ".avi")
                {
                    item.FilePath = f;
                    item.Type = WallpaperType.Video;
                    item.Title = CleanTitle(Path.GetFileNameWithoutExtension(f));
                    return;
                }
                if (ext == ".gif")
                {
                    item.FilePath = f;
                    item.Type = WallpaperType.Gif;
                    item.Title = CleanTitle(Path.GetFileNameWithoutExtension(f));
                    return;
                }
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".webp")
                {
                    item.FilePath = f;
                    item.Type = WallpaperType.Picture;
                    item.Title = CleanTitle(Path.GetFileNameWithoutExtension(f));
                    return;
                }
                if (ext == ".exe")
                {
                    item.FilePath = f;
                    item.Type = WallpaperType.App;
                    item.Title = CleanTitle(Path.GetFileNameWithoutExtension(f));
                    return;
                }
            }
        }

        public static WallpaperType DetectWallpaperType(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext switch
            {
                ".mp4" or ".webm" or ".mkv" or ".mov" or ".avi" or ".wmv" or ".flv" => WallpaperType.Video,
                ".html" or ".htm" => WallpaperType.Web,
                ".gif" => WallpaperType.Gif,
                ".jpg" or ".jpeg" or ".png" or ".bmp" or ".webp" => WallpaperType.Picture,
                ".exe" => WallpaperType.App,
                _ => WallpaperType.Video
            };
        }

        private static string CleanTitle(string raw)
        {
            return raw.Replace("_", " ").Replace("-", " ").Trim();
        }

        public long GetStorageUsageBytes()
        {
            if (!Directory.Exists(_libraryDirectory)) return 0;
            return Directory.GetFiles(_libraryDirectory, "*", SearchOption.AllDirectories)
                .Select(f => new FileInfo(f).Length)
                .Sum();
        }

        #endregion
    }
}
