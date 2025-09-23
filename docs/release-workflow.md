# AGI.Captor Release Workflow# AGI.Captor 发布流程指南



## 📋 Overview## 📋 概述



AGI.Captor uses an automated release workflow powered by GitHub Actions, featuring multi-platform builds, automated packaging, and seamless GitHub Release creation.AGI.Captor 发布流程已升级为 **“锁定版本 + 标签驱动”** 的确定性模型：



## 🚀 Release Process- 不再使用 GitVersion 动态计算版本。

- 版本通过 Nuke 目标 `UpgradeVersion` 生成并写入根目录 `version.json`，随后**锁定**。

### 1. Development Workflow- 创建发布标签前必须确保：标签名 `v<version>` 与 `version.json` 内字段完全一致。

```bash- 仅当推送符合规则的版本标签时才执行完整跨平台发布。

# Feature development on feature branch- 工作流内实施并发互斥、祖先校验、产物完整性与 SHA256 清单验证、分类变更日志生成。

git checkout -b feature/new-overlay-mode

# Make changes and commit## 🔧 核心组件（新版）

git commit -m "feat: add new overlay selection mode"

git push origin feature/new-overlay-mode### 1. 锁定时间序列版本 (Time-based Locked Version)

# Create PR → triggers CI workflow- **来源**: 运行 `./build.ps1 UpgradeVersion --lock`（或对应 Nuke 目标）生成。

```- **文件**: `version.json`（唯一可信源，含统一的版本字段）。

- **Display 格式**: `YYYY.M.D.HHmm` （示例：`2025.9.22.1547`）。

### 2. Release Preparation   - 正则校验：`^\d{4}\.[1-9]\d?\.[1-9]\d?\.[0-2]\d[0-5]\d$`

```bash   - 所有版本字段（`version`/`assemblyVersion`/`fileVersion`/`informationalVersion`）均使用相同值。

# Switch to release branch   - 时间冲突概率极低；若同分钟生成可手动调整或等待。

git checkout release- **锁定机制**: 生成后必须提交。`CheckVersionLocked` / `verify-version` 工作流阻止未锁定或篡改。

git pull origin release

### 2. 工作流

# Merge completed features- **创建标签**: `.github/workflows/create-release.yml` 仅负责读取已经锁定的 `version.json`，验证并创建注释标签 `v<version>`。

git merge feature/new-overlay-mode- **发布构建**: `.github/workflows/release.yml` 由标签触发，执行构建、打包、完整性校验与 GitHub Release 发布。

- **版本校验**: `.github/workflows/verify-version.yml`（PR 守卫），确保 PR 不引入未锁定版本与非法格式。

# Push to trigger quality workflow

git push origin release### 3. 并发与祖先控制

```- 通过 `concurrency: group: release-${{ github.ref }} cancel-in-progress: true` 防止同一标签重复执行。

- 早期步骤校验标签 commit 是否为 `release` 分支可达（祖先校验），防止脱离发布分支的野生标签。

### 3. Release Creation

```bash### 4. 分类变更日志 (Categorized Changelog)

# Create release tag (triggers release workflow)- 解析自上一个版本标签以来的提交消息。

git tag v2025.9.23.1200- 根据前缀（`feat:`, `fix:`, `refactor:`, `perf:`, `docs:`, `build:` 等）分组。

git push origin v2025.9.23.1200- 生成临时文件（`CHANGELOG_BODY.md`）并以 `body_path` 方式传入 `gh release create`，屏蔽 GitHub 自动生成说明。

```

### 5. 产物完整性与清单

## 🎯 Automated Workflows- 验证所有预期 RID 目录是否存在（如：`win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`）。

- 缺失即失败（快速反馈）。

### Main CI Pipeline (`ci.yml`)- 汇总文件至统一目录 `final-release/`。

**Triggers**: Push to any branch, Pull requests- 生成 `SHASUMS-<version>.txt`（按文件名排序）。

**Purpose**: Fast feedback for development- 将 SHA256 清单与发布资产一起上传，并在发布说明中附“Integrity”区块。



