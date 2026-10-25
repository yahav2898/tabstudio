using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Threading;

namespace TabStudio.App.Services;

/// <summary>
/// Times the UI thread's part of every frame a window draws, on any platform: from the start of the frame's render pass
/// (an animation frame callback, before layout) to its end (a job queued behind the pass at render priority), which
/// covers the layout, the recording of what changed and the commit to the compositor. While it runs, the window draws
/// a frame on every display refresh, as during an animation.
/// </summary>
internal sealed class UiFrameTimer : IDisposable
{
    private readonly TopLevel _topLevel;
    private readonly List<double> _frames = [];
    private bool _stopped;
    private long _frameStart;

    /// <summary>Starts timing the frames of a window. Call on the UI thread.</summary>
    /// <param name="topLevel">The window.</param>
    public UiFrameTimer(TopLevel topLevel)
    {
        _topLevel = topLevel;
        _topLevel.RequestAnimationFrame(OnFrame);
    }

    /// <summary>The UI thread's time for each frame so far, in milliseconds.</summary>
    public IReadOnlyList<double> Frames => _frames;

    /// <inheritdoc />
    public void Dispose() => _stopped = true;

    private void OnFrame(TimeSpan time)
    {
        if (_stopped)
        {
            return;
        }

        _frameStart = Stopwatch.GetTimestamp();
        Dispatcher.UIThread.Post(OnFrameDone, DispatcherPriority.Render);
        _topLevel.RequestAnimationFrame(OnFrame);
    }

    private void OnFrameDone()
    {
        if (!_stopped)
        {
            _frames.Add(Stopwatch.GetElapsedTime(_frameStart).TotalMilliseconds);
        }
    }
}
