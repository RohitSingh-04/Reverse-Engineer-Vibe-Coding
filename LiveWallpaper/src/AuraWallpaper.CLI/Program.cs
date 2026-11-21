using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using AuraWallpaper.Core.IPC;
using AuraWallpaper.Core.Native;

namespace AuraWallpaper.CLI
{
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            try
            {
                NativeMethods.SetProcessDpiAwarenessContext(NativeMethods.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
            }
            catch { }

            if (args.Length == 0 || args[0] == "-h" || args[0] == "--help" || args[0] == "help")
            {
                PrintHelp();
                return 0;
            }

            bool jsonOutput = false;
            var req = new IpcRequest();
            string command = args[0].ToLowerInvariant();
            req.Command = command;

            // Parse flags
            for (int i = 1; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg == "--json")
                {
                    jsonOutput = true;
                }
                else if ((arg == "--file" || arg == "-f") && i + 1 < args.Length)
                {
                    req.FilePath = args[++i];
                }
                else if ((arg == "--monitor" || arg == "-m") && i + 1 < args.Length)
                {
                    if (int.TryParse(args[++i], out int mon))
                    {
                        req.MonitorIndex = mon;
                    }
                }
                else if (arg == "--random" || arg == "-r")
                {
                    req.Random = true;
                }
                else if (arg == "--all" || arg == "-a")
                {
                    req.All = true;
                }
                else if ((arg == "--mode" || arg == "-M") && i + 1 < args.Length)
                {
                    req.Mode = args[++i];
                }
                else if ((arg == "--scaling" || arg == "-s") && i + 1 < args.Length)
                {
                    req.Scaling = args[++i];
                }
                else if ((arg == "--property" || arg == "-p") && i + 1 < args.Length)
                {
                    req.PropertyName = args[++i];
                }
                else if ((arg == "--value" || arg == "-v") && i + 1 < args.Length)
                {
                    req.PropertyValue = args[++i];
                }
                else if (command == "volume" && int.TryParse(arg, out int vol))
                {
                    req.Volume = vol;
                }
            }

            // Specific command handling
            if ((command == "view" || command == "navigate") && string.IsNullOrEmpty(req.PropertyName) && args.Length > 1)
            {
                req.PropertyName = args[1];
            }

            if (command == "volume" && req.Volume == -1 && args.Length > 1 && int.TryParse(args[1], out int v))
            {
                req.Volume = v;
            }

            if (command == "set" && !req.Random && string.IsNullOrEmpty(req.FilePath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine("Error: 'set' requires either --file <path> or --random.");
                Console.ResetColor();
                return 1;
            }

            var resp = await IpcClient.SendCommandAsync(req);

            if (jsonOutput)
            {
                Console.WriteLine(JsonSerializer.Serialize(resp, new JsonSerializerOptions { WriteIndented = true }));
                return resp.Success ? 0 : 2;
            }

            if (resp.Success)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[Aura] {resp.Message}");
                Console.ResetColor();

                if (resp.Data != null)
                {
                    Console.WriteLine(JsonSerializer.Serialize(resp.Data, new JsonSerializerOptions { WriteIndented = true }));
                }
                return 0;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"[Aura Error] {resp.Message}");
                Console.ResetColor();
                return 2;
            }
        }

        private static void PrintHelp()
        {
            Console.WriteLine(@"Aura Wallpaper CLI (wallctl) - Desktop Live Wallpaper Automation Tool

USAGE:
    wallctl <command> [options]

COMMANDS:
    show                         Show main application window
    hide                         Hide main application window (minimize to tray)
    view <tab>                   Switch UI view (library, active, settings, about, playlists, screensaver, creator, performance)
    play                         Resume wallpaper playback across all monitors
    pause                        Pause wallpaper playback across all monitors
    mute                         Mute wallpaper audio
    unmute                       Unmute wallpaper audio
    volume <0-100>               Set global wallpaper volume
    set --file <path> [--monitor <id>]  Apply a wallpaper file to a specific or all monitors
    set --random                 Set a random wallpaper from the library
    close [--monitor <id>] [--all]      Close wallpaper on a specific monitor or all
    next                         Switch to the next wallpaper in the active collection
    previous                     Switch to the previous wallpaper in the active collection
    screensaver --show           Trigger Windows live wallpaper screensaver immediately
    list                         List all installed wallpapers in library
    monitors                     List all detected displays and resolutions
    status                       Display playback status, active assignments, and system metrics
    shutdown                     Gracefully terminate Aura Wallpaper application

OPTIONS:
    --file, -f <path>            Path to wallpaper file (video, html, gif, image, package)
    --monitor, -m <index>        Target monitor index (0, 1, 2...)
    --mode, -M <mode>            Multi-display mode: duplicate, span, permonitor
    --scaling, -s <scaling>      Scaling mode: fill (cropped), fit, stretch
    --random, -r                 Select a random wallpaper
    --all, -a                    Apply to all monitors (duplicate)
    --json                       Output results in structured JSON format
    --help, -h                   Display this help message

EXAMPLES:
    wallctl play
    wallctl pause
    wallctl volume 65
    wallctl set --file ""C:\Wallpapers\nebula.mp4"" --monitor 0
    wallctl set --random
    wallctl close --all
    wallctl status --json
");
        }
    }
}
