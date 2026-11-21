using System;
using AuraWallpaper.Core.Native;

namespace AuraWallpaper.Player.Web
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                NativeMethods.SetProcessDpiAwarenessContext(NativeMethods.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
            }
            catch { }

            DesktopBridge.EnsureDefaultDesktop();
            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
    }
}
