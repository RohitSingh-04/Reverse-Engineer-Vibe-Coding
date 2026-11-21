using System;
using System.IO;
using System.Text;
using System.Threading;

namespace AuraWallpaper.Core.Native
{
    public class DesktopBridge
    {
        private static readonly object _lock = new object();
        private static DesktopBridge? _instance;
        public static DesktopBridge Instance => _instance ??= new DesktopBridge();

        private IntPtr _workerW = IntPtr.Zero;
        private IntPtr _progman = IntPtr.Zero;
        public IntPtr WorkerWHwnd => _workerW;
        public IntPtr ProgmanHwnd => _progman;

        public event EventHandler? ExplorerRestarted;

        private DesktopBridge()
        {
            InitializeWorkerW();
        }

        private static void LogBridge(string msg)
        {
            try
            {
                string logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "desktop_bridge.log");
                File.AppendAllText(logFile, $"[{DateTime.UtcNow:O}] {msg}\r\n");
            }
            catch { }
        }

        public static bool EnsureDefaultDesktop()
        {
            try
            {
                IntPtr progman = NativeMethods.FindWindow("Progman", null);
                if (progman != IntPtr.Zero)
                {
                    LogBridge($"EnsureDefaultDesktop: Found Progman 0x{progman.ToInt64():X}");
                    return true;
                }

                int err1 = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                IntPtr hDesk = NativeMethods.OpenDesktop("Default", 0, false, NativeMethods.DESKTOP_ALL_ACCESS);
                int err2 = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                LogBridge($"EnsureDefaultDesktop: FindProgman=0 (err={err1}), OpenDesktop=0x{hDesk.ToInt64():X} (err={err2})");

                if (hDesk != IntPtr.Zero)
                {
                    bool setDesk = NativeMethods.SetThreadDesktop(hDesk);
                    int err3 = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                    IntPtr pAfter = NativeMethods.FindWindow("Progman", null);
                    LogBridge($"EnsureDefaultDesktop: SetThreadDesktop={setDesk} (err={err3}), ProgmanAfter=0x{pAfter.ToInt64():X}");
                    return setDesk;
                }
            }
            catch (Exception ex)
            {
                LogBridge("EnsureDefaultDesktop exception: " + ex);
            }
            return false;
        }

