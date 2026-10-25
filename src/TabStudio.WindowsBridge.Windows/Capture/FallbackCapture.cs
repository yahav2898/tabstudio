using TabStudio.WindowsBridge.Frames;
using TabStudio.WindowsBridge.Hosting;
using TabStudio.WindowsBridge.Windows.Native;

namespace TabStudio.WindowsBridge.Windows.Capture;

/// <summary>
/// Captures a window one way, and another way from the first time that fails: Windows.Graphics.Capture can lose its
/// Direct3D device, as when the graphics driver restarts, while PrintWindow keeps working.
/// </summary>
internal sealed class FallbackCapture : IWindowCapture
{
    private readonly Func<IWindowCapture> _fallback;
    private IWindowCapture _current;
    private bool _fellBack;

    /// <summary>Captures with <paramref name="primary"/> until it fails.</summary>
    /// <param name="primary">The first way to capture.</param>
    /// <param name="fallback">Starts the other way.</param>
    public FallbackCapture(IWindowCapture primary, Func<IWindowCapture> fallback)
    {
        _current = primary;
        _fallback = fallback;
    }

    /// <inheritdoc />
    public string Method => _current.Method;

    /// <inheritdoc />
    public CaptureResult Capture(FrameBitmap? reuse)
    {
        if (_fellBack)
        {
            return _current.Capture(reuse);
        }

        try
        {
            return _current.Capture(reuse);
        }
        catch (NativeCallException)
        {
            _current.Dispose();
            _current = _fallback();
            _fellBack = true;
            return _current.Capture(reuse);
        }
    }

    /// <inheritdoc />
    public void Dispose() => _current.Dispose();
}
