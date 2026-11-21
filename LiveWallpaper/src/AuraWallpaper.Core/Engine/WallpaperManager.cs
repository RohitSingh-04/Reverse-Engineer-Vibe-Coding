using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using AuraWallpaper.Core.Audio;
using AuraWallpaper.Core.IPC;
using AuraWallpaper.Core.Models;
using AuraWallpaper.Core.Native;
using AuraWallpaper.Core.Storage;

namespace AuraWallpaper.Core.Engine
{
    public class WallpaperManager : IDisposable
    {
        private static WallpaperManager? _instance;
        private static readonly object _lock = new object();
        public static WallpaperManager Instance
        {
            get
            {
                lock (_lock)
                {
                    return _instance ??= new WallpaperManager();
                }
            }
        }

        private readonly ConcurrentDictionary<int, MonitorAssignment> _activeAssignments = new();
        private readonly ConcurrentDictionary<int, Process> _playerProcesses = new();
        private readonly IpcServer _ipcServer;
        private bool _isMuted;
        private int _globalVolume = 80;

        public bool IsSpanMode => _activeAssignments.ContainsKey(-1);
        public bool IsDuplicateMode => _activeAssignments.Values.Any(a => a.Mode == AssignmentMode.Duplicate);
        public MonitorAssignment? GetAssignment(int monitorIndex) => _activeAssignments.TryGetValue(monitorIndex, out var a) ? a : null;

        public event Action? AssignmentsChanged;
        public event Action? WindowShowRequested;
        public event Action? WindowHideRequested;
        public event Action<string>? ViewNavigationRequested;
        public void RequestViewNavigation(string viewName) => ViewNavigationRequested?.Invoke(viewName);

        private WallpaperManager()
        {
            _ipcServer = new IpcServer(HandleIpcCommand);
            _ipcServer.Start();

            // Hook desktop Explorer restart and display changes
            DesktopBridge.Instance.ExplorerRestarted += OnExplorerRestarted;
            DisplayManager.Instance.DisplayConfigurationChanged += OnDisplayChanged;
            PerformanceCoordinator.Instance.PauseStateRequested += OnPerformancePauseRequested;
            PlaylistCoordinator.Instance.WallpaperRotationRequested += OnPlaylistRotationRequested;

            // Start audio capture for interactive visualizer wallpapers
            try
            {
                WasapiAudioCapture.Instance.Start();
            }
            catch
            {
            }
        }

        #region Core Wallpaper Lifecycle

