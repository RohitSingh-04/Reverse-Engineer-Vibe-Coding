$code = @'
using System;
using System.Runtime.InteropServices;
public class WinTest {
    [DllImport("user32.dll")] public static extern IntPtr GetDesktopWindow();
    [DllImport("user32.dll")] public static extern IntPtr GetShellWindow();
}
'@
Add-Type -TypeDefinition $code
Write-Host "DesktopWindow: $([WinTest]::GetDesktopWindow())"
Write-Host "ShellWindow: $([WinTest]::GetShellWindow())"
