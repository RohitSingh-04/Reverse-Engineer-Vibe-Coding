using System.Windows;
using System.Windows.Controls;
using AuraWallpaper.Core.Models;
using AuraWallpaper.Core.Storage;

namespace AuraWallpaper.Screensaver
{
    public partial class ScreensaverConfigWindow : Window
    {
        public ScreensaverConfigWindow()
        {
            InitializeComponent();
            LoadData();
        }

        private void LoadData()
        {
            var settings = AuraDatabase.Instance.LoadSettings();
            var wallpapers = AuraDatabase.Instance.GetAllWallpapers();
            WallpaperCombo.ItemsSource = wallpapers;

            SourceCombo.SelectedIndex = settings.ScreensaverSourceMode == ScreensaverSource.SpecificWallpaper ? 1 : 0;
            if (!string.IsNullOrEmpty(settings.ScreensaverWallpaperId))
            {
                var matched = wallpapers.Find(w => w.Id == settings.ScreensaverWallpaperId);
                if (matched != null)
                {
                    WallpaperCombo.SelectedItem = matched;
                }
            }
        }

        private void SourceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (WallpaperSelectPanel != null)
            {
                WallpaperSelectPanel.Visibility = SourceCombo.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var settings = AuraDatabase.Instance.LoadSettings();
            settings.ScreensaverSourceMode = SourceCombo.SelectedIndex == 1 ? ScreensaverSource.SpecificWallpaper : ScreensaverSource.ActiveWallpaper;

            if (WallpaperCombo.SelectedItem is WallpaperItem item)
            {
                settings.ScreensaverWallpaperId = item.Id;
            }

            AuraDatabase.Instance.SaveSettings(settings);
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
