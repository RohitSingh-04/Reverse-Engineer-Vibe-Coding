using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Interop;
using Microsoft.Win32;
using TransparentTaskbar.Models;
using TransparentTaskbar.Native;

namespace TransparentTaskbar.Services
{
    public class TaskbarService : IDisposable
    {
        private AppConfig _currentConfig;
        private readonly uint _wmTaskbarCreated;
        private readonly object _applyLock = new();
        private IntPtr _winEventHook = IntPtr.Zero;
        private NativeMethods.WinEventDelegate? _winEventProc;
        private CancellationTokenSource? _watcherCts;
        private bool _isDisposed;

        public TaskbarService()
        {
            _currentConfig = new AppConfig();

            // 1. Register TaskbarCreated message to immediately re-apply transparency if explorer restarts
            _wmTaskbarCreated = NativeMethods.RegisterWindowMessage("TaskbarCreated");
            ComponentDispatcher.ThreadFilterMessage += OnThreadFilterMessage;

            // 2. React immediately to system theme or display resolution changes
            SystemEvents.DisplaySettingsChanged += OnSystemEnvironmentChanged;
            SystemEvents.UserPreferenceChanged += OnSystemEnvironmentChanged;

            // 3. Set up OS WinEventHook for 0-latency instant detection of taskbar and bridge window creation
            try
            {
                _winEventProc = OnWinEvent;
                _winEventHook = NativeMethods.SetWinEventHook(
                    NativeMethods.EVENT_OBJECT_CREATE,
                    NativeMethods.EVENT_OBJECT_SHOW,
                    IntPtr.Zero,
                    _winEventProc,
                    0,
                    0,
                    NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TaskbarService] WinEventHook setup notice: {ex.Message}");
            }
        }

        private void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (_isDisposed || hwnd == IntPtr.Zero) return;

            // Check if created or shown window is the taskbar or its XAML content bridge
            try
            {
                var sb = new StringBuilder(64);
                NativeMethods.GetClassName(hwnd, sb, sb.Capacity);
                string className = sb.ToString();

                if (className == "Shell_TrayWnd" ||
                    className == "Shell_SecondaryTrayWnd" ||
                    className.Contains("DesktopWindowContentBridge", StringComparison.OrdinalIgnoreCase))
                {
                    ApplyCurrent();
                }
            }
            catch
            {
                // Ignore transient window access errors during desktop destruction/creation
            }
        }

        private void OnThreadFilterMessage(ref MSG msg, ref bool handled)
        {
            if (_wmTaskbarCreated != 0 && (uint)msg.message == _wmTaskbarCreated)
            {
                ApplyCurrent();
            }
        }

        private void OnSystemEnvironmentChanged(object? sender, EventArgs e)
        {
            ApplyCurrent();
        }

        public void Start(AppConfig config)
        {
            _currentConfig = config;

            // Apply immediately
            ApplyCurrent();

            // Start aggressive Priority-1 Startup Watcher to catch the exact millisecond the taskbar finishes rendering
            StartStartupWatcher();
        }

        public void UpdateConfig(AppConfig config)
        {
            _currentConfig = config;
            // Apply immediately for instant real-time feedback
            ApplyCurrent();
        }

        public void Stop()
        {
            _watcherCts?.Cancel();
            RestoreDefault();
        }

