using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using AuraWallpaper.Core.Engine;
using AuraWallpaper.Core.Models;
using AuraWallpaper.Core.Storage;
using AuraWallpaper.UI.Views;

namespace AuraWallpaper.UI
{
    public partial class MainWindow : Window
    {
        private readonly LibraryView _libraryView = new();
        private readonly ActiveWallpapersView _activeView = new();
        private readonly PlaylistsView _playlistsView = new();
        private readonly ScreensaverView _screensaverView = new();
        private readonly CreatorView _creatorView = new();
        private readonly PerformanceView _performanceView = new();
        private readonly SettingsView _settingsView = new();
        private readonly AboutView _aboutView = new();

        private bool _isPaused;
        private bool _isMuted;

        public MainWindow()
        {
            InitializeComponent();
            MainContentHost.Content = _libraryView;

            try
            {
                string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "app.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri(iconPath, UriKind.Absolute));
                }
            }
            catch { }

            WallpaperManager.Instance.AssignmentsChanged += () => Dispatcher.Invoke(UpdatePlaybackControls);
            ThemeManager.ThemeChanged += (t) => Dispatcher.Invoke(UpdateThemeButton);
            UpdateThemeButton();
        }

        private void UpdateThemeButton()
        {
            if (ThemeToggleIcon == null || ThemeToggleText == null) return;
            if (ThemeManager.CurrentTheme == AppTheme.Light)
            {
                ThemeToggleIcon.Text = "🌙";
                ThemeToggleText.Text = "Dark";
            }
            else
            {
                ThemeToggleIcon.Text = "☀️";
                ThemeToggleText.Text = "Light";
            }
        }

        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.ToggleTheme();
            UpdateThemeButton();
        }

        private void Nav_Checked(object sender, RoutedEventArgs e)
        {
            if (MainContentHost == null) return;

            if (NavLibrary?.IsChecked == true)
            {
                MainContentHost.Content = _libraryView;
                _libraryView.RefreshWallpapers();
            }
            else if (NavActive?.IsChecked == true)
            {
                MainContentHost.Content = _activeView;
                _activeView.RefreshCards();
            }
            else if (NavPlaylists?.IsChecked == true)
            {
                MainContentHost.Content = _playlistsView;
            }
            else if (NavScreensaver?.IsChecked == true)
            {
                MainContentHost.Content = _screensaverView;
            }
            else if (NavCreator?.IsChecked == true)
            {
                MainContentHost.Content = _creatorView;
            }
            else if (NavPerformance?.IsChecked == true)
            {
                MainContentHost.Content = _performanceView;
            }
            else if (NavSettings?.IsChecked == true)
            {
                MainContentHost.Content = _settingsView;
            }
            else if (NavAbout?.IsChecked == true)
            {
                MainContentHost.Content = _aboutView;
            }
        }

        private void UpdatePlaybackControls()
        {
            var assignments = WallpaperManager.Instance.ActiveAssignments.Values.ToList();
            if (assignments.Count > 0)
            {
                _isPaused = assignments.All(a => a.State == PlaybackState.Paused);
                GlobalPlayPauseButton.Content = _isPaused ? "Resume All" : "Pause All";
                _isMuted = assignments.All(a => a.IsMuted);
                GlobalMuteButton.Content = _isMuted ? "Unmute" : "Mute";
            }
        }

        private void GlobalPlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isPaused)
            {
                WallpaperManager.Instance.ResumeAll();
                _isPaused = false;
                GlobalPlayPauseButton.Content = "Pause All";
            }
            else
            {
                WallpaperManager.Instance.PauseAll();
                _isPaused = true;
                GlobalPlayPauseButton.Content = "Resume All";
            }
        }

        private void GlobalMuteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isMuted)
            {
                WallpaperManager.Instance.UnmuteAll();
                _isMuted = false;
                GlobalMuteButton.Content = "Mute";
            }
            else
            {
                WallpaperManager.Instance.MuteAll();
                _isMuted = true;
                GlobalMuteButton.Content = "Unmute";
            }
        }

        private void GlobalVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (GlobalVolumeSlider != null)
            {
                int vol = (int)GlobalVolumeSlider.Value;
                WallpaperManager.Instance.SetVolume(vol);
            }
        }

        private void RandomWallpaperButton_Click(object sender, RoutedEventArgs e)
        {
            WallpaperManager.Instance.SetRandomWallpaper();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            var settings = AuraDatabase.Instance.LoadSettings();
            if (settings.MinimizeToTray)
            {
                e.Cancel = true;
                Hide();
            }
        }
    }
}
