using CommunityToolkit.Mvvm.ComponentModel;

namespace AGI.Captor.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string title = "AGI.Captor";
}


