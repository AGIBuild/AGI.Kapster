# 截图服务架构重构分析

## 当前架构问题

### 1. 职责混乱

#### ScreenshotService（平台服务）
- ✅ **应该做**：平台策略（计算虚拟桌面bounds、判断单/多窗口）
- ❌ **不应该做**：
  - 创建窗口（调用 `session.CreateWindowBuilder()`）
  - 捕获背景（`PrecaptureBackgroundAsync`）
  - 调度背景加载（`session.LoadAndSetBackgroundAsync`）

#### OverlaySession（会话管理器）
- ✅ **应该做**：截图会话的完整生命周期协调
- ❌ **当前问题**：
  - 被动接收窗口（`AddWindow`）
  - 被动接收背景（`SetFrozenBackground`）
  - 不拥有捕获能力，依赖外部传入

### 2. 依赖倒置不彻底

```
当前流程：
ScreenshotService (持有 IScreenCaptureStrategy)
    ↓ 创建 Session
    ↓ 捕获背景 (PrecaptureBackgroundAsync)
    ↓ 传递给 Session (LoadAndSetBackgroundAsync)
Session (被动接收)

问题：
- ScreenshotService 需要知道"如何捕获"（持有 _captureStrategy）
- Session 不知道"如何捕获"（只知道如何设置）
- 职责割裂：捕获逻辑在 Service，设置逻辑在 Session
```

### 3. 代码重复

- `WindowsScreenshotService` 和 `MacScreenshotService` 都有类似的窗口创建+背景加载代码
- `LoadAndSetBackgroundAsync` 是对重复代码的封装，但治标不治本

---

## 优化方案：Session 中心化架构

### 核心思想

**Session 应该是截图会话的完整协调者，拥有从窗口创建到背景捕获的所有能力**

```
优化后流程：
ScreenshotService (纯策略)
    ↓ 计算区域 (CalculateTargetRegions)
    ↓ 创建 Session (注入捕获能力)
Session (主动协调)
    ↓ 创建窗口 (CreateWindowWithBackground)
    ↓ 捕获背景 (内部调用 _captureStrategy)
    ↓ 设置窗口背景
    ↓ 初始化 Orchestrator
```

### 架构对比

#### 优化前（当前）

```csharp
// ScreenshotService: 职责过重
public class WindowsScreenshotService
{
    private IScreenCaptureStrategy _captureStrategy;  // 持有捕获能力
    private IScreenCoordinateMapper _coordinateMapper;
    
    protected override async Task CreateAndConfigureWindowsAsync(session, screens, regions)
    {
        var window = session.CreateWindowBuilder()
            .WithBounds(virtualBounds)
            .WithScreens(screens)
            .Build();  // 需要知道如何创建窗口
        
        _ = session.LoadAndSetBackgroundAsync(
            window,
            () => PrecaptureBackgroundAsync(virtualBounds, screen),  // 需要知道如何捕获
            PlatformName);
    }
    
    protected async Task<Bitmap?> PrecaptureBackgroundAsync(bounds, screen)
    {
        var physicalBounds = _coordinateMapper.MapToPhysicalRect(bounds, screen);
        var skBitmap = await _captureStrategy.CaptureRegionAsync(physicalBounds);
        return BitmapConverter.ConvertToAvaloniaBitmapFast(skBitmap);
    }
}

// Session: 被动接收
public class OverlaySession
{
    public OverlaySession(IServiceProvider serviceProvider)  // 没有捕获能力
    {
        _orchestrator = serviceProvider.GetRequiredService<IOverlayOrchestrator>();
    }
    
    public IOverlayWindowBuilder CreateWindowBuilder() { }  // 只提供 Builder
    
    public Task LoadAndSetBackgroundAsync(
        IOverlayWindow window,
        Func<Task<Bitmap?>> loadFunc,  // 外部传入捕获逻辑
        string platformName) { }
}
```

#### 优化后（推荐）

```csharp
// ScreenshotService: 纯策略，职责清晰
public class WindowsScreenshotService
{
    // 不再需要 _captureStrategy 和 _coordinateMapper
    
    protected override async Task CreateAndConfigureWindowsAsync(session, screens, regions)
    {
        var virtualBounds = regions.First();
        
        // 直接调用 Session API：捕获 → 创建 → 设置，一气呵成
        await session.CreateWindowWithBackgroundAsync(virtualBounds, screens);
    }
}

// Session: 主动协调，拥有完整能力
public class OverlaySession
{
    private readonly IScreenCaptureStrategy _captureStrategy;
    private readonly IScreenCoordinateMapper _coordinateMapper;
    
    public OverlaySession(
        IServiceProvider serviceProvider,
        IScreenCaptureStrategy captureStrategy,  // 注入捕获能力
        IScreenCoordinateMapper coordinateMapper)
    {
        _captureStrategy = captureStrategy;
        _coordinateMapper = coordinateMapper;
        _orchestrator = serviceProvider.GetRequiredService<IOverlayOrchestrator>();
    }
    
    /// <summary>
    /// 高级 API：创建窗口并设置背景
    /// 流程：先捕获 → 再创建窗口 → 再设置背景（直线流程，不绕圈）
    /// </summary>
    public async Task<IOverlayWindow> CreateWindowWithBackgroundAsync(
        Rect bounds, 
        IReadOnlyList<Screen> screens)
    {
        // 1. 先捕获背景（预捕获，此时窗口还未创建，避免窗口出现在截图中）
        var background = await CaptureBackgroundAsync(bounds, screens[0]);
        
        // 2. 创建窗口
        var window = CreateWindowBuilder()
            .WithBounds(bounds)
            .WithScreens(screens)
            .Build();
        
        // 3. 设置背景（在 UI 线程）
        if (background != null)
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                window.SetPrecapturedAvaloniaBitmap(background);
                SetFrozenBackground(background);
                Log.Debug("[OverlaySession] Background captured and set");
            });
        }
        
        return window;
    }
    
    /// <summary>
    /// 内部方法：捕获背景
    /// 封装了坐标映射、截图、格式转换的完整流程
    /// </summary>
    private async Task<Bitmap?> CaptureBackgroundAsync(Rect bounds, Screen screen)
    {
        try
        {
            var physicalBounds = _coordinateMapper.MapToPhysicalRect(bounds, screen);
            Log.Debug("[OverlaySession] Capturing background: {PhysicalBounds}", physicalBounds);
            
            var skBitmap = await _captureStrategy.CaptureRegionAsync(physicalBounds);
            if (skBitmap == null)
            {
                Log.Warning("[OverlaySession] Screen capture returned null");
                return null;
            }
            
            return BitmapConverter.ConvertToAvaloniaBitmapFast(skBitmap);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[OverlaySession] Failed to capture background");
            return null;
        }
    }
}
```

