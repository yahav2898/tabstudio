using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using TabStudio.Forms.Runtime;

namespace TabStudio.Forms.Hosting;

/// <summary>The device's services an app's code reaches on its own thread: the clipboard and the storage picker.</summary>
internal sealed partial class FormsDesktop
{
    private string? _clipboardFallback;
    private byte[]? _imageFallback;

    /// <summary>The device's clipboard for images, set by the IDE where the platform has one of its own, as on Android.</summary>
    public static IClipboardImages? ClipboardImages { get; set; }

    /// <summary>The storage the app's file dialogs open, which the run owns; without it, file dialogs are cancelled.</summary>
    public StorageBridge? Storage { get; set; }

    /// <inheritdoc />
    public Task<string[]?> PickFilesAsync(FileRequest request) => Dispatcher.UIThread.InvokeAsync(async () =>
        TopLevel.GetTopLevel(this) is { } topLevel && Storage is { } storage ? await storage.PickAsync(topLevel, request) : null);

    /// <inheritdoc />
    public Task SetClipboardTextAsync(string text) => Dispatcher.UIThread.InvokeAsync(async () =>
    {
        _clipboardFallback = text;
        _imageFallback = null;
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    });

    /// <inheritdoc />
    public Task<string?> GetClipboardTextAsync() => Dispatcher.UIThread.InvokeAsync(async () =>
        TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard ? await clipboard.TryGetTextAsync() : _clipboardFallback);

    /// <inheritdoc />
    public Task SetClipboardImageAsync(byte[] png) => Dispatcher.UIThread.InvokeAsync(async () =>
    {
        _imageFallback = png;
        _clipboardFallback = null;
        try
        {
            if (ClipboardImages is { } images)
            {
                await images.SetImageAsync(png);
            }
            else if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            {
                using var stream = new MemoryStream(png);
                await clipboard.SetBitmapAsync(new Avalonia.Media.Imaging.Bitmap(stream));
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The image stays on the app's own clipboard, so it can still be pasted in the app.
            System.Diagnostics.Debug.WriteLine($"The image could not be copied to the device's clipboard: {ex.Message}");
        }
    });

    /// <inheritdoc />
    public Task<byte[]?> GetClipboardImageAsync() => Dispatcher.UIThread.InvokeAsync(async () =>
    {
        try
        {
            if (ClipboardImages is { } images)
            {
                return await images.GetImageAsync() ?? _imageFallback;
            }

            if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard && await clipboard.TryGetBitmapAsync() is { } bitmap)
            {
                using var stream = new MemoryStream();
                bitmap.Save(stream);
                return stream.ToArray();
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            System.Diagnostics.Debug.WriteLine($"The device's clipboard could not be read: {ex.Message}");
        }

        return _imageFallback;
    });
}
