using System;
using System.Threading.Tasks;

namespace AGI.Kapster.Desktop.Services.Hotkeys;

/// <summary>
/// 热键管理器接口
/// </summary>
public interface IHotkeyManager : IDisposable
{
    /// <summary>
    /// 初始化热键管理器
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// 从设置重新加载热键
    /// </summary>
    Task ReloadHotkeysAsync();

    /// <summary>
    /// 注册ESC热键用于关闭截图遮罩层
    /// </summary>
    void RegisterEscapeHotkey();

    /// <summary>
    /// 注销ESC热键
    /// </summary>
    void UnregisterEscapeHotkey();
}