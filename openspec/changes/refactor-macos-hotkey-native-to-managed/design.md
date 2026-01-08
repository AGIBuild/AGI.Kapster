## Context
当前 macOS 全局热键通过 Carbon `RegisterEventHotKey` 实现，但事件分发由一个 native helper（`libkapster_hotkey.dylib`）负责。为了移除 “native 前置编译 + dylib 分发/签名” 的复杂度，本设计尝试将 Carbon 事件 handler 与 hotkey 注册全部收敛到 managed 侧（C# P/Invoke）。

## Goals / Non-Goals

### Goals
- **构建目标**：`dotnet build/publish` 不再依赖 `clang`，也不需要产出/分发 `libkapster_hotkey.dylib`。
- **行为目标**：维持现有热键行为（触发稳定、无 Input Monitoring 权限）。
- **线程目标**：Carbon handler 安装与 hotkey 注册在**主/UI 线程**进行，依赖系统 RunLoop 分发事件。

### Non-Goals
- 不引入新的第三方 native 打包系统（CMake / Xcode 工程等）。
- 不回退到 `CGEventTap`（Input Monitoring）。
- 不做 “polling event loop”（如 `ReceiveNextEvent` 轮询）。

## Decisions

### Decision 1: 使用 `InstallApplicationEventHandler`（而不是轮询）
**Choice**：使用 Carbon Event Manager 的应用级 handler，接收 `kEventHotKeyPressed` 并读取 `EventHotKeyID`。

**Rationale**：
- 事件由系统 RunLoop 分发，减少 “手写 event loop” 的脆弱性。
- 理论上更贴近 native helper 的工作方式，只是把实现挪到 managed。

### Decision 2: 仅 P/Invoke 最小 API 面积
**Choice**：仅引入注册/注销 hotkey + handler 安装 + `GetEventParameter`。

**Rationale**：
- P/Invoke 表面越小，越容易稳定与排错。

### Decision 3: 显式生命周期管理与 delegate pinning
**Choice**：
- handler delegate 使用 `GCHandle.Alloc` 固定生命周期
- 在 `Dispose` 时显式移除 handler（若可用）并注销所有热键

**Rationale**：
- 避免 callback 被 GC 回收导致崩溃/不触发。

## Alternatives considered
- **Keep native helper (current)**：最可靠，但不满足 “移除 native 前置编译”。
- **打包预编译 dylib (方案 A/B/C)**：能去掉 clang 前置编译，但仍保留 dylib 分发与签名复杂度；用户此轮明确要试方案 D。

## Migration Plan
1. 提供 managed-only 的 `MacCarbonHotkeyProvider` 实现（或新类），确保功能 parity。
2. 删除 native helper 代码与 csproj clang targets。
3. 更新 packaging 脚本：移除 dylib 签名/拷贝逻辑（如存在）。
4. 增加回滚开关（可选）：通过编译常量或配置在运行时禁用 global hotkey（仅用于紧急止血）。

## Validation Plan
- macOS 非沙盒：热键注册/触发，sleep/wake 后仍可用。
- macOS sandbox（如有）：热键注册/触发；确认无 Input Monitoring。
- 键盘布局变化：字符热键在 layout 切换后可重解析并重注册（仅验证现有行为不被破坏）。

## Open Questions
- 在不同 macOS 版本下，`InstallApplicationEventHandler` 的 callback calling convention 是否统一可用（需验证）。
- Avalonia 主线程与 Carbon 主 RunLoop 的耦合是否在 sandbox 中存在边缘问题（需实际跑包验证）。



