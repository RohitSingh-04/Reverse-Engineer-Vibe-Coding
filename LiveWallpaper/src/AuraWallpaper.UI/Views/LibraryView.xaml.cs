using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AuraWallpaper.Core.Engine;
using AuraWallpaper.Core.Models;
using AuraWallpaper.Core.Native;
using AuraWallpaper.Core.Storage;
using Microsoft.Win32;
using UserControl = System.Windows.Controls.UserControl;

namespace AuraWallpaper.UI.Views
{
    public partial class LibraryView : UserControl
    {
        private List<WallpaperItem> _allWallpapers = new();
        private bool _isLoaded;

        public LibraryView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = true;
            RefreshWallpapers();
        }

        public void RefreshWallpapers()
        {
            _allWallpapers = AuraDatabase.Instance.GetAllWallpapers() ?? new List<WallpaperItem>();
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (!_isLoaded || _allWallpapers == null || WallpapersItemsControl == null) return;

            string query = SearchBox?.Text?.Trim() ?? "";
            var filtered = _allWallpapers.AsEnumerable();

            if (!string.IsNullOrEmpty(query))
            {
                filtered = filtered.Where(w =>
                    (w.Title ?? "").Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    (w.Author ?? "").Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    (w.Tags ?? "").Contains(query, StringComparison.OrdinalIgnoreCase));
            }

            if (FilterVideo?.IsChecked == true)
                filtered = filtered.Where(w => w.Type == WallpaperType.Video);
            else if (FilterWeb?.IsChecked == true)
                filtered = filtered.Where(w => w.Type == WallpaperType.Web);
            else if (FilterGif?.IsChecked == true)
                filtered = filtered.Where(w => w.Type == WallpaperType.Gif);
            else if (FilterPictures?.IsChecked == true)
                filtered = filtered.Where(w => w.Type == WallpaperType.Picture);
            else if (FilterApps?.IsChecked == true)
                filtered = filtered.Where(w => w.Type == WallpaperType.App);
            else if (FilterFavorites?.IsChecked == true)
                filtered = filtered.Where(w => w.IsFavorite);

            WallpapersItemsControl.ItemsSource = filtered.ToList();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isLoaded) return;
            ApplyFilter();
        }

        private void Filter_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            ApplyFilter();
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Import Wallpaper",
                Filter = "All Supported Files|*.mp4;*.webm;*.mkv;*.mov;*.avi;*.wmv;*.html;*.htm;*.gif;*.png;*.jpg;*.jpeg;*.webp;*.zip;*.livelypkg;*.aurapkg;*.exe|" +
                         "Video Files|*.mp4;*.webm;*.mkv;*.mov;*.avi;*.wmv|" +
                         "Web / HTML Files|*.html;*.htm|" +
                         "Wallpaper Packages|*.zip;*.livelypkg;*.aurapkg|" +
                         "Image & GIF Files|*.gif;*.png;*.jpg;*.jpeg;*.webp|" +
                         "Applications|*.exe"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    WallpaperPackageService.Instance.ImportFile(dlg.FileName);
                    RefreshWallpapers();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to import wallpaper: {ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LibraryView_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }

        private void LibraryView_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    int importedCount = 0;
                    foreach (var f in files)
                    {
                        try
                        {
                            WallpaperPackageService.Instance.ImportFile(f);
                            importedCount++;
                        }
                        catch { }
                    }
                    if (importedCount > 0)
                    {
                        RefreshWallpapers();
                    }
                }
            }
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is WallpaperItem item)
            {
                ApplyWallpaperWithSelection(item);
            }
        }

        private void ApplyWallpaperWithSelection(WallpaperItem item)
        {
            var monitors = DisplayManager.Instance.GetMonitors();
            if (monitors.Count > 1)
            {
                WallpaperManager.Instance.SetWallpaper(item.Id, 0, AssignmentMode.Duplicate, WallpaperScaling.Fill);
            }
            else
            {
                WallpaperManager.Instance.SetWallpaper(item.Id, 0, AssignmentMode.PerMonitor, WallpaperScaling.Fill);
            }
        }

        private void ApplyMenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is WallpaperItem item)
            {
                var menu = new ContextMenu();

                var miDup = new MenuItem { Header = "🔁 Duplicate on All Displays" };
                miDup.Click += (s, args) => WallpaperManager.Instance.SetWallpaper(item.Id, 0, AssignmentMode.Duplicate, WallpaperScaling.Fill);
                menu.Items.Add(miDup);

                var miSpanCrop = new MenuItem { Header = "🖥️ Extend / Span Cropped Across Displays" };
                miSpanCrop.Click += (s, args) => WallpaperManager.Instance.SetWallpaper(item.Id, -1, AssignmentMode.Span, WallpaperScaling.Fill);
                menu.Items.Add(miSpanCrop);

                var miSpanFit = new MenuItem { Header = "✨ Extend / Span Fit (Ambient Glow)" };
                miSpanFit.Click += (s, args) => WallpaperManager.Instance.SetWallpaper(item.Id, -1, AssignmentMode.Span, WallpaperScaling.Fit);
                menu.Items.Add(miSpanFit);

                var monitors = DisplayManager.Instance.GetMonitors();
                if (monitors.Count > 0)
                {
                    menu.Items.Add(new Separator());
                    for (int i = 0; i < monitors.Count; i++)
                    {
                        int monIdx = i;
                        var m = monitors[i];
                        string label = $"Apply to Monitor {i + 1} ({m.DeviceName}) - {m.Bounds.Width}x{m.Bounds.Height}{(m.IsPrimary ? " [Primary]" : "")}";
                        var miMon = new MenuItem { Header = label };
                        miMon.Click += (s, args) => WallpaperManager.Instance.SetWallpaper(item.Id, monIdx, AssignmentMode.PerMonitor, WallpaperScaling.Fill);
                        menu.Items.Add(miMon);
                    }
                }

                menu.PlacementTarget = btn;
                menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                menu.IsOpen = true;
            }
        }

        private void CustomizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is WallpaperItem item)
            {
                var dlg = new CustomizeDialog(item) { Owner = Window.GetWindow(this) };
                dlg.ShowDialog();
            }
        }

        private void MenuApply_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.DataContext is WallpaperItem item)
            {
                ApplyWallpaperWithSelection(item);
            }
        }

        private void MenuSpanCropped_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.DataContext is WallpaperItem item)
            {
                WallpaperManager.Instance.SetWallpaper(item.Id, -1, AssignmentMode.Span, WallpaperScaling.Fill);
            }
        }

        private void MenuSpanFit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.DataContext is WallpaperItem item)
            {
                WallpaperManager.Instance.SetWallpaper(item.Id, -1, AssignmentMode.Span, WallpaperScaling.Fit);
            }
        }

        private void MenuDuplicate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.DataContext is WallpaperItem item)
            {
                WallpaperManager.Instance.SetWallpaper(item.Id, 0, AssignmentMode.Duplicate, WallpaperScaling.Fill);
            }
        }

        private void MenuCustomize_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.DataContext is WallpaperItem item)
            {
                var dlg = new CustomizeDialog(item) { Owner = Window.GetWindow(this) };
                dlg.ShowDialog();
            }
        }

        private void MenuFavorite_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.DataContext is WallpaperItem item)
            {
                item.IsFavorite = !item.IsFavorite;
                AuraDatabase.Instance.SaveWallpaper(item);
                RefreshWallpapers();
            }
        }

        private void MenuExport_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.DataContext is WallpaperItem item)
            {
                var dlg = new SaveFileDialog
                {
                    Title = "Export Wallpaper Package",
                    FileName = $"{item.Title}.aurapkg",
                    Filter = "Aura Wallpaper Package (*.aurapkg)|*.aurapkg|ZIP Archive (*.zip)|*.zip"
                };

                if (dlg.ShowDialog() == true)
                {
                    try
                    {
                        WallpaperPackageService.Instance.ExportPackage(item.Id, dlg.FileName);
                        MessageBox.Show("Wallpaper exported successfully!", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Export failed: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void MenuDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.DataContext is WallpaperItem item)
            {
                var result = MessageBox.Show($"Are you sure you want to delete '{item.Title}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    AuraDatabase.Instance.DeleteWallpaper(item.Id);
                    RefreshWallpapers();
                }
            }
        }
    }
}
