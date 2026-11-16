using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using AuraWallpaper.Core.Models;
using AuraWallpaper.Core.Native;
using AuraWallpaper.Core.Storage;
using Microsoft.Win32;
using UserControl = System.Windows.Controls.UserControl;

namespace AuraWallpaper.UI.Views
{
    public partial class SettingsView : UserControl
    {
        private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "AuraWallpaper";

        public SettingsView()
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

            StartupCheckBox.IsChecked = IsStartupEnabled();
            StartMinimizedCheckBox.IsChecked = settings.StartMinimized;
            MinimizeToTrayCheckBox.IsChecked = settings.MinimizeToTray;
            HwAccelCheckBox.IsChecked = settings.HardwareAcceleration;

            TaskbarThemeCombo.SelectedIndex = (int)settings.TaskbarMode;
            AppThemeCombo.SelectedIndex = settings.Theme == AppTheme.Light ? 1 : 0;
            LibraryPathTextBox.Text = WallpaperPackageService.Instance.LibraryDirectory;
        }

        private void AppThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AppThemeCombo == null) return;
            var chosenTheme = AppThemeCombo.SelectedIndex == 1 ? AppTheme.Light : AppTheme.Dark;
            if (ThemeManager.CurrentTheme != chosenTheme)
            {
                ThemeManager.SetTheme(chosenTheme);
            }
        }

        private void Setting_Changed(object sender, RoutedEventArgs e)
        {
            if (StartupCheckBox == null || StartMinimizedCheckBox == null || MinimizeToTrayCheckBox == null || HwAccelCheckBox == null) return;

            var settings = AuraDatabase.Instance.LoadSettings();

            settings.LaunchOnStartup = StartupCheckBox.IsChecked == true;
            settings.StartMinimized = StartMinimizedCheckBox.IsChecked == true;
            settings.MinimizeToTray = MinimizeToTrayCheckBox.IsChecked == true;
            settings.HardwareAcceleration = HwAccelCheckBox.IsChecked == true;

            SetStartupRegistry(settings.LaunchOnStartup);
            AuraDatabase.Instance.SaveSettings(settings);
        }

        private void TaskbarThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TaskbarThemeCombo == null) return;
            var mode = (TaskbarThemeMode)TaskbarThemeCombo.SelectedIndex;
            var settings = AuraDatabase.Instance.LoadSettings();
            settings.TaskbarMode = mode;
            AuraDatabase.Instance.SaveSettings(settings);

            // Apply immediately to desktop taskbars
            TaskbarStyleHelper.SetTaskbarStyle(mode, settings.TaskbarTintColor);
        }

        private void OpenWindowsColorSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:personalization-colors") { UseShellExecute = true });
            }
            catch { }
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            string dir = WallpaperPackageService.Instance.LibraryDirectory;
            if (Directory.Exists(dir))
            {
                Process.Start("explorer.exe", dir);
            }
        }

        private void OpenLogsFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AuraWallpaper", "logs");
                Directory.CreateDirectory(logDir);
                Process.Start("explorer.exe", logDir);
            }
            catch { }
        }

        private void ChangeFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFolderDialog
            {
                Title = "Select New Aura Live Wallpaper Storage Directory"
            };

            if (dlg.ShowDialog() == true)
            {
                WallpaperPackageService.Instance.SetLibraryDirectory(dlg.FolderName);
                LibraryPathTextBox.Text = dlg.FolderName;

                var settings = AuraDatabase.Instance.LoadSettings();
                settings.CustomLibraryPath = dlg.FolderName;
                AuraDatabase.Instance.SaveSettings(settings);
            }
        }

        private void BackupButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title = "Backup Database",
                FileName = $"aura_backup_{DateTime.Now:yyyyMMdd}.db",
                Filter = "Database Files (*.db)|*.db"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    File.Copy(AuraDatabase.Instance.DatabasePath, dlg.FileName, true);
                    MessageBox.Show("Database backed up successfully!", "Backup", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Backup failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void RestoreButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Restore Database",
                Filter = "Database Files (*.db)|*.db"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    File.Copy(dlg.FileName, AuraDatabase.Instance.DatabasePath, true);
                    MessageBox.Show("Database restored! Please restart Aura Live Wallpaper.", "Restore Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Restore failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ResetSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var res = MessageBox.Show("Reset all settings to default values?", "Confirm Reset", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res == MessageBoxResult.Yes)
            {
                AuraDatabase.Instance.SaveSettings(new AuraSettings());
                LoadSettings();
                TaskbarStyleHelper.SetTaskbarStyle(TaskbarThemeMode.Default);
            }
        }

        private static bool IsStartupEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
                return key?.GetValue(AppName) != null;
            }
            catch
            {
                return false;
            }
        }

        private static void SetStartupRegistry(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
                if (key == null) return;

                if (enable)
                {
                    string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        key.SetValue(AppName, $"\"{exePath}\"");
                    }
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }
            }
            catch
            {
            }
        }
    }
}
