# AGI.Captor 安装包创建指南

## 概述

AGI.Captor 现已支持跨平台安装包创建，包括 Windows MSI、macOS PKG/DMG 和 Linux DEB/RPM 包。

## 快速开始

### 创建所有平台的安装包
```powershell
.\build.cmd Package
```

### 创建特定平台的安装包
```powershell
# Windows 64位 (生成 MSI 安装包)
.\build.cmd Package --rids win-x64

# Windows ARM64
.\build.cmd Package --rids win-arm64

# macOS Intel
.\build.cmd Package --rids osx-x64

# macOS Apple Silicon
.\build.cmd Package --rids osx-arm64

# Linux 64位
.\build.cmd Package --rids linux-x64

# 创建 Windows ARM64 版本
.\build.cmd Package --rids win-arm64

# 创建所有 Windows 版本
.\build.cmd Package --rids win-x64,win-arm64
```

### 创建 macOS 安装包
```bash
# 创建 macOS Intel 版本
.\build.cmd Package --rids osx-x64

# 创建 macOS Apple Silicon 版本  
.\build.cmd Package --rids osx-arm64

# 创建通用 macOS 版本
.\build.cmd Package --rids osx-x64,osx-arm64
```

### 创建 Linux 安装包
```bash
# 创建 Linux x64 版本
.\build.cmd Package --rids linux-x64

# 创建 Linux ARM64 版本
.\build.cmd Package --rids linux-arm64
```

## 安装包类型

### Windows 平台
- **便携版 ZIP**: 无需安装，解压即用
- **MSI 安装包**: 需要 WiX Toolset（未检测到时自动创建 ZIP）
- **MSIX 包**: 适用于 Windows 10/11 商店分发

### macOS 平台
- **PKG 安装包**: 系统原生安装程序
- **DMG 镜像**: 拖拽安装包
- **应用签名和公证**: 支持 Apple 开发者签名

### Linux 平台
- **DEB 包**: 适用于 Debian/Ubuntu
- **RPM 包**: 适用于 RedHat/SUSE/Fedora
- **AppImage**: 通用 Linux 可执行包

## 高级配置

### 代码签名

#### Windows 代码签名
```bash
.\build.cmd Package --rids win-x64 \
  --windows-signing-thumbprint "证书指纹" \
  --windows-signing-password "密码"
```

#### macOS 应用签名
```bash
.\build.cmd Package --rids osx-x64 \
  --mac-signing-identity "Developer ID Application: Your Name"
```

#### macOS 公证
```bash
.\build.cmd Package --rids osx-x64 \
  --mac-signing-identity "Developer ID Application: Your Name" \
  --apple-id "your.apple.id@example.com" \
  --app-password "app-specific-password" \
  --team-id "TEAM123456"
```

### 自定义版本
```bash
.\build.cmd Package --rids win-x64 \
  --configuration Release
```

## 输出目录

所有安装包将输出到：
```
artifacts/packages/
├── AGI.Captor-1.0.0.0-win-x64-portable.zip
├── AGI.Captor-1.0.0.0-win-x64.msi
├── AGI.Captor-1.0.0.0-osx-x64.pkg
├── AGI.Captor-1.0.0.0-osx-x64.dmg
├── agi-captor_1.0.0.0_amd64.deb
└── agi-captor-1.0.0.0-1.x86_64.rpm
```

## 依赖要求

### Windows
- .NET 9.0 SDK
- WiX Toolset v3.x 或 v4.x（用于 MSI）
- Windows SDK（用于代码签名）

### macOS
- Xcode Command Line Tools
- Apple 开发者账户（用于签名和公证）

### Linux
- dpkg-deb（用于 DEB 包）
- rpmbuild（用于 RPM 包）
- fakeroot

## 故障排除

### 常见问题

1. **WiX Toolset 未找到**
   - 安装 WiX Toolset
   - 或者使用便携版 ZIP 包

2. **macOS 签名失败**
   - 检查签名身份是否正确
   - 确保证书已安装在钥匙串中

3. **Linux 包创建失败**
   - 确保安装了必要的打包工具
   - 检查脚本执行权限

### 调试模式
```bash
.\build.cmd Package --rids win-x64 --verbosity detailed
```

## 自动化集成

### GitHub Actions
可以集成到 CI/CD 流水线中：

```yaml
- name: Create Packages
  run: |
    .\build.cmd Package --rids win-x64,osx-x64,linux-x64 --configuration Release
    
- name: Upload Artifacts
  uses: actions/upload-artifact@v3
  with:
    name: packages
    path: artifacts/packages/
```

## 安全注意事项

1. **签名证书**: 妥善保管代码签名证书
2. **公证凭据**: 使用环境变量存储敏感信息
3. **发布渠道**: 通过官方渠道分发签名包

## 相关文件

- `packaging/windows/`: Windows 安装包配置
- `packaging/macos/`: macOS 安装包脚本
- `packaging/linux/`: Linux 安装包脚本
- `build/BuildTasks.cs`: 构建系统配置

## 更多信息

查看各平台特定的配置文件和脚本了解详细设置。