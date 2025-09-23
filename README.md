# AGI.Captor 📸

**Modern Cross-Platform Screen Capture and Annotation Tool**

A high-performance screen capture tool built with .NET 9 and Avalonia UI, featuring intelligent overlay system, comprehensive annotation capabilities, and cross-platform support.

![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-blue)
![Framework](https://img.shields.io/badge/.NET-9.0-purple)
![UI](https://img.shields.io/badge/UI-Avalonia%2011-green)
![License](https://img.shields.io/badge/license-MIT-orange)
![CI/CD](https://github.com/AGIBuild/AGI.Captor/actions/workflows/ci.yml/badge.svg)

[🌍 中文文档](README_CN.md) | [🤝 Contributing](CONTRIBUTING.md)

## ✨ Key Features

### 🎯 Smart Screen Capture
- **Global Hotkeys**: Customizable shortcuts for instant capture (`Alt+A` default)
- **Region Selection**: Precise pixel-level selection with visual feedback
- **Multi-Monitor Support**: Seamless operation across multiple displays
- **Element Detection**: Automatic UI element detection (Windows only)

### 🎨 Professional Annotation Tools
- **Drawing Tools**: Arrow, rectangle, ellipse, text, freehand drawing, emoji
- **Style Customization**: Colors, thickness, fonts, and sizes
- **Undo/Redo System**: Multi-step operation history
- **Layer Management**: Independent annotation editing and deletion

### 💾 Flexible Export Options
- **Multiple Formats**: PNG, JPEG, BMP, TIFF, WebP with quality control
- **Quick Actions**: Copy to clipboard (`Enter`/Double-click) or save to file (`Ctrl+S`)
- **Batch Processing**: Export multiple captures with consistent settings
- **Clipboard Integration**: Advanced clipboard operations

### ⚙️ Modern Architecture
- **Background Operation**: System tray integration with minimal resources
- **Cross-Platform**: Windows, macOS, and Linux (planned) support
- **Dependency Injection**: Clean architecture with comprehensive testing

## 🚀 Quick Start

### System Requirements

| Platform | Version | Architecture | Runtime |
|----------|---------|--------------|---------|
| **Windows** | Windows 10 1809+ | x64, ARM64 | .NET 9.0 Desktop |
| **macOS** | macOS 10.15+ | x64, ARM64 | .NET 9.0 Runtime |
| **Linux** | Ubuntu 20.04+ | x64, ARM64 | .NET 9.0 Runtime |

### Installation

#### Pre-built Packages (Recommended)
Download from [GitHub Releases](../../releases/latest):
- **Windows**: `AGI.Captor-win-x64.msi` or `AGI.Captor-win-x64.zip`
- **macOS**: `AGI.Captor-osx-x64.pkg` (Intel) or `AGI.Captor-osx-arm64.pkg` (Apple Silicon)
- **Linux**: `agi-captor-linux-x64.deb` or `agi-captor-linux-x64.rpm`

#### Build from Source
```bash
git clone https://github.com/AGIBuild/AGI.Captor.git
cd AGI.Captor
./build.ps1                    # Build and test
./build.ps1 Publish           # Create executable
```

### First Launch

1. **Start Application**: Launch from Start Menu/Applications or run executable
2. **Grant Permissions**: Allow screen recording permissions (macOS)
3. **Take Screenshot**: Use `Alt+A` hotkey or click system tray icon
4. **Annotate**: Use toolbar tools to add annotations
5. **Export**: Press `Enter` to copy or `Ctrl+S` to save

## 📖 User Guide

### Hotkey Commands
| Action | Default | Description |
|--------|---------|-------------|
| **Capture Screen** | `Alt+A` | Start screen capture |
| **Quick Export** | `Ctrl+S` | Save to file |
| **Copy to Clipboard** | `Enter` / Double-click | Copy current capture |
| **Cancel Operation** | `Escape` | Cancel current operation |

### Annotation Tools
| Tool | Hotkey | Description |
|------|--------|-------------|
| **Arrow** | `A` | Draw directional arrows |
| **Rectangle** | `R` | Draw rectangle frames |
| **Ellipse** | `E` | Draw ellipses and circles |
| **Text** | `T` | Add text annotations |
| **Freehand** | `F` | Free drawing tool |
| **Emoji** | `M` | Insert emoji symbols |

## 🤝 Contributing

We welcome community contributions! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for details.

### Development Setup
```bash
# Clone and build
git clone https://github.com/AGIBuild/AGI.Captor.git
cd AGI.Captor
dotnet restore
./build.ps1

# Run application
dotnet run --project src/AGI.Captor.Desktop
```

### Requirements
- .NET 9.0 SDK
- Visual Studio 2022 or JetBrains Rider

## 📄 License

This project is licensed under the [MIT License](LICENSE).

---

**AGI.Captor** - Making screenshot annotation simpler and more efficient! 🚀