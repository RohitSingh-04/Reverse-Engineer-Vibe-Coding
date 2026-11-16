using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using AuraWallpaper.Core.Audio;
using AuraWallpaper.Core.Native;
using AuraWallpaper.Core.Utils;
using Microsoft.Web.WebView2.Core;

namespace AuraWallpaper.Player.Web
{
    public partial class MainWindow : Window
    {
        private readonly string _targetUrl;
        private readonly int _x;
        private readonly int _y;
        private readonly int _w;
        private readonly int _h;
        private readonly int _monitorIndex;
        private readonly int _volume;
        private readonly bool _isMuted;
        private IntPtr _hwnd = IntPtr.Zero;
        private bool _isInitialized;

        public MainWindow(string url, int x, int y, int w, int h, int mon, int vol, bool mute)
        {
            InitializeComponent();
            _targetUrl = url;
            _x = x;
            _y = y;
            _w = w;
            _h = h;
            _monitorIndex = mon;
            _volume = vol;
            _isMuted = mute;

            Left = x;
            Top = y;
            Width = w;
            Height = h;

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

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_hwnd != IntPtr.Zero)
            {
                DesktopBridge.Instance.AttachToDesktop(_hwnd, _x, _y, _w, _h);
            }
            await InitializeWebViewAsync();
        }

        private async Task InitializeWebViewAsync()
        {
            try
            {
                string dataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AuraWallpaper",
                    $"WebView2Data_mon{_monitorIndex}"
                );
                Directory.CreateDirectory(dataFolder);

                var options = new CoreWebView2EnvironmentOptions(
                    "--autoplay-policy=no-user-gesture-required --disable-background-timer-throttling --disable-renderer-backgrounding --allow-file-access-from-files"
                );
                var env = await CoreWebView2Environment.CreateAsync(null, dataFolder, options);
                await WebBrowser.EnsureCoreWebView2Async(env);

                WebBrowser.CoreWebView2.Settings.IsStatusBarEnabled = false;
                WebBrowser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                WebBrowser.CoreWebView2.Settings.AreDevToolsEnabled = true;
                WebBrowser.CoreWebView2.Settings.IsZoomControlEnabled = false;

                // Inject Aura SDK and Lively Wallpaper backwards-compatibility bridge
                await WebBrowser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(WallpaperSdkBridge.InjectedJavaScript);

                // Subscribe to audio loopback frames
                WasapiAudioCapture.Instance.AudioDataAvailable += OnAudioDataAvailable;

                // Handle crash recovery
                WebBrowser.CoreWebView2.ProcessFailed += OnProcessFailed;

                // Navigate to target
                if (!string.IsNullOrEmpty(_targetUrl))
                {
                    if (File.Exists(_targetUrl))
                    {
                        WebBrowser.CoreWebView2.Navigate(new Uri(_targetUrl).AbsoluteUri);
                    }
                    else if (_targetUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                             _targetUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        WebBrowser.CoreWebView2.Navigate(_targetUrl);
                    }
                }

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebView2 initialization failed: {ex.Message}");
            }
        }

        private void OnAudioDataAvailable(object? sender, AudioFrameData frame)
        {
            if (!_isInitialized || WebBrowser?.CoreWebView2 == null) return;

            try
            {
                string json = WallpaperSdkBridge.CreateAudioMessageJson(frame);
                Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        WebBrowser.CoreWebView2.PostWebMessageAsJson(json);
                    }
                    catch
                    {
                    }
                });
            }
            catch
            {
            }
        }

        private void OnProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
        {
            // Automatic recovery on renderer crash
            Dispatcher.InvokeAsync(async () =>
            {
                await Task.Delay(1000);
                if (WebBrowser?.CoreWebView2 != null)
                {
                    WebBrowser.Reload();
                }
            });
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            WasapiAudioCapture.Instance.AudioDataAvailable -= OnAudioDataAvailable;
            DesktopBridge.Instance.DetachFromDesktop(_hwnd);
        }
    }
}