```yaml### 6. 已移除内容

Build → Test → Upload Artifacts → Build Preview (PR only)- GitVersion 逻辑与动态增量策略。

```- 重复的 `create-release-tag.yml` 旧工作流。

- 多分散平台 Job：现以矩阵统一生成。

**Features**:

- .NET 9.0 environment setup### 7. 提交规范

- Unit test execution with coverage- 仍建议使用 Conventional Commits，以便分类 changelog 更清晰；但不再驱动版本号。

- Artifact generation and upload

- PR preview builds### 8. 安全

- 限制权限（`contents: write` + 最小化）。

### Quality Assurance (`quality.yml`)  - 校验输入版本与目录结构，拒绝伪造产物。

**Triggers**: Push to `main` branch

**Purpose**: Comprehensive quality validation## 🚀 发布触发方式（新版）



```yaml仅支持“锁定版本 + 匹配标签”路径：

Build → Test → Coverage → Multi-Platform Publish → Upload Artifacts

```1. 运行升级锁定：

```powershell

**Features**:./build.ps1 UpgradeVersion --lock

- Complete test suite executiongit add version.json

- Code coverage reportinggit commit -m "build(version): lock version <new-version>"

- Multi-platform publishing (win-x64, linux-x64, osx-x64)```

- Quality gate validation2. 创建匹配标签（必须从 `release` 分支最新提交或其祖先上执行）

```powershell

### Release Automation (`release.yml`)git tag v<new-version>

**Triggers**: Git tags matching `v*`git push origin v<new-version>

**Purpose**: Production release creation```

3. 推送标签后自动触发 `release.yml`。

```yaml4. 工作流将再次校验：标签 = `version.json`，结构完整，产物齐全。

Multi-Platform Build → Package Creation → GitHub Release → Asset Upload

```（可选）使用 `.github/workflows/create-release.yml` 在 GitHub Actions 中触发“Create Release Tag”——它不会计算版本，只会读取已锁定版本并创建标签。



**Features**:## 🏗️ 发布流程详解（新版）

- Parallel multi-platform builds

- Platform-specific packaging### 阶段 0: 锁定版本

- Automated GitHub Release creation开发者在主仓库执行 `UpgradeVersion`。版本写入并锁定于 `version.json` —— 未提交或修改将被 PR 守卫拒绝。

- Asset upload with checksums

### 阶段 1: 创建标签

## 📦 Build Matrix执行 `.github/workflows/create-release.yml`（或手动本地 tag）：

- 读取 `version.json` → 得到 `<ver>`

### Supported Platforms- 校验本地是否已存在同名标签

| Platform | Runtime ID | Package Format | Notes |- 创建注释标签 `v<ver>` 并推送

|----------|------------|----------------|-------|

| Windows x64 | `win-x64` | MSI | Primary Windows platform |### 阶段 2: 触发发布 (`release.yml`)

| Windows ARM64 | `win-arm64` | MSI | Windows on ARM |事件：`push` 到 `refs/tags/v*`。

| Linux x64 | `linux-x64` | DEB/RPM | Primary Linux platform |- 并发防重：同标签重复触发会被自动取消早期运行。

| Linux ARM64 | `linux-arm64` | DEB/RPM | ARM-based Linux |- 祖先校验：确保标签 commit 位于 `origin/release` 历史之内。

| macOS Intel | `osx-x64` | PKG | Intel Macs |- 读取并复核 `version.json` 与标签一致。

| macOS Apple Silicon | `osx-arm64` | PKG | M1/M2/M3 Macs |

### 阶段 3: 构建与测试

### Package Naming Convention统一 Job 执行核心构建与测试，输出基础工件（中间层）。

```

AGI.Captor-{version}-{runtime-id}.{extension}### 阶段 4: 多 RID 打包（矩阵）

```矩阵包含所有需支持的运行时标识（win/linux/osx × x64/arm64）。

Examples:输出隔离存放：`artifacts/packages/by-rid/<rid>/...`。

