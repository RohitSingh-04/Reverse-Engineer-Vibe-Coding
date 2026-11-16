# Aura Wallpaper CLI (`wallctl`) Automation Guide

`wallctl.exe` is the official automation and scripting interface for Aura Live Wallpaper. It controls playback, monitor assignments, volume, playlists, and screensavers via high-speed named pipe IPC.

---

## 1. Command Syntax & Options

```powershell
wallctl <command> [options]
```

### Commands:
| Command | Description |
|---|---|
| `show` | Shows and focuses the main application window. |
| `hide` | Minimizes the application to the system tray. |
| `play` | Resumes wallpaper playback across all displays. |
| `pause` | Pauses wallpaper playback across all displays. |
| `mute` | Mutes wallpaper audio globally. |
| `unmute` | Unmutes wallpaper audio globally. |
| `volume <0-100>` | Sets global wallpaper volume percentage. |
| `set --file <path> [--monitor <id>]` | Applies a wallpaper file to a specific or all monitors. |
| `set --random` | Selects and applies a random wallpaper from the library. |
| `close [--monitor <id>] [--all]` | Closes live wallpaper on a specific display or all displays. |
| `next` | Cycles to the next wallpaper in the current playlist/collection. |
| `previous` | Cycles to the previous wallpaper in the collection. |
| `screensaver --show` | Triggers the native live wallpaper screensaver immediately. |
| `list` | Lists all indexed wallpapers in the local database. |
| `monitors` | Enumerates all connected monitors, coordinates, and primary flags. |
| `status` | Displays real-time playback state, active monitor assignments, and CPU/RAM metrics. |
| `shutdown` | Gracefully terminates Aura Live Wallpaper and all child renderers. |

### Global Flags:
- `--json`: Outputs structured JSON for automated script parsing.
- `--monitor, -m <index>`: Target display index (0, 1, 2...).
- `--file, -f <path>`: Path to `.mp4`, `.webm`, `.html`, `.gif`, `.png`, or `.aurapkg`.
- `--all, -a`: Applies command to all displays simultaneously.

---

## 2. PowerShell Automation Examples

### Automatically pause wallpapers during gaming:
```powershell
$gameProcesses = @("Cyberpunk2077", "Valorant", "Overwatch")
while ($true) {
    $running = Get-Process -Name $gameProcesses -ErrorAction SilentlyContinue
    if ($running) {
        wallctl pause
    } else {
        wallctl play
    }
    Start-Sleep -Seconds 5
}
```

### Rotate wallpapers on a custom timer:
```powershell
while ($true) {
    Start-Sleep -Seconds 600 # 10 minutes
    wallctl next
}
```

### Parse JSON status:
```powershell
$status = wallctl status --json | ConvertFrom-Json
Write-Host "Active Wallpapers: $($status.Data.ActiveWallpapers.Count)"
Write-Host "Volume: $($status.Data.GlobalVolume)%"
```

---

## 3. Python Integration

```python
import subprocess
import json

def set_wallpaper(file_path, monitor=0):
    cmd = ["wallctl", "set", "--file", file_path, "--monitor", str(monitor)]
    subprocess.run(cmd, check=True)

def get_status():
    result = subprocess.run(["wallctl", "status", "--json"], capture_output=True, text=True)
    return json.loads(result.stdout)

if __name__ == "__main__":
    print(get_status())
    set_wallpaper("C:\\Wallpapers\\matrix.mp4", monitor=0)
```

---

## 4. AutoHotkey Integration

```autohotkey
; Win + Alt + P: Toggle Pause/Play
#!p::
    Run, wallctl play, , Hide
return

; Win + Alt + M: Toggle Mute
#!m::
    Run, wallctl mute, , Hide
return

; Win + Alt + Right: Next Wallpaper
#!Right::
    Run, wallctl next, , Hide
return
```
