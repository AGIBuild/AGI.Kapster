namespace AGI.Captor.Desktop.Services.Overlay;

public interface IOverlayController
{
    void ShowAll();
    void CloseAll();
    bool IsActive { get; }
}


