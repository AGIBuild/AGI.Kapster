# AGI.Captor 常用命令快速参考

## 🚀 快速开始

```powershell
# 克隆项目
git clone https://github.com/AGIBuild/AGI.Captor.git
cd AGI.Captor

# 获取项目信息
./build.ps1 Info

# 完整构建
./build.ps1 Clean Build Test
```

## 🔧 构建命令

### 基础构建命令
```powershell
# 清理构建输出
## 🏷️ 版本管理（锁定时间序列模型）

### 基本操作
```powershell
# 升级并锁定版本（写入 version.json，三段展示 + 派生四段 assembly/file）
./build.ps1 UpgradeVersion --lock

# 查看锁定版本
Get-Content version.json | ConvertFrom-Json | Select-Object version,assemblyVersion,fileVersion,informationalVersion

# 仅查看展示版本
(Get-Content version.json | ConvertFrom-Json).version
```

### 版本字段说明
```text
version               -> 展示版 (YYYY.MDD.Hmmss)
assemblyVersion       -> 派生四段 (YYYY.(M*100+D).H.(m*100+s))
fileVersion           -> 同 assemblyVersion
informationalVersion  -> 与 version 一致（可扩展附加 build metadata）
```

### 示例
```
version: 2025.922.90115
assemblyVersion: 2025.922.9.115
fileVersion: 2025.922.9.115
informationalVersion: 2025.922.90115
```

### 常见检查
```powershell
# 验证派生规则（简单快速）
$j = Get-Content version.json | ConvertFrom-Json
$v = $j.version.Split('.')
$year = [int]$v[0]; $mdd=[int]$v[1]; $hmmss=[int]$v[2]
$hour = [int]($hmmss.ToString().Substring(0, if($hmmss -ge 100000){2}else{1}))
$mmss = $hmmss.ToString().Substring($hour -lt 10 ? 1 : 2)
$minute = [int]$mmss.Substring(0,2); $sec=[int]$mmss.Substring(2,2)
$derived = "$year.$mdd.$hour." + ($minute*100 + $sec)
if($derived -ne $j.assemblyVersion){ Write-Host "❌ 派生不匹配" } else { Write-Host "✅ 派生匹配" }
```

### 组合命令
```powershell
# 完整的开发构建
./build.ps1 Clean Build Test

# 完整的发布构建
./build.ps1 Clean Build Test Publish Package

# 仅构建和测试（快速验证）
./build.ps1 Build Test --skip-slow-tests
```

### 平台特定构建
### 提交规范
```bash
# 功能提交
git commit -m "feat: add auto-update feature"

# 修复提交
git commit -m "fix: resolve memory leak"

# 破坏性变更（正文解释迁移）
git commit -m "feat!: new API design" -m "BREAKING: 旧 API 将在下版本移除"

# 文档更新
git commit -m "docs: update README"
```
./build.ps1 Publish --rids win-x64,linux-x64,osx-x64,osx-arm64
# 1. 生成预览构建 (未改变锁定 version.json)

# 3. 构建使用锁定的时间序列版本

# 2. 修复问题并提交
git commit -m "fix: critical security issue"
```powershell
dotnet gitversion

# 获取特定版本字段
dotnet gitversion /showvariable SemVer
dotnet gitversion /showvariable FullSemVer
dotnet gitversion /showvariable InformationalVersion
dotnet gitversion /showvariable Major
dotnet gitversion /showvariable Minor
dotnet gitversion /showvariable Patch

# 显示配置信息
dotnet gitversion /showconfig

# 详细调试信息
dotnet gitversion /verbosity Diagnostic
```

### 版本字段说明
```powershell
# 常用版本字段
SemVer                 # 1.3.0-alpha.1
FullSemVer            # 1.3.0-alpha.1+Branch.main.Sha.abc1234
InformationalVersion  # 1.3.0-alpha.1+Branch.main.Sha.abc1234
AssemblySemVer        # 1.3.0.0
MajorMinorPatch       # 1.3.0
BranchName           # main
Sha                  # abc1234567890
ShortSha             # abc1234
```

## 🌿 Git 工作流

### 分支操作
```bash
# 创建功能分支
git checkout -b features/new-feature

# 创建发布分支
git checkout -b release/1.3.0

# 创建热修复分支
git checkout -b hotfix/critical-fix

# 切换到主分支
git checkout main

# 删除本地分支
git branch -d features/old-feature

# 删除远程分支
git push origin --delete features/old-feature
```

### 标签操作
```bash
# 创建标签
git tag v1.3.0