        public bool SetWallpaper(string wallpaperId, int monitorIndex = 0, AssignmentMode mode = AssignmentMode.PerMonitor, WallpaperScaling scaling = WallpaperScaling.Fill)
        {
            var wallpaper = AuraDatabase.Instance.GetWallpaperById(wallpaperId);
            if (wallpaper == null) return false;

            wallpaper.LastUsed = DateTime.UtcNow;
            AuraDatabase.Instance.SaveWallpaper(wallpaper);

            var monitors = DisplayManager.Instance.GetMonitors();
            if (monitors.Count == 0) return false;

            if (mode == AssignmentMode.Span)
            {
                // Span single wallpaper across entire virtual screen
                var vBounds = DisplayManager.Instance.GetVirtualScreenBounds();
                CloseAllWallpapers();
                AuraDatabase.Instance.ClearAllMonitorAssignments();

                var assignment = new MonitorAssignment
                {
                    MonitorDeviceId = "SPAN_VIRTUAL",
                    MonitorIndex = -1,
                    WallpaperId = wallpaperId,
                    Mode = AssignmentMode.Span,
                    State = PlaybackState.Playing,
                    Volume = _globalVolume,
                    IsMuted = _isMuted,
                    Scaling = scaling,
                    AssignedAt = DateTime.UtcNow
                };

                LaunchPlayer(wallpaper, vBounds.X, vBounds.Y, vBounds.Width, vBounds.Height, -1, assignment);
                _activeAssignments[-1] = assignment;
                AuraDatabase.Instance.SaveMonitorAssignment(assignment);
                AssignmentsChanged?.Invoke();
                return true;
            }

            if (mode == AssignmentMode.Duplicate)
            {
                // Duplicate across all monitors
                CloseAllWallpapers();
                AuraDatabase.Instance.ClearAllMonitorAssignments();
                for (int i = 0; i < monitors.Count; i++)
                {
                    var mon = monitors[i];
                    var assignment = new MonitorAssignment
                    {
                        MonitorDeviceId = mon.DeviceId,
                        MonitorIndex = i,
                        WallpaperId = wallpaper.Id,
                        Mode = AssignmentMode.Duplicate,
                        State = PlaybackState.Playing,
                        Volume = _globalVolume,
                        IsMuted = _isMuted,
                        Scaling = scaling,
                        AssignedAt = DateTime.UtcNow
                    };

                    LaunchPlayer(wallpaper, mon.Bounds.X, mon.Bounds.Y, mon.Bounds.Width, mon.Bounds.Height, i, assignment);
                    _activeAssignments[i] = assignment;
                    AuraDatabase.Instance.SaveMonitorAssignment(assignment);
                }
                AssignmentsChanged?.Invoke();
                return true;
            }

            // Per-monitor mode: if Span was running, close it first and clear SPAN_VIRTUAL in DB
            if (_activeAssignments.ContainsKey(-1))
            {
                CloseWallpaper(-1);
                AuraDatabase.Instance.ClearMonitorAssignment("SPAN_VIRTUAL");
            }

            if (monitorIndex < 0 || monitorIndex >= monitors.Count) monitorIndex = 0;
            bool result = SetSingleMonitorWallpaper(wallpaper, monitorIndex, AssignmentMode.PerMonitor, scaling);
            AssignmentsChanged?.Invoke();
            return result;
        }

        private bool SetSingleMonitorWallpaper(WallpaperItem wallpaper, int monitorIndex, AssignmentMode mode, WallpaperScaling scaling = WallpaperScaling.Fill)
        {
            var monitors = DisplayManager.Instance.GetMonitors();
            if (monitorIndex < 0 || monitorIndex >= monitors.Count) return false;

            var mon = monitors[monitorIndex];
            CloseWallpaper(monitorIndex);

            var assignment = new MonitorAssignment
            {
                MonitorDeviceId = mon.DeviceId,
                MonitorIndex = monitorIndex,
                WallpaperId = wallpaper.Id,
                Mode = mode,
                State = PlaybackState.Playing,
                Volume = _globalVolume,
                IsMuted = _isMuted,
                Scaling = scaling,
                AssignedAt = DateTime.UtcNow
            };

            LaunchPlayer(wallpaper, mon.Bounds.X, mon.Bounds.Y, mon.Bounds.Width, mon.Bounds.Height, monitorIndex, assignment);
            _activeAssignments[monitorIndex] = assignment;
            AuraDatabase.Instance.SaveMonitorAssignment(assignment);
            return true;
        }

        private void LaunchPlayer(WallpaperItem wallpaper, int x, int y, int width, int height, int monitorIndex, MonitorAssignment assignment)
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string playerExe;
            string args;

