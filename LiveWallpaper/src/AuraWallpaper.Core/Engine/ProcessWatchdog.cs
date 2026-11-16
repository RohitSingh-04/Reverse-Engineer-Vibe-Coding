using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace AuraWallpaper.Core.Engine
{
    public class MonitoredProcessInfo
    {
        public int MonitorIndex { get; set; }
        public string WallpaperId { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public int RestartCount { get; set; }
        public DateTime LastRestartTime { get; set; } = DateTime.UtcNow;
    }

    public class ProcessWatchdog : IDisposable
    {
        private static ProcessWatchdog? _instance;
        public static ProcessWatchdog Instance => _instance ??= new ProcessWatchdog();

        private readonly ConcurrentDictionary<int, (Process Process, MonitoredProcessInfo Info)> _activeProcesses = new();
        private bool _isDisposed;

        public event Action<int, MonitoredProcessInfo>? ProcessCrashed;

        private ProcessWatchdog()
        {
        }

        public void RegisterProcess(Process process, MonitoredProcessInfo info)
        {
            if (process == null) return;

            process.EnableRaisingEvents = true;
            process.Exited += (s, e) => OnProcessExited(process.Id);

            _activeProcesses[process.Id] = (process, info);
        }

        public void UnregisterProcess(int processId)
        {
            _activeProcesses.TryRemove(processId, out _);
        }

        private void OnProcessExited(int processId)
        {
            if (_isDisposed) return;

            if (_activeProcesses.TryRemove(processId, out var item))
            {
                // Check if exit was clean (0) or crash
                int exitCode = -1;
                try
                {
                    exitCode = item.Process.ExitCode;
                }
                catch
                {
                }

                if (exitCode != 0)
                {
                    // Unexpected exit: throttled restart
                    var now = DateTime.UtcNow;
                    if ((now - item.Info.LastRestartTime).TotalSeconds < 30)
                    {
                        item.Info.RestartCount++;
                    }
                    else
                    {
                        item.Info.RestartCount = 1;
                    }
                    item.Info.LastRestartTime = now;

                    if (item.Info.RestartCount <= 3)
                    {
                        ProcessCrashed?.Invoke(processId, item.Info);
                    }
                }
            }
        }

        public void KillAllProcesses()
        {
            foreach (var kvp in _activeProcesses)
            {
                try
                {
                    if (!kvp.Value.Process.HasExited)
                    {
                        kvp.Value.Process.Kill(true);
                    }
                }
                catch
                {
                }
            }
            _activeProcesses.Clear();
        }

        public void Dispose()
        {
            _isDisposed = true;
            KillAllProcesses();
        }
    }
}