        private void StartStartupWatcher()
        {
            _watcherCts?.Cancel();
            _watcherCts = new CancellationTokenSource();
            var token = _watcherCts.Token;

            Task.Run(async () =>
            {
                // Startup Sprint Phase (first 10 seconds of login / process start):
                // Polls every 40ms for the first 3 seconds, then every 120ms up to 10 seconds.
                // Catches the exact instant Shell_TrayWnd or DesktopWindowContentBridge is created by Explorer!
                var sw = Stopwatch.StartNew();
                while (!token.IsCancellationRequested && sw.ElapsedMilliseconds < 10000)
                {
                    if (!IsTaskbarFullyStyled())
                    {
                        ApplyCurrent();
                    }

                    int delay = sw.ElapsedMilliseconds < 3000 ? 40 : 120;
                    try
                    {
                        await Task.Delay(delay, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }

                // Steady-State Maintenance Phase:
                // Periodically verifies that taskbar transparency is preserved across any explorer hiccups or restarts
                while (!token.IsCancellationRequested)
                {
                    if (!IsTaskbarFullyStyled())
                    {
                        ApplyCurrent();
                    }

                    try
                    {
                        await Task.Delay(1500, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
            }, token);
        }

        private bool IsTaskbarFullyStyled()
        {
            if (_currentConfig.Style == TaskbarStyleMode.Default) return true;

            var windows = GetAllTaskbarWindows();
            int count = 0;
            bool allStyled = true;

            foreach (var trayHwnd in windows)
            {
                count++;
                bool hasBridge = false;

                NativeMethods.EnumChildWindows(trayHwnd, (hChild, l) =>
                {
                    var sb = new StringBuilder(128);
                    NativeMethods.GetClassName(hChild, sb, sb.Capacity);
                    if (sb.ToString().Contains("DesktopWindowContentBridge", StringComparison.OrdinalIgnoreCase))
                    {
                        hasBridge = true;
                        int ex = NativeMethods.GetWindowLong(hChild, NativeMethods.GWL_EXSTYLE);
                        if ((ex & NativeMethods.WS_EX_LAYERED) == 0)
                        {
                            allStyled = false;
                            return false;
                        }
                    }
                    return true;
                }, IntPtr.Zero);

                if (!hasBridge)
                {
                    allStyled = false;
                }
            }

            return count > 0 && allStyled;
        }

        public void ApplyCurrent()
        {
            if (_isDisposed) return;

            lock (_applyLock)
            {
                var (state, flags, color) = GetAccentParameters(_currentConfig);

                // Calculate bridge alpha:
                // CRITICAL: Icons must NEVER vanish into invisibility under any circumstance.
                // Even at 0% opacity (100% transparency), icons and system tools stay solid, crisp, and visible.
                byte bridgeAlpha;
                if (_currentConfig.PreserveSolidIcons)
                {
                    // Solid, razor-sharp icons: alpha range [235, 255]
                    bridgeAlpha = (byte)Math.Clamp(235 + (int)Math.Round((_currentConfig.Opacity / 100.0) * 20.0), 235, 255);
                }
                else
                {
                    // Smooth range that never drops below 200, so icons NEVER vanish
                    bridgeAlpha = (byte)Math.Clamp(200 + (int)Math.Round((_currentConfig.Opacity / 100.0) * 55.0), 200, 255);
                }

                int topCutoff = _currentConfig.RemoveTopBorder ? 2 : 0;

                foreach (var hwnd in GetAllTaskbarWindows())
                {
                    if (_currentConfig.Style == TaskbarStyleMode.Default)
                    {
                        int backdrop = 0;
                        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));

                        SetTaskbarAccent(hwnd, AccentState.ACCENT_DISABLED, 0, 0);

                        // Restore window region to full default
                        NativeMethods.SetWindowRgn(hwnd, IntPtr.Zero, true);

                        RestoreTaskbarChildren(hwnd);

                        NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_FRAMECHANGED);
                        NativeMethods.RedrawWindow(hwnd, IntPtr.Zero, IntPtr.Zero,
                            NativeMethods.RDW_INVALIDATE | NativeMethods.RDW_ERASE | NativeMethods.RDW_UPDATENOW | NativeMethods.RDW_FRAME | NativeMethods.RDW_ALLCHILDREN);
                    }
                    else
                    {
                        // 1. Set Windows 11 DWM backdrop type
                        int backdrop = _currentConfig.Style switch
                        {
                            TaskbarStyleMode.Clear => 1,    // None / Clear
                            TaskbarStyleMode.Acrylic => 3,  // Acrylic
                            TaskbarStyleMode.Blur => 3,     // Blur
                            _ => 0
                        };
                        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));

                        // 2. Apply DWM Window Composition Attribute on the taskbar shell window
                        SetTaskbarAccent(hwnd, state, flags, color);

                        // 3. Apply top border removal on shell window if requested
                        if (_currentConfig.RemoveTopBorder && NativeMethods.GetWindowRect(hwnd, out NativeMethods.RECT tr))
                        {
                            int tw = tr.Right - tr.Left;
                            int th = tr.Bottom - tr.Top;
                            if (tw > 0 && th > topCutoff)
                            {
                                IntPtr rgnTray = NativeMethods.CreateRectRgn(0, topCutoff, tw, th);
                                NativeMethods.SetWindowRgn(hwnd, rgnTray, true);
                            }
                        }
                        else
                        {
                            NativeMethods.SetWindowRgn(hwnd, IntPtr.Zero, true);
                        }

                        // 4. Adjust XAML island content bridge (DesktopWindowContentBridge)
                        ApplyToTaskbarChildren(hwnd, _currentConfig, bridgeAlpha, topCutoff);

                        // 5. Force immediate real-time DWM frame redraw without stealing active focus
                        NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_FRAMECHANGED);
                        NativeMethods.RedrawWindow(hwnd, IntPtr.Zero, IntPtr.Zero,
                            NativeMethods.RDW_INVALIDATE | NativeMethods.RDW_ERASE | NativeMethods.RDW_UPDATENOW | NativeMethods.RDW_FRAME | NativeMethods.RDW_ALLCHILDREN);
                    }
                }
            }
        }

        public void RestoreDefault()
        {
            lock (_applyLock)
            {
                foreach (var hwnd in GetAllTaskbarWindows())
                {
                    int backdrop = 0; // Auto/Default
                    NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));

                    SetTaskbarAccent(hwnd, AccentState.ACCENT_DISABLED, 0, 0);

                    NativeMethods.SetWindowRgn(hwnd, IntPtr.Zero, true);

                    NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                        NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_FRAMECHANGED);
                    NativeMethods.RedrawWindow(hwnd, IntPtr.Zero, IntPtr.Zero,
                        NativeMethods.RDW_INVALIDATE | NativeMethods.RDW_ERASE | NativeMethods.RDW_UPDATENOW | NativeMethods.RDW_FRAME | NativeMethods.RDW_ALLCHILDREN);

                    RestoreTaskbarChildren(hwnd);
                }
            }
        }

        private static (AccentState state, int flags, int color) GetAccentParameters(AppConfig config)
        {
            byte alpha = config.AlphaByte;
            int abgrColor = ParseColorToAbgr(config.TintColorHex, alpha);

            switch (config.Style)
            {
                case TaskbarStyleMode.Clear:
                    return (AccentState.ACCENT_ENABLE_TRANSPARENTGRADIENT, 2, alpha == 0 ? 0 : abgrColor);

                case TaskbarStyleMode.Acrylic:
                    return (AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND, 2, abgrColor);

                case TaskbarStyleMode.Blur:
                    return (AccentState.ACCENT_ENABLE_BLURBEHIND, 0, 0);

                case TaskbarStyleMode.Default:
                default:
                    return (AccentState.ACCENT_DISABLED, 0, 0);
            }
        }

        private static void ApplyToTaskbarChildren(IntPtr trayHwnd, AppConfig config, byte bridgeAlpha, int topCutoff)
        {
            if (trayHwnd == IntPtr.Zero) return;

            NativeMethods.EnumChildWindows(trayHwnd, (hChild, l) =>
            {
                var sb = new StringBuilder(128);
                NativeMethods.GetClassName(hChild, sb, sb.Capacity);
                string className = sb.ToString();

                if (className.Contains("DesktopWindowContentBridge", StringComparison.OrdinalIgnoreCase))
                {
                    if (config.Style == TaskbarStyleMode.Default)
                    {
                        RestoreChildBridge(hChild);
                    }
                    else
                    {
                        if (!NativeMethods.IsWindowVisible(hChild))
                        {
                            NativeMethods.ShowWindow(hChild, NativeMethods.SW_SHOW);
                        }

                        int ex = NativeMethods.GetWindowLong(hChild, NativeMethods.GWL_EXSTYLE);

                        if ((ex & NativeMethods.WS_EX_LAYERED) == 0)
                        {
                            NativeMethods.SetWindowLong(hChild, NativeMethods.GWL_EXSTYLE, ex | NativeMethods.WS_EX_LAYERED);
                        }

                        // Apply the safe, crisp bridgeAlpha (NEVER 0, icons always solid and visible!)
                        NativeMethods.SetLayeredWindowAttributes(hChild, 0, bridgeAlpha, NativeMethods.LWA_ALPHA);

                        // Apply top border removal region on XAML content window
                        if (config.RemoveTopBorder && NativeMethods.GetWindowRect(hChild, out NativeMethods.RECT cr))
                        {
                            int cw = cr.Right - cr.Left;
                            int ch = cr.Bottom - cr.Top;
                            if (cw > 0 && ch > topCutoff)
                            {
                                IntPtr rgnChild = NativeMethods.CreateRectRgn(0, topCutoff, cw, ch);
                                NativeMethods.SetWindowRgn(hChild, rgnChild, true);
                            }
                        }
                        else
                        {
                            NativeMethods.SetWindowRgn(hChild, IntPtr.Zero, true);
                        }

                        // Force child window immediate redraw
                        NativeMethods.SetWindowPos(hChild, IntPtr.Zero, 0, 0, 0, 0,
                            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_FRAMECHANGED);
                        NativeMethods.RedrawWindow(hChild, IntPtr.Zero, IntPtr.Zero,
                            NativeMethods.RDW_INVALIDATE | NativeMethods.RDW_ERASE | NativeMethods.RDW_UPDATENOW | NativeMethods.RDW_FRAME | NativeMethods.RDW_ALLCHILDREN);
                    }
                }

                return true;
            }, IntPtr.Zero);
        }

        private static void RestoreTaskbarChildren(IntPtr trayHwnd)
        {
            if (trayHwnd == IntPtr.Zero) return;

            NativeMethods.EnumChildWindows(trayHwnd, (hChild, l) =>
            {
                var sb = new StringBuilder(128);
                NativeMethods.GetClassName(hChild, sb, sb.Capacity);
                if (sb.ToString().Contains("DesktopWindowContentBridge", StringComparison.OrdinalIgnoreCase))
                {
                    RestoreChildBridge(hChild);
                }
                return true;
            }, IntPtr.Zero);
        }

        private static void RestoreChildBridge(IntPtr hChild)
        {
            if (!NativeMethods.IsWindowVisible(hChild))
            {
                NativeMethods.ShowWindow(hChild, NativeMethods.SW_SHOW);
            }

            int ex = NativeMethods.GetWindowLong(hChild, NativeMethods.GWL_EXSTYLE);
            if ((ex & NativeMethods.WS_EX_LAYERED) != 0)
            {
                NativeMethods.SetLayeredWindowAttributes(hChild, 0, 255, NativeMethods.LWA_ALPHA);
                NativeMethods.SetWindowLong(hChild, NativeMethods.GWL_EXSTYLE, ex & ~NativeMethods.WS_EX_LAYERED);
            }

            NativeMethods.SetWindowRgn(hChild, IntPtr.Zero, true);

            NativeMethods.SetWindowPos(hChild, IntPtr.Zero, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_FRAMECHANGED);
            NativeMethods.RedrawWindow(hChild, IntPtr.Zero, IntPtr.Zero,
                NativeMethods.RDW_INVALIDATE | NativeMethods.RDW_ERASE | NativeMethods.RDW_UPDATENOW | NativeMethods.RDW_FRAME | NativeMethods.RDW_ALLCHILDREN);
        }

        private static void SetTaskbarAccent(IntPtr hwnd, AccentState accentState, int accentFlags, int color)
        {
            var policy = new AccentPolicy
            {
                AccentState = accentState,
                AccentFlags = accentFlags,
                GradientColor = color,
                AnimationId = 0
            };

            int structSize = Marshal.SizeOf(policy);
            IntPtr pPolicy = Marshal.AllocHGlobal(structSize);

            try
            {
                Marshal.StructureToPtr(policy, pPolicy, false);

                var data = new WindowCompositionAttributeData
                {
                    Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                    Data = pPolicy,
                    SizeOfData = structSize
                };

                NativeMethods.SetWindowCompositionAttribute(hwnd, ref data);
            }
            finally
            {
                Marshal.FreeHGlobal(pPolicy);
            }
        }

        private static int ParseColorToAbgr(string hex, byte alpha)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                return (alpha << 24);
            }

            hex = hex.Trim().TrimStart('#');
            if (hex.Length == 6 &&
                byte.TryParse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r) &&
                byte.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g) &&
                byte.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
            {
                return (alpha << 24) | (b << 16) | (g << 8) | r;
            }

            return (alpha << 24);
        }

        private static IEnumerable<IntPtr> GetAllTaskbarWindows()
        {
            var list = new List<IntPtr>();

            // Primary taskbar (with fallbacks to ensure finding it in all desktop environments)
            IntPtr primary = NativeMethods.FindWindow("Shell_TrayWnd", null);
            if (primary == IntPtr.Zero)
            {
                primary = NativeMethods.FindWindowEx(IntPtr.Zero, IntPtr.Zero, "Shell_TrayWnd", null);
            }

            if (primary == IntPtr.Zero)
            {
                NativeMethods.EnumWindows((hWnd, lParam) =>
                {
                    var sb = new StringBuilder(64);
                    NativeMethods.GetClassName(hWnd, sb, sb.Capacity);
                    if (sb.ToString() == "Shell_TrayWnd")
                    {
                        primary = hWnd;
                        return false;
                    }
                    return true;
                }, IntPtr.Zero);
            }

            if (primary != IntPtr.Zero)
            {
                list.Add(primary);
            }

            // Secondary taskbars on multi-monitor setups
            IntPtr secondary = IntPtr.Zero;
            while ((secondary = NativeMethods.FindWindowEx(IntPtr.Zero, secondary, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero)
            {
                list.Add(secondary);
            }

            return list;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _watcherCts?.Cancel();
            _watcherCts?.Dispose();

            if (_winEventHook != IntPtr.Zero)
            {
                try
                {
                    NativeMethods.UnhookWinEvent(_winEventHook);
                }
                catch
                {
                    // Ignore
                }
                _winEventHook = IntPtr.Zero;
            }

            ComponentDispatcher.ThreadFilterMessage -= OnThreadFilterMessage;
            SystemEvents.DisplaySettingsChanged -= OnSystemEnvironmentChanged;
            SystemEvents.UserPreferenceChanged -= OnSystemEnvironmentChanged;
        }
    }
}