- `AGI.Captor-2025.9.23.1200-win-x64.msi`

- `AGI.Captor-2025.9.23.1200-linux-x64.deb`### 阶段 5: 汇总与验证

- `AGI.Captor-2025.9.23.1200-osx-arm64.pkg`- 收集所有矩阵产物

- 验证期望 RID 集是否全部存在

## 🏗️ Workflow Architecture- 生成 `final-release/` 聚合目录

- 计算 SHA256 → 生成 `SHASUMS-<ver>.txt`

### Composite Actions Integration

The release workflow leverages reusable composite actions:### 阶段 6: 生成分类变更日志

- 获取上一个版本标签（若存在）到当前标签之间提交

- **setup-environment**: Complete development environment setup- 按类别分组并写入 `CHANGELOG_BODY.md`

- **build-and-test**: Standardized build and test execution- 附加 Integrity 部分 (hash manifest)

- **publish-package**: Multi-platform publishing and packaging

### 阶段 7: 创建 GitHub Release

### Artifact Flow- 使用 `gh release create v<ver> final-release/* --title "AGI.Captor <ver>" --draft=false --notes-file CHANGELOG_BODY.md`

```- 上传所有安装包与 `SHASUMS-<ver>.txt`

Source Code → Build → Test → Publish → Package → Release

```### 阶段 8: 完成与清理

保留最终产物，清理中间输出。

1. **Build Stage**: Compile for all target platforms

2. **Test Stage**: Execute unit tests with coverage collection### 阶段 2: 自动发布构建

3. **Publish Stage**: Create runtime-specific builds**工作流**: `.github/workflows/release.yml`

4. **Package Stage**: Generate platform installers**触发**: 推送版本标签 (v*.*.*)

5. **Release Stage**: Upload to GitHub Releases

#### 2.1 准备发布 (prepare-release)

## ⚙️ Configuration- **环境**: Ubuntu Latest

- **功能**: 

### Environment Variables  - 从标签解析版本号和预发布状态

```bash  - 标准化版本号格式

# .NET Configuration  - 设置构建环境变量

DOTNET_NOLOGO=true  - 输出版本信息供后续阶段使用

DOTNET_CLI_TELEMETRY_OPTOUT=true

DOTNET_SKIP_FIRST_TIME_EXPERIENCE=true#### 2.2 构建和测试 (build-and-test)

- **环境**: Ubuntu Latest

# Build Configuration- **功能**:

Configuration=Release  - 完整的构建和单元测试流程

```  - 使用统一版本号进行构建

  - 生成测试报告和代码覆盖率

### GitHub Secrets (Optional)  - 支持灵活的构建脚本策略

For enhanced functionality, configure these secrets:

**构建脚本优先级**:

| Secret | Purpose | Required |1. `./build.ps1` (PowerShell 脚本)

|--------|---------|----------|2. `./build.sh` (Bash 脚本)  

| `GITHUB_TOKEN` | Basic GitHub API access | ✅ (auto-provided) |3. 直接使用 `dotnet` 命令

| Additional secrets | Code signing, notarization | ❌ (optional) |

### 阶段 3: 多平台构建 (并行执行)

## 🔄 Version Management

#### Windows 构建 (release-windows)

### Version Strategy- **环境**: Windows Latest

AGI.Captor uses time-based versioning:- **架构**: x64, ARM64

- Format: `YYYY.M.D.HHmm`- **产物**: .msi 安装包

- Example: `2025.9.23.1200`- **工具**: WiX Toolset v4+

- Generated from: Build timestamp- **备用**: ZIP 压缩包

- Consistency: All assemblies use same version

#### macOS 构建 (release-macos)

### Tag Creation- **环境**: macOS Latest

```bash- **架构**: x64 (Intel), ARM64 (Apple Silicon)

# Create version tag- **产物**: .pkg 安装包

git tag v2025.9.23.1200- **备用**: ZIP 压缩包



# Push to trigger release#### Linux 构建 (release-linux)

git push origin v2025.9.23.1200- **环境**: Ubuntu Latest  

```- **架构**: x64, ARM64

