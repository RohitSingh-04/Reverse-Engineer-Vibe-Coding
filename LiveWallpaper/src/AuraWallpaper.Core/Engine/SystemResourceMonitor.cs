using System;
using System.Diagnostics;
using System.Threading;

namespace AuraWallpaper.Core.Engine
{
    public class SystemMetrics
    {
        public float CpuUsagePercent { get; set; }
        public long MemoryUsedBytes { get; set; }
        public long TotalMemoryBytes { get; set; }
        public int ActiveRenderers { get; set; }
    }

    public class SystemResourceMonitor : IDisposable
    {
        private static SystemResourceMonitor? _instance;
        public static SystemResourceMonitor Instance => _instance ??= new SystemResourceMonitor();

        private readonly Timer _timer;
        private DateTime _lastCpuTime = DateTime.UtcNow;
        private TimeSpan _lastTotalProcessorTime = TimeSpan.Zero;
        private readonly Process _currentProcess = Process.GetCurrentProcess();

        public SystemMetrics CurrentMetrics { get; private set; } = new SystemMetrics();
        public event EventHandler<SystemMetrics>? MetricsUpdated;

        private SystemResourceMonitor()
        {
            _lastTotalProcessorTime = _currentProcess.TotalProcessorTime;
            // Update stats every 2 seconds
            _timer = new Timer(UpdateMetrics, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
        }

        private void UpdateMetrics(object? state)
        {
            try
            {
                var now = DateTime.UtcNow;
                var cpuPassed = (now - _lastCpuTime).TotalMilliseconds;
                var totalProcTime = _currentProcess.TotalProcessorTime;
                var procPassed = (totalProcTime - _lastTotalProcessorTime).TotalMilliseconds;

                _lastCpuTime = now;
                _lastTotalProcessorTime = totalProcTime;

                float cpuPercent = 0;
                if (cpuPassed > 0)
                {
                    cpuPercent = (float)((procPassed / (cpuPassed * Environment.ProcessorCount)) * 100);
                    if (cpuPercent > 100) cpuPercent = 100;
                    if (cpuPercent < 0) cpuPercent = 0;
                }

                _currentProcess.Refresh();
                long memUsed = _currentProcess.WorkingSet64;

                CurrentMetrics = new SystemMetrics
                {
                    CpuUsagePercent = cpuPercent,
                    MemoryUsedBytes = memUsed,
                    TotalMemoryBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes
                };

                MetricsUpdated?.Invoke(this, CurrentMetrics);
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            _timer.Dispose();
        }
    }
}
