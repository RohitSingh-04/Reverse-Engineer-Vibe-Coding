using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace AuraWallpaper.UI.Views
{
    public partial class AboutView : UserControl
    {
        public AboutView()
        {
            InitializeComponent();
        }

        private void GitHubButton_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://github.com/RohitSingh-04");
        }

        private void LinkedInButton_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://linkedin.com/in/rohiyaa");
        }

        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to open link: {ex.Message}", "Link Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
