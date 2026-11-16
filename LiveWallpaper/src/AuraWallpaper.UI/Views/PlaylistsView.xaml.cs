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
    public class PlaylistItemViewModel
    {
        public string ItemId { get; set; } = string.Empty;
        public string WallpaperId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
    }

    public partial class PlaylistsView : UserControl
    {
        private List<Playlist> _playlists = new();
        private Playlist? _selectedPlaylist;

        public PlaylistsView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LoadPlaylists();
        }

        private void LoadPlaylists()
        {
            if (PlaylistsListBox == null) return;
            _playlists = AuraDatabase.Instance.GetAllPlaylists();
            PlaylistsListBox.ItemsSource = null;
            PlaylistsListBox.ItemsSource = _playlists;

            if (_playlists.Count > 0 && _selectedPlaylist == null)
            {
                PlaylistsListBox.SelectedIndex = 0;
            }
        }

        private void PlaylistsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PlaylistsListBox.SelectedItem is Playlist p)
            {
                _selectedPlaylist = p;
                DisplayPlaylistDetails(p);
            }
            else
            {
                PlaylistDetailsGrid.Visibility = Visibility.Collapsed;
            }
        }

        private void DisplayPlaylistDetails(Playlist p)
        {
            PlaylistDetailsGrid.Visibility = Visibility.Visible;
            PlaylistNameTextBox.Text = p.Name;
            ShuffleCheckBox.IsChecked = p.IsShuffle;
            RepeatCheckBox.IsChecked = p.IsRepeat;
            ToggleActiveButton.Content = p.IsActive ? "Stop Playlist" : "Start Playlist";

            // Interval
            int interval = p.RotationIntervalSeconds;
            if (interval <= 60) IntervalCombo.SelectedIndex = 0;
            else if (interval <= 300) IntervalCombo.SelectedIndex = 1;
            else if (interval <= 900) IntervalCombo.SelectedIndex = 2;
            else if (interval <= 1800) IntervalCombo.SelectedIndex = 3;
            else IntervalCombo.SelectedIndex = 4;

            RefreshPlaylistItemsList();
        }

        private void RefreshPlaylistItemsList()
        {
            if (_selectedPlaylist == null || PlaylistItemsListBox == null) return;

            var items = new List<PlaylistItemViewModel>();
            var allWallpapers = AuraDatabase.Instance.GetAllWallpapers();

            foreach (var item in _selectedPlaylist.Items)
            {
                var wp = allWallpapers.FirstOrDefault(w => w.Id == item.WallpaperId);
                items.Add(new PlaylistItemViewModel
                {
                    ItemId = item.Id,
                    WallpaperId = item.WallpaperId,
                    Title = wp != null ? wp.Title : "Unknown Wallpaper"
                });
            }

            PlaylistItemsListBox.ItemsSource = items;
        }

        private void NewPlaylistButton_Click(object sender, RoutedEventArgs e)
        {
            var p = new Playlist
            {
                Name = $"Playlist {_playlists.Count + 1}",
                RotationIntervalSeconds = 300,
                IsRepeat = true
            };
            AuraDatabase.Instance.SavePlaylist(p);
            LoadPlaylists();
            PlaylistsListBox.SelectedItem = p;
        }

        private void ToggleActiveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPlaylist == null) return;

            if (_selectedPlaylist.IsActive)
            {
                PlaylistCoordinator.Instance.StopPlaylist(_selectedPlaylist.Id);
                _selectedPlaylist.IsActive = false;
                AuraDatabase.Instance.SavePlaylist(_selectedPlaylist);
                ToggleActiveButton.Content = "Start Playlist";
            }
            else
            {
                PlaylistCoordinator.Instance.StartPlaylist(_selectedPlaylist);
                ToggleActiveButton.Content = "Stop Playlist";
            }
        }

        private void AddWallpaperToPlaylist_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPlaylist == null) return;

            var wallpapers = AuraDatabase.Instance.GetAllWallpapers();
            if (wallpapers.Count == 0)
            {
                MessageBox.Show("No wallpapers found in library. Import some wallpapers first!", "Library Empty", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Quick picker dialog or pick next available
            var firstAvailable = wallpapers.FirstOrDefault(w => !_selectedPlaylist.Items.Any(i => i.WallpaperId == w.Id)) ?? wallpapers.First();
            _selectedPlaylist.Items.Add(new PlaylistItem
            {
                PlaylistId = _selectedPlaylist.Id,
                WallpaperId = firstAvailable.Id,
                OrderIndex = _selectedPlaylist.Items.Count
            });

            AuraDatabase.Instance.SavePlaylist(_selectedPlaylist);
            RefreshPlaylistItemsList();
        }

        private void RemovePlaylistItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is PlaylistItemViewModel vm && _selectedPlaylist != null)
            {
                _selectedPlaylist.Items.RemoveAll(i => i.Id == vm.ItemId);
                AuraDatabase.Instance.SavePlaylist(_selectedPlaylist);
                RefreshPlaylistItemsList();
            }
        }

        private void IntervalCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_selectedPlaylist == null || IntervalCombo.SelectedItem is not ComboBoxItem item) return;
            if (int.TryParse(item.Tag?.ToString(), out int seconds))
            {
                _selectedPlaylist.RotationIntervalSeconds = seconds;
                AuraDatabase.Instance.SavePlaylist(_selectedPlaylist);
            }
        }

        private void ShuffleCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPlaylist == null) return;
            _selectedPlaylist.IsShuffle = ShuffleCheckBox.IsChecked == true;
            AuraDatabase.Instance.SavePlaylist(_selectedPlaylist);
        }

        private void RepeatCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPlaylist == null) return;
            _selectedPlaylist.IsRepeat = RepeatCheckBox.IsChecked == true;
            AuraDatabase.Instance.SavePlaylist(_selectedPlaylist);
        }

        private void DeletePlaylistButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPlaylist == null) return;
            var res = MessageBox.Show($"Delete playlist '{_selectedPlaylist.Name}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                PlaylistCoordinator.Instance.StopPlaylist(_selectedPlaylist.Id);
                AuraDatabase.Instance.DeletePlaylist(_selectedPlaylist.Id);
                _selectedPlaylist = null;
                LoadPlaylists();
            }
        }
    }
}
