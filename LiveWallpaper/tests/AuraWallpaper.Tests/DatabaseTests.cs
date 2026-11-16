using System;
using System.IO;
using AuraWallpaper.Core.Models;
using AuraWallpaper.Core.Storage;
using Xunit;

namespace AuraWallpaper.Tests
{
    public class DatabaseTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly AuraDatabase _db;

        public DatabaseTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"aura_test_{Guid.NewGuid():N}.db");
            _db = new AuraDatabase(_testDbPath);
        }

        [Fact]
        public void SaveAndRetrieveWallpaper_PersistsCorrectly()
        {
            var item = new WallpaperItem
            {
                Id = "test_wp_01",
                Title = "Matrix Rain",
                Author = "AuraDev",
                Type = WallpaperType.Web,
                FilePath = "C:\\Wallpapers\\matrix\\index.html",
                Category = "Sci-Fi",
                IsFavorite = true
            };

            _db.SaveWallpaper(item);

            var retrieved = _db.GetWallpaperById("test_wp_01");
            Assert.NotNull(retrieved);
            Assert.Equal("Matrix Rain", retrieved.Title);
            Assert.Equal("AuraDev", retrieved.Author);
            Assert.Equal(WallpaperType.Web, retrieved.Type);
            Assert.True(retrieved.IsFavorite);

            bool deleted = _db.DeleteWallpaper("test_wp_01");
            Assert.True(deleted);
            Assert.Null(_db.GetWallpaperById("test_wp_01"));
        }

        [Fact]
        public void MonitorAssignment_SavesAndRetrieves()
        {
            var assignment = new MonitorAssignment
            {
                MonitorDeviceId = "DISPLAY1",
                MonitorIndex = 0,
                WallpaperId = "wp_100",
                Mode = AssignmentMode.PerMonitor,
                Volume = 65,
                IsMuted = false
            };

            _db.SaveMonitorAssignment(assignment);

            var list = _db.GetMonitorAssignments();
            Assert.NotEmpty(list);
            var matched = list.Find(a => a.MonitorDeviceId == "DISPLAY1");
            Assert.NotNull(matched);
            Assert.Equal(65, matched.Volume);
            Assert.Equal("wp_100", matched.WallpaperId);

            _db.ClearMonitorAssignment("DISPLAY1");
            Assert.Empty(_db.GetMonitorAssignments().FindAll(a => a.MonitorDeviceId == "DISPLAY1"));
        }

        [Fact]
        public void Playlists_SaveWithItems()
        {
            var playlist = new Playlist
            {
                Id = "playlist_01",
                Name = "Chill Night",
                RotationIntervalSeconds = 600,
                IsShuffle = true,
                IsRepeat = true
            };
            playlist.Items.Add(new PlaylistItem { Id = "item_1", PlaylistId = playlist.Id, WallpaperId = "wp_a", OrderIndex = 0 });
            playlist.Items.Add(new PlaylistItem { Id = "item_2", PlaylistId = playlist.Id, WallpaperId = "wp_b", OrderIndex = 1 });

            _db.SavePlaylist(playlist);

            var retrieved = _db.GetAllPlaylists().Find(p => p.Id == "playlist_01");
            Assert.NotNull(retrieved);
            Assert.Equal("Chill Night", retrieved.Name);
            Assert.True(retrieved.IsShuffle);
            Assert.Equal(2, retrieved.Items.Count);

            _db.DeletePlaylist("playlist_01");
            Assert.Null(_db.GetAllPlaylists().Find(p => p.Id == "playlist_01"));
        }

        [Fact]
        public void Settings_SaveAndLoad()
        {
            var settings = new AuraSettings
            {
                GlobalVolume = 42,
                LaunchOnStartup = true,
                TaskbarMode = Core.Native.TaskbarThemeMode.Acrylic,
                BatteryAction = RuleAction.Pause
            };

            _db.SaveSettings(settings);

            var loaded = _db.LoadSettings();
            Assert.Equal(42, loaded.GlobalVolume);
            Assert.True(loaded.LaunchOnStartup);
            Assert.Equal(Core.Native.TaskbarThemeMode.Acrylic, loaded.TaskbarMode);
            Assert.Equal(RuleAction.Pause, loaded.BatteryAction);
        }

        public void Dispose()
        {
            try
            {
                _db.Dispose();
                if (File.Exists(_testDbPath))
                    File.Delete(_testDbPath);
            }
            catch
            {
            }
        }
    }
}
