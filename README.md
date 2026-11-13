# ReNotchPixel — Dynamic Island & Desktop Notch for Windows

An open-source desktop island and hardware notch companion for Windows 10 and Windows 11. Built with modern C# and WPF on .NET 10.

---

## Features

- **Dynamic Island & Hardware Notch Modes**: Toggle between a floating rounded pill and an anchored bezel notch with smooth cubic bezier fillets.
- **Media Controls & Live Audio Equalizer**: Real-time integration with Windows Media Transport Controls (Spotify, YouTube, browsers, media players) with live peak audio waveform bars.
- **Volume & Microphone HUD**: Floating on-screen indicators for volume adjustment, mute toggles, Caps Lock, and Num Lock.
- **System Telemetry & Weather**: Live CPU load, RAM usage, Battery & charging indicator, local weather, and date/time.
- **Pomodoro Focus Timer**: Customizable work and break intervals with audio notifications.
- **Quick File Shelf**: Interactive drag-and-drop dropzone for temporary file storage and quick drag-out.
- **Clipboard History**: Background clipboard change listener with one-click re-copying.
- **AI Coding Agent Live Activity**: Real-time telemetry feed and webhook receiver for local AI coding assistant sessions (Antigravity CLI, Claude Code).
- **Mobile Companion Deck**: Built-in lightweight local web server serving an interactive mobile deck (touchpad, media remote, volume slider, quick action buttons).
- **Fullscreen & Game Detection**: Automatically slims down or hides during fullscreen exclusive games and maximized media playback.
- **Preferences & System Tray Menu**: Complete customization of shapes, screen edge positioning, hotkeys (Win+Alt+N), and startup preferences.

---

## Getting Started

### Prerequisites
- Windows 10 (version 19041 or higher) or Windows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Building from Source

```powershell
# Clone the repository
git clone https://github.com/VEDANT1109/ReNotchPixel.git
cd ReNotchPixel

# Build Release binary
dotnet build -c Release

# Run the application
dotnet run --project src/ReNotchPixel -c Release
```

---

## Architecture & Project Layout

```
ReNotchPixel/
├── src/ReNotchPixel/
│   ├── Controls/
│   │   ├── NotchGeometryHelper.cs   # Bezier fillet curves
│   │   ├── VirtualPetControl.cs     # Animated pet companion
│   │   └── WaveformVisualizer.cs    # Real-time audio waveform
│   ├── Models/
│   │   ├── AppSettings.cs           # User configuration model
│   │   ├── IslandEnums.cs           # Mode, Shape, and Panel enumerations
│   │   ├── MediaInfo.cs             # Playback status & metadata model
│   │   ├── ClipboardItem.cs         # Clipboard entry model
│   │   ├── ShelfItem.cs             # Drag-and-drop item model
│   │   ├── WeatherInfo.cs           # Weather data model
│   │   └── AgentStatus.cs           # AI Agent activity model
│   ├── Services/
│   │   ├── Win32Helper.cs           # Window styling, click-through, and input
│   │   ├── MediaService.cs          # Windows GSMTC playback manager
│   │   ├── AudioService.cs          # CoreAudio MMDevice volume controller
│   │   ├── SystemMonitorService.cs  # CPU, RAM, battery, and lock keys
│   │   ├── ClipboardService.cs      # Clipboard update format listener
│   │   ├── WeatherService.cs        # Meteorological forecast fetcher
│   │   ├── OcclusionService.cs      # Foreground fullscreen & game detector
│   │   ├── DeckServerService.cs     # Mobile companion HTTP server
│   │   ├── SettingsService.cs       # JSON preferences & autostart registry
│   │   ├── ScreenshotService.cs     # Screen snipping tool caller
│   │   └── TrayService.cs           # Native system tray icon and menu
│   ├── ViewModels/
│   │   └── MainViewModel.cs         # Core MVVM orchestrator
│   ├── MainWindow.xaml / .cs        # Layered hardware-accelerated notch window
│   ├── SettingsWindow.xaml / .cs    # Preferences dialog
│   └── App.xaml / .cs               # WPF application lifecycle
├── .gitignore
├── LICENSE
├── ReNotchPixel.slnx
└── README.md
```

---

## Keyboard Shortcuts & Quick Actions

- `Win + Alt + N`: Toggle expanded / collapsed island
- `Right-click on System Tray Icon`: Open settings, switch shapes, or capture screenshot

---

## License

This project is licensed under the [MIT License](LICENSE).