---

## 优化收益

### 1. 职责清晰化

| 组件 | 优化前 | 优化后 |
|------|--------|--------|
| **ScreenshotService** | 策略 + 窗口创建 + 背景捕获 | **纯策略**（计算区域） |
| **OverlaySession** | 被动接收窗口和背景 | **主动协调**（完整会话管理） |
| **依赖关系** | Service 持有捕获能力 → 传递给 Session | **Session 持有捕获能力**（自给自足） |

### 2. 代码简化 + 流程优化

```
WindowsScreenshotService.CreateAndConfigureWindowsAsync:
- 优化前：25 行（创建窗口 + 后台捕获 + UI 更新）
- 优化后：2 行（await session.CreateWindowWithBackgroundAsync）
- 减少：92%

MacScreenshotService.CreateWindowForScreenAsync:
- 优化前：28 行
- 优化后：2 行
- 减少：93%

流程优化：
- 优化前：创建窗口 → 异步捕获背景 → 设置（绕一圈）
- 优化后：捕获背景 → 创建窗口 → 设置（直线流程）
```

### 3. 可测试性提升

```csharp
// 优化前：需要 Mock ScreenshotService 的 PrecaptureBackgroundAsync
// 优化后：直接测试 Session.CreateWindowWithBackground

[Fact]
public async Task Session_CreateWindowWithBackground_ShouldCaptureAndSetBackground()
{
    // Arrange
    var mockCaptureStrategy = Substitute.For<IScreenCaptureStrategy>();
    var mockCoordinateMapper = Substitute.For<IScreenCoordinateMapper>();
    var session = new OverlaySession(serviceProvider, mockCaptureStrategy, mockCoordinateMapper);
    
    // Act
    var window = session.CreateWindowWithBackground(bounds, screens);
    
    // Assert
    mockCaptureStrategy.Received(1).CaptureRegionAsync(Arg.Any<Rect>());
}
```

### 4. 符合 SOLID 原则

- **Single Responsibility**：Session 负责会话，Service 负责策略
- **Dependency Inversion**：Session 依赖抽象（IScreenCaptureStrategy），不依赖具体实现
- **Interface Segregation**：Session 提供高级 API，隐藏内部复杂性

---

## 实施计划

### Phase 1: 重构 Session（核心）
1. ✅ Session 构造函数注入 `IScreenCaptureStrategy` 和 `IScreenCoordinateMapper`
2. ✅ 添加 `CreateWindowWithBackgroundAsync(Rect, IReadOnlyList<Screen>)` 高级 API（async）
3. ✅ 实现内部方法 `CaptureBackgroundAsync`（先捕获，再创建，再设置）
4. ✅ 移除 `LoadAndSetBackgroundAsync`（被新 API 取代）

### Phase 2: 重构 ScreenshotService（简化）
1. ✅ `WindowsScreenshotService.CreateAndConfigureWindowsAsync` 简化为 `await session.CreateWindowWithBackgroundAsync`
2. ✅ `MacScreenshotService.CreateWindowForScreenAsync` 简化为 `await session.CreateWindowWithBackgroundAsync`
3. ✅ 移除 `PrecaptureBackgroundAsync` 方法（逻辑移到 Session.CaptureBackgroundAsync）
4. ✅ 移除 `_captureStrategy` 和 `_coordinateMapper` 字段（不再需要）

### Phase 3: 更新 Factory（依赖注入）
1. ✅ `OverlaySessionFactory.CreateSession` 传递 `IScreenCaptureStrategy` 和 `IScreenCoordinateMapper`
2. ✅ 从 `IServiceProvider` 解析这些依赖

### Phase 4: 验证
1. ✅ 编译验证
2. ✅ 单元测试
3. ✅ 集成测试

---

## 预期结果

✅ **Session 成为真正的"截图会话中心"**
- 完全控制窗口创建、背景捕获、用户交互
- 拥有完整的能力（不依赖外部传入）

✅ **ScreenshotService 简化为"纯策略层"**
- 只负责平台特定的区域计算
- 不需要知道窗口如何创建、背景如何捕获

✅ **代码质量提升**
- 减少重复代码 ~80%
- 提高可测试性
- 符合 SOLID 原则

✅ **易于扩展**
- 新增平台：只需实现区域计算逻辑
- 新增捕获策略：Session 透明支持（依赖注入）

