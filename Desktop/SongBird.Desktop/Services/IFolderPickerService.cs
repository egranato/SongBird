using System.Threading.Tasks;

namespace SongBird.Desktop.Services;

public interface IFolderPickerService
{
    Task<string?> PickFolderAsync();
}
