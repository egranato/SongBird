using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SongBird.Core.Abstractions;
using SongBird.Desktop.Services;
using SongBird.Desktop.ViewModels;
using SongBird.Desktop.Views;
using SongBird.Infrastructure.Metadata;
using SongBird.Infrastructure.Persistence;
using SongBird.Infrastructure.Scanning;

namespace SongBird.Desktop;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // MainWindow is created before the service provider because FolderPickerService
            // needs a real Window (its StorageProvider) to be registered into DI.
            var window = new MainWindow();
            Services = BuildServices(window);
            window.DataContext = Services.GetRequiredService<MainViewModel>();
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static IServiceProvider BuildServices(MainWindow window)
    {
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.AddConsole());
        services.AddTransient<MainViewModel>();
        services.AddSingleton<PlayerViewModel>();
        services.AddSingleton<IFolderPickerService>(new FolderPickerService(window));

        // Local host app data only - never a network share (SQLite WAL assumes same-host coordination).
        var appDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SongBird");
        var databasePath = Path.Combine(appDataDirectory, "library.db");

        services.AddSingleton<ILibraryRepository>(_ => new SqliteLibraryRepository(databasePath));
        services.AddSingleton<IMetadataReader, TagLibMetadataReader>();
        services.AddSingleton<IMediaScanner, FileSystemMediaScanner>();

        return services.BuildServiceProvider();
    }
}