- **产物**: .tar.gz 压缩包

## 📊 Release Validation- **备用**: tar.gz 压缩包



### Pre-Release Checks### 阶段 4: 发布到 GitHub (publish-release)

Before creating a release tag:- **环境**: Ubuntu Latest

- **功能**:

1. **Build Verification**  - 下载所有平台构建产物

   ```powershell  - 验证文件版本号一致性

   .\build.ps1 Clean Build Test  - 创建 GitHub Release

   ```  - 上传所有安装包

  - 生成发布说明

2. **Multi-Platform Testing**

   ```powershell### 阶段 5: 清理 (cleanup)

   .\build.ps1 Publish --runtime-id win-x64,linux-x64,osx-x64- **功能**: 清理中间构建产物

   ```- **保留**: 最终发布的安装包（90天）



3. **Package Generation**## 📦 发布产物（新版统一命名示例）

   ```powershell

   .\build.ps1 Package### Windows

   ```- `AGI.Captor-v{version}-win-x64.msi` - Windows 64位安装程序

- `AGI.Captor-v{version}-win-arm64.msi` - Windows ARM64安装程序

### Post-Release Verification

After release creation:### macOS

- `AGI.Captor-v{version}-osx-x64.pkg` - macOS Intel安装程序

1. **GitHub Release Page**: Verify all packages are uploaded- `AGI.Captor-v{version}-osx-arm64.pkg` - macOS Apple Silicon安装程序

2. **Package Integrity**: Check file sizes and checksums

3. **Download Testing**: Test installation on target platforms### Linux

4. **Update Mechanism**: Verify auto-update detection- `AGI.Captor-v{version}-linux-x64.tar.gz` - Linux 64位压缩包

- `AGI.Captor-v{version}-linux-arm64.tar.gz` - Linux ARM64压缩包

## 🔧 Troubleshooting

## 🔧 技术要求

### Common Issues

### .NET 运行时

1. **Build Failures**- **主要**: .NET 9.0.x (预览版)

   - Check .NET version compatibility- **回退**: .NET 8.0.x (LTS版本)

   - Verify NuGet package restoration- **质量**: 支持预览版本

   - Review build logs in GitHub Actions

### 构建工具

2. **Package Creation Failures**- **Windows**: WiX Toolset v4+

   - Ensure platform-specific tools are available- **macOS**: Xcode Command Line Tools

   - Check artifact paths and naming- **Linux**: 标准构建工具

   - Verify runtime-specific dependencies

### GitHub 权限

3. **Release Upload Failures**- `contents: write` - 创建发布和上传文件

   - Verify GitHub token permissions- `id-token: write` - 身份验证

   - Check repository release settings

   - Ensure artifact sizes are within limits## 🎯 发布最佳实践



### Debug Commands### 1. 明确版本唯一来源

```powershell仅 `version.json`；不要手动编辑项目文件内的 AssemblyVersion（构建会同步）。

# Local debugging

.\build.ps1 Build --verbosity diagnostic### 2. 提交消息规范

仍推荐 Conventional Commits（用于 changelog 分类，而非驱动版本号）。

# Test multi-platform locally

.\build.ps1 Publish --runtime-id win-x64 --verbosity normal### 3. 发布前检查

```powershell

# Verify package creationgit checkout release

.\build.ps1 Package --runtime-id win-x64git pull --ff-only

```pwsh ./build.ps1 CheckVersionLocked

pwsh ./build.ps1 UpgradeVersion --dryrun   # （可选：估计下一个时间基版本，不写入）

### Workflow Debugginggit log --oneline $(git describe --tags --abbrev=0)..HEAD

- Review GitHub Actions logs for detailed error messages```

- Check workflow artifact uploads

- Verify composite action execution### 4. 预发布策略

当前模型不鼓励附加 `-beta` 等后缀（时间基版本已保证唯一性）。如需临时测试，可使用分支构建工件而非发布标签。

## 🚦 Quality Gates

### 5. 发布验证

