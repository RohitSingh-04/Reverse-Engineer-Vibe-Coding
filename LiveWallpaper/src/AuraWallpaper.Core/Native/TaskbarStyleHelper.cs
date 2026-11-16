using System;
using System.Runtime.InteropServices;

namespace AuraWallpaper.Core.Native
{
    public enum TaskbarThemeMode
    {
        Default = 0,
        Clear = 1,
        Blur = 2,
        Acrylic = 3,
        Tinted = 4
    }

    public static class TaskbarStyleHelper
    {
        public static void SetTaskbarStyle(TaskbarThemeMode mode, int tintColor = 0)
        {
            try
            {
                IntPtr taskbarHwnd = NativeMethods.FindWindow("Shell_TrayWnd", null);
                if (taskbarHwnd == IntPtr.Zero)
                    return;

                ApplyAccent(taskbarHwnd, mode, tintColor);

                // Also apply to secondary monitor taskbars if present
                IntPtr secondaryTaskbar = IntPtr.Zero;
                while ((secondaryTaskbar = NativeMethods.FindWindowEx(IntPtr.Zero, secondaryTaskbar, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero)
                {
                    ApplyAccent(secondaryTaskbar, mode, tintColor);
                }
            }
            catch
            {
                // Safe fallback: never crash or destabilize the desktop
            }
        }

        private static void ApplyAccent(IntPtr hwnd, TaskbarThemeMode mode, int tintColor)
        {
            var policy = new NativeMethods.AccentPolicy();

            switch (mode)
            {
                case TaskbarThemeMode.Default:
                    policy.AccentState = NativeMethods.AccentState.ACCENT_DISABLED;
                    policy.AccentFlags = 0;
                    break;
                case TaskbarThemeMode.Clear:
                    policy.AccentState = NativeMethods.AccentState.ACCENT_ENABLE_TRANSPARENTGRADIENT;
                    policy.AccentFlags = 2; // AllowTransparency / DrawAllBorders
                    policy.GradientColor = 0x00000000; // completely clear (alpha 0)
                    break;
                case TaskbarThemeMode.Blur:
                    policy.AccentState = NativeMethods.AccentState.ACCENT_ENABLE_BLURBEHIND;
                    policy.AccentFlags = 2;
                    break;
                case TaskbarThemeMode.Acrylic:
                    policy.AccentState = NativeMethods.AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND;
                    policy.AccentFlags = 2;
                    policy.GradientColor = tintColor != 0 ? tintColor : unchecked((int)0x99202020);
                    break;
                case TaskbarThemeMode.Tinted:
                    policy.AccentState = NativeMethods.AccentState.ACCENT_ENABLE_GRADIENT;
                    policy.AccentFlags = 2;
                    policy.GradientColor = tintColor != 0 ? tintColor : unchecked((int)0xCC181818);
                    break;
            }

            int accentStructSize = Marshal.SizeOf(policy);
            IntPtr accentPtr = Marshal.AllocHGlobal(accentStructSize);
            try
            {
                Marshal.StructureToPtr(policy, accentPtr, false);
                var data = new NativeMethods.WindowCompositionAttributeData
                {
                    Attribute = NativeMethods.WindowCompositionAttribute.WCA_ACCENT_POLICY,
                    Data = accentPtr,
                    SizeOfData = accentStructSize
                };

                NativeMethods.SetWindowCompositionAttribute(hwnd, ref data);
                NativeMethods.RedrawWindow(hwnd, IntPtr.Zero, IntPtr.Zero, 0x0101 /* RDW_INVALIDATE | RDW_ERASE */);
            }
            finally
            {
                Marshal.FreeHGlobal(accentPtr);
            }
        }
    }
}
