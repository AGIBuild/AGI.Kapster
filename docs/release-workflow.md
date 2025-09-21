# AGI.Captor 发布流程指南

## 📋 概述

AGI.Captor 采用自动化的发布流程，支持多种发布触发方式和全自动的跨平台构建。本文档详细描述了当前的发布工作流程。

## 🚀 发布触发方式

### 1. 自动创建标签（推荐）
```bash
# 在 GitHub Actions 页面触发 "Create Release Tag" 工作流
# 选择版本增量类型：auto/patch/minor/major
# 系统会自动使用 GitVersion 计算版本号并创建标签
```
- 使用 GitVersion 自动计算版本号，遵循 SemVer 规范
- 支持自动检测提交类型决定版本增量
- 可选择手动指定版本增量类型
- 支持预发布版本和干运行模式

### 2. 手动创建标签
```bash
# 创建版本标签
git tag v1.4.0
git push origin v1.4.0
```
- 使用标签名作为版本号
- 支持预发布标签（包含 alpha、beta、rc、preview 的自动识别为预发布）
- 确保版本号一致性

### 3. 手动触发发布（GitHub Actions）
在 GitHub Actions 页面手动触发 Release 工作流：
- 可指定自定义版本标签
- 可选择是否为预发布版本
- 适用于临时或测试发布

**注意**: 推荐使用自动创建标签的方式，这样可以确保版本号的一致性和规范性。

## 🏗️ 发布流程详解

### 阶段 1: 准备发布 (prepare-release)
- **环境**: Ubuntu Latest
- **功能**: 
  - 确定发布版本号（从标签或手动输入）
  - 设置预发布标志
  - 生成版本号变量供构建使用
  - 输出版本信息供后续阶段使用

**版本确定逻辑**:
```
Create Release Tag 工作流 → GitVersion 计算 → 创建标签 → 触发 Release 构建
手动创建标签 → 使用标签版本
手动触发 → 使用输入的标签
```

### 自动版本计算规则

**GitVersion 增量类型**:
- `auto`: 根据最近提交自动决定增量类型
  - 检测到 `BREAKING CHANGE` 或 `!:` → 主版本增量
  - 检测到 `feat:` → 次版本增量  
  - 其他情况 → 修订版本增量
- `patch`: 强制修订版本增量 (1.0.0 → 1.0.1)
- `minor`: 强制次版本增量 (1.0.0 → 1.1.0)
- `major`: 强制主版本增量 (1.0.0 → 2.0.0)

### 阶段 2: 构建和测试 (build-and-test)
- **环境**: Ubuntu Latest
- **功能**:
  - 完整的构建和单元测试
  - 使用统一的版本号进行构建
  - 生成测试报告和覆盖率报告
  - 支持灵活的构建脚本（build.ps1 → build.sh → dotnet 命令）

**构建脚本优先级**:
1. `./build.ps1` (PowerShell 脚本)
2. `./build.sh` (Bash 脚本)  
3. 直接使用 `dotnet` 命令

### 阶段 3: 多平台构建 (并行执行)

#### Windows 构建 (release-windows)
- **环境**: Windows Latest
- **架构**: x64, ARM64
- **产物**: .msi 安装包
- **工具**: WiX Toolset v4+
- **备用**: ZIP 压缩包

#### macOS 构建 (release-macos)
- **环境**: macOS Latest
- **架构**: x64 (Intel), ARM64 (Apple Silicon)
- **产物**: .pkg 安装包
- **备用**: ZIP 压缩包

#### Linux 构建 (release-linux)
- **环境**: Ubuntu Latest  
- **架构**: x64, ARM64
- **产物**: .tar.gz 压缩包
- **备用**: tar.gz 压缩包

### 阶段 4: 发布到 GitHub (publish-release)
- **环境**: Ubuntu Latest
- **功能**:
  - 下载所有平台构建产物
  - 验证文件版本号一致性
  - 创建 GitHub Release
  - 上传所有安装包
  - 生成发布说明

### 阶段 5: 清理 (cleanup)
- **功能**: 清理中间构建产物
- **保留**: 最终发布的安装包（90天）

## 📦 发布产物

### Windows
- `AGI.Captor-v{version}-win-x64.msi` - Windows 64位安装程序
- `AGI.Captor-v{version}-win-arm64.msi` - Windows ARM64安装程序

### macOS
- `AGI.Captor-v{version}-osx-x64.pkg` - macOS Intel安装程序
- `AGI.Captor-v{version}-osx-arm64.pkg` - macOS Apple Silicon安装程序

### Linux
- `AGI.Captor-v{version}-linux-x64.tar.gz` - Linux 64位压缩包
- `AGI.Captor-v{version}-linux-arm64.tar.gz` - Linux ARM64压缩包

## 🔧 技术要求

### .NET 运行时
- **主要**: .NET 9.0.x (预览版)
- **回退**: .NET 8.0.x (LTS版本)
- **质量**: 支持预览版本

