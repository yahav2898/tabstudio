using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using TabStudio.Forms.Runtime;

namespace TabStudio.Forms.Hosting;

/// <summary>The device's services an app's code reaches on its own thread: the clipboard and the storage picker.</summary>
internal sealed partial class FormsDesktop
{
    private string? _clipboardFallback;

    /// <summary>The storage the app's file dialogs open, which the run owns; without it, file dialogs are cancelled.</summary>
    public StorageBridge? Storage { get; set; }

    /// <inheritdoc />
    public Task<string[]?> PickFilesAsync(FileRequest request) => Dispatcher.UIThread.InvokeAsync(async () =>
        TopLevel.GetTopLevel(this) is { } topLevel && Storage is { } storage ? await storage.PickAsync(topLevel, request) : null);

    /// <inheritdoc />
    public Task SetClipboardTextAsync(string text) => Dispatcher.UIThread.InvokeAsync(async () =>
    {
        _clipboardFallback = text;
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    });

    /// <inheritdoc />
    public Task<string?> GetClipboardTextAsync() => Dispatcher.UIThread.InvokeAsync(async () =>
        TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard ? await clipboard.TryGetTextAsync() : _clipboardFallback);
}
