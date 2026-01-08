using System;
using AGI.Kapster.Desktop.Models;

namespace AGI.Kapster.Desktop.Services.Hotkeys;

/// <summary>
/// 热键提供者接口 - 策略模式的核心接口
/// </summary>
public interface IHotkeyProvider : IDisposable
{
    /// <summary>
    /// 当前平台是否支持热键
    /// </summary>
    bool IsSupported { get; }

    /// <summary>
    /// 是否有必要的权限
    /// </summary>
    bool HasPermissions { get; }

    /// <summary>
    /// Register a hotkey using a gesture (preferred, handles character-stable resolution internally)
    /// </summary>
    bool RegisterHotkey(string id, HotkeyGesture gesture, Action callback);

    /// <summary>
    /// 注销热键
    /// </summary>
    bool UnregisterHotkey(string id);

    /// <summary>
    /// 注销所有热键
    /// </summary>
    void UnregisterAll();
}