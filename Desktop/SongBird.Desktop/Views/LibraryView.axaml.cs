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

    // Avalonia has no built-in "double-click a list item" binding, so this is handled here
    // rather than in the ViewModel. The template root's DataContext is the tapped Track.
    private void OnTrackDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is LibraryViewModel viewModel
            && sender is Control { DataContext: Track track })
        {
            viewModel.PlayTrackCommand.Execute(track);
        }
    }
}
