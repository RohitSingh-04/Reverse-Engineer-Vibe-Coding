using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using AuraWallpaper.Core.Models;
using AuraWallpaper.Core.Storage;

using UserControl = System.Windows.Controls.UserControl;

namespace AuraWallpaper.UI.Views
{
    public partial class ScreensaverView : UserControl
    {
        public ScreensaverView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
        }

        private void LoadSettings()
        {
            var settings = AuraDatabase.Instance.LoadSettings();
            var wallpapers = AuraDatabase.Instance.GetAllWallpapers();
            var playlists = AuraDatabase.Instance.GetAllPlaylists();

            SpecificWallpaperCombo.ItemsSource = wallpapers;
            PlaylistCombo.ItemsSource = playlists;

            if (settings.ScreensaverSourceMode == ScreensaverSource.ActiveWallpaper)
            {
                SourceActiveWallpaper.IsChecked = true;
            }
            else if (settings.ScreensaverSourceMode == ScreensaverSource.SpecificWallpaper)
            {
                SourceSpecificWallpaper.IsChecked = true;
                SpecificWallpaperCombo.Visibility = Visibility.Visible;
                if (!string.IsNullOrEmpty(settings.ScreensaverWallpaperId))
                {
                    SpecificWallpaperCombo.SelectedItem = wallpapers.Find(w => w.Id == settings.ScreensaverWallpaperId);
                }
            }
            else
            {
                SourcePlaylist.IsChecked = true;
                PlaylistCombo.Visibility = Visibility.Visible;
                if (!string.IsNullOrEmpty(settings.ScreensaverPlaylistId))
                {
                    PlaylistCombo.SelectedItem = playlists.Find(p => p.Id == settings.ScreensaverPlaylistId);
                }
            }
        }

        private void Source_Checked(object sender, RoutedEventArgs e)
        {
            if (SourceActiveWallpaper == null) return;
            var settings = AuraDatabase.Instance.LoadSettings();

            if (SourceActiveWallpaper?.IsChecked == true)
            {
                settings.ScreensaverSourceMode = ScreensaverSource.ActiveWallpaper;
                if (SpecificWallpaperCombo != null) SpecificWallpaperCombo.Visibility = Visibility.Collapsed;
                if (PlaylistCombo != null) PlaylistCombo.Visibility = Visibility.Collapsed;
            }
            else if (SourceSpecificWallpaper?.IsChecked == true)
            {
                settings.ScreensaverSourceMode = ScreensaverSource.SpecificWallpaper;
                if (SpecificWallpaperCombo != null) SpecificWallpaperCombo.Visibility = Visibility.Visible;
                if (PlaylistCombo != null) PlaylistCombo.Visibility = Visibility.Collapsed;
            }
            else if (SourcePlaylist?.IsChecked == true)
            {
                settings.ScreensaverSourceMode = ScreensaverSource.Playlist;
                if (SpecificWallpaperCombo != null) SpecificWallpaperCombo.Visibility = Visibility.Collapsed;
                if (PlaylistCombo != null) PlaylistCombo.Visibility = Visibility.Visible;
            }

            AuraDatabase.Instance.SaveSettings(settings);
        }

        private void SpecificWallpaperCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SpecificWallpaperCombo.SelectedItem is WallpaperItem item)
            {
                var settings = AuraDatabase.Instance.LoadSettings();
                settings.ScreensaverWallpaperId = item.Id;
                AuraDatabase.Instance.SaveSettings(settings);
            }
        }

        private void PlaylistCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PlaylistCombo.SelectedItem is Playlist p)
            {
                var settings = AuraDatabase.Instance.LoadSettings();
                settings.ScreensaverPlaylistId = p.Id;
                AuraDatabase.Instance.SaveSettings(settings);
            }
        }

        private void TestScreensaver_Click(object sender, RoutedEventArgs e)
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string scrPath = Path.Combine(appDir, "AuraScreensaver.exe");
            if (!File.Exists(scrPath))
            {
                string devPath = Path.GetFullPath(Path.Combine(appDir, @"..\..\..\..\AuraWallpaper.Screensaver\bin\Debug\net8.0-windows\AuraScreensaver.exe"));
                if (File.Exists(devPath)) scrPath = devPath;
            }

            if (File.Exists(scrPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = scrPath,
                    Arguments = "/s",
                    UseShellExecute = true
                });
            }
            else
            {
                MessageBox.Show("Screensaver binary (AuraScreensaver.exe) not found.", "Screensaver", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OpenWindowsSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "control.exe",
                    Arguments = "desk.cpl,,@screensaver",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open Windows Screen Saver settings: {ex.Message}");
            }
        }

        private void InstallScr_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string exePath = Path.Combine(appDir, "AuraScreensaver.exe");
                string scrPath = Path.Combine(appDir, "AuraScreensaver.scr");

                if (File.Exists(exePath))
                {
                    File.Copy(exePath, scrPath, true);
                    MessageBox.Show($"Created screensaver file:\n{scrPath}\n\nRight-click 'AuraScreensaver.scr' and select 'Install' to set it as your default Windows screensaver.", "Screensaver Installed", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("AuraScreensaver.exe was not found in application directory.", "Notice", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to generate .scr file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
