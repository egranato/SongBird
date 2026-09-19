using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SongBird.Core.Abstractions;
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
        Services = BuildServices();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainViewModel>(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.AddConsole());
        services.AddTransient<MainViewModel>();

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