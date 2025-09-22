# 🚀 AGI.Captor 构建流程更新说明

## 📋 概览

AGI.Captor 项目已成功升级为使用 **Nuke 构建系统**，实现了更现代化的 CI/CD 流程。

## 🔧 新构建系统特性

### 本地构建命令
```powershell
# Windows (PowerShell)
.\build.ps1 [Target]

# Linux/macOS (Bash)
./build.sh [Target]
```

### 可用构建目标
- `Clean` - 清理构建输出
- `Restore` - 恢复 NuGet 包
- `Build` - 编译项目
- `Test` - 运行所有测试
- `Publish` - 针对指定 RID 发布自包含构建
- `Package` - 创建安装包（输出至 `artifacts/packages/by-rid/<rid>`）
- `Info` - 显示构建信息（含当前锁定版本）
- `UpgradeVersion` - 生成并锁定新时间序列版本（更新 `version.json`）
- `CheckVersionLocked` - 验证版本文件是否已锁定且格式正确

### 示例用法
```powershell
# 基础构建
./build.ps1 Clean Restore Build Test

# 发布特定 RID 自包含包
./build.ps1 Publish --rids win-x64

# 打包多个平台
./build.ps1 Package --rids win-x64,osx-arm64,linux-x64

# 升级并锁定版本
./build.ps1 UpgradeVersion --lock
```

## 🎯 CI/CD 工作流（更新：锁定版本 + 验证式发布 + 可选签名）

| 工作流 | 作用 | 关键特性 |
| ------ | ---- | -------- |
| `ci.yml` | 主分支与 PR 持续集成 | 构建/测试/覆盖率/静态分析，不产生 Release 产物 |
| `verify-version.yml` | PR 版本守卫 | 校验 `version.json` 锁定格式与变更合法性 |
| `release.yml` | 标签驱动发布 | 版本一致性校验、祖先校验、矩阵打包、完整性验证、SHA256 清单、分类变更日志、可选代码签名/公证 |

### 发布流程（手动控制）
1. 在 `release` 分支执行：`./build.ps1 UpgradeVersion --lock`
2. 提交并推送（含更新后的 `version.json`）
3. 创建标签：`git tag v<version>` 并推送
4. 触发 `release.yml`：跨平台构建 + 打包 + （可选）签名 + 校验 + 生成 Release

### 发布阶段安全/一致性验证
- 标签名 == `version.json` 中锁定版本
- 标签指向 commit 必须为 `release` 分支祖先
- 所有预期 RID 目录存在（缺失即失败）
- 生成并发布 `SHASUMS-<version>.txt`（跨平台 SHA256 清单）
- 禁用 GitHub 自动 Release Notes，使用自定义分类变更日志
- （可选）Windows/MSI 签名、macOS codesign、公证

## 🔐 可选代码签名与公证（CI 环境）

`release.yml` 中不会硬编码 Secrets，而是读取以下环境变量：

| 功能 | 环境变量 | 说明 | 触发条件 |
| ---- | -------- | ---- | -------- |
| Windows MSI 签名 | `CODE_SIGN_WINDOWS_PFX_BASE64` | Base64 PFX 内容 | 非空即执行 |
| Windows MSI 签名 | `CODE_SIGN_WINDOWS_PFX_PASSWORD` | PFX 密码 | 同上 |
| macOS codesign | `MACOS_SIGN_IDENTITY` | Developer ID Application 标识 | 非空即执行 |
| macOS notarize | `MACOS_NOTARIZE_APPLE_ID` | Apple 账号 | 三项均非空 |
| macOS notarize | `MACOS_NOTARIZE_PASSWORD` | App-Specific Password | 同上 |
| macOS notarize | `MACOS_NOTARIZE_TEAM_ID` | 团队 ID | 同上 |

只需在 GitHub `Secrets / Actions` 中添加对应条目并映射到环境（Environment / Org / Repo 级）即可启用；删除或留空即自动跳过。

### 本地签名 vs CI 签名
| 场景 | 推荐方式 |
| ---- | -------- |
| 开发调试 | 不签名，直接运行便携包 / 未签名安装包 |
| 预发布内测 | 仅 Windows 签名（减少 Apple 公证等待） |
| 正式发布 | 全量：Windows 签名 + macOS 签名 + 公证 |

### 验证命令示例
```powershell
# Windows
signtool verify /pa /all artifacts\packages\by-rid\win-x64\*.msi
```
```bash
# macOS
codesign --verify --deep --strict --verbose=2 *.app
spctl --assess --type exec -vv *.app
xcrun stapler validate *.pkg
```

## 🧪 质量基线
- 单元/集成测试必须 100% 通过
- 构建脚本仅读取锁定版本（无动态 Git 计算）
- 发布前产物清单与 SHA256 严格匹配

## 🔄 版本管理策略
- 时间序列锁定格式：`YYYY.BBB.PPPPP`（详见 `versioning-strategy.md`）
- 通过 `UpgradeVersion --lock` 原子更新
- 禁止在发布窗口手动编辑 `version.json`

## 🛠 典型问题排查
| 问题 | 可能原因 | 处理 |
| ---- | -------- | ---- |
| 发布失败：版本不匹配 | 标签与文件不一致 | 重新打标签或修复版本文件 |
| 缺少某平台产物 | 打包脚本异常/权限问题 | 查看该矩阵 Job 日志，重试 |
| MSI 未签名 | 未注入签名变量 | 补充 `CODE_SIGN_WINDOWS_*` 变量重新发布 |
| PKG 未公证 | Apple 账号变量缺失 | 注入 3 个 `MACOS_NOTARIZE_*` 变量 |

## 📁 目录结构关键输出
```
artifacts/
  packages/
    by-rid/
      win-x64/
      win-arm64/
      osx-x64/
      osx-arm64/
      linux-x64/
      linux-arm64/
  publish/ (自包含构建输出)
```

## 🔍 相关文档
- `versioning-strategy.md`
- `release-workflow.md`
- `packaging/README.md`
- `testing-architecture.md`

---
最后更新：2025-09-22 · 已集成可选代码签名与公证逻辑