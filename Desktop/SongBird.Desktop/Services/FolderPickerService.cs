using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace SongBird.Desktop.Services;

/// <summary>Wraps Avalonia's StorageProvider (tied to a Window) behind an interface ViewModels can depend on.</summary>
public sealed class FolderPickerService(Window window) : IFolderPickerService
{
    public async Task<string?> PickFolderAsync()
    {
        var folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose your music folder",
            AllowMultiple = false,
        });

        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }
}
