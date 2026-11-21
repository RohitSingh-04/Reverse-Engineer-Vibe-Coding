using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AuraWallpaper.Core.Engine;
using AuraWallpaper.Core.Models;
using AuraWallpaper.Core.Native;
using AuraWallpaper.Core.Storage;

using UserControl = System.Windows.Controls.UserControl;

namespace AuraWallpaper.UI.Views
{
    public class DisplayTopologyItem
    {
        public string DisplayName { get; set; } = string.Empty;
        public string ResolutionText { get; set; } = string.Empty;
    }

    public class ActiveMonitorCardViewModel
    {
        public int MonitorIndex { get; set; }
        public string MonitorName { get; set; } = string.Empty;
        public string ResolutionText { get; set; } = string.Empty;
        public string WallpaperId { get; set; } = string.Empty;
        public string WallpaperTitle { get; set; } = "None";
        public string WallpaperThumbnail { get; set; } = string.Empty;
        public string StatusText { get; set; } = "Idle";
        public int Volume { get; set; } = 80;
        public string VolumeText => $"{Volume}%";
        public bool IsPlaying { get; set; }
        public string PlayPauseButtonText => IsPlaying ? "⏸ Pause" : "▶ Resume";
        public bool IsSpan { get; set; }
    }

    public partial class ActiveWallpapersView : UserControl
    {
        public ActiveWallpapersView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshCards();
            WallpaperManager.Instance.AssignmentsChanged += () => Dispatcher.Invoke(RefreshCards);
        }

        public void RefreshCards()
        {
            var monitors = DisplayManager.Instance.GetMonitors();
            var assignments = AuraDatabase.Instance.GetMonitorAssignments();

            // 1. Update Display Topology Strip
            var topologyList = new List<DisplayTopologyItem>();
            for (int i = 0; i < monitors.Count; i++)
            {
                var m = monitors[i];
                topologyList.Add(new DisplayTopologyItem
                {
                    DisplayName = $"Display {i + 1}{(m.IsPrimary ? " [Primary]" : "")}",
                    ResolutionText = $"{m.Bounds.Width}x{m.Bounds.Height}"
                });
            }
            if (DisplayTopologyItemsControl != null)
            {
                DisplayTopologyItemsControl.ItemsSource = topologyList;
            }

            // 2. Build Active Wallpaper Cards
            var list = new List<ActiveMonitorCardViewModel>();

            var spanAssignment = assignments.FirstOrDefault(a => a.Mode == AssignmentMode.Span || a.MonitorIndex == -1 || a.MonitorDeviceId == "SPAN_VIRTUAL");
            if (spanAssignment != null || WallpaperManager.Instance.IsSpanMode)
            {
                var vBounds = DisplayManager.Instance.GetVirtualScreenBounds();
                var spanItem = spanAssignment ?? WallpaperManager.Instance.GetAssignment(-1);
                string scaleDesc = spanItem?.Scaling == WallpaperScaling.Fit ? "Extend Fit (Ambient Glow)" : "Extend Cropped (Fill All Displays)";

                var vm = new ActiveMonitorCardViewModel
                {
                    MonitorIndex = -1,
                    MonitorName = $"🖥️ Spanned Desktop ({monitors.Count} Displays Combined)",
                    ResolutionText = $"{vBounds.Width}x{vBounds.Height}",
                    IsSpan = true
                };

                if (spanItem != null && !string.IsNullOrEmpty(spanItem.WallpaperId))
                {
                    var wp = AuraDatabase.Instance.GetWallpaperById(spanItem.WallpaperId);
                    if (wp != null)
                    {
                        vm.WallpaperId = wp.Id;
                        vm.WallpaperTitle = wp.Title;
                        vm.WallpaperThumbnail = wp.ThumbnailPath;
                    }
                    vm.Volume = spanItem.Volume;
                    vm.IsPlaying = spanItem.State == PlaybackState.Playing;
                    vm.StatusText = $"Status: {spanItem.State} | Scaling: {scaleDesc}";
                }
                else
                {
                    vm.StatusText = "Active Spanned Wallpaper";
                    vm.WallpaperTitle = "Spanned Live Wallpaper";
                    vm.IsPlaying = true;
                }

                list.Add(vm);
            }
            else
            {
                bool isDupMode = assignments.Any(a => a.Mode == AssignmentMode.Duplicate) || WallpaperManager.Instance.IsDuplicateMode;

                for (int i = 0; i < monitors.Count; i++)
                {
                    var mon = monitors[i];
                    var assignment = assignments.FirstOrDefault(a => a.MonitorIndex == i) ?? WallpaperManager.Instance.GetAssignment(i);
                    var vm = new ActiveMonitorCardViewModel
                    {
                        MonitorIndex = i,
                        MonitorName = mon.DeviceName + (mon.IsPrimary ? " (Primary)" : ""),
                        ResolutionText = $"{mon.Bounds.Width}x{mon.Bounds.Height}"
                    };

                    if (assignment != null && !string.IsNullOrEmpty(assignment.WallpaperId))
                    {
                        var wp = AuraDatabase.Instance.GetWallpaperById(assignment.WallpaperId);
                        if (wp != null)
                        {
                            vm.WallpaperId = wp.Id;
                            vm.WallpaperTitle = wp.Title;
                            vm.WallpaperThumbnail = wp.ThumbnailPath;
                        }
                        vm.Volume = assignment.Volume;
                        vm.IsPlaying = assignment.State == PlaybackState.Playing;
                        string modeName = isDupMode ? "Duplicate (All Displays)" : "Per-Monitor";
                        vm.StatusText = $"Status: {assignment.State} | Mode: {modeName}";
                    }
                    else
                    {
                        vm.StatusText = "No active live wallpaper";
                        vm.WallpaperTitle = "Default Windows Wallpaper";
                        vm.IsPlaying = false;
                    }

                    list.Add(vm);
                }
            }

            if (MonitorsItemsControl != null)
            {
                MonitorsItemsControl.ItemsSource = list;
            }
        }

