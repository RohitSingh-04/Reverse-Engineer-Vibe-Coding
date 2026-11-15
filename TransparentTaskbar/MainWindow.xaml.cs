using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using TransparentTaskbar.Models;
using TransparentTaskbar.Services;
using WpfMessageBox = System.Windows.MessageBox;

namespace TransparentTaskbar
{
    public partial class MainWindow : Window
    {
        private readonly TaskbarService _taskbarService;
        private AppConfig _config;
        private bool _isInitializing = true;
        private bool _isExplicitClose = false;

        public MainWindow(TaskbarService taskbarService, AppConfig config)
        {
            InitializeComponent();
            _taskbarService = taskbarService ?? throw new ArgumentNullException(nameof(taskbarService));
            _config = config ?? throw new ArgumentNullException(nameof(config));

            SafeLoadIcon();
            LoadConfigToUi();
            _isInitializing = false;
        }

        private void SafeLoadIcon()
        {
            try
            {
                var iconUri = new Uri("pack://application:,,,/Assets/app.ico", UriKind.Absolute);
                Icon = BitmapFrame.Create(iconUri);
            }
            catch
            {
                try
                {
                    string? exe = Environment.ProcessPath;
                    if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
                    {
                        using var sysIcon = System.Drawing.Icon.ExtractAssociatedIcon(exe);
                        if (sysIcon != null)
                        {
                            Icon = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                                sysIcon.Handle,
                                Int32Rect.Empty,
                                BitmapSizeOptions.FromEmptyOptions());
                        }
                    }
                }
                catch
                {
                    // Fail gracefully without crashing
                }
            }
        }

        private void LoadConfigToUi()
        {
            // Sync with current registry state
            _config.StartWithWindows = StartupService.IsAutoStartEnabled();

            StyleComboBox.SelectedIndex = (int)_config.Style;
            OpacitySlider.Value = _config.Opacity;
            OpacityValueText.Text = $"{_config.Opacity}%";
            RemoveTopBorderCheckBox.IsChecked = _config.RemoveTopBorder;
            PreserveSolidIconsCheckBox.IsChecked = _config.PreserveSolidIcons;
            StartWithWindowsCheckBox.IsChecked = _config.StartWithWindows;
        }

        private void OnOpacitySliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing || OpacityValueText == null) return;

            int intVal = (int)Math.Round(e.NewValue);
            OpacityValueText.Text = $"{intVal}%";
            _config.Opacity = intVal;

            // Live preview update
            _taskbarService.UpdateConfig(_config);
        }

        private void OnSettingChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;

            if (StyleComboBox.SelectedIndex >= 0)
            {
                _config.Style = (TaskbarStyleMode)StyleComboBox.SelectedIndex;
                _taskbarService.UpdateConfig(_config);
            }
        }

        private void OnOptionToggleChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            _config.RemoveTopBorder = RemoveTopBorderCheckBox.IsChecked == true;
            _config.PreserveSolidIcons = PreserveSolidIconsCheckBox.IsChecked == true;

            // Apply immediately for live preview
            _taskbarService.UpdateConfig(_config);
        }

        private void OnStartupToggleChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            bool isChecked = StartWithWindowsCheckBox.IsChecked == true;
            _config.StartWithWindows = isChecked;

            var (success, error) = StartupService.SetAutoStart(isChecked);
            if (!success && !string.IsNullOrEmpty(error))
            {
                StatusMessageText.Text = $"Startup registry error: {error}";
                // Revert checkbox if registry failed
                _isInitializing = true;
                StartWithWindowsCheckBox.IsChecked = !isChecked;
                _config.StartWithWindows = !isChecked;
                _isInitializing = false;
            }
            else
            {
                StatusMessageText.Text = isChecked ? "Priority 1 Startup enabled (0s delay)" : "Startup disabled";
            }
        }

        private void OnSaveClicked(object sender, RoutedEventArgs e)
        {
            _config.Opacity = (int)Math.Round(OpacitySlider.Value);
            if (StyleComboBox.SelectedIndex >= 0)
            {
                _config.Style = (TaskbarStyleMode)StyleComboBox.SelectedIndex;
            }
            _config.RemoveTopBorder = RemoveTopBorderCheckBox.IsChecked == true;
            _config.PreserveSolidIcons = PreserveSolidIconsCheckBox.IsChecked == true;
            _config.StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;

            // Save config to JSON
            bool saved = ConfigService.Save(_config);
            if (!saved)
            {
                WpfMessageBox.Show("Could not save settings to config.json. Check file permissions.",
                                   "TransparentTaskbar", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            // Sync Startup registry
            var (success, error) = StartupService.SetAutoStart(_config.StartWithWindows);
            if (!success && !string.IsNullOrEmpty(error))
            {
                WpfMessageBox.Show($"Settings saved, but startup registry failed: {error}",
                                   "TransparentTaskbar", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            // Hide to tray
            Hide();
        }

        private void OnCloseClicked(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_isExplicitClose)
            {
                // Prevent app from quitting when user closes the window - hide to system tray
                e.Cancel = true;
                Hide();
            }
            else
            {
                base.OnClosing(e);
            }
        }

        public void ForceClose()
        {
            _isExplicitClose = true;
            Close();
        }
    }
}
