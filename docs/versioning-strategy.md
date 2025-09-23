# AGI.Captor Versioning Strategy# AGI.Captor 版本策略（锁定时间序列版本体系）



## 📋 Overview## 📋 概述



AGI.Captor uses a **time-based versioning strategy** that provides predictable, chronological version numbering for clear release tracking and dependency management.本项目采用 **“时间序列（Time-based）+ 显式锁定（Locked）+ 标签驱动（Tag-driven）”** 的确定性版本模型（已完全移除 GitVersion 依赖）：



## 🎯 Version Format| 目标 | 方案 |

|------|------|

### Standard Format| 版本生成 | 单次生成并写入 `version.json` |

```| 单一来源 | 锁定文件 `version.json` |

YYYY.M.D.HHmm| 可重复性 | 纯文件可审计，无外部计算工具 |

```| 并行冲突 | UTC 秒级时间戳（冲突概率极低，必要时重新生成） |

| Changelog 分类 | 仅用于 Release Notes 分类，不驱动版本号变化 |

### Examples| 版本语义 | 线性时间序列，放弃主/次/补丁语义判断 |

- `2025.9.23.1200` - September 23, 2025 at 12:00

- `2025.12.1.945` - December 1, 2025 at 09:45> 版本不再“被计算”，而是“被声明并锁定”。流水线只接受与 `version.json` 一致的标签。

- `2024.1.15.1530` - January 15, 2024 at 15:30

## 🔧 版本文件 `version.json`

### Format Rules

- **Year**: Full 4-digit year (e.g., 2025)示例（新四段 Display 格式）：

- **Month**: 1-12 without leading zeros (e.g., 1, 12)  ```json

- **Day**: 1-31 without leading zeros (e.g., 1, 23){

- **Time**: HHMM in 24-hour format (e.g., 0945, 1530)  "version": "2025.9.22.070405",

  "assemblyVersion": "2025.9.22.7",

## 🔧 Version Generation  "fileVersion": "2025.9.22.405",

  "informationalVersion": "2025.9.22.070405"

### Automated Generation}

Versions are automatically generated during the build process based on the current timestamp:```



```powershell规则更新：

# Generate version during build1. `version`（Display）采用四段结构：`YYYY.M.D.HHmm`（月日无前导零，时间固定 4 位）。

.\build.ps1 Build2. `assemblyVersion` = Display（所有版本字段统一）。

3. `fileVersion` = Display（所有版本字段统一）。

# Manual version generation (if needed)4. `informationalVersion` = Display（所有版本字段统一）。

.\build.ps1 Info5. `.csproj` 中对应字段由 Nuke 写回，不得手动编辑。

```6. 发布标签名仍使用 `v<version>`（即四段 Display 版本）。

7. 守卫：Display 正则 + 版本字段一致性。

### Version Consistency

All .NET assembly attributes use the same version:Display 正则：

- `AssemblyVersion````

- `FileVersion`^\d{4}\.[1-9]\d?\.[1-9]\d?\.[0-2]\d[0-5]\d$

- `AssemblyInformationalVersion````

- `PackageVersion`

结构分段（Display 四段）：

## 🏷️ Tagging Strategy```

YYYY . M . D . HHmm

### Release Tags│      │   │    └─ 24h 时间四位（小时两位+分两位）

Release tags follow the format:│      │   └─ 日 (1-31 无前导零)

```│      └─ 月 (1-12 无前导零)

v{version}└─ 年

``````



Examples:派生映射：

- `v2025.9.23.1200````

- `v2025.12.1.945`assemblyVersion = YYYY . M . D . Hour

fileVersion     = YYYY . M . D . (Minute*100 + Second)

### Tag Creationinformational   = Display

```bash```

# Create release tag示例：UTC 2025-09-22 07:04:05

git tag v2025.9.23.1200```

Display         = 2025.9.22.070405

# Push tag to trigger releaseassemblyVersion = 2025.9.22.7

git push origin v2025.9.23.1200fileVersion     = 2025.9.22.(04*100 + 05) = 2025.9.22.405

```informational   = 2025.9.22.070405

