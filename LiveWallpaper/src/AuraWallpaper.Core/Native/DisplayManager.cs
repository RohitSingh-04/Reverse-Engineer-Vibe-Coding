using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AuraWallpaper.Core.Models;
using Microsoft.Win32;

namespace AuraWallpaper.Core.Native
{
    public class DisplayManager
    {
        private static DisplayManager? _instance;
        public static DisplayManager Instance => _instance ??= new DisplayManager();

        public event EventHandler? DisplayConfigurationChanged;

        private DisplayManager()
        {
            try
            {
                SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            }
            catch
            {
                // In non-UI thread or isolated tests, ignore failure
            }
        }

        private void OnDisplaySettingsChanged(object? sender, EventArgs e)
        {
            DisplayConfigurationChanged?.Invoke(this, EventArgs.Empty);
        }

        public List<MonitorDevice> GetMonitors()
        {
            var monitors = new List<MonitorDevice>();
            int index = 0;

            NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref NativeMethods.RECT lprcMonitor, IntPtr dwData) =>
            {
                var mi = new NativeMethods.MONITORINFOEX();
                mi.cbSize = Marshal.SizeOf(typeof(NativeMethods.MONITORINFOEX));

                if (NativeMethods.GetMonitorInfo(hMonitor, ref mi))
                {
                    bool isPrimary = (mi.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0;
                    string deviceName = mi.szDevice ?? $"Display {index + 1}";
                    string friendlyName = GetFriendlyMonitorName(deviceName, index + 1);

                    var device = new MonitorDevice
                    {
                        Index = index,
                        DeviceId = mi.szDevice ?? $"MONITOR_{index}",
                        DeviceName = friendlyName,
                        HMonitor = hMonitor,
                        Bounds = new MonitorRect(mi.rcMonitor.Left, mi.rcMonitor.Top, mi.rcMonitor.Width, mi.rcMonitor.Height),
                        WorkingArea = new MonitorRect(mi.rcWork.Left, mi.rcWork.Top, mi.rcWork.Width, mi.rcWork.Height),
                        IsPrimary = isPrimary,
                        ScalingFactor = 1.0 // Normalized DPI scale
                    };

                    monitors.Add(device);
                    index++;
                }
                return true;
            }, IntPtr.Zero);

            if (monitors.Count == 0)
            {
                // Fallback to primary screen
                int w = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN);
                int h = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN);
                monitors.Add(new MonitorDevice
                {
                    Index = 0,
                    DeviceId = "\\\\.\\DISPLAY1",
                    DeviceName = "Primary Display",
                    Bounds = new MonitorRect(0, 0, w > 0 ? w : 1920, h > 0 ? h : 1080),
                    WorkingArea = new MonitorRect(0, 0, w > 0 ? w : 1920, h > 0 ? h : 1080),
                    IsPrimary = true,
                    ScalingFactor = 1.0
                });
            }

            return monitors;
        }

        public MonitorRect GetVirtualScreenBounds()
        {
            int x = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
            int y = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
            int cx = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
            int cy = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);

            if (cx <= 0 || cy <= 0)
            {
                cx = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN);
                cy = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN);
                x = 0;
                y = 0;
            }

            return new MonitorRect(x, y, cx, cy);
        }

        private static string GetFriendlyMonitorName(string deviceName, int fallbackNumber)
        {
            if (string.IsNullOrWhiteSpace(deviceName))
                return $"Monitor {fallbackNumber}";

            return $"Monitor {fallbackNumber} ({deviceName.Replace("\\\\.\\", "")})";
        }
    }
}