# 创建带注释的标签
git tag -a v1.3.0 -m "Release version 1.3.0"

# 推送标签
git push origin v1.3.0

# 推送所有标签
git push origin --tags

# 删除本地标签
git tag -d v1.3.0

# 删除远程标签
git push origin --delete v1.3.0
```

### 提交规范
```bash
# 功能提交

# 修复提交

# 破坏性变更

# 文档更新（不增量版本）
```

## 🧪 测试命令

### 单元测试
```powershell
# 运行所有测试
./build.ps1 Test

# 运行特定测试项目
dotnet test tests/AGI.Captor.Tests/

# 运行特定测试类
dotnet test --filter "ClassName=UpdateServiceTests"

# 运行特定测试方法
dotnet test --filter "MethodName=ShouldCheckForUpdates"

# 详细输出
dotnet test --verbosity normal
```

### 覆盖率测试
```powershell
# 生成覆盖率报告
./build.ps1 Test --coverage

# 查看覆盖率报告
start artifacts/coverage/index.html

# 仅生成覆盖率数据
dotnet test --collect:"XPlat Code Coverage"
```

## 📦 打包命令

### 应用打包
```powershell
# 创建所有平台安装包
./build.ps1 Package

# Windows MSI
./build.ps1 Package --runtime win-x64

# Linux DEB
./build.ps1 Package --runtime linux-x64 --format deb

# Linux RPM
./build.ps1 Package --runtime linux-x64 --format rpm

# macOS PKG
./build.ps1 Package --runtime osx-x64 --format pkg

# macOS App Store
./build.ps1 Package --runtime osx-x64 --format appstore
```

### 手动打包
```bash
# Windows
cd packaging/windows
dotnet build AGI.Captor.wixproj

# Linux DEB
cd packaging/linux
./create-deb.sh

# Linux RPM
cd packaging/linux
./create-rpm.sh

# macOS PKG
cd packaging/macos
./create-pkg.sh

# macOS App Store
cd packaging/macos
./create-appstore.sh
```

## 🔍 调试命令

### 日志查看
```powershell
# 查看应用日志
Get-Content logs/app-*.log -Tail 50

# 实时监控日志
Get-Content logs/app-*.log -Wait

# 查看构建日志
Get-Content artifacts/logs/build.log
```

### 诊断信息
```powershell
# 系统信息
dotnet --info

# 环境变量
Get-ChildItem Env: | Where-Object Name -like "*DOTNET*"

# 工具版本
dotnet tool list --global
dotnet tool list --local
```

## 🚀 发布流程

### 开发发布（预览版）
```bash
# 1. 推送到main分支
git push origin main

# 2. GitHub Actions 自动构建
# 3. 生成预览版本 (1.3.0-alpha.X)
```

### 正式发布
```bash
# 1. 创建发布分支
git checkout -b release/1.3.0
git push origin release/1.3.0

# 2. 创建发布标签
git tag v1.3.0
git push origin v1.3.0

# 3. GitHub Actions 自动发布
# 4. 生成正式版本 (1.3.0)
```

### 热修复发布
```bash
# 1. 从主分支创建热修复分支
git checkout -b hotfix/critical-fix

# 2. 修复问题并提交

# 3. 推送分支
git push origin hotfix/critical-fix

# 4. 合并到main和release分支
git checkout main
git merge hotfix/critical-fix
git checkout release/1.3.0
git merge hotfix/critical-fix

# 5. 创建热修复标签
git tag v1.3.1
git push origin v1.3.1
```

## 📚 一键脚本

### 创建便捷脚本
```powershell
# scripts/dev-build.ps1
./build.ps1 Clean Build Test --coverage
if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ 开发构建成功!" -ForegroundColor Green
    start artifacts/coverage/index.html
} else {
    Write-Host "❌ 构建失败!" -ForegroundColor Red
}

# scripts/release-build.ps1
./build.ps1 Clean Build Test Publish Package
if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ 发布构建成功!" -ForegroundColor Green
    Get-ChildItem artifacts/publish/
} else {
    Write-Host "❌ 构建失败!" -ForegroundColor Red
}
```


### 性能优化
```powershell
# 并行构建
./build.ps1 Build --parallel

# 跳过慢速测试
./build.ps1 Test --skip-slow-tests

# 仅构建特定项目
dotnet build src/AGI.Captor.Desktop/
```

---

💡 **提示**: 将常用命令添加到 PowerShell 配置文件中，创建别名以提高效率：

```powershell
# 添加到 $PROFILE
New-Alias -Name build -Value "./build.ps1"
```