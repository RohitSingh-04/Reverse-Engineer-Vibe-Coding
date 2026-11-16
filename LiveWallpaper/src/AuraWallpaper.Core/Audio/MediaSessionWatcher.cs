using System;

namespace AuraWallpaper.Core.Audio
{
    public class MediaTrackInfo
    {
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Album { get; set; } = string.Empty;
        public bool IsPlaying { get; set; }
    }

    public class MediaSessionWatcher
    {
        private static MediaSessionWatcher? _instance;
        public static MediaSessionWatcher Instance => _instance ??= new MediaSessionWatcher();

        public MediaTrackInfo CurrentTrack { get; private set; } = new MediaTrackInfo();
        public event EventHandler<MediaTrackInfo>? TrackChanged;

        private MediaSessionWatcher()
        {
        }

        public void UpdateTrackInfo(string title, string artist, string album, bool isPlaying)
        {
            CurrentTrack = new MediaTrackInfo
            {
                Title = title,
                Artist = artist,
                Album = album,
                IsPlaying = isPlaying
            };
            TrackChanged?.Invoke(this, CurrentTrack);
        }
    }
}