            if (wallpaper.Type == WallpaperType.Web)
            {
                playerExe = Path.Combine(appDir, "AuraWallpaper.Player.Web.exe");
                args = $"--url \"{wallpaper.FilePath}\" --x {x} --y {y} --w {width} --h {height} --mon {monitorIndex} --vol {assignment.Volume} --mute {assignment.IsMuted}";
            }
            else if (wallpaper.Type == WallpaperType.Video)
            {
                // Video wallpaper: route to AuraWallpaper.Player.Web with HTML5 video engine
                // Ensures 100% GPU hardware acceleration, VP9/AV1/H.264 support, and zero DirectShow EVR glitches under WorkerW
                string videoDir = Path.GetDirectoryName(wallpaper.FilePath) ?? "";
                string scaleMode = assignment.Scaling switch
                {
                    WallpaperScaling.Fit => "contain",
                    WallpaperScaling.Stretch => "fill",
                    WallpaperScaling.Center => "none",
                    _ => "cover" // Fill / Span (Cropped)
                };

                string fileName = Path.GetFileName(wallpaper.FilePath);
                string indexHtml = Path.Combine(videoDir, $"player_{assignment.Scaling.ToString().ToLowerInvariant()}.html");
                string htmlContent;

                if (assignment.Scaling == WallpaperScaling.Fit)
                {
                    // Ambient Frosted Glow Backdrop mode:
                    // Main video preserves 100% aspect ratio (contain) centered
                    // Background video fills full canvas with a blurred, saturated glow (cover + blur)
                    htmlContent = $@"<!DOCTYPE html>
<html>
<head>
  <meta charset=""utf-8"">
  <style>
    * {{ margin: 0; padding: 0; box-sizing: border-box; }}
    html, body {{ width: 100vw; height: 100vh; overflow: hidden; background: #000; position: relative; }}
    .ambient-bg {{
      position: absolute;
      top: -10%; left: -10%;
      width: 120vw; height: 120vh;
      object-fit: cover;
      filter: blur(50px) brightness(0.6) saturate(1.35);
      z-index: 1;
      transform: scale(1.08);
      pointer-events: none;
    }}
    .main-video {{
      position: absolute;
      top: 0; left: 0;
      width: 100vw; height: 100vh;
      object-fit: contain;
      z-index: 2;
      display: block;
    }}
  </style>
</head>
<body>
  <video id=""bg"" class=""ambient-bg"" src=""./{fileName}"" autoplay loop muted playsinline aria-hidden=""true""></video>
  <video id=""v"" class=""main-video"" src=""./{fileName}"" autoplay loop playsinline></video>
  <script>
    const v = document.getElementById('v');
    const bg = document.getElementById('bg');
    v.volume = {((double)assignment.Volume / 100.0).ToString(System.Globalization.CultureInfo.InvariantCulture)};
    if ({assignment.IsMuted.ToString().ToLower()}) v.muted = true;
    v.play().catch(e => {{ v.muted = true; v.play(); }});
    bg.play().catch(() => {{}});
    v.addEventListener('timeupdate', () => {{
      if (Math.abs(bg.currentTime - v.currentTime) > 0.3) {{
        bg.currentTime = v.currentTime;
      }}
    }});
    v.addEventListener('pause', () => bg.pause());
    v.addEventListener('play', () => bg.play());
    v.addEventListener('seeking', () => {{ bg.currentTime = v.currentTime; }});
  </script>
</body>
</html>";
                }
                else
                {
                    htmlContent = $@"<!DOCTYPE html>
<html>
<head>
  <meta charset=""utf-8"">
  <style>
    * {{ margin: 0; padding: 0; box-sizing: border-box; }}
    html, body {{ width: 100vw; height: 100vh; overflow: hidden; background: #000; }}
    video {{ width: 100vw; height: 100vh; object-fit: {scaleMode}; display: block; }}
  </style>
</head>
<body>
  <video id=""v"" src=""./{fileName}"" autoplay loop playsinline></video>
  <script>
    const v = document.getElementById('v');
    v.volume = {((double)assignment.Volume / 100.0).ToString(System.Globalization.CultureInfo.InvariantCulture)};
    if ({assignment.IsMuted.ToString().ToLower()}) v.muted = true;
    v.play().catch(e => {{ v.muted = true; v.play(); }});
  </script>
</body>
</html>";
                }
                try { File.WriteAllText(indexHtml, htmlContent); } catch { }

                string targetUrl = File.Exists(indexHtml) ? indexHtml : wallpaper.FilePath;
                playerExe = Path.Combine(appDir, "AuraWallpaper.Player.Web.exe");
                args = $"--url \"{targetUrl}\" --x {x} --y {y} --w {width} --h {height} --mon {monitorIndex} --vol {assignment.Volume} --mute {assignment.IsMuted}";
            }
            else if (wallpaper.Type == WallpaperType.App)
            {
                // Launch external application and embed
                LaunchExternalAppWallpaper(wallpaper, x, y, width, height, monitorIndex, assignment);
                return;
            }
            else
            {
                // Picture or GIF
                playerExe = Path.Combine(appDir, "AuraWallpaper.Player.Media.exe");
                args = $"--file \"{wallpaper.FilePath}\" --type {(int)wallpaper.Type} --x {x} --y {y} --w {width} --h {height} --mon {monitorIndex} --vol {assignment.Volume} --mute {assignment.IsMuted} --scale {(int)assignment.Scaling}";
            }

            if (!File.Exists(playerExe))
            {
                // In dev environment, check relative build directories
                string debugMedia = Path.GetFullPath(Path.Combine(appDir, @"..\..\..\..\AuraWallpaper.Player.Media\bin\Debug\net8.0-windows\AuraWallpaper.Player.Media.exe"));
                string debugWeb = Path.GetFullPath(Path.Combine(appDir, @"..\..\..\..\AuraWallpaper.Player.Web\bin\Debug\net8.0-windows\AuraWallpaper.Player.Web.exe"));

                if ((wallpaper.Type == WallpaperType.Web || wallpaper.Type == WallpaperType.Video) && File.Exists(debugWeb)) playerExe = debugWeb;
                else if (File.Exists(debugMedia)) playerExe = debugMedia;
            }

            try
            {
                Process? proc = null;
                try
                {
                    var si = new NativeMethods.STARTUPINFO();
                    si.cb = System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.STARTUPINFO));
                    si.lpDesktop = @"WinSta0\Default";

                    string cmdLine = $"\"{playerExe}\" {args}";
                    if (NativeMethods.CreateProcess(null, cmdLine, IntPtr.Zero, IntPtr.Zero, false, 0, IntPtr.Zero, null, ref si, out var pi))
                    {
                        NativeMethods.CloseHandle(pi.hThread);
                        NativeMethods.CloseHandle(pi.hProcess);
                        proc = Process.GetProcessById(pi.dwProcessId);
                    }
                }
                catch { }

                if (proc == null)
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = playerExe,
                        Arguments = args,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    proc = Process.Start(psi);
                }

                if (proc != null)
                {
                    assignment.PlayerProcessId = proc.Id;
                    _playerProcesses[monitorIndex] = proc;

                    ProcessWatchdog.Instance.RegisterProcess(proc, new MonitoredProcessInfo
                    {
                        MonitorIndex = monitorIndex,
                        WallpaperId = wallpaper.Id,
                        ExecutablePath = playerExe,
                        Arguments = args
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to launch player: {ex.Message}");
            }
        }

        private void LaunchExternalAppWallpaper(WallpaperItem wallpaper, int x, int y, int width, int height, int monitorIndex, MonitorAssignment assignment)
        {
            if (!File.Exists(wallpaper.FilePath)) return;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = wallpaper.FilePath,
                    Arguments = wallpaper.Arguments,
                    WorkingDirectory = Path.GetDirectoryName(wallpaper.FilePath),
                    UseShellExecute = true
                };

                var proc = Process.Start(psi);
                if (proc != null)
                {
                    assignment.PlayerProcessId = proc.Id;
                    _playerProcesses[monitorIndex] = proc;

                    // Give window a moment to initialize then embed to desktop WorkerW
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        Thread.Sleep(1500);
                        try
                        {
                            proc.Refresh();
                            IntPtr hwnd = proc.MainWindowHandle;
                            if (hwnd != IntPtr.Zero)
                            {
                                assignment.PlayerWindowHwnd = hwnd;
                                DesktopBridge.Instance.AttachToDesktop(hwnd, x, y, width, height);
                            }
                        }
                        catch
                        {
                        }
                    });
                }
            }
            catch
            {
            }
        }

