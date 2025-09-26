namespace AGI.Kapster.Desktop.Services.Overlay;

public interface IOverlayController
{
    void ShowAll();
    void CloseAll();
    bool IsActive { get; }
}


