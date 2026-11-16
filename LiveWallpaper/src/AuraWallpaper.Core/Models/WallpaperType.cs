namespace AuraWallpaper.Core.Models
{
    public enum WallpaperType
    {
        Video = 0,
        Web = 1,
        Picture = 2,
        Gif = 3,
        App = 4
    }

    public enum WallpaperScaling
    {
        Fill = 0,
        Fit = 1,
        Stretch = 2,
        Center = 3,
        Span = 4
    }

    public enum AssignmentMode
    {
        PerMonitor = 0,
        Span = 1,
        Duplicate = 2
    }

    public enum PlaybackState
    {
        Playing = 0,
        Paused = 1,
        Stopped = 2
    }
}
