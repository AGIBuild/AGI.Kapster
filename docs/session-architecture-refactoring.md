# Session 架构重构方案

## 背景

当前架构中 Session 的职责过重，Window 对 Session 存在不合理的依赖，导致：
- Window 持有 Session 引用（违反 "Session 拥有 Window" 的原则）
- 事件订阅分散在 Coordinator 和 Window 中（职责不清）
- 跨窗口选择状态管理是过度设计（实际场景不需要）

## 优化目标

1. **职责清晰**：Session 职责单一，仅管理生命周期和事件
2. **依赖正确**：Session 拥有 Window，Window 不依赖 Session
3. **架构简化**：移除过度设计，简化代码
4. **解耦事件**：事件订阅集中在 Session，减少 Coordinator 和 Window 的耦合

---

## 架构设计

### 核心原则

```
┌─────────────────────────────────────────┐
│         Coordinator (协调者)             │
│  - 创建 Session                          │
│  - 订阅 Session 事件（统一接口）         │
│  - 防止重复创建 Session                  │
└─────────────────────────────────────────┘
                │
                │ creates & subscribes
                ↓
┌─────────────────────────────────────────┐
│         Session (事件中心)               │
│  - 管理 Window 生命周期                  │
│  - 统一订阅 Window 事件                  │
│  - 转发事件给 Coordinator                │
│  - 移除跨窗口选择状态管理                │
└─────────────────────────────────────────┘
                │
                │ owns & subscribes
                ↓
┌─────────────────────────────────────────┐
│         OverlayWindow (纯 UI)            │
│  - 管理 UI 渲染和交互                    │
│  - 暴露公共事件                          │
│  - 不持有 Session 引用                   │
└─────────────────────────────────────────┘
                │
                │ uses
                ↓
┌─────────────────────────────────────────┐
│         Handlers (内部组件)              │
│  - SelectionHandler                      │
│  - AnnotationHandler                     │
│  - CaptureHandler                        │
└─────────────────────────────────────────┘
```

### 分层职责

#### 1. Coordinator 层（防止重复 + 统一订阅）

**职责**：
- 维护当前活动 Session 引用
- 防止重复创建 Session
- 订阅 Session 级别事件（非 Window 事件）
- 处理业务逻辑（保存截图、关闭 Session）

**关键实现**：
```csharp
private IOverlaySession? _currentSession;
private readonly object _sessionLock = new object();

public async Task<IOverlaySession> StartSessionAsync()
{
    lock (_sessionLock)
    {
        if (_currentSession != null)
        {
            Log.Warning("Session already active, ignoring duplicate request");
            return _currentSession;
        }
    }
    
    var session = _sessionFactory.CreateSession();
    
    // 订阅 Session 事件（而非 Window 事件）
    session.RegionSelected += OnRegionSelected;
    session.Cancelled += OnCancelled;
    
    // ...
}
```

#### 2. Session 层（生命周期 + 事件中心）

**职责**：
- 管理 Window 生命周期（AddWindow, RemoveWindow, ShowAll, CloseAll）
- 统一订阅所有 Window 事件
- 转发事件给外部（Coordinator）
- **移除**：跨窗口选择状态管理（过度设计）

**简化内容**：
```csharp
// 移除以下方法和属性
- bool CanStartSelection(object window)
- void SetSelection(object window)
- void ClearSelection(object? window = null)
- event Action<bool>? SelectionStateChanged
- bool HasSelection
- object? ActiveSelectionWindow
```

**新增内容**：
```csharp
// 添加事件转发
public event Action<RegionSelectedEventArgs>? RegionSelected;
public event Action<OverlayCancelledEventArgs>? Cancelled;

private void SubscribeToWindowEvents(Window window)
{
    if (window is IOverlayWindow overlayWindow)
    {
        overlayWindow.RegionSelected += OnWindowRegionSelected;
        overlayWindow.Cancelled += OnWindowCancelled;
    }
}
```

#### 3. Window 层（纯 UI）

**职责**：
- 管理 UI 渲染和用户交互
- 暴露公共事件（RegionSelected, Cancelled）
- 内部协调 Handler 组件
- **不持有 Session 引用**

**移除内容**：
```csharp
// 移除 Session 相关代码
private IOverlaySession? _session;  ❌
public void SetSession(IOverlaySession? session) ❌
internal IOverlaySession? GetSession() => _session; ❌
```

**保持内容**：
```csharp
// 保留公共事件
public event EventHandler<RegionSelectedEventArgs>? RegionSelected;
public event EventHandler<OverlayCancelledEventArgs>? Cancelled;

// 内部事件处理
private void SetupSelectionHandlerEvents()
{
    _selectionHandler.ConfirmRequested += async r =>
    {
        var finalImage = await CaptureWithFallbackAsync(r);
        RegionSelected?.Invoke(this, new RegionSelectedEventArgs(...));
    };
}
```

#### 4. SelectionOverlay 层（UI 控件）

**移除内容**：
- 所有 Session 相关代码
- `GetSession()` 调用
- 跨窗口选择状态检查

---

## 实施步骤

### Phase 1: 简化 Session（移除状态管理）

