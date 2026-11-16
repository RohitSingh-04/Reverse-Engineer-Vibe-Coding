using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AuraWallpaper.Core.Models;
using AuraWallpaper.Core.Native;
using AuraWallpaper.Core.Storage;

namespace AuraWallpaper.Screensaver
{
    public partial class ScreensaverWindow : Window
    {
        private readonly bool _isPreview;
        private readonly IntPtr _parentHwnd = IntPtr.Zero;
        private readonly MonitorDevice? _monitor;
        private Point? _lastMousePos;

        public ScreensaverWindow(MonitorDevice monitor)
        {
            InitializeComponent();
            _isPreview = false;
            _monitor = monitor;

            Left = monitor.Bounds.X;
            Top = monitor.Bounds.Y;
            Width = monitor.Bounds.Width;
            Height = monitor.Bounds.Height;

            Loaded += OnLoaded;
        }

        public ScreensaverWindow(IntPtr previewHwnd)
        {
            InitializeComponent();
            _isPreview = true;
            _parentHwnd = previewHwnd;

            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = false;

            SourceInitialized += (s, e) =>
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                NativeMethods.SetParent(hwnd, _parentHwnd);
                NativeMethods.GetClientRect(_parentHwnd, out var rc);
                NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, rc.Width, rc.Height, NativeMethods.SWP_SHOWWINDOW);
            };

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            LoadWallpaperContent();
        }

        private void LoadWallpaperContent()
        {
            var settings = AuraDatabase.Instance.LoadSettings();
            WallpaperItem? item = null;

            if (settings.ScreensaverSourceMode == ScreensaverSource.ActiveWallpaper)
            {
                var assignments = AuraDatabase.Instance.GetMonitorAssignments();
                var active = assignments.FirstOrDefault();
                if (active != null)
                {
                    item = AuraDatabase.Instance.GetWallpaperById(active.WallpaperId);
                }
            }
            else if (settings.ScreensaverSourceMode == ScreensaverSource.SpecificWallpaper &&
                     !string.IsNullOrEmpty(settings.ScreensaverWallpaperId))
            {
                item = AuraDatabase.Instance.GetWallpaperById(settings.ScreensaverWallpaperId);
            }

            if (item == null)
            {
                // Fallback to first installed wallpaper
                item = AuraDatabase.Instance.GetAllWallpapers().FirstOrDefault();
            }

            if (item == null)
            {
                // Empty fallback screen
                return;
            }

            if (item.Type == WallpaperType.Video)
            {
                VideoPlayer.Visibility = Visibility.Visible;
                VideoPlayer.Stretch = Stretch.UniformToFill;
                VideoPlayer.Source = new Uri(item.FilePath);
                VideoPlayer.IsMuted = true;
                VideoPlayer.MediaEnded += (s, a) =>
                {
                    VideoPlayer.Position = TimeSpan.Zero;
                    VideoPlayer.Play();
                };
                VideoPlayer.Play();
            }
            else if (item.Type == WallpaperType.Picture || item.Type == WallpaperType.Gif)
            {
                ImagePlayer.Visibility = Visibility.Visible;
                ImagePlayer.Stretch = Stretch.UniformToFill;
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(item.FilePath);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    ImagePlayer.Source = bmp;
                }
                catch
                {
                }
            }
            else if (item.Type == WallpaperType.Web)
            {
                WebPlayer.Visibility = Visibility.Visible;
                InitializeWebAsync(item.FilePath);
            }
        }

        private async void InitializeWebAsync(string url)
        {
            try
            {
                await WebPlayer.EnsureCoreWebView2Async();
                if (File.Exists(url)) WebPlayer.CoreWebView2.Navigate(new Uri(url).AbsoluteUri);
                else WebPlayer.CoreWebView2.Navigate(url);
            }
            catch
            {
            }
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isPreview) return;

            var pos = e.GetPosition(this);
            if (!_lastMousePos.HasValue)
            {
                _lastMousePos = pos;
                return;
            }

            // Dismiss screensaver if moved more than 10 pixels
            if (Math.Abs(pos.X - _lastMousePos.Value.X) > 10 || Math.Abs(pos.Y - _lastMousePos.Value.Y) > 10)
            {
                Application.Current.Shutdown();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (!_isPreview)
            {
                Application.Current.Shutdown();
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isPreview)
            {
                Application.Current.Shutdown();
            }
        }
    }
}
