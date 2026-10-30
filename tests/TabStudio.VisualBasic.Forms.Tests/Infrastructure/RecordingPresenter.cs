using System.Drawing;
using TabStudio.Forms.Runtime;

namespace TabStudio.VisualBasic.Forms.Tests.Infrastructure;

/// <summary>A presenter that keeps the latest frame of each window, for tests to wait for.</summary>
internal sealed class RecordingPresenter : IFormsPresenter
{
    private readonly object _gate = new();
    private readonly Dictionary<int, WindowFrame> _frames = [];
    private string? _clipboard;
    private byte[]? _clipboardImage;

    /// <inheritdoc />
    public Size DesktopSize => new(1280, 800);

    /// <inheritdoc />
    public void Present(IReadOnlyList<WindowFrame> frames, IReadOnlyList<int> closed, IReadOnlyList<int> order)
    {
        lock (_gate)
        {
            foreach (var id in closed)
            {
                _frames.Remove(id);
            }

            foreach (var frame in frames)
            {
                _frames[frame.Id] = frame;
            }

            Monitor.PulseAll(_gate);
        }
    }

    /// <inheritdoc />
    public Task<string[]?> PickFilesAsync(FileRequest request) => Task.FromResult<string[]?>(null);

    /// <inheritdoc />
    public Task SetClipboardTextAsync(string text)
    {
        _clipboard = text;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string?> GetClipboardTextAsync() => Task.FromResult(_clipboard);

    /// <inheritdoc />
    public Task SetClipboardImageAsync(byte[] png)
    {
        _clipboardImage = png;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<byte[]?> GetClipboardImageAsync() => Task.FromResult(_clipboardImage);

    /// <summary>Waits until a condition on the open windows' frames holds.</summary>
    public bool WaitFor(Func<IReadOnlyList<WindowFrame>, bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        lock (_gate)
        {
            while (!condition([.. _frames.Values]))
            {
                var left = deadline - DateTime.UtcNow;
                if (left <= TimeSpan.Zero)
                {
                    return false;
                }

                Monitor.Wait(_gate, left);
            }

            return true;
        }
    }
}