        public void CloseWallpaper(int monitorIndex)
        {
            if (_playerProcesses.TryRemove(monitorIndex, out var proc))
            {
                try
                {
                    ProcessWatchdog.Instance.UnregisterProcess(proc.Id);
                    if (!proc.HasExited)
                    {
                        proc.Kill(true);
                    }
                }
                catch
                {
                }
            }

            if (_activeAssignments.TryRemove(monitorIndex, out var assignment))
            {
                AuraDatabase.Instance.ClearMonitorAssignment(assignment.MonitorDeviceId);
            }
            AssignmentsChanged?.Invoke();
        }

        public void CloseAllWallpapers()
        {
            foreach (var key in _playerProcesses.Keys.ToList())
            {
                CloseWallpaper(key);
            }
            AuraDatabase.Instance.ClearAllMonitorAssignments();
            AssignmentsChanged?.Invoke();
        }

        #endregion

        #region Playback Controls

        public void PauseAll()
        {
            foreach (var kvp in _activeAssignments)
            {
                kvp.Value.State = PlaybackState.Paused;
                SendPlayerControl(kvp.Key, "pause");
            }
            AssignmentsChanged?.Invoke();
        }

        public void ResumeAll()
        {
            foreach (var kvp in _activeAssignments)
            {
                kvp.Value.State = PlaybackState.Playing;
                SendPlayerControl(kvp.Key, "play");
            }
            AssignmentsChanged?.Invoke();
        }