### Automated Checks确认：

- ✅ Unit tests must pass (100%)- Release 页面存在所有 RID 产物 + `SHASUMS-<ver>.txt`

- ✅ Build must succeed on all platforms- 校验：`sha256sum -c SHASUMS-<ver>.txt`（Windows 可用 `Get-FileHash` + 对比）

- ✅ Packages must be generated successfully- 下载主要平台测试启动与更新检查

- ✅ Artifacts must be uploaded correctly

## 🔄 自动更新机制

### Manual Validation

- 📝 Release notes accuracy发布的版本包含自动更新功能：

- 📝 Package installation testing- **检查频率**: 每24小时检查一次

- 📝 Auto-update functionality- **更新方式**: 可配置自动或手动更新

- 📝 Cross-platform compatibility- **支持平台**: Windows, macOS, Linux

- **更新源**: GitHub Releases

## 📈 Performance Metrics

## 🐛 故障排除（新增场景）

### Build Times

- **CI Builds**: ~5-10 minutes### 常见问题

- **Quality Builds**: ~15-20 minutes  

- **Release Builds**: ~25-35 minutes1. **WiX 安装失败**

   ```bash

### Optimizations   dotnet tool install --global wix

- NuGet package caching   ```

- Parallel matrix builds

- Composite action reuse2. **版本不匹配**

- Artifact caching strategies   - 标签 `vX` 与 `version.json` 不一致 → 工作流直接失败

   - 解决：更新并锁定版本后重新打标签

## 📚 Related Documentation

3. **缺失 RID 目录**

- [GitHub Actions Workflows](../.github/README.md) - Complete workflow documentation   - 某个平台打包失败 → 汇总验证阶段失败

- [Build System](build-system.md) - NUKE build system details   - 解决：查看对应矩阵 Job 日志修复，再重新推送标签（删除旧标签后重建）

- [Commands Reference](commands-reference.md) - Build command examples

- [Packaging Guide](packaging-guide.md) - Platform-specific packaging4. **构建脚本不存在**

- [Testing Architecture](testing-architecture.md) - Test strategy and coverage   - 回退 `dotnet build`，检查 `build.ps1` / `build.sh`



---3. **权限不足**

*Last updated: September 2025 · GitHub Actions automation complete*   - 检查 GitHub Token 权限
   - 确认仓库设置允许 Actions 创建 Release

5. **版本回滚需求**
   - 不支持覆盖同标签：需删除旧标签 + 重新创建（历史可见，不建议频繁回滚）

### 日志查看
- GitHub Actions 页面查看详细构建日志
- 每个阶段都有独立的日志输出
- 失败时会保留构建产物便于调试

## 🔖 Create Release Tag 工作流详解（新版）

### 功能特性
- 读取并验证已锁定 `version.json`
- 校验版本格式 & 是否已存在标签
- 创建注释标签（不做版本计算）
- 可选 dry-run（仅验证不推送）

### 使用步骤
1. Actions 页面选择该工作流
2. （可选）启用 dry-run 先做一致性检查
3. 执行后在日志中查看即将创建的 `v<version>`
4. 确认无误后在非 dry-run 模式下执行创建标签

### 不再支持
- 任何“版本增量类型”参数
- 基于提交类型自动推导版本
- 动态预发布序列号

### Changelog 分类逻辑（参考）
| 前缀 | 归类 | 示例 |
|------|------|------|
| feat: | Features | feat: 添加新渲染管线 |
| fix: | Fixes | fix: 修复窗口闪烁 |
| refactor: | Refactors | refactor: 简化 overlay 调度 |
| perf: | Performance | perf: 降低 CPU 占用 |
| docs: | Docs | docs: 更新 release 流程 |
| build: | Build | build(ci): 合并矩阵 |

## �📈 发布统计

发布产物保留策略：
- **发布安装包**: 永久保留
- **测试结果**: 30天
- **覆盖率报告**: 30天
- **中间构建产物**: 自动清理

---

*最后更新: 2025-09-22*
*文档版本: 3.0*