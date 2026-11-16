using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AuraWallpaper.Core.Models;
using AuraWallpaper.Core.Storage;

namespace AuraWallpaper.Core.Engine
{
    public class PlaylistCoordinator : IDisposable
    {
        private static PlaylistCoordinator? _instance;
        public static PlaylistCoordinator Instance => _instance ??= new PlaylistCoordinator();

        private readonly Dictionary<string, Timer> _activeTimers = new();
        private readonly Dictionary<string, int> _currentIndexes = new();
        private readonly Random _random = new();

        public event Action<string, string, int>? WallpaperRotationRequested; // playlistId, wallpaperId, monitorIndex

        private PlaylistCoordinator()
        {
        }

        public void StartPlaylist(Playlist playlist)
        {
            StopPlaylist(playlist.Id);

            if (playlist.Items.Count == 0) return;

            playlist.IsActive = true;
            AuraDatabase.Instance.SavePlaylist(playlist);

            _currentIndexes[playlist.Id] = 0;
            // Apply first wallpaper immediately
            RotateNext(playlist);

            int intervalMs = Math.Max(5, playlist.RotationIntervalSeconds) * 1000;
            var timer = new Timer(s =>
            {
                var current = AuraDatabase.Instance.GetAllPlaylists().FirstOrDefault(p => p.Id == playlist.Id);
                if (current != null && current.IsActive)
                {
                    RotateNext(current);
                }
            }, null, intervalMs, intervalMs);

            _activeTimers[playlist.Id] = timer;
        }

        public void StopPlaylist(string playlistId)
        {
            if (_activeTimers.TryGetValue(playlistId, out var timer))
            {
                timer.Dispose();
                _activeTimers.Remove(playlistId);
            }
            _currentIndexes.Remove(playlistId);
        }

        public void RotateNext(Playlist playlist)
        {
            if (playlist.Items.Count == 0) return;

            int nextIndex;
            if (playlist.IsShuffle && playlist.Items.Count > 1)
            {
                nextIndex = _random.Next(0, playlist.Items.Count);
            }
            else
            {
                int current = _currentIndexes.GetValueOrDefault(playlist.Id, 0);
                nextIndex = (current + 1) % playlist.Items.Count;
            }

            _currentIndexes[playlist.Id] = nextIndex;
            var item = playlist.Items[nextIndex];
            WallpaperRotationRequested?.Invoke(playlist.Id, item.WallpaperId, playlist.TargetMonitorIndex);
        }

        public void RotatePrevious(Playlist playlist)
        {
            if (playlist.Items.Count == 0) return;

            int current = _currentIndexes.GetValueOrDefault(playlist.Id, 0);
            int prevIndex = (current - 1 + playlist.Items.Count) % playlist.Items.Count;

            _currentIndexes[playlist.Id] = prevIndex;
            var item = playlist.Items[prevIndex];
            WallpaperRotationRequested?.Invoke(playlist.Id, item.WallpaperId, playlist.TargetMonitorIndex);
        }

        public void Dispose()
        {
            foreach (var timer in _activeTimers.Values)
            {
                timer.Dispose();
            }
            _activeTimers.Clear();
        }
    }
}