        public void SetVolume(int volume)
        {
            _globalVolume = Math.Clamp(volume, 0, 100);
            foreach (var kvp in _activeAssignments)
            {
                kvp.Value.Volume = _globalVolume;
                SendPlayerControl(kvp.Key, $"volume {_globalVolume}");
            }
            AssignmentsChanged?.Invoke();
        }

        public void MuteAll()
        {
            _isMuted = true;
            foreach (var kvp in _activeAssignments)
            {
                kvp.Value.IsMuted = true;
                SendPlayerControl(kvp.Key, "mute");
            }
            AssignmentsChanged?.Invoke();
        }

        public void UnmuteAll()
        {
            _isMuted = false;
            foreach (var kvp in _activeAssignments)
            {
                kvp.Value.IsMuted = false;
                SendPlayerControl(kvp.Key, "unmute");
            }
            AssignmentsChanged?.Invoke();
        }

        public void NextWallpaper()
        {
            var wallpapers = AuraDatabase.Instance.GetAllWallpapers();
            if (wallpapers.Count == 0) return;

            string currentWid = _activeAssignments.Values.FirstOrDefault()?.WallpaperId ?? "";
            int idx = wallpapers.FindIndex(w => w.Id == currentWid);
            int nextIdx = (idx + 1) % wallpapers.Count;

            var monitors = DisplayManager.Instance.GetMonitors();
            var curMode = IsSpanMode ? AssignmentMode.Span : (IsDuplicateMode || monitors.Count > 1 ? AssignmentMode.Duplicate : AssignmentMode.PerMonitor);
            var curScaling = _activeAssignments.Values.FirstOrDefault()?.Scaling ?? WallpaperScaling.Fill;
            int monIdx = curMode == AssignmentMode.Span ? -1 : 0;

            SetWallpaper(wallpapers[nextIdx].Id, monIdx, curMode, curScaling);
        }

        public void PreviousWallpaper()
        {
            var wallpapers = AuraDatabase.Instance.GetAllWallpapers();
            if (wallpapers.Count == 0) return;

            string currentWid = _activeAssignments.Values.FirstOrDefault()?.WallpaperId ?? "";
            int idx = wallpapers.FindIndex(w => w.Id == currentWid);
            int prevIdx = (idx - 1 + wallpapers.Count) % wallpapers.Count;

            var monitors = DisplayManager.Instance.GetMonitors();
            var curMode = IsSpanMode ? AssignmentMode.Span : (IsDuplicateMode || monitors.Count > 1 ? AssignmentMode.Duplicate : AssignmentMode.PerMonitor);
            var curScaling = _activeAssignments.Values.FirstOrDefault()?.Scaling ?? WallpaperScaling.Fill;
            int monIdx = curMode == AssignmentMode.Span ? -1 : 0;

            SetWallpaper(wallpapers[prevIdx].Id, monIdx, curMode, curScaling);
        }

