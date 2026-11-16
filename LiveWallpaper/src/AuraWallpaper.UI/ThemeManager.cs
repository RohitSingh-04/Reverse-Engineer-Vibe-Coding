using System;
using System.Windows;
using System.Windows.Media;
using AuraWallpaper.Core.Models;
using AuraWallpaper.Core.Storage;

namespace AuraWallpaper.UI
{
    public static class ThemeManager
    {
        public static AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;
        public static event Action<AppTheme>? ThemeChanged;

        public static void Initialize()
        {
            try
            {
                var settings = AuraDatabase.Instance.LoadSettings();
                ApplyTheme(settings.Theme);
            }
            catch
            {
                ApplyTheme(AppTheme.Dark);
            }
        }

        public static void ToggleTheme()
        {
            var newTheme = CurrentTheme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light;
            ApplyTheme(newTheme);

            try
            {
                var settings = AuraDatabase.Instance.LoadSettings();
                settings.Theme = newTheme;
                AuraDatabase.Instance.SaveSettings(settings);
            }
            catch { }
        }

        public static void SetTheme(AppTheme theme)
        {
            ApplyTheme(theme);

            try
            {
                var settings = AuraDatabase.Instance.LoadSettings();
                settings.Theme = theme;
                AuraDatabase.Instance.SaveSettings(settings);
            }
            catch { }
        }

        public static void ApplyTheme(AppTheme theme)
        {
            bool isLight = (theme == AppTheme.Light);
            CurrentTheme = isLight ? AppTheme.Light : AppTheme.Dark;

            var res = Application.Current?.Resources;
            if (res == null) return;

            // Background & Surfaces
            SetBrush(res, "WindowBackground", isLight ? "#F3F5F9" : "#14161C");
            SetBrush(res, "SurfaceBackground", isLight ? "#FFFFFF" : "#1C1F28");
            SetBrush(res, "HeaderBackground", isLight ? "#FFFFFF" : "#161922");
            SetBrush(res, "SidebarBackground", isLight ? "#F8F9FC" : "#161922");
            SetBrush(res, "CardBackground", isLight ? "#FFFFFF" : "#202430");
            SetBrush(res, "CardBorder", isLight ? "#E2E6EE" : "#2C3242");
            SetBrush(res, "CardHover", isLight ? "#F6F8FC" : "#272C3A");

            // Typography
            SetBrush(res, "TextPrimary", isLight ? "#1A1D24" : "#FFFFFF");
            SetBrush(res, "TextSecondary", isLight ? "#5A6272" : "#A0A6B5");
            SetBrush(res, "TextMuted", isLight ? "#8A92A2" : "#707685");

            // Form Controls
            SetBrush(res, "InputBackground", isLight ? "#FFFFFF" : "#1C1F28");
            SetBrush(res, "InputBorder", isLight ? "#CED4E0" : "#363C4E");
            SetBrush(res, "ButtonBackground", isLight ? "#EAEFF6" : "#2C3242");
            SetBrush(res, "ButtonHoverBackground", isLight ? "#DEE5EF" : "#373E52");
            SetBrush(res, "ButtonBorder", isLight ? "#D0D8E5" : "#41495E");

            // Modern Scrollbar
            SetBrush(res, "ScrollbarTrack", "Transparent");
            SetBrush(res, "ScrollbarThumb", isLight ? "#C2C8D6" : "#4A5266");
            SetBrush(res, "ScrollbarThumbHover", isLight ? "#9EA7B8" : "#6A748E");

            // Navigation
            SetBrush(res, "NavActiveBackground", isLight ? "#E1EBF9" : "#2A3245");
            SetBrush(res, "NavHoverBackground", isLight ? "#EEF2F8" : "#212635");
            SetBrush(res, "NavActiveForeground", isLight ? "#0078D4" : "#60CDFF");

            // Brand Accent
            SetBrush(res, "AccentBlue", "#0078D4");

            ThemeChanged?.Invoke(CurrentTheme);
        }

        private static void SetBrush(ResourceDictionary res, string key, string hex)
        {
            try
            {
                if (hex.Equals("Transparent", StringComparison.OrdinalIgnoreCase))
                {
                    res[key] = Brushes.Transparent;
                    return;
                }
                var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
                brush.Freeze();
                res[key] = brush;
            }
            catch { }
        }
    }
}
