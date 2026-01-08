## Why
当前 macOS 全局热键依赖 `libkapster_hotkey.dylib`（Carbon native helper），并在 `AGI.Kapster.Desktop.csproj` 中通过 `clang` 进行前置编译。这带来以下问题：

- 构建环境耦合：`dotnet build/publish` 需要本机 `clang` + Carbon 框架，CI/开发机一致性差。
- 打包复杂度：dylib 需要纳入 bundle、签名顺序更严格，排查成本高。
- 发布链路：App Store / notarize 流程对 “额外 native 动态库” 的签名与验证更敏感。

因此希望尝试**方案 D**：移除 native helper，改为**纯 C# P/Invoke Carbon**，从根源上去掉 native 文件的前置编译与分发。

## What Changes
- **BREAKING (internal)**：macOS 热键注册从 “native helper + managed wrapper” 切换为 “managed-only Carbon P/Invoke”。
- 移除 `libkapster_hotkey.dylib` 及其构建/复制逻辑（不再在 csproj 中调用 `clang`）。
- `MacCarbonHotkeyProvider` 直接 P/Invoke：
  - `InstallApplicationEventHandler`
  - `RegisterEventHotKey` / `UnregisterEventHotKey`
  - `GetEventParameter`（读取 `EventHotKeyID`）
- 保持现有热键语义与默认值：`Alt+A`（capture），`Alt+S`（settings）。
- 保持 “character-stable” 逻辑不变：继续使用现有 `IHotkeyResolver`（仅影响 macOS provider 的注册通道）。

## Impact
- **Affected specs**:
  - `openspec/changes/refactor-hotkey-model-char-stable/specs/hotkey-management/spec.md`（新增/强化对“无 native 前置编译”的要求）
- **Affected code**（实施阶段将涉及）:
  - `src/AGI.Kapster.Desktop/Services/Hotkeys/MacCarbonHotkeyProvider.cs`
  - `src/AGI.Kapster.Desktop/Services/Hotkeys/MacNativeHotkeyLibrary.cs`（预期删除）
  - `src/AGI.Kapster.Desktop/Native/macos/kapster_hotkey.c`（预期删除）
  - `src/AGI.Kapster.Desktop/AGI.Kapster.Desktop.csproj`（移除 clang Targets）
  - `packaging/macos/*`（可选：去除 dylib 签名/拷贝分支）

## Risks / Trade-offs
- **高风险：可靠性回退**。之前已验证 “managed-only 自己桥接 Carbon event handler” 容易出现：
  - 回调签名/CallingConvention 不一致导致崩溃或不触发
  - RunLoop 集成不稳定（尤其在 Avalonia + sandbox 场景）
- **调试成本更高**：一旦签名错误，往往是 crash/无事件，定位困难。

**缓解策略**：
- 采用最小 P/Invoke 面积：只做 `InstallApplicationEventHandler + RegisterEventHotKey`，不做 ReceiveNextEvent 轮询。
- 所有 handler 安装与注册强制在 UI 线程执行。
- 增加可观测性：失败必须有明确错误码日志；提供 “self-check” 启动诊断日志（仅 Debug）。

## Compatibility / Decision Note
此 change 与 `refactor-hotkey-model-char-stable` 的 **Decision 2（native helper）** 相冲突：
- 本 change 若被接受，应在后续把原 change 的 design 决策更新为 “managed-only”，或将其拆分/归档后再合并。