        public bool InitializeWorkerW()
        {
            lock (_lock)
            {
                EnsureDefaultDesktop();

                _progman = NativeMethods.FindWindow("Progman", null);
                if (_progman == IntPtr.Zero)
                {
                    return false;
                }

                // Send 0x052C to Progman. This causes Windows Explorer to spawn WorkerW behind SHELLDLL_DefView
                NativeMethods.SendMessageTimeout(
                    _progman,
                    0x052C,
                    new IntPtr(0xD),
                    new IntPtr(1),
                    NativeMethods.SMTO_NORMAL,
                    1000,
                    out _
                );

                // Try alternative 0x052C message if needed
                NativeMethods.SendMessageTimeout(
                    _progman,
                    0x052C,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    NativeMethods.SMTO_NORMAL,
                    1000,
                    out _
                );

                IntPtr workerW = IntPtr.Zero;

                // Search for WorkerW in the window hierarchy containing SHELLDLL_DefView
                NativeMethods.EnumWindows((toplevelHwnd, lParam) =>
                {
                    var shellView = NativeMethods.FindWindowEx(toplevelHwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                    if (shellView != IntPtr.Zero)
                    {
                        // Windows 11: WorkerW is spawned as a child of the window with DefView (Progman)
                        var child = NativeMethods.FindWindowEx(toplevelHwnd, IntPtr.Zero, "WorkerW", null);
                        if (child != IntPtr.Zero)
                        {
                            workerW = child;
                            return false;
                        }

                        // Windows 10: WorkerW is spawned as a top-level sibling immediately after toplevelHwnd
                        var sibling = NativeMethods.FindWindowEx(IntPtr.Zero, toplevelHwnd, "WorkerW", null);
                        if (sibling != IntPtr.Zero)
                        {
                            workerW = sibling;
                            return false;
                        }
                    }
                    return true;
                }, IntPtr.Zero);

                // Fallback: Check if Progman has a WorkerW child
                if (workerW == IntPtr.Zero)
                {
                    IntPtr childWorker = NativeMethods.FindWindowEx(_progman, IntPtr.Zero, "WorkerW", null);
                    if (childWorker != IntPtr.Zero)
                    {
                        workerW = childWorker;
                    }
                }

                // Ultimate fallback: Progman itself
                _workerW = workerW != IntPtr.Zero ? workerW : _progman;
                LogBridge($"InitializeWorkerW: _workerW=0x{_workerW.ToInt64():X}, Progman=0x{_progman.ToInt64():X}");
                return _workerW != IntPtr.Zero;
            }
        }

        public bool AttachToDesktop(IntPtr windowHwnd, int x, int y, int width, int height)
        {
            if (windowHwnd == IntPtr.Zero)
                return false;

            EnsureDefaultDesktop();

            if (_workerW == IntPtr.Zero || !NativeMethods.IsWindowVisible(_workerW))
            {
                InitializeWorkerW();
            }

            if (_workerW == IntPtr.Zero)
            {
                LogBridge("AttachToDesktop: _workerW is Zero!");
                return false;
            }

            // Ensure WorkerW covers the entire virtual screen bounds so multi-monitor child windows are never clipped
            var vBounds = DisplayManager.Instance.GetVirtualScreenBounds();
            IntPtr parent = NativeMethods.GetParent(_workerW);
            int targetX = vBounds.X;
            int targetY = vBounds.Y;

            if (parent != IntPtr.Zero)
            {
                // WorkerW is a child window (e.g. child of Progman). SetWindowPos requires parent client coordinates!
                var ptV = new NativeMethods.POINT { x = vBounds.X, y = vBounds.Y };
                NativeMethods.ScreenToClient(parent, ref ptV);
                targetX = ptV.x;
                targetY = ptV.y;
            }

            NativeMethods.SetWindowPos(
                _workerW,
                IntPtr.Zero,
                targetX,
                targetY,
                vBounds.Width,
                vBounds.Height,
                NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW | NativeMethods.SWP_FRAMECHANGED
            );

            // Set parent to WorkerW (or Progman)
            IntPtr oldParent = NativeMethods.SetParent(windowHwnd, _workerW);

            // Set styles: WS_CHILD, remove WS_POPUP, ensure toolwindow and no-activate
            IntPtr currentStyle = NativeMethods.GetWindowLongPtr(windowHwnd, NativeMethods.GWL_STYLE);
            long newStyle = currentStyle.ToInt64();
            newStyle |= NativeMethods.WS_CHILD;
            newStyle &= ~NativeMethods.WS_POPUP;
            NativeMethods.SetWindowLongPtr(windowHwnd, NativeMethods.GWL_STYLE, new IntPtr(newStyle));

            IntPtr currentExStyle = NativeMethods.GetWindowLongPtr(windowHwnd, NativeMethods.GWL_EXSTYLE);
            long newExStyle = currentExStyle.ToInt64() | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;
            NativeMethods.SetWindowLongPtr(windowHwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(newExStyle));

            // Convert screen coordinates (x, y) to WorkerW parent client coordinates using ScreenToClient
            var pt = new NativeMethods.POINT { x = x, y = y };
            NativeMethods.ScreenToClient(_workerW, ref pt);
            int clientX = pt.x;
            int clientY = pt.y;

            // Set position and size inside WorkerW
            IntPtr insertAfter = (_workerW == _progman) ? NativeMethods.HWND_BOTTOM : IntPtr.Zero;
            bool posResult = NativeMethods.SetWindowPos(
                windowHwnd,
                insertAfter,
                clientX,
                clientY,
                width,
                height,
                NativeMethods.SWP_SHOWWINDOW | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_FRAMECHANGED
            );

            NativeMethods.ShowWindow(windowHwnd, NativeMethods.SW_SHOW);
            LogBridge($"AttachToDesktop: hwnd=0x{windowHwnd.ToInt64():X}, parent=0x{_workerW.ToInt64():X}, oldParent=0x{oldParent.ToInt64():X}, screen=({x},{y},{width},{height}), client=({clientX},{clientY}), posResult={posResult}");

            return true;
        }

        public void DetachFromDesktop(IntPtr windowHwnd)
        {
            if (windowHwnd == IntPtr.Zero)
                return;

            // Remove parent
            NativeMethods.SetParent(windowHwnd, IntPtr.Zero);
        }

        public void RefreshDesktopIcons()
        {
            // Forces wallpaper refresh if needed
            var progman = NativeMethods.FindWindow("Progman", null);
            if (progman != IntPtr.Zero)
            {
                NativeMethods.SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, NativeMethods.SMTO_NORMAL, 500, out _);
            }
        }

        public void NotifyExplorerRestart()
        {
            _workerW = IntPtr.Zero;
            InitializeWorkerW();
            ExplorerRestarted?.Invoke(this, EventArgs.Empty);
        }
    }
}
