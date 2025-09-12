# 贡献指南

感谢您对 AGI.Captor 的关注！我们欢迎各种形式的贡献，包括但不限于：

- 🐛 报告Bug
- 💡 提出新功能建议
- 📖 改进文档
- 💻 提交代码
- 🌍 本地化翻译

## 🚀 开发环境设置

### 系统要求

- **.NET 9.0 SDK** 或更高版本
- **Visual Studio 2022** (推荐) 或 **JetBrains Rider**
- **Git** 版本控制工具

### 推荐工具

- **Visual Studio 2022** - 完整的IDE支持，包含Avalonia扩展
- **JetBrains Rider** - 跨平台IDE，内置Avalonia支持
- **VS Code** - 轻量级编辑器（需要C#扩展）

### 开发环境配置

1. **克隆仓库**
   ```bash
   git clone https://github.com/your-username/AGI.Captor.git
   cd AGI.Captor
   ```

2. **安装依赖**
   ```bash
   dotnet restore
   ```

3. **构建项目**
   ```bash
   dotnet build
   ```

4. **运行应用**
   ```bash
   dotnet run --project src/AGI.Captor.App
   ```

5. **运行测试**
   ```bash
   dotnet test
   ```

## 📁 项目结构

```
AGI.Captor/
├── src/
│   └── AGI.Captor.App/           # 主应用程序
│       ├── Commands/             # 命令模式实现
│       ├── Dialogs/              # 对话框窗口
│       ├── Models/               # 数据模型
│       ├── Overlays/             # 截图遮罩层
│       ├── Rendering/            # 渲染引擎
│       ├── Services/             # 业务服务
│       ├── ViewModels/           # 视图模型
│       └── Views/                # 用户界面
├── tests/
│   └── AGI.Captor.Tests/         # 单元测试
├── docs/                         # 项目文档
└── README.md
```

### 核心模块说明

