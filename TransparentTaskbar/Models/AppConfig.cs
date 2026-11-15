using System;
using System.Text.Json.Serialization;

namespace TransparentTaskbar.Models
{
    public enum TaskbarStyleMode
    {
        Clear = 0,
        Acrylic = 1,
        Blur = 2,
        Default = 3
    }

    public class AppConfig
    {
        public TaskbarStyleMode Style { get; set; } = TaskbarStyleMode.Clear;

        /// <summary>
        /// Opacity percentage from 0 (completely transparent) to 100 (fully opaque).
        /// Default is 0 for pure glass transparency.
        /// </summary>
        public int Opacity { get; set; } = 0;

        /// <summary>
        /// Optional tint hex color in #RRGGBB format. Default is black tint (#000000).
        /// </summary>
        public string TintColorHex { get; set; } = "#000000";

        /// <summary>
        /// Whether the application starts with Windows login.
        /// </summary>
        public bool StartWithWindows { get; set; } = true;

        /// <summary>
        /// Whether to remove the 1-2px top border line from the taskbar.
        /// </summary>
        public bool RemoveTopBorder { get; set; } = true;

        /// <summary>
        /// When true, keeps icons 100% solid, crisp, and vibrant without ever fading or disappearing.
        /// </summary>
        public bool PreserveSolidIcons { get; set; } = true;

        [JsonIgnore]
        public byte AlphaByte => (byte)Math.Clamp((int)Math.Round((Opacity / 100.0) * 255.0), 0, 255);
    }
}
