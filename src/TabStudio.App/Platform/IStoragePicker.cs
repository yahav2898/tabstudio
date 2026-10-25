using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace TabStudio.App.Platform;

/// <summary>Opens the system's folder picker and web browser. On Android these are the system pickers.</summary>
public interface IStoragePicker
{
    /// <summary>Asks the user to pick a folder.</summary>
    /// <param name="title">The picker's title.</param>
    /// <returns>The folder, or <see langword="null"/> when the user cancelled.</returns>
    Task<IStorageFolder?> PickFolderAsync(string title);

    /// <summary>Opens a web page in the system browser.</summary>
    /// <param name="uri">The page.</param>
    Task OpenUriAsync(Uri uri);
}

/// <summary>The <see cref="IStoragePicker"/> backed by the app's top-level window or view.</summary>
public sealed class TopLevelStoragePicker : IStoragePicker
{
    /// <inheritdoc />
    public async Task<IStorageFolder?> PickFolderAsync(string title)
    {
        if (FindTopLevel() is not { } topLevel || !topLevel.StorageProvider.CanPickFolder)
        {
            return null;
        }

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        });
        return folders.Count > 0 ? folders[0] : null;
    }

    /// <inheritdoc />
    public async Task OpenUriAsync(Uri uri)
    {
        if (FindTopLevel() is { } topLevel)
        {
            await topLevel.Launcher.LaunchUriAsync(uri);
        }
    }

    private static TopLevel? FindTopLevel() => Application.Current?.ApplicationLifetime switch
    {
        IClassicDesktopStyleApplicationLifetime desktop => desktop.MainWindow,
        ISingleViewApplicationLifetime { MainView: { } view } => TopLevel.GetTopLevel(view),
        _ => null,
    };
}
