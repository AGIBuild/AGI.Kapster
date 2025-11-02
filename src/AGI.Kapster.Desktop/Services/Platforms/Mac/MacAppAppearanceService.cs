using System.Runtime.Versioning;

namespace AGI.Kapster.Desktop.Services.Platforms.Mac;

[SupportedOSPlatform("macos")]
public class MacAppAppearanceService : Platforms.IAppAppearanceService
{
    public void ApplyOnAppStartup()
    {
        MacDockHider.TryHideFromDock();
    }
}


