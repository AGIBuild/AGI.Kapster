# AGI.Kapster 📸

**现代化跨平台截图与标注工具**

基于 .NET 9 和 Avalonia UI 构建的高性能截图工具，支持智能覆盖系统、丰富的标注功能和跨平台兼容。

![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-blue)
![Framework](https://img.shields.io/badge/.NET-9.0-purple)
![UI](https://img.shields.io/badge/UI-Avalonia%2011-green)
![License](https://img.shields.io/badge/license-MIT-orange)
![CI/CD](https://github.com/AGIBuild/AGI.Kapster/actions/workflows/ci.yml/badge.svg)

[English](README.md) | [贡献指南](CONTRIBUTING.md) | [测试指南](TESTING.md)

## 📹 演示

<div align="center">
  
![AGI.Kapster 演示](docs/kapster_demo_preview.gif)

</div>

> **📺 [观看完整演示视频](docs/kapster_demo.mp4)** (2:07，点击下载)

*快速演示 AGI.Kapster 的截图和标注功能*

## ✨ 主要特性

### 🎯 智能截图
- **全局热键**: `Alt+A` 截图，`Alt+S` 打开设置
- **多屏支持**: 跨多个显示器无缝操作
- **元素检测**: 自动UI元素检测（Windows）
- **单实例运行**: 同时只运行一个应用实例

### 🎨 标注工具
- **工具**: 箭头(A)、矩形(R)、椭圆(E)、文字(T)、手绘(F)、马赛克(M)、表情(J)
- **样式自定义**: 颜色、粗细、字体和大小
- **撤销重做**: `Ctrl+Z` / `Ctrl+Y`
- **快捷键访问**: 绘图时按工具快捷键

### 💾 导出与设置
- **格式**: PNG、JPEG、BMP、TIFF、WebP
- **快速导出**: `Enter` 复制或 `Ctrl+S` 保存
- **自动更新**: 可配置更新检查
- **系统启动**: 可选开机自启动

## 🚀 快速开始

### 系统要求

| 平台 | 版本 | 架构 | 运行时 |
|------|------|------|--------|
| **Windows** | Windows 10 1809+ | x64, ARM64 | .NET 9.0 Desktop |
| **macOS** | macOS 10.15+ | x64, ARM64 | .NET 9.0 Runtime |
| **Linux** | Ubuntu 20.04+ | x64, ARM64 | .NET 9.0 Runtime（X11/Wayland） |

### 安装

#### 预构建包（推荐）
从 [GitHub Releases](../../releases/latest) 下载：

**Windows:**
- `AGI.Kapster-win-x64.msi` - Windows安装包
- `AGI.Kapster-win-x64-portable.zip` - 便携版

**macOS:**
- `AGI.Kapster-osx-x64.pkg` - Intel Mac
- `AGI.Kapster-osx-arm64.pkg` - Apple Silicon
> 包未签名时，可能需要移除隔离属性：
> `xattr -d com.apple.quarantine <your>.pkg`

**Linux:**
- `agi-kapster_*_amd64.deb` - Debian/Ubuntu
- `agi-kapster-*-1.x86_64.rpm` - Red Hat/CentOS/Fedora
- `AGI.Kapster-linux-x64-portable.zip` - 便携版

#### 从源码构建
```bash
git clone https://github.com/AGIBuild/AGI.Kapster.git
cd AGI.Kapster
./build.ps1                    # 构建和测试
./build.ps1 Publish           # 创建包
```

### 首次启动

1. **启动应用**: 从开始菜单/应用程序启动
2. **授予权限**: 允许屏幕录制权限（macOS）
3. **截图**: 使用 `Alt+A` 热键或系统托盘图标
4. **标注**: 使用工具栏添加标注
5. **导出**: 按 `Enter` 复制或 `Ctrl+S` 保存

## ⌨️ 快捷键

### 截图命令
| 操作 | 快捷键 | 说明 |
|------|--------|------|
| **开始截图** | `Alt+A` | 启动屏幕截图 |
| **打开设置** | `Alt+S` | 打开设置窗口 |
| **保存到文件** | `Ctrl+S` | 保存当前截图 |
| **复制到剪贴板** | `Enter` | 复制到剪贴板 |
| **取消操作** | `Escape` | 取消当前操作 |

### 编辑快捷键（覆盖层内）
| 操作 | 快捷键 | 说明 |
|------|--------|------|
| **撤销** | `Ctrl+Z` | 撤销上一步 |
| **重做** | `Ctrl+Y` 或 `Ctrl+Shift+Z` | 重做上一撤销 |
| **全选** | `Ctrl+A` | 选择所有标注 |
| **删除选择** | `Delete` | 删除所选项 |
| **移动选择** | 方向键 | 1 像素微移 |
| **调整线宽** | `Ctrl+-` / `Ctrl++` | 减小/增大线宽 |

### 标注工具
| 工具 | 快捷键 | 说明 |
|------|--------|------|
| **选择** | `S` | 选择/编辑模式 |
| **箭头** | `A` | 绘制箭头 |
| **矩形** | `R` | 绘制矩形 |
| **椭圆** | `E` | 绘制椭圆 |
| **文字** | `T` | 添加文字 |
| **手绘** | `F` | 自由绘图 |
| **马赛克** | `M` | 像素化/模糊区域 |
| **表情** | `J` | 插入表情符号 |
| **取色器** | `C` | 颜色拾取 |

## 🛠️ 开发

### 开发要求
- .NET 9.0 SDK
- Visual Studio 2022 / JetBrains Rider / VS Code

### 开发环境设置
```bash
git clone https://github.com/AGIBuild/AGI.Kapster.git
cd AGI.Kapster
dotnet restore
./build.ps1

# 运行应用
dotnet run --project src/AGI.Kapster.Desktop
```

### 测试
```bash
# 运行所有测试
./build.ps1 Test

# 运行覆盖率测试
./build.ps1 Test -Coverage
```

## 🤝 社区与反馈

### 平台支持说明

AGI.Kapster 是一个充满热情与用心开发的个人开源项目。虽然我们致力于在所有平台上提供最佳体验，但请您理解：

- **主要测试平台**: 大部分测试在 **Windows** 和 **macOS** 平台上进行
- **Linux 支持**: Linux 功能已实现，但可能缺少充分的实际场景测试
- **已知限制**: 部分平台特定功能或边界场景可能未完全覆盖

作为个人项目，跨平台全面测试的资源有限，感谢您的理解！您的使用体验和反馈将帮助 AGI.Kapster 变得更好。

### 如何参与贡献

我们热情欢迎社区的各种贡献：

- 🐛 **报告问题**: 发现 Bug？欢迎在 GitHub 上[提交 Issue](../../issues/new)
- 💡 **分享反馈**: 告诉我们您在不同平台上的使用体验
- 🔧 **贡献代码**: 随时欢迎提交 Pull Request！请参阅[贡献指南](CONTRIBUTING.md)
- 📖 **改进文档**: 帮助我们完善文档内容
- ⭐ **点亮星标**: 通过为项目加星来表达您的支持

**您的平台体验很重要！** 无论您使用 Windows、macOS 还是 Linux，我们都希望听到您的声音。每一个问题反馈和建议都能帮助整个社区改进这个工具。

## 📄 许可证

本项目基于 [MIT 许可证](LICENSE) 开源。

---

**AGI.Kapster** - 让截图标注更简单、更高效！🚀