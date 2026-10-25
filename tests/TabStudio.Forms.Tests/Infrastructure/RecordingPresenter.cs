using System.Drawing;
using TabStudio.Forms.Runtime;

namespace TabStudio.Forms.Tests.Infrastructure;

/// <summary>A presenter that keeps the latest frame of each window, for tests to look at.</summary>
internal sealed class RecordingPresenter : IFormsPresenter
{
    private readonly object _gate = new();
    private readonly Dictionary<int, WindowFrame> _frames = [];
    private IReadOnlyList<int> _order = [];
    private string? _clipboard;

    /// <inheritdoc />
    public Size DesktopSize { get; set; } = new(1280, 800);

    /// <summary>The files the next file dialog picks, or <see langword="null"/> to cancel it.</summary>
    public string[]? NextPick { get; set; }

    /// <summary>The last file request.</summary>
    public FileRequest? LastRequest { get; private set; }

    /// <summary>How many times windows were presented.</summary>
    public int PresentCount { get; private set; }

    /// <summary>The open windows' latest frames, from the bottom of the z-order.</summary>
    public IReadOnlyList<WindowFrame> Frames
    {
        get
        {
            lock (_gate)
            {
                return [.. _order.Where(_frames.ContainsKey).Select(id => _frames[id])];
            }
        }
    }

    /// <inheritdoc />
    public void Present(IReadOnlyList<WindowFrame> frames, IReadOnlyList<int> closed, IReadOnlyList<int> order)
    {
        lock (_gate)
        {
            _order = order;
            foreach (var id in closed)
            {
                _frames.Remove(id);
            }

            foreach (var frame in frames)
            {
                _frames[frame.Id] = frame;
            }

            PresentCount++;
            Monitor.PulseAll(_gate);
        }
    }

    /// <inheritdoc />
    public Task<string[]?> PickFilesAsync(FileRequest request)
    {
        LastRequest = request;
        return Task.FromResult(NextPick);
    }

    /// <inheritdoc />
    public Task SetClipboardTextAsync(string text)
    {
        _clipboard = text;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string?> GetClipboardTextAsync() => Task.FromResult(_clipboard);

    /// <summary>Waits until a condition on the frames holds.</summary>
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
