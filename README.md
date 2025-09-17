# AGI.Captor 📸

**Modern Cross-Platform Screenshot and Annotation Tool**

A high-performance screenshot tool built with .NET 9 and Avalonia UI, featuring intelligent region selection, rich annotation capabilities, and flexible export options.

![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS-blue)
![Framework](https://img.shields.io/badge/.NET-9.0-purple)
![UI](https://img.shields.io/badge/UI-Avalonia%2011-green)
![License](https://img.shields.io/badge/license-MIT-orange)
![CI/CD](https://github.com/AGIBuild/AGI.Captor/actions/workflows/ci.yml/badge.svg)

[中文文档](README_CN.md) | [Contributing](CONTRIBUTING.md)

## ✨ Key Features

### 🎯 Smart Screenshot
- **Global Hotkey Support**: Quick access to screenshot interface (default `Alt+A`)
- **Free Region Selection**: Drag to create screenshots of any size
- **Multi-Monitor Support**: Seamless operation across multiple displays
- **High DPI Adaptation**: Perfect support for different scaling ratios

### 🎨 Rich Annotations
- **Drawing Tools**: Arrow, rectangle, ellipse, text, freehand, emoji
- **Style Customization**: Adjustable color, thickness, and font size
- **Undo/Redo**: Multi-step operation history support
- **Layer Management**: Independent editing and deletion of annotation elements

### 💾 Flexible Export
- **Multiple Formats**: PNG, JPEG, BMP, TIFF, GIF
- **Quality Control**: Adjustable JPEG quality and PNG compression levels
- **Quick Actions**: One-click copy to clipboard or save to file
- **Batch Processing**: Export settings presets support

### ⚙️ Personalization
- **Custom Hotkeys**: Customizable shortcuts for all functions
- **Auto-start**: Option to start with system boot
- **Tray Integration**: Minimize to system tray
- **Theme Support**: Modern user interface design

## 🚀 Quick Start

### System Requirements

- **Windows**: Windows 10 1809 (17763) or higher
- **macOS**: macOS 10.15 (Catalina) or higher
- **.NET Runtime**: .NET 9.0 or higher

### Installation

#### Windows
1. Download the latest `AGI.Captor-win-x64.zip` from [Releases](../../releases)
2. Extract to any directory
3. Run `AGI.Captor.App.exe`

#### macOS
1. Download the latest `AGI.Captor-osx-x64.zip` from [Releases](../../releases)
2. Extract to Applications folder
3. Run `AGI.Captor.App`

### First Use

1. **Launch App**: The program will minimize to system tray
2. **Take Screenshot**: Press `Alt+A` (default) to open screenshot interface
3. **Select Region**: Drag mouse to create screenshot area
4. **Add Annotations**: Use toolbar drawing tools for annotations
5. **Export**: Click save button or press `Ctrl+S` to save

## 📖 User Guide

### Basic Operations

#### Screenshot Workflow
1. **Open Interface**: `Alt+A` (customizable)
2. **Create Selection**:
   - Drag mouse to create rectangular selection
   - Use eight handles to adjust selection size
   - Drag inside selection to move position
3. **Add Annotations**: Select drawing tools from toolbar
4. **Complete Screenshot**:
   - `Ctrl+C`: Copy to clipboard
   - `Ctrl+S`: Save to file
   - `Escape`: Cancel screenshot

#### Annotation Tools

| Tool | Shortcut | Description |
|------|----------|-------------|
| Select | `S` | Select and edit annotation elements |
| Arrow | `A` | Draw pointing arrows |
| Rectangle | `R` | Draw rectangle frames |
| Ellipse | `E` | Draw ellipses |
| Text | `T` | Add text annotations |
| Freehand | `F` | Free drawing |
| Emoji | `M` | Insert emoji symbols |

#### Edit Operations

- **Undo**: `Ctrl+Z`
- **Redo**: `Ctrl+Y`
- **Delete Selected**: `Delete`
- **Select All**: `Ctrl+A`
- **Deselect**: `Ctrl+D`

### Advanced Features

#### Custom Hotkeys
1. Open Settings: `Alt+S` (default)
2. Go to "Hotkeys" tab
3. Click input box and press new key combination
4. Save settings

#### Export Settings
1. Choose export format: PNG, JPEG, BMP, TIFF, GIF
2. Adjust quality parameters:
   - **JPEG Quality**: 0-100, recommend 90+
   - **PNG Compression**: 0-9, recommend 6-9
3. Choose save location or copy to clipboard

#### Style Customization
- **Color**: Select from palette or enter hex value
- **Thickness**: 1-20 pixels, slider adjustable
- **Font Size**: 8-72pt, slider adjustable

## 🔧 Settings

### General Settings
- **Start with Windows**: Launch with system startup
- **Minimize to Tray**: Minimize to system tray when closing window
- **Show Notifications**: Display system notifications on completion
- **Default Save Format**: Choose preferred image format

### Hotkey Settings
- **Region Screenshot**: Hotkey to open screenshot interface
- **Open Settings**: Quick hotkey to open settings window

### Default Styles
- **Text**: Default font size and color
- **Shapes**: Default line thickness and color
- **Export**: Default JPEG quality and PNG compression levels

### Advanced Settings
- **Performance**: Hardware acceleration, memory limits, etc.
- **Debug**: Log levels, diagnostic information
- **Security**: Clipboard security, data protection

## 🗂️ File Structure

```
AGI.Captor/
├── AGI.Captor.App.exe          # Main executable
├── settings.json               # User settings (auto-created)
├── logs/                       # Log files
│   └── app-YYYYMMDD.log
└── runtimes/                   # Runtime dependencies
    ├── win-x64/               # Windows platform libraries
    └── osx-x64/               # macOS platform libraries
```

### Settings File Location

- **Windows**: `%APPDATA%\AGI.Captor\settings.json`
- **macOS**: `~/.config/AGI.Captor/settings.json`

## 🐛 Troubleshooting

### Common Issues

#### Hotkeys Not Responding
1. Check for conflicts with other applications
2. Run as administrator (Windows)
3. Grant accessibility permissions (macOS)

#### Blurry or Misaligned Screenshots
1. Check display scaling settings
2. Restart the application
3. Update graphics drivers

#### Cannot Save Files
1. Check target folder permissions
2. Ensure sufficient disk space
3. Check for special characters in filename

#### Annotation Tools Not Working
1. Ensure correct tool is selected
2. Check if operating within selection area
3. Reselect the tool

### Performance Optimizationgi t

#### High Resolution Screenshots
- Choose appropriate compression levels
- Avoid excessive annotation elements
- Clear history records promptly

#### Multi-Monitor Environment
- Ensure primary display is set correctly
- Use consistent scaling ratios
- Avoid mixing different DPI displays

## 🔐 Privacy & Security

- **Local Data**: All screenshots and settings stored locally
- **No Network Transfer**: No user data uploaded
- **Clipboard Security**: Secure clipboard mode support
- **Memory Protection**: Sensitive data cleared promptly

## 📋 Detailed System Requirements

### Windows
- **OS**: Windows 10 1809 (Build 17763) or higher
- **Architecture**: x64
- **Runtime**: .NET 9.0 Desktop Runtime
- **Memory**: At least 100MB available memory
- **Disk**: At least 50MB available space

### macOS
- **OS**: macOS 10.15 (Catalina) or higher
- **Architecture**: x64 (Intel) or ARM64 (Apple Silicon)
- **Runtime**: .NET 9.0 Runtime
- **Permissions**: Accessibility, screen recording permissions
- **Memory**: At least 100MB available memory
- **Disk**: At least 50MB available space

## 🆕 Changelog

### v1.2.0 (Current Release)
- ✅ **Enhanced Annotation Toolbar** (December 2024)
  - Added tooltips with hotkey shortcuts for all annotation tools
  - Improved toolbar responsiveness with real-time tool change updates
  - Better integration with annotation service events
  - Enhanced user experience with visual feedback
- ✅ **AOT Compatibility Improvements**
  - Fixed .NET 9 AOT compilation warnings
  - Replaced reflection-based method calls with dynamic invocation
  - Improved build stability across all platforms
- ✅ **Code Quality Enhancements**
  - Better error handling and logging
  - Improved event subscription management
  - Enhanced code maintainability

### v1.1.0 (Previous Release)
- ✅ **Code organization refactoring** (December 2024)
  - Reorganized Services directory by topic (Clipboard, Capture, ElementDetection, Export, Settings, Adapters)
  - Moved platform-specific implementations to Platforms subdirectories
  - Separated rendering components to Rendering/Overlays directory
  - Aligned all namespaces with physical file locations
  - Standardized using statements across all files
- ✅ **UI/UX improvements**
  - Fixed dark theme consistency across settings panels
  - Improved accessibility permission dialog (modal behavior, dynamic app path)
  - Enhanced permission guide panel styling
- ✅ **Platform-specific fixes**
  - Fixed macOS multi-screen overlay display issues
  - Improved macOS clipboard access with multiple fallback strategies
  - Maintained Windows element detection functionality

### v1.0.0 (Previous Version)
- ✅ Basic screenshot functionality
- ✅ Multiple annotation tools (Arrow, Rectangle, Ellipse, Text, Freehand, Emoji)
- ✅ Global hotkey support (Windows & macOS)
- ✅ Multi-format export (PNG, JPEG, BMP, WebP with quality control)
- ✅ Settings persistence and configuration management
- ✅ High DPI support and multi-monitor compatibility
- ✅ macOS platform support (hotkeys, screen capture, element detection)
- ✅ System tray integration with About dialog
- ✅ Clipboard functionality for all platforms
- ✅ UI/UX optimizations (toolbar icons, arrow styles)
- 🚧 CI/CD pipeline (in development)
- 🚧 Unit testing coverage (in development)
- 🚧 Auto-update system (planned)

## 🤝 Contributing

We welcome community contributions! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for details on how to participate in development.

### Development Environment
- .NET 9.0 SDK
- Visual Studio 2022 or JetBrains Rider
- Avalonia UI Extensions

### Build Instructions
```bash
# Clone repository
git clone https://github.com/your-username/AGI.Captor.git
cd AGI.Captor

# Restore dependencies
dotnet restore

# Build
dotnet build

# Run
dotnet run --project src/AGI.Captor.Desktop
```

## 📄 License

This project is licensed under the [MIT License](LICENSE).

## 🙏 Acknowledgments

- [Avalonia UI](https://avaloniaui.net/) - Cross-platform UI framework
- [SkiaSharp](https://github.com/mono/SkiaSharp) - 2D graphics library
- [Serilog](https://serilog.net/) - Structured logging library
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) - MVVM toolkit

---

**AGI.Captor** - Making screenshot annotation simpler and more efficient! 🚀
