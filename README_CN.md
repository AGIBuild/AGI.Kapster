# AGI.Captor 📸

**现代化跨平台截图与标注工具**

基于 .NET 9 和 Avalonia UI 构建的高性能截图工具，支持智能覆盖系统、丰富的标注功能和跨平台兼容。

![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-blue)
![Framework](https://img.shields.io/badge/.NET-9.0-purple)
![UI](https://img.shields.io/badge/UI-Avalonia%2011-green)
![License](https://img.shields.io/badge/license-MIT-orange)
![CI/CD](https://github.com/AGIBuild/AGI.Captor/actions/workflows/ci.yml/badge.svg)

[English](README.md) | [贡献指南](CONTRIBUTING.md) | [测试指南](TESTING.md)

## ✨ 主要特性

### 🎯 智能截图
- **全局热键**: 可自定义快捷键（默认 `Alt+A`）
- **区域选择**: 精确的像素级选择，带视觉反馈
- **多屏支持**: 跨多个显示器无缝操作
- **元素检测**: 自动UI元素检测（仅Windows）

### 🎨 标注工具
- **绘图工具**: 箭头、矩形、椭圆、文字、手绘、表情符号
- **样式自定义**: 颜色、粗细、字体和大小设置
- **撤销重做**: 多步操作历史记录
- **图层管理**: 独立的标注编辑和删除

### 💾 导出选项
- **多种格式**: PNG、JPEG、BMP、TIFF、WebP，支持质量控制
- **快速操作**: 复制到剪贴板（`Enter`）或保存（`Ctrl+S`）
- **批量处理**: 使用一致设置导出多个截图
- **剪贴板集成**: 高级剪贴板操作

## 🚀 快速开始

### 系统要求

| 平台 | 版本 | 架构 | 运行时 |
|------|------|------|--------|
| **Windows** | Windows 10 1809+ | x64, ARM64 | .NET 9.0 Desktop |
| **macOS** | macOS 10.15+ | x64, ARM64 | .NET 9.0 Runtime |
| **Linux** | Ubuntu 20.04+ | x64, ARM64 | .NET 9.0 Runtime |

### 安装

#### 预构建包（推荐）
从 [GitHub Releases](../../releases/latest) 下载：

**Windows:**
- `AGI.Captor-win-x64.msi` - Windows安装包
- `AGI.Captor-win-x64-portable.zip` - 便携版

**macOS:**
- `AGI.Captor-osx-x64.pkg` - Intel Mac
- `AGI.Captor-osx-arm64.pkg` - Apple Silicon

**Linux:**
- `agi-captor_*_amd64.deb` - Debian/Ubuntu
- `agi-captor-*-1.x86_64.rpm` - Red Hat/CentOS/Fedora
- `AGI.Captor-linux-x64-portable.zip` - 便携版

#### 从源码构建
```bash
git clone https://github.com/AGIBuild/AGI.Captor.git
cd AGI.Captor
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
| **保存到文件** | `Ctrl+S` | 保存当前截图 |
| **复制到剪贴板** | `Enter` | 复制到剪贴板 |
| **取消操作** | `Escape` | 取消当前操作 |

### 标注工具
| 工具 | 快捷键 | 说明 |
|------|--------|------|
| **箭头** | `A` | 绘制箭头 |
| **矩形** | `R` | 绘制矩形 |
| **椭圆** | `E` | 绘制椭圆 |
| **文字** | `T` | 添加文字 |
| **手绘** | `F` | 自由绘图 |
| **表情** | `M` | 插入表情符号 |

## 🛠️ 开发

### 开发要求
- .NET 9.0 SDK
- Visual Studio 2022 或 JetBrains Rider

### 开发环境设置
```bash
git clone https://github.com/AGIBuild/AGI.Captor.git
cd AGI.Captor
dotnet restore
./build.ps1

# 运行应用
dotnet run --project src/AGI.Captor.Desktop
```

### 测试
```bash
# 运行所有测试
./build.ps1 Test

# 运行覆盖率测试
./build.ps1 Test -Coverage
```

## 📄 许可证

本项目基于 [MIT 许可证](LICENSE) 开源。

---

**AGI.Captor** - 让截图标注更简单、更高效！🚀