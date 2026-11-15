using System;
using System.IO;
using System.Security;
using Microsoft.Win32;

namespace TransparentTaskbar.Services
{
    public static class StartupService
    {
        private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string StartupApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        private const string SerializeKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize";
        private const string AppRegistryName = "TransparentTaskbar";

        private static string StartupFolderPath =>
            Environment.GetFolderPath(Environment.SpecialFolder.Startup);

        private static string StartupShortcutPath =>
            Path.Combine(StartupFolderPath, $"{AppRegistryName}.lnk");

        public static bool IsAutoStartEnabled()
        {
            try
            {
                // Check 1: Startup folder shortcut
                if (File.Exists(StartupShortcutPath))
                {
                    return true;
                }

                // Check 2: HKCU Run registry key
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
                if (key == null) return false;

                var value = key.GetValue(AppRegistryName) as string;
                if (string.IsNullOrWhiteSpace(value)) return false;

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StartupService] Error reading startup registry: {ex.Message}");
                return false;
            }
        }

        public static (bool Success, string? ErrorMessage) SetAutoStart(bool enable)
        {
            try
            {
                string? currentExe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(currentExe))
                {
                    currentExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TransparentTaskbar.exe");
                }

                if (enable)
                {
                    // 1. Set HKCU\Software\Microsoft\Windows\CurrentVersion\Run
                    using (var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true))
                    {
                        if (key != null)
                        {
                            string command = $"\"{currentExe}\"";
                            key.SetValue(AppRegistryName, command, RegistryValueKind.String);
                        }
                    }

                    // 2. Set StartupApproved\Run as explicitly Enabled with priority
                    try
                    {
                        using var approvedKey = Registry.CurrentUser.CreateSubKey(StartupApprovedKey, true);
                        if (approvedKey != null)
                        {
                            byte[] enabledBinary = new byte[] { 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
                            approvedKey.SetValue(AppRegistryName, enabledBinary, RegistryValueKind.Binary);
                        }
                    }
                    catch
                    {
                        // Non-critical fallback
                    }

                    // 3. Remove Windows Explorer 10-15s startup delay (StartupDelayInMSec = 0)
                    try
                    {
                        using var serializeKey = Registry.CurrentUser.CreateSubKey(SerializeKey, true);
                        if (serializeKey != null)
                        {
                            serializeKey.SetValue("StartupDelayInMSec", 0, RegistryValueKind.DWord);
                        }
                    }
                    catch
                    {
                        // Non-critical fallback
                    }

                    // 4. Create high-priority Startup folder shortcut for immediate logon execution
                    CreateStartupShortcut(currentExe);
                }
                else
                {
                    // 1. Remove from HKCU\Software\Microsoft\Windows\CurrentVersion\Run
                    using (var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true))
                    {
                        if (key?.GetValue(AppRegistryName) != null)
                        {
                            key.DeleteValue(AppRegistryName, false);
                        }
                    }

                    // 2. Remove from StartupApproved\Run
                    try
                    {
                        using var approvedKey = Registry.CurrentUser.OpenSubKey(StartupApprovedKey, true);
                        if (approvedKey?.GetValue(AppRegistryName) != null)
                        {
                            approvedKey.DeleteValue(AppRegistryName, false);
                        }
                    }
                    catch
                    {
                        // Ignore
                    }

                    // 3. Delete Startup folder shortcut
                    RemoveStartupShortcut();
                }

                return (true, null);
            }
            catch (SecurityException ex)
            {
                return (false, $"Registry security access denied: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                return (false, $"Unauthorized access while updating startup configuration: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, $"Failed to update Windows startup configuration: {ex.Message}");
            }
        }

        private static void CreateStartupShortcut(string targetExe)
        {
            try
            {
                string shortcutPath = StartupShortcutPath;
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    dynamic? shell = Activator.CreateInstance(shellType);
                    if (shell != null)
                    {
                        dynamic? shortcut = shell.CreateShortcut(shortcutPath);
                        if (shortcut != null)
                        {
                            shortcut.TargetPath = targetExe;
                            shortcut.WorkingDirectory = Path.GetDirectoryName(targetExe);
                            shortcut.Description = "TransparentTaskbar - Fast Taskbar Transparency";
                            shortcut.Save();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StartupService] Failed to create startup shortcut: {ex.Message}");
            }
        }

        private static void RemoveStartupShortcut()
        {
            try
            {
                string shortcutPath = StartupShortcutPath;
                if (File.Exists(shortcutPath))
                {
                    File.Delete(shortcutPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StartupService] Failed to remove startup shortcut: {ex.Message}");
            }
        }
    }
}
