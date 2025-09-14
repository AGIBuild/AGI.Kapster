using CommunityToolkit.Mvvm.ComponentModel;

namespace AGI.Captor.Desktop.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string title = "AGI.Captor";
}


