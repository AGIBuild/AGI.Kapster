## 1. Implementation
- [ ] 1.1 设计并实现 managed-only Carbon P/Invoke（handler 安装 + hotkey 注册/注销）
- [ ] 1.2 将 macOS provider 切换到 managed-only 实现，确保保持现有行为与日志质量
- [ ] 1.3 删除 native helper：`kapster_hotkey.c`、`MacNativeHotkeyLibrary.cs`、以及 csproj 中的 clang Targets
- [ ] 1.4 更新 macOS 打包脚本：移除 dylib 相关签名/拷贝步骤（如存在）
- [ ] 1.5 添加最小验证：启动自检日志 + 手动验证清单（非沙盒 + sandbox）

## 2. Testing
- [ ] 2.1 回归：`Alt+A` / `Alt+S` 热键触发
- [ ] 2.2 回归：布局切换触发重注册（仅字符热键）
- [ ] 2.3 回归：冲突/注册失败时不假成功（日志/提示）



