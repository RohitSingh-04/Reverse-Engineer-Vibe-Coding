using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using AuraWallpaper.Core.Engine;
using AuraWallpaper.Core.IPC;
using AuraWallpaper.Core.Native;
using AuraWallpaper.Core.Storage;

namespace AuraWallpaper.UI
{
    public partial class App : Application
    {
        private Mutex? _instanceMutex;
        private bool _hasMutexOwnership;
        private TrayIconManager? _trayManager;
        private MainWindow? _mainWindow;

        private static void LogTrace(string message)
        {
            try
            {
                string baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AuraWallpaper");
                string logDir = Path.Combine(baseDir, "logs");
                Directory.CreateDirectory(logDir);
                string entry = $"[{DateTime.UtcNow:O}] {message}\r\n";
                File.AppendAllText(Path.Combine(logDir, "aura_ui.log"), entry);
                File.AppendAllText(Path.Combine(baseDir, "aura_ui.log"), entry);
            }
            catch { }
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            DesktopBridge.EnsureDefaultDesktop();
            LogTrace("Application_Startup enter");

            // Global unhandled exception logging
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                LogCrash("AppDomain: " + args.ExceptionObject);
                LogTrace("AppDomain: " + args.ExceptionObject);
            };
            DispatcherUnhandledException += (s, args) =>
            {
                LogCrash("Dispatcher: " + args.Exception);
                LogTrace("Dispatcher: " + args.Exception);
                args.Handled = true; // Prevent crash
            };

            const string mutexName = "AuraWallpaper_SingleInstance_Mutex";
            _instanceMutex = new Mutex(true, mutexName, out bool createdNew);
            _hasMutexOwnership = createdNew;
            LogTrace($"Mutex createdNew={createdNew}");

            if (!createdNew)
            {
                LogTrace("Potential existing instance detected; attempting to contact via IPC...");
                bool contacted = false;
                try
                {
                    var task = Task.Run(() => IpcClient.SendCommandAsync(new IpcRequest { Command = "show" }, 2000));
                    if (task.Wait(2500) && task.Result != null && task.Result.Success)
                    {
                        contacted = true;
                    }
                }
                catch (Exception ex)
                {
                    LogTrace("IpcClient contact failed: " + ex.Message);
                }

                if (contacted)
                {
                    LogTrace("Active instance responded. Shutting down secondary instance.");
                    Shutdown();
                    return;
                }

                LogTrace("No active instance responded to IPC. Assuming orphaned mutex and proceeding as primary instance.");
            }

            LogTrace("Starting WallpaperManager...");
            // Start WallpaperManager & IPC Server immediately
            var manager = WallpaperManager.Instance;

            LogTrace("Loading settings...");
            var settings = AuraDatabase.Instance.LoadSettings();

            LogTrace("Seeding default wallpapers...");
            // Seed default wallpapers if library is empty
            DefaultWallpaperSeeder.SeedDefaultsIfEmpty();

            // Apply taskbar style
            if (settings.TaskbarMode != TaskbarThemeMode.Default)
            {
                LogTrace("Applying taskbar style...");
                TaskbarStyleHelper.SetTaskbarStyle(settings.TaskbarMode, settings.TaskbarTintColor);
            }

            LogTrace("Initializing ThemeManager...");
            ThemeManager.Initialize();

            LogTrace("Instantiating MainWindow...");
            try
            {
                // Create main window
                _mainWindow = new MainWindow();
                MainWindow = _mainWindow;
                manager.WindowShowRequested += () => Dispatcher.Invoke(ShowMainWindow);
                manager.WindowHideRequested += () => Dispatcher.Invoke(() => _mainWindow?.Hide());
                LogTrace("MainWindow instantiated successfully.");
            }
            catch (Exception ex)
            {
                LogCrash("MainWindow instantiation failed: " + ex);
                LogTrace("MainWindow instantiation failed: " + ex);
            }

            // Setup system tray safely
            try
            {
                LogTrace("Setting up System Tray...");
                SetupSystemTray();
                LogTrace("System Tray set up.");
            }
            catch (Exception ex)
            {
                LogCrash("SystemTray: " + ex);
                LogTrace("SystemTray: " + ex);
            }

