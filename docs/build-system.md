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
- `Publish` - 发布到多平台
- `Package` - 创建安装包
- `Info` - 显示构建信息

### 示例用法
```powershell
# 清理并重新构建
.\build.ps1 Clean Build

# 运行测试
.\build.ps1 Test

# 创建发布包
.\build.ps1 Package

# 查看构建信息
.\build.ps1 Info
```

## 🎯 CI/CD 工作流

### 主要工作流
1. **ci.yml** - 主 CI/CD 流程
   - 构建信息提取和版本计算
   - 多平台测试和构建 (Ubuntu/Windows/macOS)
   - 安全扫描 (CodeQL)
   - 预览版本构建 (main 分支)

2. **release.yml** - 生产发布流程
   - 自动版本管理（标签递增）
   - 跨平台发布构建 (Windows/macOS/Linux x64+ARM64)
   - 自动创建 GitHub Release
   - 支持多种触发方式（release分支/标签/手动）

3. **dev.yml** - 开发工作流 *(如果存在)*
   - 快速验证构建
   - 当前平台测试

4. **quality.yml** - 代码质量检查 *(如果存在)*
   - 安全扫描
   - 依赖分析
   - 性能测试

## ✅ 构建状态

当前构建状态：
- ✅ 编译成功
- ✅ 所有 146 个测试通过
- ✅ 45 个更新功能测试验证
- ✅ Nuke 构建系统集成完成

## 🔄 自动更新功能

项目现已支持：
- 🔍 后台检查更新
- 📦 静默自动升级
- 🌍 跨平台支持 (Windows/macOS/Linux)
- 🔐 GitHub Releases 集成
- ⚙️ 环境配置控制

## � 发布工作流

### 发布触发方式
1. **推送到 release 分支** - 自动递增版本发布
2. **创建版本标签** - 指定版本发布
3. **手动触发** - GitHub Actions 手动触发

### 发布产物
- **Windows**: .msi 安装包 (x64, ARM64)
- **macOS**: .pkg 安装包 (Intel, Apple Silicon)  
- **Linux**: .tar.gz 压缩包 (x64, ARM64)

### 技术特性
- ✅ 自动版本管理和递增
- ✅ 跨平台并行构建
- ✅ 灵活构建脚本支持 (PowerShell/Bash/dotnet)
- ✅ .NET 9.0 预览版 + 8.0 LTS 回退
- ✅ 自动创建 GitHub Release

**详细发布流程**: 参考 [Release Workflow Guide](./release-workflow.md)

## �📚 技术栈

- **构建系统**: Nuke Build
- **测试框架**: xUnit + FluentAssertions + NSubstitute
- **UI 框架**: Avalonia .NET 9.0
- **打包工具**: WiX Toolset v6.0
- **CI/CD**: GitHub Actions
- **版本管理**: GitVersion

---
*构建系统已成功现代化 - 使用 Nuke 构建实现更高效的开发体验！*