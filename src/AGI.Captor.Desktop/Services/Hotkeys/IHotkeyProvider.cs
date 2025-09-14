using System;

namespace AGI.Captor.Desktop.Services.Hotkeys;

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
    /// 注册热键
    /// </summary>
    bool RegisterHotkey(string id, HotkeyModifiers modifiers, uint keyCode, Action callback);
    
    /// <summary>
    /// 注销热键
    /// </summary>
    bool UnregisterHotkey(string id);
    
    /// <summary>
    /// 注销所有热键
    /// </summary>
    void UnregisterAll();
}

/// <summary>
/// 热键修饰符
/// </summary>
[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8
}