        public void SetRandomWallpaper(int targetMonitorIndex = -2)
        {
            var wallpapers = AuraDatabase.Instance.GetAllWallpapers();
            if (wallpapers.Count == 0) return;

            int r = new Random().Next(0, wallpapers.Count);
            var monitors = DisplayManager.Instance.GetMonitors();

            if (targetMonitorIndex >= 0 && targetMonitorIndex < monitors.Count)
            {
                // Target specific monitor
                SetWallpaper(wallpapers[r].Id, targetMonitorIndex, AssignmentMode.PerMonitor, WallpaperScaling.Fill);
                return;
            }

            var curMode = IsSpanMode ? AssignmentMode.Span : (IsDuplicateMode || monitors.Count > 1 ? AssignmentMode.Duplicate : AssignmentMode.PerMonitor);
            var curScaling = _activeAssignments.Values.FirstOrDefault()?.Scaling ?? WallpaperScaling.Fill;
            int monIdx = curMode == AssignmentMode.Span ? -1 : 0;

            SetWallpaper(wallpapers[r].Id, monIdx, curMode, curScaling);
        }

        private void SendPlayerControl(int monitorIndex, string command)
        {
            // Player processes communicate via their own specific pipe or window message
        }

        #endregion

        #region Event Handlers

        private void OnExplorerRestarted(object? sender, EventArgs e)
        {
            // Restore wallpapers immediately to newly spawned WorkerW
            RestoreSavedWallpapers();
        }

        private void OnDisplayChanged(object? sender, EventArgs e)
        {
            // Display topology changed: re-apply assignments to appropriate screens
            var monitors = DisplayManager.Instance.GetMonitors();
            var assignments = AuraDatabase.Instance.GetMonitorAssignments();

            if (assignments.Count == 0)
            {
                AutoApplyDefaultMultiDisplayConfiguration(monitors);
                return;
            }

            RestoreSavedWallpapers();
        }

        private void OnPerformancePauseRequested(bool shouldPause, string reason)
        {
            if (shouldPause) PauseAll();
            else ResumeAll();
        }

        private void OnPlaylistRotationRequested(string playlistId, string wallpaperId, int monitorIndex)
        {
            if (monitorIndex == -1)
            {
                SetWallpaper(wallpaperId, 0, AssignmentMode.Duplicate);
            }
            else
            {
                SetWallpaper(wallpaperId, monitorIndex, AssignmentMode.PerMonitor);
            }
        }

        public void AutoApplyDefaultMultiDisplayConfiguration(List<MonitorDevice>? monitors = null)
        {
            monitors ??= DisplayManager.Instance.GetMonitors();
            if (monitors.Count == 0) return;

            var wallpapers = AuraDatabase.Instance.GetAllWallpapers();
            if (wallpapers.Count == 0) return;

            // Pick the first available favorite or first library wallpaper
            var defaultWp = wallpapers.FirstOrDefault(w => w.IsFavorite) ?? wallpapers[0];

            if (monitors.Count > 1)
            {
                // Multiple monitors automatically detected: apply Duplicate across all displays
                SetWallpaper(defaultWp.Id, 0, AssignmentMode.Duplicate, WallpaperScaling.Fill);
            }
            else
            {
                // Single display: default to main single screen
                SetWallpaper(defaultWp.Id, 0, AssignmentMode.PerMonitor, WallpaperScaling.Fill);
            }
        }

