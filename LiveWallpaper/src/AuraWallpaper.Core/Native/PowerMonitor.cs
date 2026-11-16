using System;
using System.Threading;

namespace AuraWallpaper.Core.Native
{
    public class PowerStatusEventArgs : EventArgs
    {
        public bool IsOnBattery { get; set; }
        public int BatteryPercentage { get; set; }
    }

    public class PowerMonitor : IDisposable
    {
        private static PowerMonitor? _instance;
        public static PowerMonitor Instance => _instance ??= new PowerMonitor();

        private readonly Timer? _timer;
        private bool _lastOnBattery;
        private int _lastPercentage = -1;

        public event EventHandler<PowerStatusEventArgs>? PowerStatusChanged;

        private PowerMonitor()
        {
            // Event-driven / low frequency polling (every 10 seconds, zero impact on CPU)
            _timer = new Timer(CheckPowerStatus, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
        }

        public (bool isOnBattery, int percentage) GetCurrentStatus()
        {
            if (NativeMethods.GetSystemPowerStatus(out var status))
            {
                bool onBattery = status.ACLineStatus == 0; // 0 = Offline (battery), 1 = Online (AC), 255 = Unknown
                int percent = status.BatteryLifePercent <= 100 ? status.BatteryLifePercent : 100;
                return (onBattery, percent);
            }
            return (false, 100);
        }

        private void CheckPowerStatus(object? state)
        {
            var (onBattery, percent) = GetCurrentStatus();
            if (onBattery != _lastOnBattery || Math.Abs(percent - _lastPercentage) >= 5)
            {
                _lastOnBattery = onBattery;
                _lastPercentage = percent;
                PowerStatusChanged?.Invoke(this, new PowerStatusEventArgs
                {
                    IsOnBattery = onBattery,
                    BatteryPercentage = percent
                });
            }
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