            if (!settings.StartMinimized && _mainWindow != null)
            {
                try
                {
                    LogTrace("Showing MainWindow...");
                    _mainWindow.Show();
                    LogTrace("MainWindow.Show() called.");
                }
                catch (Exception ex)
                {
                    LogCrash("MainWindow.Show failed: " + ex);
                    LogTrace("MainWindow.Show failed: " + ex);
                }
            }
            else
            {
                LogTrace("StartMinimized is true or mainWindow is null; MainWindow not shown.");
            }

            // Restore active wallpapers in background
            ThreadPool.QueueUserWorkItem(_ =>
            {
                Thread.Sleep(500);
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        LogTrace("Restoring saved wallpapers...");
                        manager.RestoreSavedWallpapers();
                        LogTrace("Wallpapers restored.");
                    }
                    catch (Exception ex)
                    {
                        LogCrash("RestoreWallpapers: " + ex);
                    }
                });
            });
        }

        private void SetupSystemTray()
        {
            if (_mainWindow == null) return;

            var menu = new ContextMenu();

            var openItem = new MenuItem { Header = "Open Aura Wallpaper" };
            openItem.Click += (s, e) => ShowMainWindow();
            menu.Items.Add(openItem);

            menu.Items.Add(new Separator());

            var pauseItem = new MenuItem { Header = "Pause All Wallpapers" };
            pauseItem.Click += (s, e) => WallpaperManager.Instance.PauseAll();
            menu.Items.Add(pauseItem);

            var resumeItem = new MenuItem { Header = "Resume All Wallpapers" };
            resumeItem.Click += (s, e) => WallpaperManager.Instance.ResumeAll();
            menu.Items.Add(resumeItem);

            menu.Items.Add(new Separator());

            var nextItem = new MenuItem { Header = "Next Wallpaper" };
            nextItem.Click += (s, e) => WallpaperManager.Instance.NextWallpaper();
            menu.Items.Add(nextItem);

            var prevItem = new MenuItem { Header = "Previous Wallpaper" };
            prevItem.Click += (s, e) => WallpaperManager.Instance.PreviousWallpaper();
            menu.Items.Add(prevItem);

            var randomItem = new MenuItem { Header = "Random Wallpaper" };
            randomItem.Click += (s, e) => WallpaperManager.Instance.SetRandomWallpaper();
            menu.Items.Add(randomItem);

            menu.Items.Add(new Separator());

            var muteItem = new MenuItem { Header = "Mute / Unmute" };
            muteItem.Click += (s, e) =>
            {
                if (WallpaperManager.Instance.ActiveAssignments.Values.Any(a => a.IsMuted))
                    WallpaperManager.Instance.UnmuteAll();
                else
                    WallpaperManager.Instance.MuteAll();
            };
            menu.Items.Add(muteItem);

            menu.Items.Add(new Separator());

            var exitItem = new MenuItem { Header = "Exit" };
            exitItem.Click += (s, e) =>
            {
                _trayManager?.Dispose();
                Shutdown();
            };
            menu.Items.Add(exitItem);

            _trayManager = new TrayIconManager();
            _trayManager.Initialize(_mainWindow, menu, "Aura Live Wallpaper");
            _trayManager.DoubleClick += ShowMainWindow;
        }

        public void ShowMainWindow()
        {
            if (_mainWindow == null) return;
            _mainWindow.Show();
            if (_mainWindow.WindowState == WindowState.Minimized)
            {
                _mainWindow.WindowState = WindowState.Normal;
            }
            _mainWindow.Activate();
        }

        private static void LogCrash(string message)
        {
            try
            {
                string baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AuraWallpaper");
                string logDir = Path.Combine(baseDir, "logs");
                Directory.CreateDirectory(logDir);
                string entry = $"[{DateTime.UtcNow:O}] {message}\r\n";
                File.AppendAllText(Path.Combine(logDir, "crash.log"), entry);
                File.AppendAllText(Path.Combine(baseDir, "crash.log"), entry);
            }
            catch { }
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            LogTrace($"Application_Exit with code {e.ApplicationExitCode}");
            try { _trayManager?.Dispose(); } catch { }
            try { WallpaperManager.Instance.Dispose(); } catch { }
            if (_hasMutexOwnership)
            {
                try { _instanceMutex?.ReleaseMutex(); } catch { }
            }
            try { _instanceMutex?.Dispose(); } catch { }
        }
    }
}
