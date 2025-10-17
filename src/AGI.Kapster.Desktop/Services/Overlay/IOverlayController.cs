using System.Threading.Tasks;

namespace AGI.Kapster.Desktop.Services.Overlay;

public interface IOverlayController
{
    Task ShowAll();
    void CloseAll();
    bool IsActive { get; }
}