        public void RestoreSavedWallpapers()
        {
            var assignments = AuraDatabase.Instance.GetMonitorAssignments();
            var monitors = DisplayManager.Instance.GetMonitors();

            if (assignments.Count == 0)
            {
                // Auto-detect monitor setup on empty/first-run state
                AutoApplyDefaultMultiDisplayConfiguration(monitors);
                return;
            }

            var spanAssignment = assignments.FirstOrDefault(a => a.Mode == AssignmentMode.Span || a.MonitorIndex == -1 || a.MonitorDeviceId == "SPAN_VIRTUAL");
            if (spanAssignment != null)
            {
                SetWallpaper(spanAssignment.WallpaperId, -1, AssignmentMode.Span, spanAssignment.Scaling);
                return;
            }

            var duplicateAssignments = assignments.Where(a => a.Mode == AssignmentMode.Duplicate).ToList();
            if (duplicateAssignments.Count > 0)
            {
                var firstDup = duplicateAssignments[0];
                SetWallpaper(firstDup.WallpaperId, 0, AssignmentMode.Duplicate, firstDup.Scaling);
                return;
            }

            // Per-monitor assignments
            foreach (var a in assignments)
            {
                SetWallpaper(a.WallpaperId, a.MonitorIndex, a.Mode, a.Scaling);
            }

            // If user has multiple monitors, but only a single non-permonitor assignment was in DB,
            // auto-duplicate across all connected displays
            if (monitors.Count > 1 && assignments.Count == 1 && assignments[0].Mode != AssignmentMode.PerMonitor)
            {
                SetWallpaper(assignments[0].WallpaperId, 0, AssignmentMode.Duplicate, assignments[0].Scaling);
            }
        }

        #endregion

        #region IPC Command Dispatcher

