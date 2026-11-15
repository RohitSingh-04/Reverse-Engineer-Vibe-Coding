using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using WpfApplication = System.Windows.Application;

namespace TransparentTaskbar.Services
{
    public class TrayIconService : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly ContextMenuStrip _contextMenu;
        private readonly Action _openSettingsAction;
        private readonly Action _exitAction;
        private bool _isDisposed;

        public TrayIconService(Action openSettingsAction, Action exitAction)
        {
            _openSettingsAction = openSettingsAction ?? throw new ArgumentNullException(nameof(openSettingsAction));
            _exitAction = exitAction ?? throw new ArgumentNullException(nameof(exitAction));

            _contextMenu = new ContextMenuStrip
            {
                RenderMode = ToolStripRenderMode.System,
                ShowImageMargin = false
            };

            var titleItem = new ToolStripMenuItem("TransparentTaskbar")
            {
                Enabled = false,
                Font = new Font(Control.DefaultFont, FontStyle.Bold)
            };
            _contextMenu.Items.Add(titleItem);
            _contextMenu.Items.Add(new ToolStripSeparator());

            var settingsItem = new ToolStripMenuItem("&Settings...", null, (s, e) =>
            {
                WpfApplication.Current?.Dispatcher.Invoke(_openSettingsAction);
            });
            _contextMenu.Items.Add(settingsItem);

            _contextMenu.Items.Add(new ToolStripSeparator());

            var exitItem = new ToolStripMenuItem("E&xit", null, (s, e) =>
            {
                WpfApplication.Current?.Dispatcher.Invoke(_exitAction);
            });
            _contextMenu.Items.Add(exitItem);

            _notifyIcon = new NotifyIcon
            {
                Text = "TransparentTaskbar - Active",
                ContextMenuStrip = _contextMenu,
                Icon = LoadOrCreateTrayIcon(),
                Visible = true
            };

            // Left-click on tray icon opens Settings; right-click is natively handled by ContextMenuStrip
            _notifyIcon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    WpfApplication.Current?.Dispatcher.Invoke(_openSettingsAction);
                }
            };
        }

        private static Icon LoadOrCreateTrayIcon()
        {
            try
            {
                // 1. Try loading from Assets\app.ico next to executable
                string localIco = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
                if (File.Exists(localIco))
                {
                    return new Icon(localIco);
                }

                // 2. Try extracting from current running executable
                string? exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    var extracted = Icon.ExtractAssociatedIcon(exePath);
                    if (extracted != null) return extracted;
                }
            }
            catch
            {
                // Ignore and fall through to fallback
            }

            // 3. Fallback: Generate a crisp icon dynamically in memory
            return CreateFallbackIcon();
        }

        private static Icon CreateFallbackIcon()
        {
            using var bitmap = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bitmap);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Background circle with gradient look
            using (var brush = new SolidBrush(Color.FromArgb(0, 120, 215)))
            {
                g.FillEllipse(brush, 2, 2, 28, 28);
            }

            // Inner "T" letter for TransparentTaskbar
            using (var pen = new Pen(Color.White, 3))
            {
                g.DrawLine(pen, 9, 10, 23, 10);
                g.DrawLine(pen, 16, 10, 16, 23);
            }

            IntPtr hIcon = bitmap.GetHicon();
            Icon icon = Icon.FromHandle(hIcon);
            return icon;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _notifyIcon.Visible = false;
            _notifyIcon.ContextMenuStrip = null;
            _contextMenu.Dispose();
            _notifyIcon.Dispose();
        }
    }
}
