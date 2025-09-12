using System;
using System.Threading.Tasks;

namespace AGI.Captor.App.Services.Hotkeys;

public sealed class MacHotkeyProvider : IHotkeyProvider
{
	public bool Register(string id, HotkeyChord chord, Action callback) => false;
	public void Unregister(string id) {}
	public Task<bool> UnregisterAsync(string id) => Task.FromResult(true);
	public void Dispose() {}
}
