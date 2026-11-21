using System;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AuraWallpaper.Core.Models;
using AuraWallpaper.Core.Native;

namespace AuraWallpaper.Player.Media
{
    public partial class MainWindow : Window
    {
        private readonly string _filePath;
        private readonly WallpaperType _type;
        private readonly int _x;
        private readonly int _y;
        private readonly int _w;
        private readonly int _h;
        private readonly int _monitorIndex;
        private readonly int _volume;
        private readonly bool _isMuted;
        private readonly WallpaperScaling _scaling;
        private IntPtr _hwnd = IntPtr.Zero;

        public MainWindow(string file, WallpaperType type, int x, int y, int w, int h, int mon, int vol, bool mute, WallpaperScaling scale)
        {
            InitializeComponent();
            _filePath = file;
            _type = type;
            _x = x;
            _y = y;
            _w = w;
            _h = h;
            _monitorIndex = mon;
            _volume = vol;
            _isMuted = mute;
            _scaling = scale;

            SourceInitialized += OnSourceInitialized;
            Loaded += OnLoaded;
            Closed += OnClosed;
        }

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            // Attach to desktop WorkerW behind icons
            DesktopBridge.Instance.AttachToDesktop(_hwnd, _x, _y, _w, _h);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_hwnd != IntPtr.Zero)
            {
                DesktopBridge.Instance.AttachToDesktop(_hwnd, _x, _y, _w, _h);
            }

            if (string.IsNullOrEmpty(_filePath) || !File.Exists(_filePath))
                return;

            var stretchMode = _scaling switch
            {
                WallpaperScaling.Fit => Stretch.Uniform,
                WallpaperScaling.Stretch => Stretch.Fill,
                WallpaperScaling.Center => Stretch.None,
                _ => Stretch.UniformToFill
            };

            if (_type == WallpaperType.Video)
            {
                MediaPlayer.Visibility = Visibility.Visible;
                MediaPlayer.Stretch = stretchMode;
                MediaPlayer.Source = new Uri(_filePath);
                MediaPlayer.Volume = _volume / 100.0;
                MediaPlayer.IsMuted = _isMuted;
                MediaPlayer.MediaEnded += (s, args) =>
                {
                    MediaPlayer.Position = TimeSpan.Zero;
                    MediaPlayer.Play();
                };
                MediaPlayer.MediaFailed += (s, args) =>
                {
                    System.Diagnostics.Debug.WriteLine($"Media failed: {args.ErrorException.Message}");
                };
                MediaPlayer.Play();
            }
            else
            {
                // Picture or GIF
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(_filePath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();

                    if (_scaling == WallpaperScaling.Fit)
                    {
                        AmbientPicturePlayer.Source = bitmap;
                        AmbientPicturePlayer.Visibility = Visibility.Visible;
                    }

                    PicturePlayer.Visibility = Visibility.Visible;
                    PicturePlayer.Stretch = stretchMode;
                    PicturePlayer.Source = bitmap;
                }
                catch
                {
                }
            }
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            if (_type == WallpaperType.Video)
            {
                MediaPlayer.Stop();
                MediaPlayer.Close();
            }
            DesktopBridge.Instance.DetachFromDesktop(_hwnd);
        }
    }
}
