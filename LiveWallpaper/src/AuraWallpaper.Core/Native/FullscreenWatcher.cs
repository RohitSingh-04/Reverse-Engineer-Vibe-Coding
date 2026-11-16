using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace AuraWallpaper.Core.Native
{
    public class FullscreenChangedEventArgs : EventArgs
    {
        public bool IsFullscreen { get; set; }
        public bool IsMaximized { get; set; }
        public string? ProcessName { get; set; }
        public int MonitorIndex { get; set; }
    }

    public class FullscreenWatcher : IDisposable
    {
        private static FullscreenWatcher? _instance;
        public static FullscreenWatcher Instance => _instance ??= new FullscreenWatcher();

        private readonly Timer? _timer;
        private bool _lastFullscreen;
        private string? _lastProcess;
        private IntPtr _hook = IntPtr.Zero;
        private NativeMethods.WinEventDelegate? _hookDelegate;

        public event EventHandler<FullscreenChangedEventArgs>? FullscreenChanged;

        private FullscreenWatcher()
        {
            // Lightweight timer polling every 1 second
            _timer = new Timer(CheckForegroundWindow, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(1000));

            try
            {
                _hookDelegate = new NativeMethods.WinEventDelegate(WinEventProc);
                _hook = NativeMethods.SetWinEventHook(
                    NativeMethods.EVENT_SYSTEM_FOREGROUND,
                    NativeMethods.EVENT_SYSTEM_FOREGROUND,
                    IntPtr.Zero,
                    _hookDelegate,
                    0,
                    0,
                    NativeMethods.WINEVENT_OUTOFCONTEXT
                );
            }
            catch
            {
                // Fallback to timer
            }
        }

        private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            CheckForegroundWindow(null);
        }

        public void CheckForegroundWindow(object? state)
        {
            try
            {
                IntPtr hwnd = NativeMethods.GetForegroundWindow();
                if (hwnd == IntPtr.Zero)
                    return;

                var className = new StringBuilder(256);
                NativeMethods.GetClassName(hwnd, className, 256);
                string cls = className.ToString();

                string processName = "";
                try
                {
                    NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
                    if (pid != 0)
                    {
                        var proc = Process.GetProcessById((int)pid);
                        processName = proc.ProcessName;
                    }
                }
                catch
                {
                    // Process may have exited
                }

                // Skip desktop, shell components, Aura wallpaper windows, and any child windows
                IntPtr parentHwnd = NativeMethods.GetParent(hwnd);
                if (parentHwnd != IntPtr.Zero ||
                    cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd" || cls == "Windows.UI.Core.CoreWindow" ||
                    processName.StartsWith("AuraWallpaper", StringComparison.OrdinalIgnoreCase) ||
                    processName.StartsWith("AuraScreensaver", StringComparison.OrdinalIgnoreCase) ||
                    cls.Contains("AuraWallpaper", StringComparison.OrdinalIgnoreCase))
                {
                    if (_lastFullscreen)
                    {
                        _lastFullscreen = false;
                        _lastProcess = null;
                        FullscreenChanged?.Invoke(this, new FullscreenChangedEventArgs { IsFullscreen = false });
                    }
                    return;
                }

                if (!NativeMethods.GetWindowRect(hwnd, out var rect))
                    return;

                var monitors = DisplayManager.Instance.GetMonitors();
                bool isFullscreen = false;
                int matchedMon = 0;

                for (int i = 0; i < monitors.Count; i++)
                {
                    var mon = monitors[i];
                    // If window bounds match or cover the monitor bounds
                    if (rect.Left <= mon.Bounds.X &&
                        rect.Top <= mon.Bounds.Y &&
                        rect.Right >= (mon.Bounds.X + mon.Bounds.Width) &&
                        rect.Bottom >= (mon.Bounds.Y + mon.Bounds.Height))
                    {
                        isFullscreen = true;
                        matchedMon = i;
                        break;
                    }
                }

                if (isFullscreen != _lastFullscreen || processName != _lastProcess)
                {
                    _lastFullscreen = isFullscreen;
                    _lastProcess = processName;

                    FullscreenChanged?.Invoke(this, new FullscreenChangedEventArgs
                    {
                        IsFullscreen = isFullscreen,
                        ProcessName = processName,
                        MonitorIndex = matchedMon
                    });
                }
            }
            catch
            {
                // Never throw in background watcher
            }
        }

        public void Dispose()
        {
            if (_hook != IntPtr.Zero)
            {
                NativeMethods.UnhookWinEvent(_hook);
                _hook = IntPtr.Zero;
            }
            _timer?.Dispose();
        }
    }
}
