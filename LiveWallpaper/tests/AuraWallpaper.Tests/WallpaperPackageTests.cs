using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using AuraWallpaper.Core.Models;
using AuraWallpaper.Core.Storage;
using Xunit;

namespace AuraWallpaper.Tests
{
    public class WallpaperPackageTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly WallpaperPackageService _service;

        public WallpaperPackageTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "AuraTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _service = new WallpaperPackageService(_tempDir);
        }

        [Fact]
        public void DetectWallpaperType_IdentifiesFormatsCorrectly()
        {
            Assert.Equal(WallpaperType.Video, WallpaperPackageService.DetectWallpaperType("test.mp4"));
            Assert.Equal(WallpaperType.Video, WallpaperPackageService.DetectWallpaperType("test.webm"));
            Assert.Equal(WallpaperType.Web, WallpaperPackageService.DetectWallpaperType("index.html"));
            Assert.Equal(WallpaperType.Gif, WallpaperPackageService.DetectWallpaperType("animation.gif"));
            Assert.Equal(WallpaperType.Picture, WallpaperPackageService.DetectWallpaperType("photo.png"));
            Assert.Equal(WallpaperType.Picture, WallpaperPackageService.DetectWallpaperType("photo.jpg"));
            Assert.Equal(WallpaperType.App, WallpaperPackageService.DetectWallpaperType("game.exe"));
        }

        [Fact]
        public void ImportUrl_CreatesValidWebWallpaper()
        {
            var item = _service.ImportUrl("https://example.com/interactive", "Example Web");
            Assert.NotNull(item);
            Assert.Equal("Example Web", item.Title);
            Assert.Equal(WallpaperType.Web, item.Type);
            Assert.Equal("https://example.com/interactive", item.FilePath);
        }

        [Fact]
        public void ImportPackage_ParsesLivelyInfoManifest()
        {
            string pkgDir = Path.Combine(_tempDir, "sample_pkg");
            Directory.CreateDirectory(pkgDir);

            // Write index.html
            File.WriteAllText(Path.Combine(pkgDir, "index.html"), "<html><body>Wallpaper</body></html>");

            // Write LivelyInfo.json
            var manifest = new
            {
                Title = "Neon Cyber City",
                Author = "AuraArtist",
                Description = "A futuristic neon city interactive wallpaper",
                Version = "1.2.0",
                Type = 1, // Web
                FileName = "index.html",
                Tags = new[] { "cyberpunk", "neon", "interactive" }
            };
            File.WriteAllText(Path.Combine(pkgDir, "LivelyInfo.json"), JsonSerializer.Serialize(manifest));

            // Create ZIP package
            string zipPath = Path.Combine(_tempDir, "cyber.zip");
            ZipFile.CreateFromDirectory(pkgDir, zipPath);

            // Import package
            var imported = _service.ImportPackage(zipPath);

            Assert.NotNull(imported);
            Assert.Equal("Neon Cyber City", imported.Title);
            Assert.Equal("AuraArtist", imported.Author);
            Assert.Equal(WallpaperType.Web, imported.Type);
            Assert.True(File.Exists(imported.FilePath));
            Assert.Contains("cyberpunk", imported.Tags);
        }

        [Fact]
        public void ImportPackage_BlocksZipSlipPathTraversal()
        {
            string zipPath = Path.Combine(_tempDir, "malicious.zip");
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                // Create an entry attempting path traversal outside destination
                var entry = zip.CreateEntry(@"..\..\..\evil.txt");
                using var writer = new StreamWriter(entry.Open());
                writer.WriteLine("malicious payload");
            }

            Assert.Throws<InvalidOperationException>(() =>
            {
                _service.ImportPackage(zipPath);
            });
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                    Directory.Delete(_tempDir, true);
            }
            catch
            {
            }
        }
    }
}