```

## 🚀 Integration with CI/CD

### 版本生成逻辑（Nuke 目标 `UpgradeVersion`）

### Workflow Triggers1. 获取当前 UTC 时间。

- **Development builds**: Use timestamp-based versions2. 按格式生成候选版本；若与上次相同秒，做补偿递增。

- **Release builds**: Triggered by version tags3. 计算派生四段版本并写回 `version.json` / `.csproj`。

- **Quality builds**: Use consistent versioning across platforms4. 使用 `--lock` 标记锁定（内部记录防止未授权改写）。

5. 提交该文件；否则 PR 与发布检查会失败。

### GitHub Actions Integration

The versioning system integrates seamlessly with GitHub Actions:## 🌿 分支策略（精简化）



1. **Build Stage**: Version generated from timestamp| 操作 | 要求 |

2. **Package Stage**: Version embedded in package names|------|------|

3. **Release Stage**: Version used for GitHub Release titles and tags| 锁定新版本 | 在 `release` 分支执行 `UpgradeVersion --lock` 并提交 |

| 创建发布标签 | 仅可在 `release` 分支祖先 commit 上打 `v<version>` 标签 |

## 📦 Package Versioning| 功能开发 | `feature/*` 分支开发，合并后再锁定版本 |

| 修复补丁 | 修复合并后重新生成新时间基版本 |

### Package Names

All packages include the full version in their names:> 版本含义与功能规模解耦：更快发布、避免语义主观判断延迟。



```（旧的基于分支+增量示意已废弃）

AGI.Captor-{version}-{platform}.{extension}

```## 🏷️ 版本号格式（Time-based Display）



Examples:```

- `AGI.Captor-2025.9.23.1200-win-x64.msi`YYYY.M.D.HHmm

- `AGI.Captor-2025.9.23.1200-linux-x64.deb````

- `AGI.Captor-2025.9.23.1200-osx-arm64.pkg`

示例：`2025.9.22.1547`

### Version Tracking

- Each release is uniquely identifiable by its timestamp优势：

- Version progression is chronological and predictable- 线性时序即可判定新旧

- No semantic version conflicts or confusion- 不需讨论“是否该 minor/major”

- 解析简单，日志与构件命名直接关联

## 🔍 Benefits

不包含：预发布 / build metadata / hotfix 后缀——额外状态通过 Release Notes 描述；若需要标记内测，使用 GitHub Release `prerelease` flag。若扩展附加信息，可在未来通过 `informationalVersion` 增加 `+meta`。 

### 1. Predictability

- Versions increase chronologically## 📝 Conventional Commits（仅用于分类展示）

- No complex branching or semantic rules

- Easy to understand progression```bash

# 功能增加 → Minor 版本增量

### 2. Uniquenessfeat(ui): add new dashboard layout

- Each build has a unique timestamp# 1.2.3 → 1.3.0

- Collision probability is extremely low

- Clear ordering of releases# 问题修复 → Patch 版本增量  

fix(auth): resolve login timeout issue

### 3. Simplicity# 1.2.3 → 1.2.4

- No semantic versioning complexity

- Straightforward automation# 破坏性变更 → Major 版本增量

- Easy integration with CI/CDfeat(api)!: redesign REST endpoints

# 或在提交正文中包含 "BREAKING CHANGE:"

### 4. Traceability# 1.2.3 → 2.0.0

- Version directly maps to build time```

- Easy correlation with development timeline

- Clear release history### 已废弃标记

`+semver:major|minor|patch|breaking|skip|none` —— 由于不再使用 GitVersion 全部失效，应删除。

## 🛠️ Implementation Details

### 分类引用示例（供 changelog 抓取）

### NUKE Build System Integration```

The versioning system integrates with the NUKE build system:feat: 新增 GPU overlay pipeline

fix: 修复窗口闪烁

```csharprefactor: 抽象渲染调度器接口

// Version generated based on current timestampperf: 降低内存占用 12%

var version = $"{DateTime.UtcNow:yyyy.M.d.HHmm}";docs: 更新 release 流程

build: 合并矩阵并增加 SHA256 清单

// Applied to all relevant MSBuild properties```

MSBuildProject.SetProperty("Version", version);

MSBuildProject.SetProperty("AssemblyVersion", version);## 🔧 常用命令（新版）

MSBuildProject.SetProperty("FileVersion", version);

``````powershell

# 生成并锁定新版本（写入 version.json）

### GitHub Actions Workflow./build.ps1 UpgradeVersion --lock

Workflows use the generated version for:

- Package naming# 验证版本已锁定

- Release creation./build.ps1 CheckVersionLocked

- Artifact organization

- Tag validation# 显示构建信息（含当前锁定版本）

./build.ps1 Info

## 📋 Version Management Workflow

# 创建安装包（示例）

### 1. Development./build.ps1 Package --rids win-x64,linux-x64

```bash```

# Regular development work

git commit -m "feat: add new overlay mode"### 构建系统命令

git push origin feature/new-mode

# → Triggers CI with timestamp version```powershell

```# 获取构建信息（包含版本）

./build.ps1 Info

### 2. Release Preparation

```bash# 清理构建输出

# Merge to release branch./build.ps1 Clean

git checkout release

git merge feature/new-mode# 构建项目

git push origin release./build.ps1 Build

# → Triggers quality workflow

```# 运行测试

./build.ps1 Test

### 3. Release Creation

```bash# 运行测试并生成覆盖率报告

# Create version tag./build.ps1 Test --coverage

git tag v2025.9.23.1200

git push origin v2025.9.23.1200# 发布应用（指定平台）

# → Triggers release workflow./build.ps1 Publish --rids win-x64,linux-x64,osx-x64

```

# 创建安装包

## 🔄 Migration from Semantic Versioning./build.ps1 Package



### Why Time-Based?# 完整的CI构建流程

- **Eliminates ambiguity**: No debate about major/minor/patch./build.ps1 Clean Build Test Publish Package

- **Simplifies automation**: No complex version calculation```

- **Improves consistency**: Same version across all components

- **Reduces conflicts**: Timestamp-based uniqueness### Git 标签与发布



### Transition Benefits```bash

- Cleaner CI/CD pipelinesgit tag v2025.121.915304

- Reduced complexity in build scriptsgit push origin v2025.121.915304

- Better integration with automated workflows

- More predictable release process# 查看所有标签

git tag -l

## 📚 Related Documentation

# 删除标签（如果需要）

- [Build System](build-system.md) - NUKE build integrationgit tag -d v1.4.0

- [Release Workflow](release-workflow.md) - Automated release processgit push origin :refs/tags/v1.4.0

- [Commands Reference](commands-reference.md) - Version commands```

- [GitHub Actions Workflows](../.github/README.md) - CI/CD integration

**发布策略说明**:

---- 仅允许 “锁定版本 + 匹配标签” 发布路径。

*Last updated: September 2025 · Time-based versioning strategy*- 任何与 `version.json` 不一致的标签会在 `release.yml` 失败。

## 🚀 CI/CD 工作流程（高层）

### 开发流程
1. 功能 / 修复分支 → 合并入 `release`
2. CI 验证（测试 / 质量 / 覆盖率）
3. 需要发布时执行：`UpgradeVersion --lock` → 提交

### 发布流程
1. 创建并推送 `v<locked-version>` 标签
2. `release.yml`：祖先校验 + 并发互斥 + 版本匹配
3. 矩阵打包（所有 RID）→ 验证缺失即 fail-fast
4. 生成分类 changelog + `SHASUMS-<ver>.txt`
5. 创建 GitHub Release（禁用自动 notes，使用自生成 body）

详见： [Release Workflow Guide](./release-workflow.md)

## 📊 版本信息获取

### PowerShell 脚本示例

```powershell
function Get-LockedVersion {
  ($json = Get-Content version.json | ConvertFrom-Json) | Out-Null
  Write-Host "Locked Version: $($json.version)"
  return $json.version
}
Get-LockedVersion | Out-Null
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

#### 1. 版本未锁定
执行：`./build.ps1 UpgradeVersion --lock` 并提交。

#### 2. 标签不匹配
删除错误标签：
```bash
git tag -d v2025.121.915304
git push origin :refs/tags/v2025.121.915304
```
确保 `version.json` 正确后重新创建。

#### 3. 祖先校验失败
标签指向 commit 不在 `release` 分支内 → 在正确基准重新打标签。

#### 4. 缺失某平台产物
矩阵某 Job 失败 → 修复后需删除旧标签重新创建。

### 调试工具

#### 本地版本验证
```powershell
Get-Content version.json | ConvertFrom-Json | Format-List
```

#### 构建问题诊断
```powershell
# 清理并重新构建
./build.ps1 Clean Build

# 检查构建输出
./build.ps1 Build --verbosity detailed

# 验证版本注入
dotnet build --verbosity normal | findstr Version
```

## 🎯 最佳实践

### 1. 发布前检查清单
- [ ] `UpgradeVersion --lock` 已执行并提交
- [ ] CI 全绿（测试/质量/安全）
- [ ] `release` 分支为最新且无未提交
- [ ] 差异审阅清晰（上一个标签..HEAD）
- [ ] 无临时/调试文件

### 2. 分支管理策略
- 保持 main 分支的稳定性
- 功能开发使用 `feature/` 前缀分支
- 紧急修复使用 `hotfix/` 前缀分支
- 及时清理已合并的分支

### 3. 标签管理规范
- 标签 = 版本号的唯一绑定
- 删除标签仅在产物错误且需重新发布时执行
- 使用注释标签记录上下文

### 4. 提交消息规范
- 使用前缀（feat|fix|refactor|perf|docs|build|chore|ci|test）
- 破坏性变更在正文说明迁移策略
- 简洁且可读

## 📚 相关文档

- [发布工作流指南](./release-workflow.md)
- [测试架构文档](./testing-architecture.md)
- [构建系统说明](./build-system.md)
- [项目状态报告](./project-status.md)

---

---
最后更新：2025-09-22 · 文档版本：2.0（迁移至锁定时间序列版本体系）