        private string? GetFirstActiveWallpaperId()
        {
            var assignment = AuraDatabase.Instance.GetMonitorAssignments().FirstOrDefault(a => !string.IsNullOrEmpty(a.WallpaperId));
            if (assignment != null) return assignment.WallpaperId;

            var all = AuraDatabase.Instance.GetAllWallpapers();
            return all.Count > 0 ? all[0].Id : null;
        }

        private void DuplicateAll_Click(object sender, RoutedEventArgs e)
        {
            string? wid = GetFirstActiveWallpaperId();
            if (!string.IsNullOrEmpty(wid))
            {
                WallpaperManager.Instance.SetWallpaper(wid, 0, AssignmentMode.Duplicate, WallpaperScaling.Fill);
                RefreshCards();
            }
        }

        private void SpanCropped_Click(object sender, RoutedEventArgs e)
        {
            string? wid = GetFirstActiveWallpaperId();
            if (!string.IsNullOrEmpty(wid))
            {
                WallpaperManager.Instance.SetWallpaper(wid, -1, AssignmentMode.Span, WallpaperScaling.Fill);
                RefreshCards();
            }
        }

        private void SpanFit_Click(object sender, RoutedEventArgs e)
        {
            string? wid = GetFirstActiveWallpaperId();
            if (!string.IsNullOrEmpty(wid))
            {
                WallpaperManager.Instance.SetWallpaper(wid, -1, AssignmentMode.Span, WallpaperScaling.Fit);
                RefreshCards();
            }
        }

        private void CloseAll_Click(object sender, RoutedEventArgs e)
        {
            WallpaperManager.Instance.CloseAllWallpapers();
            RefreshCards();
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ActiveMonitorCardViewModel vm)
            {
                if (vm.IsPlaying)
                {
                    WallpaperManager.Instance.PauseAll();
                }
                else
                {
                    WallpaperManager.Instance.ResumeAll();
                }
                RefreshCards();
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender is Slider slider && slider.DataContext is ActiveMonitorCardViewModel vm)
            {
                int newVol = (int)slider.Value;
                WallpaperManager.Instance.SetVolume(newVol);
            }
        }

        private void ChangeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ActiveMonitorCardViewModel vm)
            {
                var menu = new ContextMenu();

                var miBrowse = new MenuItem { Header = $"📂 Browse Library for {(vm.IsSpan ? "Spanned Desktop" : vm.MonitorName)}..." };
                miBrowse.Click += (s, args) =>
                {
                    WallpaperManager.Instance.RequestViewNavigation("library");
                };
                menu.Items.Add(miBrowse);

                menu.Items.Add(new Separator());

                var allWallpapers = AuraDatabase.Instance.GetAllWallpapers();
                int count = Math.Min(allWallpapers.Count, 6);
                for (int i = 0; i < count; i++)
                {
                    var wp = allWallpapers[i];
                    var miWp = new MenuItem { Header = $"▶ {wp.Title} ({wp.Type})" };
                    miWp.Click += (s, args) =>
                    {
                        if (vm.IsSpan)
                        {
                            WallpaperManager.Instance.SetWallpaper(wp.Id, -1, AssignmentMode.Span, WallpaperScaling.Fill);
                        }
                        else
                        {
                            WallpaperManager.Instance.SetWallpaper(wp.Id, vm.MonitorIndex, AssignmentMode.PerMonitor, WallpaperScaling.Fill);
                        }
                        RefreshCards();
                    };
                    menu.Items.Add(miWp);
                }

                if (allWallpapers.Count > 0)
                {
                    menu.Items.Add(new Separator());
                    var miRand = new MenuItem { Header = "🎲 Apply Random Wallpaper to This Screen" };
                    miRand.Click += (s, args) =>
                    {
                        WallpaperManager.Instance.SetRandomWallpaper(vm.MonitorIndex);
                        RefreshCards();
                    };
                    menu.Items.Add(miRand);
                }

                menu.PlacementTarget = btn;
                menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                menu.IsOpen = true;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ActiveMonitorCardViewModel vm)
            {
                WallpaperManager.Instance.CloseWallpaper(vm.MonitorIndex);
                RefreshCards();
            }
        }
    }
}
