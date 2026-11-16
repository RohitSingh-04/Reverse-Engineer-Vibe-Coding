using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AuraWallpaper.Core.Engine;
using AuraWallpaper.Core.Models;
using AuraWallpaper.Core.Storage;

using UserControl = System.Windows.Controls.UserControl;

namespace AuraWallpaper.UI.Views
{
    public class RuleViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public RuleAction Action { get; set; }
        public string ActionText => Action.ToString();
    }

    public partial class PerformanceView : UserControl
    {
        public PerformanceView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            LoadRules();

            SystemResourceMonitor.Instance.MetricsUpdated += (s, m) =>
            {
                Dispatcher.Invoke(() =>
                {
                    CpuText.Text = $"{m.CpuUsagePercent:0.0} %";
                    MemoryText.Text = $"{m.MemoryUsedBytes / (1024 * 1024)} MB";
                    RenderersText.Text = $"{WallpaperManager.Instance.ActiveAssignments.Count} Active";
                });
            };
        }

        private void LoadSettings()
        {
            var settings = AuraDatabase.Instance.LoadSettings();

            FullscreenCombo.SelectedIndex = settings.FullscreenAction switch
            {
                RuleAction.Pause => 0,
                RuleAction.Mute => 1,
                _ => 2
            };

            BatteryCombo.SelectedIndex = settings.BatteryAction switch
            {
                RuleAction.Pause when settings.BatteryPauseThreshold == 0 => 0,
                RuleAction.Pause when settings.BatteryPauseThreshold > 0 => 1,
                _ => 2
            };
        }

        private void LoadRules()
        {
            var rules = AuraDatabase.Instance.GetAllApplicationRules();
            var list = rules.Select(r => new RuleViewModel
            {
                Id = r.Id,
                ProcessName = r.ProcessName,
                Action = r.Action
            }).ToList();

            RulesListBox.ItemsSource = list;
        }

        private void FullscreenCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FullscreenCombo == null) return;
            var settings = AuraDatabase.Instance.LoadSettings();
            settings.FullscreenAction = FullscreenCombo.SelectedIndex switch
            {
                0 => RuleAction.Pause,
                1 => RuleAction.Mute,
                _ => RuleAction.Ignore
            };
            AuraDatabase.Instance.SaveSettings(settings);
        }

        private void BatteryCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BatteryCombo == null) return;
            var settings = AuraDatabase.Instance.LoadSettings();
            if (BatteryCombo.SelectedIndex == 0)
            {
                settings.BatteryAction = RuleAction.Pause;
                settings.BatteryPauseThreshold = 0;
            }
            else if (BatteryCombo.SelectedIndex == 1)
            {
                settings.BatteryAction = RuleAction.Pause;
                settings.BatteryPauseThreshold = 20;
            }
            else
            {
                settings.BatteryAction = RuleAction.Ignore;
            }
            AuraDatabase.Instance.SaveSettings(settings);
        }

        private void AddRuleButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new Window
            {
                Title = "Add Application Optimization Rule",
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x20, 0x20, 0x20)),
                Foreground = System.Windows.Media.Brushes.White,
                ResizeMode = ResizeMode.NoResize
            };

            var sp = new StackPanel { Margin = new Thickness(20) };
            sp.Children.Add(new TextBlock { Text = "Process Name (e.g. game.exe, chrome.exe):", Margin = new Thickness(0, 0, 0, 8) });
            var tb = new TextBox { Height = 32, Margin = new Thickness(0, 0, 0, 16), Text = "game.exe" };
            sp.Children.Add(tb);

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var okBtn = new Button { Content = "Add Rule", Width = 90, Height = 30, Margin = new Thickness(0, 0, 8, 0), Background = System.Windows.Media.Brushes.DodgerBlue, Foreground = System.Windows.Media.Brushes.White };
            var cancelBtn = new Button { Content = "Cancel", Width = 80, Height = 30, Background = System.Windows.Media.Brushes.Gray, Foreground = System.Windows.Media.Brushes.White };

            okBtn.Click += (s, args) =>
            {
                if (!string.IsNullOrWhiteSpace(tb.Text))
                {
                    AuraDatabase.Instance.SaveApplicationRule(new ApplicationRule
                    {
                        ProcessName = tb.Text.Trim().ToLowerInvariant(),
                        Action = RuleAction.Pause,
                        Description = "Auto-paused during process execution",
                        IsEnabled = true
                    });
                    win.Close();
                    LoadRules();
                }
            };
            cancelBtn.Click += (s, args) => win.Close();

            btnRow.Children.Add(okBtn);
            btnRow.Children.Add(cancelBtn);
            sp.Children.Add(btnRow);

            win.Content = sp;
            win.ShowDialog();
        }

        private void RemoveRuleButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is RuleViewModel vm)
            {
                AuraDatabase.Instance.DeleteApplicationRule(vm.Id);
                LoadRules();
            }
        }
    }
}
