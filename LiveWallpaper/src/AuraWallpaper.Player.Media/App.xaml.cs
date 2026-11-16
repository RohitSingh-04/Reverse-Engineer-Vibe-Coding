using System;
using System.Windows;
using AuraWallpaper.Core.Models;
using AuraWallpaper.Core.Native;

namespace AuraWallpaper.Player.Media
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            DesktopBridge.EnsureDefaultDesktop();

            string file = "";
            int type = 0, x = 0, y = 0, w = 1920, h = 1080, mon = 0, vol = 80, scale = 0;
            bool mute = false;

            for (int i = 0; i < e.Args.Length; i++)
            {
                string arg = e.Args[i];
                if (arg == "--file" && i + 1 < e.Args.Length) file = e.Args[++i];
                else if (arg == "--type" && i + 1 < e.Args.Length) int.TryParse(e.Args[++i], out type);
                else if (arg == "--x" && i + 1 < e.Args.Length) int.TryParse(e.Args[++i], out x);
                else if (arg == "--y" && i + 1 < e.Args.Length) int.TryParse(e.Args[++i], out y);
                else if (arg == "--w" && i + 1 < e.Args.Length) int.TryParse(e.Args[++i], out w);
                else if (arg == "--h" && i + 1 < e.Args.Length) int.TryParse(e.Args[++i], out h);
                else if (arg == "--mon" && i + 1 < e.Args.Length) int.TryParse(e.Args[++i], out mon);
                else if (arg == "--vol" && i + 1 < e.Args.Length) int.TryParse(e.Args[++i], out vol);
                else if (arg == "--mute" && i + 1 < e.Args.Length) bool.TryParse(e.Args[++i], out mute);
                else if (arg == "--scale" && i + 1 < e.Args.Length) int.TryParse(e.Args[++i], out scale);
            }

            var win = new MainWindow(file, (WallpaperType)type, x, y, w, h, mon, vol, mute, (WallpaperScaling)scale);
            win.Show();
        }
    }
}
