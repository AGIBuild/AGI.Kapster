# AGI.Captor Packaging Guide

## 📋 Overview

AGI.Captor provides automated multi-platform packaging through GitHub Actions, supporting Windows (MSI), macOS (PKG), and Linux (DEB/RPM) packages.

## 🎯 Supported Platforms

| Platform | Architecture | Package Format | Build Tool |
|----------|--------------|----------------|------------|
| Windows | x64, ARM64 | MSI | WiX Toolset v4 |
| macOS | Intel (x64), Apple Silicon (ARM64) | PKG | pkgbuild |
| Linux | x64, ARM64 | DEB/RPM | dpkg-deb, rpmbuild |

## 🚀 Automated Packaging

### GitHub Actions Integration
Packaging is fully automated through GitHub Actions workflows:

1. **CI Pipeline**: Basic build verification
2. **Quality Pipeline**: Multi-platform build validation  
3. **Release Pipeline**: Complete package generation and distribution

### Build Commands
```powershell
# Build all platform packages
.\build.ps1 Package

# Build specific platform
.\build.ps1 Package --runtime-id win-x64
.\build.ps1 Package --runtime-id linux-x64
.\build.ps1 Package --runtime-id osx-arm64

# Build with specific configuration
.\build.ps1 Package --configuration Release
```

## 📦 Package Structure

### Output Organization
```
artifacts/
├── publish/               # Runtime-specific binaries
│   ├── win-x64/
│   ├── linux-x64/
│   └── osx-x64/
└── packages/              # Platform-specific installers
    ├── AGI.Captor-{version}-win-x64.msi
    ├── AGI.Captor-{version}-linux-x64.deb
    └── AGI.Captor-{version}-osx-x64.pkg
```

### Package Naming Convention
```
AGI.Captor-{version}-{runtime-id}.{extension}
```

Examples:
- `AGI.Captor-2025.9.23.1200-win-x64.msi`
- `AGI.Captor-2025.9.23.1200-linux-x64.deb`  
- `AGI.Captor-2025.9.23.1200-osx-arm64.pkg`
<sha256>  AGI.Captor-2025.121.915304-win-x64.msi
<sha256>  AGI.Captor-2025.121.915304-win-arm64.msi
...
```
校验示例：
```bash
sha256sum -c SHASUMS-2025.121.915304.txt
```
PowerShell：
```powershell
Get-Content SHASUMS-2025.121.915304.txt | ForEach-Object {
  $p=$_ -split "  "; if((Get-FileHash $p[1] -Algorithm SHA256).Hash.ToLower() -ne $p[0]) { Write-Error "Mismatch: $($p[1])" }
}
```
```

### 可用的运行时标识符 (RIDs)
- `win-x64` - Windows 64位
- `win-arm64` - Windows ARM64
- `osx-x64` - macOS Intel
- `osx-arm64` - macOS Apple Silicon
#### 2. 缺失 RID 产物
发布阶段会检测所有预期 RID 目录是否存在，缺失即失败：
处理：查看对应矩阵 Job 日志，修复后删除并重建标签。
- `linux-arm64` - Linux ARM64

## 构建产物
## 版本管理

版本仅来源于根目录 `version.json`（锁定时间序列格式）。
构建时同步到程序集 / WiX / 包元数据；禁止手动编辑下游文件内版本字段。
```
artifacts/packages/
├── AGI.Captor-1.0.0.0-osx-x64.dmg          # macOS Intel DMG镜像
├── AGI.Captor-1.0.0.0-osx-arm64.dmg        # macOS Apple Silicon DMG
├── AGI.Captor-1.0.0.0-linux-x64.deb        # Linux DEB包
├── AGI.Captor-1.0.0.0-linux-x64.rpm        # Linux RPM包
├── AGI.Captor-1.0.0.0-linux-arm64.deb      # Linux ARM64 DEB
└── AGI.Captor-1.0.0.0-linux-arm64.rpm      # Linux ARM64 RPM
```

## Windows MSI 安装包

### 功能特性
- ✅ **自动升级支持** - 支持同版本覆盖安装和修复
- ✅ **开始菜单快捷方式** - 自动创建程序组和快捷方式
- ✅ **控制面板集成** - 正确显示在"程序和功能"中
- ✅ **唯一标识管理** - 防止重复安装条目
- ✅ **卸载支持** - 完整的卸载功能