### 构建工具
- **Windows**: WiX Toolset v4+
- **macOS**: Xcode Command Line Tools
- **Linux**: 标准构建工具

### GitHub 权限
- `contents: write` - 创建发布和上传文件
- `id-token: write` - 身份验证

## 🎯 发布最佳实践

### 1. 使用自动版本管理（推荐）
```bash
# 在 GitHub 仓库的 Actions 页面
# 1. 选择 "Create Release Tag" 工作流
# 2. 点击 "Run workflow"
# 3. 选择版本增量类型或使用 "auto"
# 4. 点击 "Run workflow" 开始
```

### 2. 提交消息规范
遵循约定式提交格式，以便 GitVersion 正确计算版本：
```bash
# 修订版本增量
git commit -m "fix: 修复内存泄漏问题"
git commit -m "docs: 更新 README"

# 次版本增量  
git commit -m "feat: 添加自动更新功能"
git commit -m "feat(ui): 新增设置页面"

# 主版本增量
git commit -m "feat!: 重构 API 接口"
git commit -m "feat: 更改配置格式 BREAKING CHANGE: 配置文件格式已更改"
```

### 3. 发布前检查
```bash
# 确保代码已推送到 release 分支
git checkout release
git pull origin release

# 检查 GitVersion 当前计算的版本
dotnet gitversion

# 查看自上次发布以来的更改
git log --oneline $(git describe --tags --abbrev=0)..HEAD
```

### 3. 预发布测试
```bash
# 创建预发布版本进行测试
git tag v1.4.0-beta.1
git push origin v1.4.0-beta.1
```

### 4. 发布验证
发布完成后验证：
- GitHub Release 页面检查所有安装包
- 下载并测试各平台安装包
- 验证自动更新功能

## 🔄 自动更新机制

发布的版本包含自动更新功能：
- **检查频率**: 每24小时检查一次
- **更新方式**: 可配置自动或手动更新
- **支持平台**: Windows, macOS, Linux
- **更新源**: GitHub Releases

## 🐛 故障排除

### 常见问题

1. **WiX 安装失败**
   ```bash
   dotnet tool install --global wix
   ```

2. **构建脚本不存在**
   - 自动回退到 dotnet 命令
   - 检查 build.ps1 或 build.sh 文件

3. **权限不足**
   - 检查 GitHub Token 权限
   - 确认仓库设置允许 Actions 创建 Release

4. **版本冲突**
   - 删除冲突的标签：`git tag -d v1.0.0 && git push origin :refs/tags/v1.0.0`
   - 重新创建正确的标签

### 日志查看
- GitHub Actions 页面查看详细构建日志
- 每个阶段都有独立的日志输出
- 失败时会保留构建产物便于调试

## � Create Release Tag 工作流详解

### 功能特性
- **GitVersion 集成**: 自动计算符合 SemVer 规范的版本号
- **智能增量**: 根据提交消息自动决定版本增量类型
- **安全检查**: 防止创建重复标签
- **干运行模式**: 可以预览版本号而不实际创建标签
- **预发布支持**: 可创建预发布版本标签

### 使用步骤
1. **进入 GitHub Actions 页面**
   - 导航到仓库的 Actions 标签页
   - 选择 "Create Release Tag" 工作流

2. **配置参数**
   - **版本增量类型**: 选择 auto/patch/minor/major
   - **预发布版本**: 勾选以创建预发布版本
   - **干运行**: 勾选以仅预览版本号

3. **运行工作流**
   - 点击 "Run workflow" 按钮
   - 工作流将自动计算版本号并创建标签
   - 标签创建后会自动触发 Release Build & Publish 工作流

### 版本增量示例
```bash
# 当前版本: v1.2.0

# auto 模式 + 最近有 feat: 提交
# 计算结果: v1.3.0

# patch 模式
# 计算结果: v1.2.1

# minor 模式  
# 计算结果: v1.3.0

# major 模式
# 计算结果: v2.0.0

# 预发布模式
# 计算结果: v1.3.0-preview.1
```

### 提交消息影响
Create Release Tag 工作流会分析最近的提交消息来决定版本增量：

| 提交消息模式 | 版本增量 | 示例 |
|-------------|---------|------|
| `feat:` | minor | `feat: 添加新功能` → 1.0.0 → 1.1.0 |
| `fix:` | patch | `fix: 修复bug` → 1.0.0 → 1.0.1 |
| `BREAKING CHANGE:` | major | `feat!: 重构API` → 1.0.0 → 2.0.0 |
| `docs:`, `ci:`, etc. | patch | `docs: 更新文档` → 1.0.0 → 1.0.1 |

## �📈 发布统计

发布产物保留策略：
- **发布安装包**: 永久保留
- **测试结果**: 30天
- **覆盖率报告**: 30天
- **中间构建产物**: 自动清理

---

*最后更新: 2025-09-21*
*文档版本: 2.0*