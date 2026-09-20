using Avalonia.Controls;
using Avalonia.Input;
using SongBird.Desktop.ViewModels;

namespace SongBird.Desktop.Views;

public partial class PlayerBarView : UserControl
{
    public PlayerBarView()
    {
        InitializeComponent();
    }

    // Seeking is handled here rather than via a two-way binding: Position is read-only on the
    // ViewModel and updates ~4x/sec from playback, which would fight a two-way-bound slider
    // while the user drags. IsUserSeeking suppresses those updates for the duration of the drag.
    private void OnSeekSliderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is PlayerViewModel viewModel)
        {
            viewModel.IsUserSeeking = true;
        }
    }

    private void OnSeekSliderPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is PlayerViewModel viewModel && sender is Slider slider)
        {
            viewModel.IsUserSeeking = false;
            viewModel.SeekCommand.Execute(slider.Value);
        }
    }
}
