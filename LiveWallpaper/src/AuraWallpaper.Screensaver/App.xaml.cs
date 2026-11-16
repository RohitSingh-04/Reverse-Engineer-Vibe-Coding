using System;
using System.Windows;
using AuraWallpaper.Core.Native;

namespace AuraWallpaper.Screensaver
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            DesktopBridge.EnsureDefaultDesktop();
            string mode = "/s";
            IntPtr previewHwnd = IntPtr.Zero;

            if (e.Args.Length > 0)
            {
                string first = e.Args[0].ToLowerInvariant().Trim();
                if (first.StartsWith("/c")) mode = "/c";
                else if (first.StartsWith("/p"))
                {
                    mode = "/p";
                    if (e.Args.Length > 1 && long.TryParse(e.Args[1], out long h))
                    {
                        previewHwnd = new IntPtr(h);
                    }
                    else if (first.Length > 3 && long.TryParse(first.Substring(3).Trim(), out long h2))
                    {
                        previewHwnd = new IntPtr(h2);
                    }
                }
                else if (first.StartsWith("/s")) mode = "/s";
            }

            if (mode == "/c")
            {
                var cfg = new ScreensaverConfigWindow();
                cfg.ShowDialog();
                Shutdown();
                return;
            }

            if (mode == "/p" && previewHwnd != IntPtr.Zero)
            {
                var win = new ScreensaverWindow(previewHwnd);
                win.Show();
                return;
            }

            // Fullscreen screensaver mode (/s)
            var monitors = DisplayManager.Instance.GetMonitors();
            foreach (var mon in monitors)
            {
                var win = new ScreensaverWindow(mon);
                win.Show();
            }
        }
    }
}
