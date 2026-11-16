using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using AuraWallpaper.Core.Models;
using AuraWallpaper.Core.Storage;
using Microsoft.Win32;

using UserControl = System.Windows.Controls.UserControl;

namespace AuraWallpaper.UI.Views
{
    public partial class CreatorView : UserControl
    {
        public CreatorView()
        {
            InitializeComponent();
        }

        private void GenerateProject_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string title = ProjectTitleTextBox.Text.Trim();
                if (string.IsNullOrEmpty(title)) title = "My Interactive Wallpaper";
                string author = ProjectAuthorTextBox.Text.Trim();
                if (string.IsNullOrEmpty(author)) author = "Developer";
                string desc = ProjectDescTextBox.Text.Trim();

                int templateIdx = TemplateCombo.SelectedIndex;

                string id = Guid.NewGuid().ToString("N");
                string projectDir = Path.Combine(WallpaperPackageService.Instance.LibraryDirectory, id);
                Directory.CreateDirectory(projectDir);

                string html = GetTemplateHtml(templateIdx, title);
                string propsJson = GetTemplateProperties(templateIdx);

                File.WriteAllText(Path.Combine(projectDir, "index.html"), html);
                File.WriteAllText(Path.Combine(projectDir, "LivelyProperties.json"), propsJson);

