# AGI.Captor 版本计算策略和使用指南

## 📋 概述

AGI.Captor 采用基于 GitVersion 的自动版本计算策略，结合 Git 分支和提交消息来自动生成语义化版本号。

## 🌿 分支策略

### 分支类型和版本规则

| 分支类型 | 分支模式 | 版本标签 | 增量策略 | 示例版本 |
|---------|---------|---------|---------|---------|
| **main** | `main`, `master` | `alpha` | `Minor` | `1.3.0-alpha.1+sha` |
| **feature** | `features/*`, `feature/*` | `branch-name` | `Inherit` | `1.3.0-autoupdate.1+sha` |
| **release** | `release` | *无* | `Auto-increment` | `1.3.1` (从标签自动递增) |
| **hotfix** | `hotfix/*`, `hotfixes/*` | `hotfix` | `Patch` | `1.3.1-hotfix.1+sha` |

### 分支工作流程

```mermaid
gitgraph
    commit id: "Initial"
    branch main
    commit id: "Feature A"
    commit id: "Feature B"
    branch features/new-feature
    commit id: "Work 1"
    commit id: "Work 2"
    checkout main
    merge features/new-feature
    branch release
    commit id: "Release prep"
    commit id: "Version 1.3.0" tag: "v1.3.0"
    commit id: "Version 1.3.1" tag: "v1.3.1"
    checkout main
    merge release
```

**说明**: 
- 使用固定的 `release` 分支而非版本特定分支（如 `release/1.3.0`）
- 版本号通过 Git 标签管理，自动从最新标签递增补丁版本
- Release 分支可以持续接收更新并自动发布新版本

## 🏷️ 版本号格式

### 语义化版本 (SemVer)
```
主版本.次版本.修订版本[-预发布标识符][+构建元数据]
```

### 示例版本号
```bash
# 开发版本 (main分支)
1.3.0-alpha.1+Branch.main.Sha.abc1234

# 功能分支版本
1.3.0-autoupdate.1+Branch.features-autoupdate.Sha.def5678

# 发布版本 (release分支/标签)
1.3.0

# 热修复版本
1.3.1-hotfix.1+Branch.hotfix-critical-fix.Sha.ghi9012
```

## 📝 提交消息控制版本增量

### 提交消息格式
在提交消息中使用特殊标记来控制版本增量：

```bash
# 主版本增量 (破坏性变更)
git commit -m "feat: new API +semver:breaking"
git commit -m "refactor: change interface +semver:major"

# 次版本增量 (新功能)
git commit -m "feat: add auto-update +semver:feature"
git commit -m "feat: new overlay system +semver:minor"

# 修订版本增量 (错误修复)
git commit -m "fix: memory leak issue +semver:fix"
git commit -m "fix: crash on startup +semver:patch"

# 不增量版本
git commit -m "docs: update README +semver:none"
git commit -m "ci: update workflow +semver:skip"
```

## 🔧 常用命令

### GitVersion 相关命令

```powershell
# 获取当前版本信息
dotnet gitversion

# 获取特定版本字段
dotnet gitversion /showvariable SemVer
dotnet gitversion /showvariable FullSemVer
dotnet gitversion /showvariable InformationalVersion

# 输出详细调试信息
dotnet gitversion /verbosity Diagnostic

# 更新程序集版本信息
dotnet gitversion /updateassemblyinfo
```

### 构建系统命令

```powershell
# 获取构建信息（包含版本）
./build.ps1 Info

# 清理构建输出
./build.ps1 Clean

# 构建项目
./build.ps1 Build

# 运行测试
./build.ps1 Test

# 运行测试并生成覆盖率报告
./build.ps1 Test --coverage

# 发布应用（指定平台）
./build.ps1 Publish --rids win-x64,linux-x64,osx-x64

# 创建安装包
./build.ps1 Package

# 完整的CI构建流程
./build.ps1 Clean Build Test Publish Package
```

