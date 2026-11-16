using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace AuraWallpaper.Core.Native
{
    public static class ShellThumbnailExtractor
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE
        {
            public int cx;
            public int cy;
            public SIZE(int cx, int cy) { this.cx = cx; this.cy = cy; }
        }

        public enum SIIGBF
        {
            SIIGBF_RESIZETOFIT = 0x00,
            SIIGBF_BIGGERSIZEOK = 0x01,
            SIIGBF_MEMORYONLY = 0x02,
            SIIGBF_ICONONLY = 0x04,
            SIIGBF_THUMBNAILONLY = 0x08,
            SIIGBF_INCACHEONLY = 0x10
        }

        [ComImport]
        [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItemImageFactory
        {
            [PreserveSig]
            int GetImage(
                [In] SIZE size,
                [In] SIIGBF flags,
                [Out] out IntPtr phbm);
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            IntPtr pbc,
            ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory? ppv);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        public static bool TryExtractThumbnail(string filePath, string outputPngPath, int width = 640, int height = 360)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return false;

            try
            {
                var guid = typeof(IShellItemImageFactory).GUID;
                int hr = SHCreateItemFromParsingName(filePath, IntPtr.Zero, ref guid, out var factory);
                if (hr != 0 || factory == null)
                    return false;

                int hrImg = factory.GetImage(new SIZE(width, height), SIIGBF.SIIGBF_BIGGERSIZEOK, out IntPtr hbm);
                if (hrImg != 0 || hbm == IntPtr.Zero)
                    return false;

                try
                {
                    var source = Imaging.CreateBitmapSourceFromHBitmap(
                        hbm,
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());

                    string? dir = Path.GetDirectoryName(outputPngPath);
                    if (!string.IsNullOrEmpty(dir))
                        Directory.CreateDirectory(dir);

                    using (var fs = new FileStream(outputPngPath, FileMode.Create, FileAccess.Write))
                    {
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(source));
                        encoder.Save(fs);
                    }

                    return File.Exists(outputPngPath) && new FileInfo(outputPngPath).Length > 0;
                }
                finally
                {
                    DeleteObject(hbm);
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