### 安装位置
- **程序文件**: `%ProgramFiles%\AGI.Captor\`
- **用户数据**: `%LOCALAPPDATA%\AGI.Captor\` (日志、配置)
- **快捷方式**: `%ProgramData%\Microsoft\Windows\Start Menu\Programs\AGI.Captor\`

### 依赖要求
- WiX Toolset v6.0+ (支持 v4+ 语法)
- .NET 9.0 运行时 (自包含部署)

## macOS 安装包

### PKG 包特性
- 签名和公证支持
- 用户和系统级安装选项
- 卸载脚本集成

### DMG 镜像特性
- 拖拽安装界面
- 背景图片和图标自定义
- 自动挂载和弹出

### 安装位置
- **应用程序**: `/Applications/AGI.Captor.app`
- **用户数据**: `~/Library/Application Support/AGI.Captor/`

## Linux 安装包

### DEB 包 (Debian/Ubuntu)
- 依赖管理和自动解析
- systemd 服务集成
- 桌面文件和图标安装

### RPM 包 (RedHat/CentOS/Fedora)
- 完整的依赖声明
- 安装前后脚本
- SELinux 兼容性

### 安装位置
- **程序文件**: `/opt/AGI.Captor/`
- **用户数据**: `~/.local/share/AGI.Captor/`
- **桌面条目**: `/usr/share/applications/agi-captor.desktop`

## 故障排除

### 常见问题

#### 1. WiX 编译失败
```powershell
# 检查 WiX 版本
wix --version

# 应显示 v6.0.2 或更高版本
# 如果版本过低，请更新 WiX Toolset
```

#### 2. 权限问题
确保构建时具有管理员权限，特别是在 Windows 平台。

#### 3. 签名问题 (macOS)
如需代码签名，请设置以下环境变量：
```bash
export DEVELOPER_ID_APPLICATION="Developer ID Application: Your Name"
export DEVELOPER_ID_INSTALLER="Developer ID Installer: Your Name"
```

#### 4. Linux 依赖问题
确保安装了必要的构建工具：
```bash
# Ubuntu/Debian
sudo apt-get install dpkg-dev rpm

# CentOS/RHEL
sudo yum install rpm-build dpkg
```

## 测试验证

### Windows MSI 测试
```powershell
# 运行 MSI 测试脚本
.\test-msi-duplicate-fix.ps1

# 手动安装测试
msiexec /i "artifacts\packages\AGI.Captor-1.0.0.0-win-x64.msi" /l*v install.log
```

### macOS 测试
```bash
# 验证 PKG 包
installer -pkg AGI.Captor-1.0.0.0-osx-x64.pkg -target /

# 挂载 DMG 并验证
hdiutil attach AGI.Captor-1.0.0.0-osx-x64.dmg
```

### Linux 测试
```bash
# 测试 DEB 包
sudo dpkg -i AGI.Captor-1.0.0.0-linux-x64.deb

# 测试 RPM 包
sudo rpm -i AGI.Captor-1.0.0.0-linux-x64.rpm
```

## 自动化 CI/CD

构建系统已准备好集成到 CI/CD 管道中：

```yaml
# GitHub Actions 示例
- name: Build Packages
  run: |
    .\build.cmd Package --rids "win-x64,osx-x64,linux-x64"
    
- name: Upload Artifacts
  uses: actions/upload-artifact@v3
  with:
    name: packages
    path: artifacts/packages/
```

## 版本管理

版本号在以下位置统一管理：
- `build/Configuration.cs` - 主版本配置
- `src/AGI.Captor.Desktop/AGI.Captor.Desktop.csproj` - 程序集版本
- WiX配置会自动从程序集版本读取

## 更新说明

### v1.0.0.0 更新 (最新)
- ✅ 修复了 MSI 重复安装问题
- ✅ 改进了单文件部署的 Serilog 配置
- ✅ 解决了 Program Files 权限问题
- ✅ 支持同版本覆盖安装和修复功能

---

**注意**: 本文档随项目更新而更新，请定期检查最新版本。