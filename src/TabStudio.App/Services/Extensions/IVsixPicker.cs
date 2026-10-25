using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace TabStudio.App.Services.Extensions;

/// <summary>A .vsix file the user picked, opened for reading. Dispose it when the extension is installed.</summary>
/// <param name="Name">The file's name, such as <c>lumen-1.0.0.vsix</c>.</param>
/// <param name="Content">The file's bytes.</param>
public sealed record PickedVsix(string Name, Stream Content) : IDisposable
{
    /// <inheritdoc />
    public void Dispose() => Content.Dispose();
}

/// <summary>Lets the user pick a .vsix file to install, through the system's file picker.</summary>
public interface IVsixPicker
{
    /// <summary>Asks the user for a .vsix file and opens it.</summary>
    /// <returns>The file, or <see langword="null"/> when the user cancelled.</returns>
    /// <exception cref="IOException">The file cannot be read.</exception>
    Task<PickedVsix?> PickAsync();
}

/// <summary>
/// The <see cref="IVsixPicker"/> backed by the storage provider of the app's window or view, which on Android is the
/// system's document picker (the IDE's storage picker for files).
/// </summary>
public sealed class TopLevelVsixPicker : IVsixPicker
{
    private static readonly FilePickerFileType VsixFiles = new("VS Code extension")
    {
        Patterns = ["*.vsix"],

        // Android has no type for .vsix files: they arrive as zips or plain bytes, so every file can be picked.
        MimeTypes = ["*/*"],
        AppleUniformTypeIdentifiers = ["public.data"],
    };

    /// <inheritdoc />
    public async Task<PickedVsix?> PickAsync()
    {
        if (FindTopLevel() is not { } topLevel || !topLevel.StorageProvider.CanOpen)
        {
            return null;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Install from VSIX",
            AllowMultiple = false,
            FileTypeFilter = [VsixFiles],
        });
        if (files.Count == 0)
        {
            return null;
        }

        using var file = files[0];
        var stream = await file.OpenReadAsync();
        return new PickedVsix(file.Name, stream);
    }

    private static TopLevel? FindTopLevel() => Application.Current?.ApplicationLifetime switch
    {
        IClassicDesktopStyleApplicationLifetime desktop => desktop.MainWindow,
        ISingleViewApplicationLifetime { MainView: { } view } => TopLevel.GetTopLevel(view),
        _ => null,
    };
}
