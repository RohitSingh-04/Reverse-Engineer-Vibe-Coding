using System;
using System.Collections.Generic;
using System.Linq;
using AuraWallpaper.Core.Models;
using AuraWallpaper.Core.Native;
using AuraWallpaper.Core.Storage;

namespace AuraWallpaper.Core.Engine
{
    public class PerformanceCoordinator : IDisposable
    {
        private static PerformanceCoordinator? _instance;
        public static PerformanceCoordinator Instance => _instance ??= new PerformanceCoordinator();

        private bool _isFullscreenPaused;
        private bool _isBatteryPaused;
        private bool _isAppRulePaused;
        private string? _pausedByRuleProcess;

        public event Action<bool, string>? PauseStateRequested;

        private PerformanceCoordinator()
        {
            FullscreenWatcher.Instance.FullscreenChanged += OnFullscreenChanged;
            PowerMonitor.Instance.PowerStatusChanged += OnPowerStatusChanged;
        }

        private void OnFullscreenChanged(object? sender, FullscreenChangedEventArgs e)
        {
            var settings = AuraDatabase.Instance.LoadSettings();

            // Check if specific application rule applies
            if (!string.IsNullOrEmpty(e.ProcessName))
            {
                var rules = AuraDatabase.Instance.GetAllApplicationRules();
                var matched = rules.FirstOrDefault(r => r.IsEnabled && r.ProcessName.Equals(e.ProcessName, StringComparison.OrdinalIgnoreCase));
                if (matched != null)
                {
                    if (matched.Action == RuleAction.Pause)
                    {
                        _isAppRulePaused = true;
                        _pausedByRuleProcess = e.ProcessName;
                        PauseStateRequested?.Invoke(true, $"Application rule: {e.ProcessName}");
                        return;
                    }
                    if (matched.Action == RuleAction.Ignore)
                    {
                        // Explicitly ignore rule
                        return;
                    }
                }
            }

            if (_isAppRulePaused && !e.IsFullscreen && (e.ProcessName != _pausedByRuleProcess))
            {
                _isAppRulePaused = false;
                _pausedByRuleProcess = null;
                EvaluateCombinedState();
                return;
            }

            // Standard fullscreen check
            if (settings.FullscreenAction == RuleAction.Pause)
            {
                if (e.IsFullscreen && !_isFullscreenPaused)
                {
                    _isFullscreenPaused = true;
                    PauseStateRequested?.Invoke(true, "Fullscreen application active");
                }
                else if (!e.IsFullscreen && _isFullscreenPaused)
                {
                    _isFullscreenPaused = false;
                    EvaluateCombinedState();
                }
            }
        }

        private void OnPowerStatusChanged(object? sender, PowerStatusEventArgs e)
        {
            var settings = AuraDatabase.Instance.LoadSettings();
            if (settings.BatteryAction == RuleAction.Pause)
            {
                bool shouldPause = e.IsOnBattery && (settings.BatteryPauseThreshold == 0 || e.BatteryPercentage <= settings.BatteryPauseThreshold);
                if (shouldPause && !_isBatteryPaused)
                {
                    _isBatteryPaused = true;
                    PauseStateRequested?.Invoke(true, $"Battery low ({e.BatteryPercentage}%)");
                }
                else if (!shouldPause && _isBatteryPaused)
                {
                    _isBatteryPaused = false;
                    EvaluateCombinedState();
                }
            }
        }

        private void EvaluateCombinedState()
        {
            bool shouldBePaused = _isFullscreenPaused || _isBatteryPaused || _isAppRulePaused;
            if (!shouldBePaused)
            {
                PauseStateRequested?.Invoke(false, "Restored normal playback");
            }
        }

        public void Dispose()
        {
            FullscreenWatcher.Instance.FullscreenChanged -= OnFullscreenChanged;
            PowerMonitor.Instance.PowerStatusChanged -= OnPowerStatusChanged;
        }
    }
}
