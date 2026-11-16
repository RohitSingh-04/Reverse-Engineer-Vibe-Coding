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
            var list = new List<ActiveMonitorCardViewModel>();

            for (int i = 0; i < monitors.Count; i++)
            {
                var mon = monitors[i];
                var assignment = assignments.FirstOrDefault(a => a.MonitorIndex == i);
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
                    vm.StatusText = $"Status: {assignment.State} | Mode: {assignment.Mode}";
                }
                else
                {
                    vm.StatusText = "No active live wallpaper";
                    vm.WallpaperTitle = "Default Windows Wallpaper";
                    vm.IsPlaying = false;
                }

                list.Add(vm);
            }

            if (MonitorsItemsControl != null)
            {
                MonitorsItemsControl.ItemsSource = list;
            }
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
                // Navigate to library or pick random
                WallpaperManager.Instance.SetRandomWallpaper();
                RefreshCards();
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
