# AGI.Kapster 📸

**Modern Cross-Platform Screen Capture and Annotation Tool**

A high-performance screen capture tool built with .NET 9 and Avalonia UI, featuring intelligent overlay system, comprehensive annotation capabilities, and cross-platform support.

![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-blue)
![Framework](https://img.shields.io/badge/.NET-9.0-purple)
![UI](https://img.shields.io/badge/UI-Avalonia%2011-green)
![License](https://img.shields.io/badge/license-MIT-orange)
![CI/CD](https://github.com/AGIBuild/AGI.Kapster/actions/workflows/ci.yml/badge.svg)

[🌍 中文文档](README_CN.md) | [🤝 Contributing](CONTRIBUTING.md) | [🧪 Testing](TESTING.md)

## ✨ Features

### 🎯 Smart Capture
- **Global Hotkeys**: Customizable shortcuts (`Alt+A` default)
- **Region Selection**: Pixel-perfect selection with visual feedback
- **Multi-Monitor Support**: Seamless across multiple displays
- **Element Detection**: Automatic UI element detection (Windows)

### 🎨 Annotation Tools
- **Drawing Tools**: Arrow, rectangle, ellipse, text, freehand, emoji
- **Style Customization**: Colors, thickness, fonts, sizes
- **Undo/Redo**: Multi-step operation history
- **Layer Management**: Independent annotation editing

### 💾 Export Options
- **Multiple Formats**: PNG, JPEG, BMP, TIFF, WebP with quality control
- **Quick Actions**: Copy to clipboard (`Enter`) or save (`Ctrl+S`)
- **Batch Processing**: Export multiple captures consistently
- **Clipboard Integration**: Advanced clipboard operations

## 🚀 Quick Start

### System Requirements

| Platform | Version | Architecture | Runtime |
|----------|---------|--------------|---------|
| **Windows** | Windows 10 1809+ | x64, ARM64 | .NET 9.0 Desktop |
| **macOS** | macOS 10.15+ | x64, ARM64 | .NET 9.0 Runtime |
| **Linux** | Ubuntu 20.04+ | x64, ARM64 | .NET 9.0 Runtime (X11/Wayland) |

### Installation

#### Pre-built Packages (Recommended)
Download from [GitHub Releases](../../releases/latest):

**Windows:**
- `AGI.Kapster-win-x64.msi` - Windows Installer
- `AGI.Kapster-win-x64-portable.zip` - Portable version

**macOS:**
- `AGI.Kapster-osx-x64.pkg` - Intel Mac
- `AGI.Kapster-osx-arm64.pkg` - Apple Silicon
> Unsigned packages may require removing the quarantine attribute:
> `xattr -d com.apple.quarantine <your>.pkg`

**Linux:**
- `agi-kapster_*_amd64.deb` - Debian/Ubuntu
- `agi-kapster-*-1.x86_64.rpm` - Red Hat/CentOS/Fedora
- `AGI.Kapster-linux-x64-portable.zip` - Portable version

#### Build from Source
```bash
git clone https://github.com/AGIBuild/AGI.Kapster.git
cd AGI.Kapster
./build.ps1                    # Build and test
./build.ps1 Publish           # Create packages
```

### First Launch

1. **Start Application**: Launch from Start Menu/Applications
2. **Grant Permissions**: Allow screen recording (macOS)
3. **Take Screenshot**: Use `Alt+A` hotkey or system tray icon
4. **Annotate**: Use toolbar tools to add annotations
5. **Export**: Press `Enter` to copy or `Ctrl+S` to save

## ⌨️ Hotkeys

### Capture Commands
| Action | Hotkey | Description |
|--------|--------|-------------|
| **Capture Screen** | `Alt+A` | Start screen capture |
| **Open Settings** | `Alt+S` | Open settings window |
| **Save to File** | `Ctrl+S` | Save current capture |
| **Copy to Clipboard** | `Enter` | Copy to clipboard |
| **Cancel** | `Escape` | Cancel operation |

### Editing Shortcuts (in overlay)
| Action | Hotkey | Description |
|--------|--------|-------------|
| **Undo** | `Ctrl+Z` | Undo last action |
| **Redo** | `Ctrl+Y` or `Ctrl+Shift+Z` | Redo last undone action |
| **Select All** | `Ctrl+A` | Select all annotations |
| **Delete Selection** | `Delete` | Remove selected items |
| **Move Selection** | Arrow keys | Nudge by 1 px |
| **Adjust Stroke Width** | `Ctrl+-` / `Ctrl++` | Decrease/Increase width |

### Annotation Tools
| Tool | Hotkey | Description |
|------|--------|-------------|
| **Select** | `S` | Selection/edit mode |
| **Arrow** | `A` | Draw arrows |
| **Rectangle** | `R` | Draw rectangles |
| **Ellipse** | `E` | Draw ellipses |
| **Text** | `T` | Add text |
| **Freehand** | `F` | Free drawing |
| **Emoji** | `M` | Insert emoji |
| **Color Picker** | `C` | Pick color (when available) |

## 🛠️ Development

### Requirements
- .NET 9.0 SDK
- Visual Studio 2022 / JetBrains Rider / VS Code

### Development Setup
```bash
git clone https://github.com/AGIBuild/AGI.Kapster.git
cd AGI.Kapster
dotnet restore
./build.ps1

# Run application
dotnet run --project src/AGI.Kapster.Desktop
```

### Testing
```bash
# Run all tests
./build.ps1 Test

# Run with coverage
./build.ps1 Test -Coverage
```

## 📄 License

This project is licensed under the [MIT License](LICENSE).

---

**AGI.Kapster** - Making screenshot annotation simpler and more efficient! 🚀