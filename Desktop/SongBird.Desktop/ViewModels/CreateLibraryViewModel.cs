using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SongBird.Core.Abstractions;
using SongBird.Core.Models;
using SongBird.Core.Scanning;
using SongBird.Desktop.Services;

namespace SongBird.Desktop.ViewModels;

public partial class CreateLibraryViewModel(
    ILibraryRepository repository,
    IMediaScanner scanner,
    IFolderPickerService folderPicker) : ViewModelBase
{
    [ObservableProperty]
    public partial bool IsScanning { get; set; }

    [ObservableProperty]
    public partial string? StatusText { get; set; }

    /// <summary>Raised once the chosen folder has been indexed. MainViewModel swaps in the browsing page.</summary>
    public event EventHandler<Library>? LibraryReady;

    [RelayCommand]
    private async Task ChooseFolderAsync()
    {
        var path = await folderPicker.PickFolderAsync();
        if (path is null)
        {
            return;
        }

        var trimmedPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var library = new Library
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(trimmedPath) is { Length: > 0 } name ? name : trimmedPath,
            RootPath = path,
        };

        await repository.SaveLibraryAsync(library);

        IsScanning = true;
        var progress = new Progress<ScanProgress>(p =>
            StatusText = $"Scanning {p.FilesProcessed}/{p.TotalFiles}: {p.CurrentRelativePath}");

        var result = await scanner.ScanAsync(library, progress);

        IsScanning = false;
        StatusText = $"Added {result.TracksAdded} tracks.";

        LibraryReady?.Invoke(this, library);
    }
}
