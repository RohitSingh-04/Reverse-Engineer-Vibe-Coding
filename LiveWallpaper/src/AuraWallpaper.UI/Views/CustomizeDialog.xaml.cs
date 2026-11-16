using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AuraWallpaper.Core.Models;
using AuraWallpaper.Core.Storage;

namespace AuraWallpaper.UI.Views
{
    public partial class CustomizeDialog : Window
    {
        private readonly WallpaperItem _wallpaper;
        private readonly Dictionary<string, CustomPropertyItem> _properties = new();
        private readonly Dictionary<string, object> _currentValues = new();

        public CustomizeDialog(WallpaperItem wallpaper)
        {
            InitializeComponent();
            _wallpaper = wallpaper;
            WallpaperTitleText.Text = $"Customize: {wallpaper.Title}";
            LoadProperties();
        }

        private void LoadProperties()
        {
            if (string.IsNullOrWhiteSpace(_wallpaper.CustomPropertiesJson))
            {
                var emptyMsg = new TextBlock
                {
                    Text = "This wallpaper does not declare any customizable properties.",
                    Foreground = Brushes.Gray,
                    Margin = new Thickness(0, 20, 0, 0),
                    FontStyle = FontStyles.Italic
                };
                PropertiesContainer.Children.Add(emptyMsg);
                return;
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<string, CustomPropertyItem>>(_wallpaper.CustomPropertiesJson);
                if (parsed != null)
                {
                    foreach (var kvp in parsed)
                    {
                        _properties[kvp.Key] = kvp.Value;
                        BuildControl(kvp.Key, kvp.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                PropertiesContainer.Children.Add(new TextBlock
                {
                    Text = $"Error parsing custom properties: {ex.Message}",
                    Foreground = Brushes.Red
                });
            }
        }

        private void BuildControl(string name, CustomPropertyItem prop)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
            string labelText = !string.IsNullOrEmpty(prop.Text) ? prop.Text : name;

            string type = prop.Type?.ToLowerInvariant() ?? "slider";

            switch (type)
            {
                case "slider":
                    var headerGrid = new Grid();
                    headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var lbl = new TextBlock { Text = labelText, FontWeight = FontWeights.Medium, Foreground = Brushes.LightGray };
                    var valLbl = new TextBlock { Text = prop.NumericValue.ToString("0.##"), Foreground = Brushes.SkyBlue };
                    Grid.SetColumn(lbl, 0);
                    Grid.SetColumn(valLbl, 1);
                    headerGrid.Children.Add(lbl);
                    headerGrid.Children.Add(valLbl);
                    panel.Children.Add(headerGrid);

                    var slider = new Slider
                    {
                        Minimum = prop.Min,
                        Maximum = prop.Max,
                        Value = prop.NumericValue,
                        TickFrequency = prop.Step > 0 ? prop.Step : 1,
                        IsSnapToTickEnabled = true,
                        Margin = new Thickness(0, 6, 0, 0)
                    };
                    slider.ValueChanged += (s, e) =>
                    {
                        valLbl.Text = e.NewValue.ToString("0.##");
                        _currentValues[name] = e.NewValue;
                    };
                    panel.Children.Add(slider);
                    break;

                case "checkbox":
                    var chk = new CheckBox
                    {
                        Content = labelText,
                        IsChecked = prop.BooleanValue,
                        Foreground = Brushes.LightGray,
                        Margin = new Thickness(0, 4, 0, 0)
                    };
                    chk.Checked += (s, e) => _currentValues[name] = true;
                    chk.Unchecked += (s, e) => _currentValues[name] = false;
                    panel.Children.Add(chk);
                    break;

                case "select":
                case "dropdown":
                    panel.Children.Add(new TextBlock { Text = labelText, FontWeight = FontWeights.Medium, Foreground = Brushes.LightGray });
                    var combo = new ComboBox
                    {
                        Margin = new Thickness(0, 6, 0, 0),
                        Height = 32,
                        ItemsSource = prop.Options ?? new List<string>()
                    };
                    if (prop.Options != null && prop.Options.Count > 0)
                    {
                        int idx = prop.Options.IndexOf(prop.StringValue);
                        combo.SelectedIndex = idx >= 0 ? idx : 0;
                    }
                    combo.SelectionChanged += (s, e) =>
                    {
                        if (combo.SelectedItem != null)
                        {
                            _currentValues[name] = combo.SelectedItem.ToString() ?? "";
                        }
                    };
                    panel.Children.Add(combo);
                    break;

                case "color":
                    panel.Children.Add(new TextBlock { Text = labelText, FontWeight = FontWeights.Medium, Foreground = Brushes.LightGray });
                    var colorBox = new TextBox
                    {
                        Text = prop.StringValue,
                        Margin = new Thickness(0, 6, 0, 0),
                        Height = 32
                    };
                    colorBox.TextChanged += (s, e) => _currentValues[name] = colorBox.Text;
                    panel.Children.Add(colorBox);
                    break;

                case "textbox":
                default:
                    panel.Children.Add(new TextBlock { Text = labelText, FontWeight = FontWeights.Medium, Foreground = Brushes.LightGray });
                    var tb = new TextBox
                    {
                        Text = prop.StringValue,
                        Margin = new Thickness(0, 6, 0, 0),
                        Height = 32
                    };
                    tb.TextChanged += (s, e) => _currentValues[name] = tb.Text;
                    panel.Children.Add(tb);
                    break;
            }

            PropertiesContainer.Children.Add(panel);
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            _currentValues.Clear();
            PropertiesContainer.Children.Clear();
            LoadProperties();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _wallpaper.PropertyValuesJson = JsonSerializer.Serialize(_currentValues);
            AuraDatabase.Instance.SaveWallpaper(_wallpaper);
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