#### 1.1 修改 `IOverlaySession` 接口
- 移除跨窗口选择状态相关方法
- 添加事件转发接口

#### 1.2 修改 `OverlaySession` 实现
- 删除选择状态管理代码（~100 行）
- 实现事件订阅和转发逻辑

### Phase 2: 移除 Window 对 Session 的依赖

#### 2.1 修改 `OverlayWindow`
- 移除 `_session` 字段
- 移除 `SetSession()` 和 `GetSession()` 方法
- 保留公共事件

#### 2.2 修改 `IOverlayWindow` 接口
- 确认不包含 Session 相关方法

#### 2.3 修改 `SelectionHandler`
- 移除 Session 参数
- 构造函数简化为 `(Window, SelectionOverlay)`

#### 2.4 修改 `SelectionOverlay`
- 移除所有 Session 相关字段和方法
- 移除 `GetSession()` 调用
- 移除跨窗口选择检查逻辑

### Phase 3: Coordinator 防止重复 + 订阅 Session

#### 3.1 修改 `OverlayCoordinatorBase`
- 添加 `_currentSession` 字段
- 实现防重复创建逻辑
- 订阅 Session 事件（而非 Window 事件）

#### 3.2 修改 `WindowsOverlayCoordinator`
- 移除 Window 事件订阅代码
- 简化 Window 创建逻辑

#### 3.3 修改 `MacOverlayCoordinator`
- 移除 Window 事件订阅代码
- 简化 Window 创建逻辑

### Phase 4: 测试和验证

#### 4.1 更新单元测试
- 更新 Session 相关测试
- 移除状态管理测试
- 添加事件转发测试

#### 4.2 集成测试
- 测试快速连续触发截屏
- 测试多窗口环境（macOS）
- 测试事件链路完整性

#### 4.3 文档更新
- 更新架构文档
- 更新 API 文档

---

## 代码变更统计

### 删除代码

| 文件 | 删除行数 | 内容 |
|------|---------|------|
| `IOverlaySession.cs` | ~50 | 选择状态管理接口 |
| `OverlaySession.cs` | ~100 | 选择状态管理实现 |
| `OverlayWindow.axaml.cs` | ~10 | Session 引用和方法 |
| `SelectionOverlay.cs` | ~80 | Session 访问和状态检查 |
| `SelectionHandler.cs` | ~50 | Session 参数和方法 |
| **总计** | **~290 行** | |

### 新增代码

| 文件 | 新增行数 | 内容 |
|------|---------|------|
| `IOverlaySession.cs` | ~10 | 事件转发接口 |
| `OverlaySession.cs` | ~60 | 事件订阅和转发 |
| `OverlayCoordinatorBase.cs` | ~40 | 防重复逻辑 + Session 事件订阅 |
| **总计** | **~110 行** | |

### 净减少：~180 行代码

---

## 依赖关系图

### 优化前（复杂）

```
Session (owns) → Window
Window (holds) → Session  ❌ 违反原则
SelectionOverlay → Window → Session  ❌ 间接依赖
Coordinator → 直接订阅每个 Window 事件  ❌ 耦合高
```

### 优化后（清晰）

```
Session (owns) → Window  ✓
Window (独立) - 无 Session 依赖  ✓
Coordinator → 订阅 Session 事件  ✓
Session → 订阅 Window 事件（内部）  ✓
```

---

## 风险评估

### 低风险

- **Session 简化**：移除的是过度设计，不影响核心功能
- **Window 解耦**：Window 本就不应该持有 Session
- **事件重构**：事件流保持不变，只是订阅位置变化

### 缓解措施

1. **渐进式重构**：按 Phase 逐步实施
2. **测试覆盖**：每个 Phase 完成后运行完整测试
3. **回滚方案**：保留 git 分支，可快速回滚

---

## 验收标准

### 功能验收

- ✅ 单次截屏正常工作
- ✅ 快速连续触发截屏不创建多个 Session
- ✅ macOS 多窗口模式正常工作
- ✅ 所有快捷键和交互正常
- ✅ 事件正确触发和处理

### 架构验收

- ✅ Window 不持有 Session 引用
- ✅ Session 职责单一（生命周期 + 事件）
- ✅ Coordinator 只订阅 Session 事件
- ✅ 代码行数减少 ~180 行
- ✅ 所有单元测试通过

---

## 时间估算

- Phase 1: 简化 Session - **2 小时**
- Phase 2: 移除 Window 依赖 - **3 小时**
- Phase 3: Coordinator 优化 - **2 小时**
- Phase 4: 测试和验证 - **2 小时**

**总计：约 9 小时（1-2 天）**

---

## 后续优化建议

1. **事件系统增强**
   - 考虑引入事件总线（如有需要）
   - 统一事件命名和参数

2. **Handler 模式优化**
   - 考虑 Handler 的生命周期管理
   - 探索 Handler 之间的通信机制

3. **性能优化**
   - 监控事件链路的性能
   - 优化事件订阅/取消订阅

---

*文档版本：1.0*  
*创建日期：2025-10-29*  
*作者：AGI.Kapster 架构团队*
