using Avalonia.Controls;
using Avalonia.Input;
using SongBird.Core.Models;
using SongBird.Desktop.ViewModels;

namespace SongBird.Desktop.Views;

public partial class LibraryView : UserControl
{
    public LibraryView()
    {
        InitializeComponent();
    }

    // DoubleTapped is attached to the ListBox itself, not to content inside its ItemTemplate:
    // the ListBoxItem's own pointer handling (for selection) swallows the gesture before it
    // reaches a recognizer nested inside the template, so a per-row handler never fires.
    private void OnTrackListDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is LibraryViewModel viewModel
            && sender is ListBox { SelectedItem: Track track })
        {
            viewModel.PlayTrackCommand.Execute(track);
        }
    }
}
