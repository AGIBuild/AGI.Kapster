# AGI.Captor 📸

**现代化跨平台截图与标注工具**

基于 .NET 9 和 Avalonia UI 构建的高性能截图工具，支持智能覆盖系统、丰富的标注功能和跨平台兼容。

![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-blue)
![Framework](https://img.shields.io/badge/.NET-9.0-purple)
![UI](https://img.shields.io/badge/UI-Avalonia%2011-green)
![License](https://img.shields.io/badge/license-MIT-orange)
![CI/CD](https://github.com/AGIBuild/AGI.Captor/actions/workflows/ci.yml/badge.svg)

[English](README.md) | [贡献指南](CONTRIBUTING.md)

## ✨ 主要特性

### 🎯 智能截图
- **全局热键**: 可自定义的快速截图快捷键（默认 `Alt+A`）
- **区域选择**: 精确的像素级选择，带有视觉反馈
- **多屏支持**: 跨多个显示器无缝操作
- **元素检测**: 自动UI元素检测（仅Windows）

### 🎨 专业标注工具
- **绘图工具**: 箭头、矩形、椭圆、文字、手绘、表情符号
- **样式自定义**: 颜色、粗细、字体和大小设置
- **撤销重做**: 多步操作历史记录
- **图层管理**: 独立的标注编辑和删除

### 💾 灵活导出选项
- **多种格式**: PNG、JPEG、BMP、TIFF、WebP，支持质量控制
- **快速操作**: 复制到剪贴板（`Enter`/双击）或保存到文件（`Ctrl+S`）
- **批量处理**: 使用一致设置导出多个截图
- **剪贴板集成**: 高级剪贴板操作

### ⚙️ 现代架构
- **后台运行**: 系统托盘集成，资源占用最小
- **跨平台**: 支持Windows、macOS和Linux（计划中）
- **依赖注入**: 清洁架构，全面测试覆盖

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
- **Windows**: `AGI.Captor-win-x64.msi` 或 `AGI.Captor-win-x64.zip`
- **macOS**: `AGI.Captor-osx-x64.pkg` (Intel) 或 `AGI.Captor-osx-arm64.pkg` (Apple Silicon)
- **Linux**: `agi-captor-linux-x64.deb` 或 `agi-captor-linux-x64.rpm`

#### 从源码构建
```bash
git clone https://github.com/AGIBuild/AGI.Captor.git
cd AGI.Captor
./build.ps1                    # 构建和测试
./build.ps1 Publish           # 创建可执行文件
```

### 首次启动

1. **启动应用**: 从开始菜单/应用程序启动或运行可执行文件
2. **授予权限**: 允许屏幕录制权限（macOS）
3. **截图**: 使用 `Alt+A` 热键或点击系统托盘图标
4. **标注**: 使用工具栏添加标注
5. **导出**: 按 `Enter` 复制或 `Ctrl+S` 保存

## 📖 用户指南

### 快捷键命令
| 操作 | 默认快捷键 | 说明 |
|------|-----------|------|
| **开始截图** | `Alt+A` | 启动屏幕截图 |
| **快速导出** | `Ctrl+S` | 保存到文件 |
| **复制到剪贴板** | `Enter` / 双击 | 复制当前截图 |
| **取消操作** | `Escape` | 取消当前操作 |

### 标注工具
| 工具 | 快捷键 | 说明 |
|------|--------|------|
| **箭头** | `A` | 绘制方向箭头 |
| **矩形** | `R` | 绘制矩形框架 |
| **椭圆** | `E` | 绘制椭圆和圆形 |
| **文字** | `T` | 添加文字标注 |
| **手绘** | `F` | 自由绘图工具 |
| **表情** | `M` | 插入表情符号 |

## 🤝 贡献

我们欢迎社区贡献！详细信息请参阅 [CONTRIBUTING.md](CONTRIBUTING.md)。

### 开发环境设置
```bash
# 克隆和构建
git clone https://github.com/AGIBuild/AGI.Captor.git
cd AGI.Captor
dotnet restore
./build.ps1

# 运行应用
dotnet run --project src/AGI.Captor.Desktop
```

### 开发要求
- .NET 9.0 SDK
- Visual Studio 2022 或 JetBrains Rider

## 📄 许可证

本项目基于 [MIT 许可证](LICENSE) 开源。

---

**AGI.Captor** - 让截图标注更简单、更高效！🚀