                var manifest = new
                {
                    Title = title,
                    Author = author,
                    Description = desc,
                    Version = "1.0.0",
                    Type = 1, // Web
                    FileName = "index.html",
                    Tags = "interactive,web,animated,canvas"
                };
                File.WriteAllText(Path.Combine(projectDir, "AuraInfo.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

                var item = new WallpaperItem
                {
                    Id = id,
                    Title = title,
                    Author = author,
                    Description = desc,
                    Type = WallpaperType.Web,
                    FilePath = Path.Combine(projectDir, "index.html"),
                    CustomPropertiesJson = propsJson,
                    Category = "Interactive",
                    DateAdded = DateTime.UtcNow,
                    LastUsed = DateTime.UtcNow
                };

                AuraDatabase.Instance.SaveWallpaper(item);

                MessageBox.Show($"Wallpaper project '{title}' generated successfully and added to your library!\nYou can apply it immediately from the Library page.", "Project Created", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to generate project: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select Entry File (index.html or media) in Wallpaper Folder",
                Filter = "All Supported Files|*.html;*.htm;*.mp4;*.webm;*.gif"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    WallpaperPackageService.Instance.ImportFile(dlg.FileName);
                    MessageBox.Show("Folder project imported into Library successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Import failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ImportUrl_Click(object sender, RoutedEventArgs e)
        {
            string url = WebUrlTextBox.Text.Trim();
            if (string.IsNullOrEmpty(url) || url == "https://" || url == "http://")
            {
                MessageBox.Show("Please enter a valid website URL.", "Invalid URL", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string title = WebUrlTitleTextBox.Text.Trim();
                var item = WallpaperPackageService.Instance.ImportUrl(url, string.IsNullOrEmpty(title) ? null : title);
                MessageBox.Show($"Web wallpaper '{item.Title}' imported successfully into your library!", "Imported", MessageBoxButton.OK, MessageBoxImage.Information);
                WebUrlTextBox.Text = "https://";
                WebUrlTitleTextBox.Text = "";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to import URL: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string GetTemplateHtml(int templateIdx, string title)
        {
            if (templateIdx == 0)
            {
                // Audio visualizer waveform
                return @"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'/>
    <style>
        body, html { margin:0; padding:0; overflow:hidden; background:#0a0a0f; width:100%; height:100%; }
        canvas { display:block; width:100%; height:100%; }
        #title { position:absolute; bottom:30px; left:40px; color:#ffffff; font-family:'Segoe UI',sans-serif; opacity:0.8; }
        #title h1 { margin:0; font-size:28px; font-weight:300; letter-spacing:2px; }
        #title p { margin:4px 0 0; font-size:12px; color:#00ffff; }
    </style>
</head>
<body>
    <canvas id='visualizer'></canvas>
    <div id='title'>
        <h1>" + title + @"</h1>
        <p>Aura Audio Visualizer</p>
    </div>
    <script>
        const canvas = document.getElementById('visualizer');
        const ctx = canvas.getContext('2d');
        let width = canvas.width = window.innerWidth;
        let height = canvas.height = window.innerHeight;

        window.addEventListener('resize', () => {
            width = canvas.width = window.innerWidth;
            height = canvas.height = window.innerHeight;
        });

        let spectrum = new Array(64).fill(0.05);
        let barColor = '#00e5ff';
        let barCount = 48;

        window.auraAudioListener = window.livelyAudioListener = function(audioData) {
            if (audioData && audioData.length) {
                spectrum = audioData;
            }
        };

        window.auraPropertyListener = window.livelyPropertyListener = function(name, val) {
            if (name === 'barColor') barColor = val;
            if (name === 'barCount') barCount = parseInt(val) || 48;
        };

        function draw() {
            ctx.fillStyle = 'rgba(10, 10, 15, 0.2)';
            ctx.fillRect(0, 0, width, height);

            const gap = 6;
            const barWidth = (width - (barCount * gap)) / barCount;
            const centerY = height * 0.6;

            for (let i = 0; i < barCount; i++) {
                const sIndex = Math.floor((i / barCount) * spectrum.length);
                const magnitude = spectrum[sIndex] || 0.05;
                const barHeight = Math.max(6, magnitude * (height * 0.45));

                const grad = ctx.createLinearGradient(0, centerY - barHeight, 0, centerY + barHeight);
                grad.addColorStop(0, '#ff007f');
                grad.addColorStop(0.5, barColor);
                grad.addColorStop(1, '#7928ca');

                ctx.fillStyle = grad;
                ctx.beginPath();
                ctx.roundRect(i * (barWidth + gap), centerY - (barHeight / 2), barWidth, barHeight, 4);
                ctx.fill();
            }

            requestAnimationFrame(draw);
        }
        draw();
    </script>
</body>
</html>";
            }
            else if (templateIdx == 1)
            {
                // Cyberpunk Matrix rain
                return @"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'/>
    <style>
        body, html { margin:0; padding:0; overflow:hidden; background:#000; width:100%; height:100%; }
        canvas { display:block; }
    </style>
</head>
<body>
    <canvas id='matrix'></canvas>
    <script>
        const canvas = document.getElementById('matrix');
        const ctx = canvas.getContext('2d');
        let width = canvas.width = window.innerWidth;
        let height = canvas.height = window.innerHeight;

        window.addEventListener('resize', () => {
            width = canvas.width = window.innerWidth;
            height = canvas.height = window.innerHeight;
        });

        const chars = '0123456789ABCDEF!@#$%&*+-=~';
        const fontSize = 16;
        let columns = Math.floor(width / fontSize);
        let drops = [];
        for (let i = 0; i < columns; i++) drops[i] = 1;

        let rainColor = '#00ff66';
        let speed = 33;

        window.auraPropertyListener = window.livelyPropertyListener = function(name, val) {
            if (name === 'rainColor') rainColor = val;
            if (name === 'speed') speed = parseInt(val) || 33;
        };

        function draw() {
            ctx.fillStyle = 'rgba(0, 0, 0, 0.05)';
            ctx.fillRect(0, 0, width, height);

            ctx.fillStyle = rainColor;
            ctx.font = fontSize + 'px monospace';

            for (let i = 0; i < drops.length; i++) {
                const text = chars[Math.floor(Math.random() * chars.length)];
                ctx.fillText(text, i * fontSize, drops[i] * fontSize);

                if (drops[i] * fontSize > height && Math.random() > 0.975) {
                    drops[i] = 0;
                }
                drops[i]++;
            }
        }
        setInterval(draw, 33);
    </script>
</body>
</html>";
            }
            else
            {
                // Digital clock
                return @"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'/>
    <style>
        body, html { margin:0; padding:0; overflow:hidden; background:linear-gradient(135deg, #0f172a, #1e1b4b); width:100%; height:100%; display:flex; align-items:center; justify-content:center; font-family:'Segoe UI',sans-serif; color:#ffffff; }
        .clock-card { text-align:center; background:rgba(255,255,255,0.04); padding:40px 60px; border-radius:24px; backdrop-filter:blur(20px); border:1px solid rgba(255,255,255,0.08); box-shadow:0 20px 50px rgba(0,0,0,0.5); }
        #time { font-size:72px; font-weight:200; letter-spacing:4px; margin:0; }
        #date { font-size:18px; color:#94a3b8; font-weight:400; margin-top:10px; letter-spacing:1px; }
    </style>
</head>
<body>
    <div class='clock-card'>
        <div id='time'>12:00:00</div>
        <div id='date'>Monday, January 1</div>
    </div>
    <script>
        function updateTime() {
            const now = new Date();
            document.getElementById('time').textContent = now.toLocaleTimeString();
            document.getElementById('date').textContent = now.toLocaleDateString(undefined, { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' });
        }
        setInterval(updateTime, 1000);
        updateTime();
    </script>
</body>
</html>";
            }
        }

        private static string GetTemplateProperties(int templateIdx)
        {
            if (templateIdx == 0)
            {
                return @"{
  ""barColor"": {
    ""type"": ""color"",
    ""value"": ""#00e5ff"",
    ""text"": ""Spectrum Bar Accent Color""
  },
  ""barCount"": {
    ""type"": ""slider"",
    ""value"": 48,
    ""min"": 16,
    ""max"": 64,
    ""step"": 4,
    ""text"": ""Number of Audio Bars""
  }
}";
            }
            else if (templateIdx == 1)
            {
                return @"{
  ""rainColor"": {
    ""type"": ""color"",
    ""value"": ""#00ff66"",
    ""text"": ""Matrix Rain Color""
  },
  ""speed"": {
    ""type"": ""slider"",
    ""value"": 33,
    ""min"": 10,
    ""max"": 100,
    ""step"": 5,
    ""text"": ""Stream Speed""
  }
}";
            }
            return @"{}";
        }
    }
}