### Git 标签和发布

```bash
# 创建特定版本标签（推荐方式）
git tag v1.4.0
git push origin v1.4.0  # 使用标签版本发布

# 查看所有标签
git tag -l

# 删除标签（如果需要）
git tag -d v1.4.0
git push origin :refs/tags/v1.4.0
```

**发布策略说明**:
- **标签发布**: 创建版本标签进行精确版本控制（推荐）
- **手动触发**: 在 GitHub Actions 页面手动触发发布

## 🚀 CI/CD 工作流程

### 开发流程 (main分支)
1. **推送到main分支** → 触发 `ci.yml`
2. **自动构建测试** → 生成预览版本
3. **安全扫描** → CodeQL 分析
4. **版本格式**: `1.3.0-alpha.X+sha`

### 发布流程 (版本标签)
1. **方式一: 标签发布** 
   ```bash
   git tag v1.4.0
   git push origin v1.4.0  # 使用指定版本发布
   ```
2. **方式二: 手动触发** → GitHub Actions 页面手动触发
3. **自动构建** → 跨平台构建 (Windows/macOS/Linux)
4. **自动发布** → 创建 GitHub Release
5. **版本格式**: `1.4.0` (正式版本)

**注意**: 不再支持通过推送 release 分支触发发布，必须使用标签或手动触发。

**详细发布流程请参考**: [Release Workflow Guide](./release-workflow.md)

## 📊 版本信息获取

### PowerShell 脚本示例

```powershell
# 获取版本信息的脚本
function Get-VersionInfo {
    $version = dotnet gitversion | ConvertFrom-Json
    
    Write-Host "🏷️ 版本信息"
    Write-Host "=============="
    Write-Host "SemVer: $($version.SemVer)"
    Write-Host "FullSemVer: $($version.FullSemVer)"
    Write-Host "InformationalVersion: $($version.InformationalVersion)"
    Write-Host "AssemblySemVer: $($version.AssemblySemVer)"
    Write-Host "BranchName: $($version.BranchName)"
    Write-Host "Sha: $($version.Sha)"
    Write-Host "ShortSha: $($version.ShortSha)"
    
    return $version
}

# 使用示例
$versionInfo = Get-VersionInfo
```

### 在代码中获取版本

```csharp
// 在 .NET 应用中获取版本信息
using System.Reflection;

// 获取程序集版本
var assembly = Assembly.GetExecutingAssembly();
var version = assembly.GetName().Version;
var informationalVersion = assembly
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
    ?.InformationalVersion;

Console.WriteLine($"Version: {version}");
Console.WriteLine($"Informational: {informationalVersion}");
```

## 🔍 故障排除

### 常见问题和解决方案

#### 1. GitVersion 配置错误
```bash
# 错误: Property 'xxx' not found
# 解决: 检查 GitVersion.yml 语法

# 验证配置
dotnet gitversion /verbosity Diagnostic
```

#### 2. 版本号不正确
```bash
# 检查当前分支和提交
git branch
git log --oneline -5

# 检查 GitVersion 计算
dotnet gitversion /showconfig
```

#### 3. 构建失败
```powershell
# 清理并重新构建
./build.ps1 Clean
./build.ps1 Build
```

## 📚 相关资源

- [GitVersion 官方文档](https://gitversion.net/)
- [语义化版本规范](https://semver.org/lang/zh-CN/)
- [Nuke 构建系统](https://nuke.build/)
- [GitHub Actions 工作流](../.github/workflows/)

## 🎯 最佳实践

1. **分支命名规范**: 使用清晰的分支名称，如 `features/auto-update`
2. **提交消息规范**: 使用约定式提交格式
3. **标签创建**: 仅在release分支创建正式版本标签
4. **版本增量**: 合理使用 `+semver:` 标记控制版本增量
5. **CI/CD**: 充分利用自动化构建和测试流程