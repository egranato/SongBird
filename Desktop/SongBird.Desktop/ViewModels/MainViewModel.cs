using CommunityToolkit.Mvvm.ComponentModel;

namespace SongBird.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string Greeting { get; set; } = "Welcome to Avalonia!";
}
