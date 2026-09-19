using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using SongBird.Core.Abstractions;
using SongBird.Desktop.Services;

namespace SongBird.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly ILibraryRepository _repository;
    private readonly IMediaScanner _scanner;
    private readonly IFolderPickerService _folderPicker;
    private readonly PlayerViewModel _player;

    [ObservableProperty]
    public partial ViewModelBase? CurrentPage { get; set; }

    public MainViewModel(
        ILibraryRepository repository,
        IMediaScanner scanner,
        IFolderPickerService folderPicker,
        PlayerViewModel player)
    {
        _repository = repository;
        _scanner = scanner;
        _folderPicker = folderPicker;
        _player = player;

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var libraries = await _repository.GetLibrariesAsync();

        if (libraries.Count > 0)
        {
            await ShowLibraryAsync();
            return;
        }

        var createPage = new CreateLibraryViewModel(_repository, _scanner, _folderPicker);
        createPage.LibraryReady += async (_, _) => await ShowLibraryAsync();
        CurrentPage = createPage;
    }

    private async Task ShowLibraryAsync()
    {
        var libraryPage = new LibraryViewModel(_repository, _scanner, _player);
        await libraryPage.LoadAsync();
        CurrentPage = libraryPage;
    }
}
