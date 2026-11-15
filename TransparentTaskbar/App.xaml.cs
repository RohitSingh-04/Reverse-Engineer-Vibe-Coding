using System;
using System.Threading;
using System.Windows;
using TransparentTaskbar.Models;
using TransparentTaskbar.Native;
using TransparentTaskbar.Services;
using WpfApplication = System.Windows.Application;

namespace TransparentTaskbar
{
    public partial class App : WpfApplication
    {
        private const string AppMutexName = "TransparentTaskbar_SingleInstance_Mutex";
        private Mutex? _singleInstanceMutex;
        private TaskbarService? _taskbarService;
        private TrayIconService? _trayIconService;
        private MainWindow? _settingsWindow;
        private AppConfig _config = new();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Priority 1: Give TransparentTaskbar high scheduling priority so transparency applies immediately on boot
            try
            {
                using var currentProc = System.Diagnostics.Process.GetCurrentProcess();
                currentProc.PriorityClass = System.Diagnostics.ProcessPriorityClass.High;
                Thread.CurrentThread.Priority = ThreadPriority.Highest;
            }
            catch
            {
                // Fallback gracefully if process priority cannot be changed
            }

            // Catch any unhandled UI exceptions gracefully
            DispatcherUnhandledException += (s, args) =>
            {
                System.Diagnostics.Debug.WriteLine($"[UnhandledUIException] {args.Exception}");
                System.Windows.MessageBox.Show($"An error occurred:\n{args.Exception.Message}",
                                               "TransparentTaskbar",
                                               MessageBoxButton.OK,
                                               MessageBoxImage.Warning);
                args.Handled = true;
            };

            // Check for explicit restore argument (e.g. from uninstaller)
            foreach (var arg in e.Args)
            {
                if (arg.Equals("--restore", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("-r", StringComparison.OrdinalIgnoreCase))
                {
                    using var restoreService = new TaskbarService();
                    restoreService.RestoreDefault();
                    Shutdown();
                    return;
                }
            }

            // Ensure single instance across the user session
            _singleInstanceMutex = new Mutex(true, AppMutexName, out bool isNewInstance);
            if (!isNewInstance)
            {
                // Another instance is already running
                Shutdown();
                return;
            }

            // 1. Load persisted configuration
            _config = ConfigService.Load();

            // 2. Initialize and start taskbar transparency service immediately
            _taskbarService = new TaskbarService();
            _taskbarService.Start(_config);

            // 3. Initialize system tray icon with context menu
            _trayIconService = new TrayIconService(
                openSettingsAction: ShowSettingsWindow,
                exitAction: ExitApplication
            );

            // If user explicitly passed --show-settings or similar, display settings window
            foreach (var arg in e.Args)
            {
                if (arg.Equals("--settings", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("-s", StringComparison.OrdinalIgnoreCase))
                {
                    ShowSettingsWindow();
                    break;
                }
            }
        }

        private void ShowSettingsWindow()
        {
            if (_settingsWindow == null)
            {
                _settingsWindow = new MainWindow(_taskbarService!, _config);
                _settingsWindow.Closed += (s, e) => _settingsWindow = null;
            }

            if (_settingsWindow.WindowState == WindowState.Minimized)
            {
                _settingsWindow.WindowState = WindowState.Normal;
            }

            _settingsWindow.Show();
            _settingsWindow.Topmost = true;

            var helper = new System.Windows.Interop.WindowInteropHelper(_settingsWindow);
            IntPtr hWnd = helper.EnsureHandle();
            NativeMethods.SetForegroundWindow(hWnd);

            _settingsWindow.Activate();
            _settingsWindow.Focus();
        }

        private void ExitApplication()
        {
            // Restore taskbar to standard Windows default
            _taskbarService?.RestoreDefault();
            _taskbarService?.Dispose();
            _taskbarService = null;

            // Clean up tray icon
            _trayIconService?.Dispose();
            _trayIconService = null;

            // Close settings window if open
            if (_settingsWindow != null)
            {
                _settingsWindow.ForceClose();
                _settingsWindow = null;
            }

            // Release single instance mutex
            if (_singleInstanceMutex != null)
            {
                try
                {
                    _singleInstanceMutex.ReleaseMutex();
                }
                catch
                {
                    // Ignore if not owned
                }
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
            }

            // Terminate WPF application
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _taskbarService?.RestoreDefault();
            _taskbarService?.Dispose();
            _trayIconService?.Dispose();

            base.OnExit(e);
        }
    }
}
