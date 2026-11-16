using System;
using System.Diagnostics;
using AuraWallpaper.Core.Engine;
using AuraWallpaper.Core.Models;
using Xunit;

namespace AuraWallpaper.Tests
{
    public class PerformanceRuleTests
    {
        [Fact]
        public void ProcessWatchdog_RegistersAndCleansUp()
        {
            var watchdog = ProcessWatchdog.Instance;
            var currentProc = Process.GetCurrentProcess();

            watchdog.RegisterProcess(currentProc, new MonitoredProcessInfo
            {
                MonitorIndex = 0,
                WallpaperId = "test_wp",
                ExecutablePath = currentProc.MainModule?.FileName ?? ""
            });

            // Unregister without killing current test runner
            watchdog.UnregisterProcess(currentProc.Id);
        }

        [Fact]
        public void ApplicationRule_SetsCorrectProperties()
        {
            var rule = new ApplicationRule
            {
                ProcessName = "Cyberpunk2077.exe",
                Action = RuleAction.Pause,
                Description = "Pause wallpapers while playing Cyberpunk",
                IsEnabled = true
            };

            Assert.Equal("Cyberpunk2077.exe", rule.ProcessName);
            Assert.Equal(RuleAction.Pause, rule.Action);
            Assert.True(rule.IsEnabled);
        }
    }
}