        public IpcResponse HandleIpcCommand(IpcRequest req)
        {
            string cmd = req.Command?.ToLowerInvariant().Trim() ?? "";

            switch (cmd)
            {
                case "show":
                    WindowShowRequested?.Invoke();
                    return IpcResponse.Ok("Application window shown.");

                case "hide":
                    WindowHideRequested?.Invoke();
                    return IpcResponse.Ok("Application window hidden.");

                case "view":
                case "navigate":
                    string target = req.PropertyName ?? req.Mode ?? "active";
                    ViewNavigationRequested?.Invoke(target);
                    return IpcResponse.Ok($"Navigated to view: {target}");

                case "play":
                    ResumeAll();
                    return IpcResponse.Ok("Wallpapers resumed.");

                case "pause":
                    PauseAll();
                    return IpcResponse.Ok("Wallpapers paused.");

                case "mute":
                    MuteAll();
                    return IpcResponse.Ok("Wallpapers muted.");

                case "unmute":
                    UnmuteAll();
                    return IpcResponse.Ok("Wallpapers unmuted.");

                case "volume":
                    if (req.Volume >= 0)
                    {
                        SetVolume(req.Volume);
                        return IpcResponse.Ok($"Volume set to {req.Volume}%.");
                    }
                    return IpcResponse.Fail("Missing volume parameter (0-100).");

                case "set":
                    if (req.Random)
                    {
                        SetRandomWallpaper();
                        return IpcResponse.Ok("Random wallpaper set.");
                    }
                    if (!string.IsNullOrEmpty(req.FilePath))
                    {
                        var item = AuraDatabase.Instance.GetAllWallpapers()
                            .FirstOrDefault(w => w.FilePath.Equals(req.FilePath, StringComparison.OrdinalIgnoreCase));

                        if (item == null)
                        {
                            item = WallpaperPackageService.Instance.ImportFile(req.FilePath);
                        }

                        int mon = req.MonitorIndex >= 0 ? req.MonitorIndex : 0;
                        AssignmentMode assignMode;
                        if (req.All || string.Equals(req.Mode, "duplicate", StringComparison.OrdinalIgnoreCase))
                        {
                            assignMode = AssignmentMode.Duplicate;
                        }
                        else if (string.Equals(req.Mode, "span", StringComparison.OrdinalIgnoreCase) || string.Equals(req.Mode, "extend", StringComparison.OrdinalIgnoreCase))
                        {
                            assignMode = AssignmentMode.Span;
                        }
                        else if (string.Equals(req.Mode, "permonitor", StringComparison.OrdinalIgnoreCase) || req.MonitorIndex >= 0)
                        {
                            assignMode = AssignmentMode.PerMonitor;
                        }
                        else
                        {
                            // Auto-detect: if multi-monitor, default to Duplicate across all screens; if single, default to PerMonitor (0)
                            assignMode = DisplayManager.Instance.GetMonitors().Count > 1 ? AssignmentMode.Duplicate : AssignmentMode.PerMonitor;
                        }

                        WallpaperScaling scaling = WallpaperScaling.Fill;
                        if (string.Equals(req.Scaling, "fit", StringComparison.OrdinalIgnoreCase))
                        {
                            scaling = WallpaperScaling.Fit;
                        }
                        else if (string.Equals(req.Scaling, "stretch", StringComparison.OrdinalIgnoreCase))
                        {
                            scaling = WallpaperScaling.Stretch;
                        }

                        SetWallpaper(item.Id, mon, assignMode, scaling);
                        return IpcResponse.Ok($"Wallpaper set: {item.Title} (Mode: {assignMode}, Scaling: {scaling})");
                    }
                    return IpcResponse.Fail("Missing --file or --random option.");

                case "close":
                    if (req.All || req.MonitorIndex == -1)
                    {
                        CloseAllWallpapers();
                        return IpcResponse.Ok("All wallpapers closed.");
                    }
                    CloseWallpaper(req.MonitorIndex);
                    return IpcResponse.Ok($"Wallpaper on monitor {req.MonitorIndex} closed.");

                case "next":
                    NextWallpaper();
                    return IpcResponse.Ok("Switched to next wallpaper.");

                case "previous":
                    PreviousWallpaper();
                    return IpcResponse.Ok("Switched to previous wallpaper.");

                case "list":
                    var list = AuraDatabase.Instance.GetAllWallpapers()
                        .Select(w => new { w.Id, w.Title, Type = w.Type.ToString(), w.FilePath, w.Author, w.Category })
                        .ToList();
                    return IpcResponse.Ok("Wallpaper list retrieved.", list);

                case "monitors":
                    var mons = DisplayManager.Instance.GetMonitors()
                        .Select(m => new { m.Index, m.DeviceId, m.DeviceName, Bounds = m.Bounds.ToString(), m.IsPrimary })
                        .ToList();
                    return IpcResponse.Ok("Monitor list retrieved.", mons);

                case "status":
                    var status = new
                    {
                        ActiveWallpapers = _activeAssignments.Values.ToList(),
                        IsMuted = _isMuted,
                        GlobalVolume = _globalVolume,
                        Metrics = SystemResourceMonitor.Instance.CurrentMetrics
                    };
                    return IpcResponse.Ok("Status retrieved.", status);

                case "random":
                    SetRandomWallpaper();
                    return IpcResponse.Ok("Random wallpaper set.");

                case "screensaver":
                    try
                    {
                        string scrPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AuraScreensaver.scr");
                        if (!File.Exists(scrPath)) scrPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AuraScreensaver.exe");
                        if (!File.Exists(scrPath))
                        {
                            string localApp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "AuraWallpaper", "AuraScreensaver.scr");
                            if (File.Exists(localApp)) scrPath = localApp;
                        }
                        if (!File.Exists(scrPath))
                        {
                            string distScr = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\..\dist\AuraWallpaper\AuraScreensaver.scr"));
                            if (File.Exists(distScr)) scrPath = distScr;
                        }

                        if (File.Exists(scrPath))
                        {
                            Process.Start(new ProcessStartInfo(scrPath, "/s") { UseShellExecute = true });
                            return IpcResponse.Ok("Screensaver launched.");
                        }
                        return IpcResponse.Fail("Screensaver binary not found.");
                    }
                    catch (Exception ex)
                    {
                        return IpcResponse.Fail("Failed to trigger screensaver: " + ex.Message);
                    }

                case "shutdown":
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        Thread.Sleep(500);
                        Environment.Exit(0);
                    });
                    return IpcResponse.Ok("Aura Wallpaper shutting down.");

                default:
                    return IpcResponse.Fail($"Unknown command: {req.Command}");
            }
        }

        #endregion

        public IReadOnlyDictionary<int, MonitorAssignment> ActiveAssignments => _activeAssignments;

        public void Dispose()
        {
            _ipcServer.Dispose();
            CloseAllWallpapers();
            ProcessWatchdog.Instance.Dispose();
            WasapiAudioCapture.Instance.Dispose();
        }
    }
}
