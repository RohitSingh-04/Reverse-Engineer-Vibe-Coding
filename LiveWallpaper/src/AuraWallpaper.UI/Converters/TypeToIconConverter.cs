using System;
using System.Globalization;
using System.Windows.Data;
using AuraWallpaper.Core.Models;

namespace AuraWallpaper.UI.Converters
{
    public class TypeToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is WallpaperType type)
            {
                return type switch
                {
                    WallpaperType.Video => "🎬",
                    WallpaperType.Web => "🌐",
                    WallpaperType.Picture => "🖼",
                    WallpaperType.Gif => "🎞",
                    WallpaperType.App => "⚡",
                    _ => "✨"
                };
            }
            return "✨";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
