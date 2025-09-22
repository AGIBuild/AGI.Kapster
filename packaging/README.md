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

### 代码签名（本地打包参数）

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

### CI 环境中的签名与公证（GitHub Actions）

在 `release.yml` 中，签名与公证是“可选的”——仅当以下环境变量（通过仓库 / 环境 / 组织级 Secret 注入）存在且非空时才执行：

| 功能 | 环境变量 | 说明 |
| ---- | -------- | ---- |
| Windows MSI 签名 | `CODE_SIGN_WINDOWS_PFX_BASE64` | Base64 编码的 PFX 证书内容 |
| Windows MSI 签名 | `CODE_SIGN_WINDOWS_PFX_PASSWORD` | PFX 证书密码 |
| macOS codesign | `MACOS_SIGN_IDENTITY` | 例如：`Developer ID Application: Example Corp (TEAMID)` |
| macOS notarize | `MACOS_NOTARIZE_APPLE_ID` | Apple 开发者账号（邮箱） |
| macOS notarize | `MACOS_NOTARIZE_PASSWORD` | App-Specific Password 或 Keychain Profile 密码 |
| macOS notarize | `MACOS_NOTARIZE_TEAM_ID` | 10位团队 ID |

#### Secret 注入示例
在仓库 `Settings -> Secrets -> Actions` 中添加：
- `CODE_SIGN_WINDOWS_PFX_BASE64`
- `CODE_SIGN_WINDOWS_PFX_PASSWORD`
- `MACOS_SIGN_IDENTITY`
- `MACOS_NOTARIZE_APPLE_ID`
- `MACOS_NOTARIZE_PASSWORD`
- `MACOS_NOTARIZE_TEAM_ID`

然后在环境或组织层配置（如需多仓库复用）。无需修改工作流文件即可启用/停用签名。

#### 生成 Windows PFX Base64
```bash
# 将 PFX 证书转为 Base64（GitHub Secret 只放单行字符串）
base64 -w0 code-signing.pfx > pfx.b64   # Linux/macOS
# PowerShell
[Convert]::ToBase64String([IO.File]::ReadAllBytes('code-signing.pfx')) > pfx.b64
```

#### 验证 MSI 签名
```powershell
signtool verify /pa /all AGI.Captor-*.msi
```

#### 验证 macOS 签名 & 公证
```bash
codesign --verify --deep --strict --verbose=2 AGI.Captor.app
spctl --assess --type exec -vv AGI.Captor.app
xcrun stapler validate AGI.Captor-*.pkg
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
   - 确保证书已安装（本地）或 CI 已注入变量

3. **公证长时间等待**
   - Apple 服务高峰期，可重试或拆分提交

4. **Linux 包创建失败**
   - 确保安装了必要的打包工具
   - 检查脚本执行权限

### 调试模式
```bash
.\build.cmd Package --rids win-x64 --verbosity detailed
```

## 自动化集成

### GitHub Actions
示例（简化）：
```yaml
- name: Create Packages
  run: .\build.cmd Package --rids win-x64,osx-x64,linux-x64 --configuration Release

- name: Upload Artifacts
  uses: actions/upload-artifact@v4
  with:
    name: packages
    path: artifacts/packages/
```

## 安全注意事项

1. **证书**: 仅在受控环境中使用，避免泄露
2. **变量注入**: 使用 GitHub Encrypted Secrets；不要提交到仓库
3. **最小权限**: Apple ID 建议使用专用账号 + App-Specific Password
4. **日志审查**: 签名步骤不回显敏感值

## 相关文件

- `packaging/windows/`: Windows 安装包配置
- `packaging/macos/`: macOS 安装包脚本
- `packaging/linux/`: Linux 安装包脚本
- `build/BuildTasks.cs`: 构建系统配置
- `.github/workflows/release.yml`: 发布与可选签名/公证

## 更多信息

查看各平台特定的配置文件和脚本了解详细设置。