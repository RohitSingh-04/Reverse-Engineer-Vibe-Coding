using System;
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

        public static bool EnsureDefaultDesktop()
        {
            try
            {
                IntPtr progman = NativeMethods.FindWindow("Progman", null);
                if (progman != IntPtr.Zero)
                {
                    return true;
                }

                IntPtr hDesk = NativeMethods.OpenDesktop("Default", 0, false, NativeMethods.DESKTOP_ALL_ACCESS);
                if (hDesk != IntPtr.Zero)
                {
                    return NativeMethods.SetThreadDesktop(hDesk);
                }
            }
            catch
            {
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

                // 1. Direct child WorkerW of Progman (Windows 11 modern architecture)
                IntPtr childWorker = NativeMethods.FindWindowEx(_progman, IntPtr.Zero, "WorkerW", null);
                if (childWorker != IntPtr.Zero && NativeMethods.IsWindowVisible(childWorker))
                {
                    workerW = childWorker;
                }

                // 2. Windows 10 / earlier Windows 11: Top-level WorkerW sibling behind SHELLDLL_DefView
                if (workerW == IntPtr.Zero)
                {
                    NativeMethods.EnumWindows((toplevelHwnd, lParam) =>
                    {
                        var shellView = NativeMethods.FindWindowEx(toplevelHwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                        if (shellView != IntPtr.Zero)
                        {
                            var sibling = NativeMethods.FindWindowEx(IntPtr.Zero, toplevelHwnd, "WorkerW", null);
                            if (sibling != IntPtr.Zero && NativeMethods.IsWindowVisible(sibling))
                            {
                                workerW = sibling;
                                return false; // Stop enumeration
                            }
                        }
                        return true;
                    }, IntPtr.Zero);
                }

                // 3. Fallback: Search any visible top-level WorkerW without DefView
                if (workerW == IntPtr.Zero)
                {
                    NativeMethods.EnumWindows((hwnd, lParam) =>
                    {
                        var sb = new StringBuilder(256);
                        NativeMethods.GetClassName(hwnd, sb, 256);
                        if (sb.ToString() == "WorkerW" && NativeMethods.IsWindowVisible(hwnd))
                        {
                            var defView = NativeMethods.FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                            if (defView == IntPtr.Zero)
                            {
                                workerW = hwnd;
                                return false; // stop enumeration
                            }
                        }
                        return true;
                    }, IntPtr.Zero);
                }

                // 4. Ultimate fallback: Progman itself
                _workerW = workerW != IntPtr.Zero ? workerW : _progman;
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
                return false;

            // Set parent to WorkerW (or Progman)
            NativeMethods.SetParent(windowHwnd, _workerW);

            // Set styles: WS_CHILD, remove WS_POPUP, ensure toolwindow and no-activate
            IntPtr currentStyle = NativeMethods.GetWindowLongPtr(windowHwnd, NativeMethods.GWL_STYLE);
            long newStyle = currentStyle.ToInt64();
            newStyle |= NativeMethods.WS_CHILD;
            newStyle &= ~NativeMethods.WS_POPUP;
            NativeMethods.SetWindowLongPtr(windowHwnd, NativeMethods.GWL_STYLE, new IntPtr(newStyle));

            IntPtr currentExStyle = NativeMethods.GetWindowLongPtr(windowHwnd, NativeMethods.GWL_EXSTYLE);
            long newExStyle = currentExStyle.ToInt64() | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;
            NativeMethods.SetWindowLongPtr(windowHwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(newExStyle));

            // Set position and size
            NativeMethods.SetWindowPos(
                windowHwnd,
                NativeMethods.HWND_BOTTOM,
                x,
                y,
                width,
                height,
                NativeMethods.SWP_SHOWWINDOW | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_FRAMECHANGED
            );

            NativeMethods.ShowWindow(windowHwnd, NativeMethods.SW_SHOW);

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
