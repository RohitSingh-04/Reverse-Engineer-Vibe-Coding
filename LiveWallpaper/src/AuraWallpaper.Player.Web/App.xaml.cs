using System;
using System.IO;
using System.Windows;
using AuraWallpaper.Core.Native;

namespace AuraWallpaper.Player.Web
{
    public partial class App : Application
    {
        private static void LogCrash(string msg)
        {
            try
            {
                string logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "player_web.log");
                File.AppendAllText(logFile, $"[{DateTime.UtcNow:O}] {msg}\r\n");
            }
            catch { }
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                LogCrash("AppDomain Unhandled: " + args.ExceptionObject);
            };
            DispatcherUnhandledException += (s, args) =>
            {
                LogCrash("Dispatcher Unhandled: " + args.Exception);
                args.Handled = true;
            };

            DesktopBridge.EnsureDefaultDesktop();
            LogCrash("Player.Web Application_Startup: Args = " + string.Join(" ", e.Args));

            string url = "";
            int x = 0, y = 0, w = 1920, h = 1080, mon = 0, vol = 80;
            bool mute = false;

            for (int i = 0; i < e.Args.Length; i++)
            {
                string arg = e.Args[i];
                if (arg == "--url" && i + 1 < e.Args.Length) url = e.Args[++i];
                else if (arg == "--x" && i + 1 < e.Args.Length) int.TryParse(e.Args[++i], out x);
                else if (arg == "--y" && i + 1 < e.Args.Length) int.TryParse(e.Args[++i], out y);
                else if (arg == "--w" && i + 1 < e.Args.Length) int.TryParse(e.Args[++i], out w);
                else if (arg == "--h" && i + 1 < e.Args.Length) int.TryParse(e.Args[++i], out h);
                else if (arg == "--mon" && i + 1 < e.Args.Length) int.TryParse(e.Args[++i], out mon);
                else if (arg == "--vol" && i + 1 < e.Args.Length) int.TryParse(e.Args[++i], out vol);
                else if (arg == "--mute" && i + 1 < e.Args.Length) bool.TryParse(e.Args[++i], out mute);
            }

            var win = new MainWindow(url, x, y, w, h, mon, vol, mute);
            win.Show();
        }
    }
}
