# TransparentTaskbar

**Developer:** Rohit Singh + Gemini  
**Publisher:** Rohit Singh Personal  

A lightweight Windows 10 & 11 utility written in C# (.NET 8 WPF) that gives your taskbar full glass transparency, modern Fluent acrylic blur, or standard blur, running quietly from the Windows system tray.

---

## Features

- **Taskbar Transparency & Styling:** Targets `Shell_TrayWnd` and all multi-monitor taskbars (`Shell_SecondaryTrayWnd`) via the Windows Desktop Window Manager (DWM) composition API (`SetWindowCompositionAttribute`).
- **Real-Time Opacity Slider:** Dynamically adjusts taskbar transparency from 0% (100% glass clear) to 100% (fully opaque) with real-time preview.
- **System Tray Operation:** Runs silently in the background with a tray icon. Right-click context menu provides quick access to "Settings..." and "Exit".
- **Settings Persistence:** Saves preferences (`config.json`) in `%AppData%\TransparentTaskbar` (or next to the executable if in portable mode).
- **Auto-Start with Windows:** Easily toggles startup via `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`.
- **Single-File Self-Contained Deployment:** Compiles to a single standalone `.exe` without requiring external .NET runtimes.
- **Inno Setup Script:** Ready-to-build script for a 1-click Windows installer.

---

## Project Structure

```
TransparentTaskbar/
├── TransparentTaskbar.csproj         # .NET 8 WPF project definition with Windows Forms tray support
├── app.manifest                      # Windows 10 & 11 OS compatibility manifest
├── App.xaml                          # Application definition with OnExplicitShutdown
├── App.xaml.cs                       # Single-instance enforcement & tray lifecycle management
├── MainWindow.xaml                   # Modern Fluent-inspired Settings UI
├── MainWindow.xaml.cs                # Real-time opacity & appearance adjustments
├── Models/
│   └── AppConfig.cs                  # Data contract for settings persistence
├── Services/
│   ├── ConfigService.cs              # JSON configuration storage in %AppData%
│   ├── StartupService.cs             # Windows Run registry key management with exception handling
│   ├── TaskbarService.cs             # Window composition engine (FindWindow + SetWindowCompositionAttribute)
│   └── TrayIconService.cs            # Windows notification area (tray) icon and context menu
├── Native/
│   ├── AccentStructures.cs           # DWM AccentPolicy and WindowCompositionAttribute structs
│   └── NativeMethods.cs              # Win32 user32.dll P/Invoke signatures
├── Assets/
│   └── app.ico                       # Application & tray icon
├── installer/
│   └── setup.iss                     # Inno Setup 6 packaging script
└── publish/
    └── TransparentTaskbar.exe        # Self-contained standalone executable
```

---

## Building and Publishing

### 1. Debug Build
```powershell
dotnet build
```

### 2. Publish Single-File Self-Contained Executable
```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o ./publish
```

This compiles a single executable `publish\TransparentTaskbar.exe` containing the .NET runtime and all dependencies.

### 3. Generate 1-Click Installer with Inno Setup
1. Install [Inno Setup 6](https://jrsoftware.org/isdl.php).
2. Open `installer\setup.iss` in Inno Setup Compiler or run:
```powershell
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" .\installer\setup.iss
```
The installer `TransparentTaskbar_Setup_v1.0.0.exe` will be generated in `installer_output\`.