- **Commands/**: 命令模式实现，用于撤销/重做功能
- **Models/**: 数据模型，包括标注对象、设置等
- **Overlays/**: 截图界面的实现，包括选区、工具栏等
- **Services/**: 核心业务逻辑，如热键、导出、设置等
- **Rendering/**: 图形渲染引擎，负责标注的绘制

## 🏗️ 架构设计

### 技术栈

- **Framework**: .NET 9.0
- **UI**: Avalonia UI 11.x
- **MVVM**: CommunityToolkit.Mvvm
- **DI**: Microsoft.Extensions.DependencyInjection
- **Logging**: Serilog
- **Graphics**: SkiaSharp
- **Testing**: xUnit

### 设计模式

- **MVVM**: 视图与业务逻辑分离
- **依赖注入**: 服务解耦和测试友好
- **命令模式**: 撤销/重做功能
- **观察者模式**: 事件驱动架构
- **策略模式**: 跨平台适配

### 关键接口

```csharp
// 核心服务接口
public interface IHotkeyProvider        // 热键管理
public interface IOverlayController     // 遮罩控制
public interface IAnnotationService     // 标注服务
public interface IExportService         // 导出服务
public interface ISettingsService       // 设置管理
```

## 🔄 开发流程

### 分支策略

- **main**: 主分支，稳定版本
- **develop**: 开发分支，集成新功能
- **feature/***: 功能分支，单个功能开发
- **bugfix/***: 修复分支，紧急问题修复
- **release/***: 发布分支，版本准备

### 提交规范

使用 [Conventional Commits](https://www.conventionalcommits.org/) 规范：

```
<type>[optional scope]: <description>

[optional body]

[optional footer(s)]
```

**类型 (type)**:
- `feat`: 新功能
- `fix`: 修复Bug
- `docs`: 文档更新
- `style`: 代码格式化
- `refactor`: 重构代码
- `test`: 测试相关
- `chore`: 构建/工具链

**示例**:
```
feat(overlay): add smart region detection
fix(hotkey): resolve conflict with system shortcuts
docs(readme): update installation instructions
```

### Pull Request 流程

1. **Fork 仓库** 到您的账户
2. **创建功能分支** 从 `develop` 分支
   ```bash
   git checkout develop
   git pull origin develop
   git checkout -b feature/your-feature-name
   ```
3. **开发功能** 并提交代码
4. **测试** 确保功能正常工作
5. **提交 PR** 到 `develop` 分支
6. **代码审查** 等待维护者审查
7. **合并** 审查通过后合并

## 🧪 测试指南

### 测试策略

- **单元测试**: 核心业务逻辑
- **集成测试**: 服务间交互
- **UI测试**: 关键用户流程
- **平台测试**: Windows/macOS 兼容性

### 测试约定

- 测试文件命名: `*Tests.cs`
- 测试方法命名: `Should_ExpectedBehavior_When_Condition`
- 使用 AAA 模式: Arrange, Act, Assert

### 运行测试

```bash
# 运行所有测试
dotnet test

# 运行特定测试项目
dotnet test tests/AGI.Captor.Tests

# 生成覆盖率报告
dotnet test --collect:"XPlat Code Coverage"
```

## 📝 代码规范

### C# 编码标准

遵循 [.NET 编码规范](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/inside-a-program/coding-conventions)：

- **命名规范**:
  - PascalCase: 类、方法、属性、枚举
  - camelCase: 字段、局部变量、参数
  - UPPER_CASE: 常量
  - _camelCase: 私有字段

- **代码组织**:
  - 每个文件一个类
  - 使用命名空间组织代码
  - 合理使用 `using` 语句

- **注释规范**:
  - 公共API使用 XML 文档注释
  - 复杂逻辑添加行内注释
  - 避免冗余注释

### XAML 规范

- **命名**: PascalCase 命名控件
- **布局**: 优先使用Grid、StackPanel等标准布局
- **绑定**: 使用强类型绑定
- **资源**: 合理使用样式和模板

### 代码示例

```csharp
/// <summary>
/// 标注服务接口，提供标注功能的核心API
/// </summary>
public interface IAnnotationService
{
    /// <summary>
    /// 当前选中的工具类型
    /// </summary>
    AnnotationToolType CurrentTool { get; set; }
    
    /// <summary>
    /// 开始创建新的标注项
    /// </summary>
    /// <param name="startPoint">起始点坐标</param>
    /// <returns>创建的标注项，如果无法创建则返回null</returns>
    IAnnotationItem? StartCreate(Point startPoint);
}
```

## 🐛 Bug 报告

### 提交Bug前

1. **搜索已有问题** 确保问题未被报告
2. **使用最新版本** 确认问题在最新版本中仍存在
3. **收集信息** 准备详细的复现步骤

### Bug报告模板

```markdown
## Bug 描述
简要描述遇到的问题

## 复现步骤
1. 打开应用
2. 点击...
3. 出现错误

## 预期行为
描述应该发生什么

## 实际行为
描述实际发生了什么

## 环境信息
- 操作系统: Windows 11 / macOS 13.0
- 应用版本: v1.0.0
- .NET版本: 9.0.0

## 附加信息
- 错误截图
- 日志文件
- 其他相关信息
```

## 💡 功能建议

### 提案流程

1. **搜索现有提案** 避免重复
2. **创建 Issue** 使用功能请求模板
3. **社区讨论** 收集反馈
4. **设计评审** 技术可行性分析
5. **开发实现** 分配开发任务

### 功能请求模板

```markdown
## 功能描述
清楚地描述您希望添加的功能

## 问题背景
这个功能要解决什么问题？

## 解决方案
描述您期望的解决方案

## 备选方案
描述您考虑过的其他解决方案

## 附加信息
- 相关截图
- 参考案例
- 技术资料
```

## 🌍 本地化

### 支持的语言

- 简体中文 (zh-CN)
- 英语 (en-US)
- 计划支持: 日语、韩语、法语、德语

### 翻译流程

1. **克隆仓库** 获取最新代码
2. **添加资源文件** 在 `Resources/Localization/` 目录
3. **翻译文本** 保持格式和占位符
4. **测试** 确保UI显示正常
5. **提交PR** 包含翻译文件

### 资源文件格式

```xml
<!-- Resources/Localization/Strings.zh-CN.resx -->
<data name="CaptureRegion" xml:space="preserve">
  <value>区域截图</value>
</data>
```

## 🏆 贡献者指南

### 贡献类型

- **代码贡献**: 新功能、Bug修复、性能优化
- **文档贡献**: README、API文档、教程
- **测试贡献**: 单元测试、集成测试、手动测试
- **设计贡献**: UI/UX设计、图标、动画
- **翻译贡献**: 多语言支持

### 贡献者权益

- **署名**: 贡献者列表中署名
- **徽章**: GitHub Profile 徽章
- **推荐信**: 开源贡献推荐信
- **技术交流**: 参与技术讨论和决策

### 社区准则

- **友善**: 友善对待所有社区成员
- **包容**: 欢迎不同背景和经验的贡献者
- **尊重**: 尊重不同的观点和建议
- **建设性**: 提供建设性的反馈和建议

## 📞 联系方式

- **GitHub Issues**: 技术问题和功能建议
- **GitHub Discussions**: 社区讨论和交流
- **Email**: your-email@example.com

## 📋 任务清单

### 当前优先级

#### 高优先级
- [ ] macOS 平台支持完善
- [ ] 自动更新机制
- [ ] 性能优化
- [ ] 单元测试覆盖率提升

#### 中优先级
- [ ] 更多标注工具 (高亮、马赛克)
- [ ] 批量处理功能
- [ ] 插件系统
- [ ] 云端同步

#### 低优先级
- [ ] 移动端支持
- [ ] 浏览器扩展
- [ ] API接口
- [ ] 第三方集成

## 🎯 开发指南

### 添加新功能

1. **创建接口** 定义服务接口
2. **实现服务** 编写具体实现
3. **注册服务** 在DI容器中注册
4. **编写测试** 确保功能正确
5. **更新文档** 添加使用说明

### 调试技巧

- **日志记录**: 使用Serilog记录关键信息
- **断点调试**: Visual Studio调试器
- **性能分析**: dotTrace性能分析
- **内存分析**: dotMemory内存分析

### 平台适配

- **条件编译**: 使用 `#if` 指令
- **运行时检测**: `RuntimeInformation.IsOSPlatform()`
- **平台服务**: 实现平台特定的服务接口
- **资源适配**: 不同平台使用不同的资源文件

---

再次感谢您的贡献！让我们一起打造更好的AGI.Captor！ 🚀
