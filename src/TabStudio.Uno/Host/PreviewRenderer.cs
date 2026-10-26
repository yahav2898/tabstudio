#nullable enable

using System;
using System.Threading;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using Uno.UI.Hosting;

namespace TabStudio.UnoHost;

/// <summary>
/// Draws the frames Uno records into a surface of its own, on a thread of its own, as Uno's software renderers do,
/// and tells TabStudio when a new frame is ready to copy into the app preview.
/// </summary>
/// <remarks>
/// Uno records each frame as a picture on its UI thread and presents it later on the renderer's thread, drawing only
/// the part that changed onto the surface it presented the previous frame on; the surface therefore lives as long as
/// its size does.
/// </remarks>
internal sealed class PreviewRenderer : IDisposable
{
    private readonly IXamlRootHost _host;
    private readonly Action _frameReady;
    private readonly AutoResetEvent _invalidated = new(false);
    private readonly object _surfaceGate = new();
    private readonly Thread _thread;
    private volatile bool _disposed;
    private SKSurface? _surface;
    private (int Width, int Height) _size;

    public PreviewRenderer(IXamlRootHost host, Action frameReady)
    {
        _host = host;
        _frameReady = frameReady;
        _thread = new Thread(Run) { Name = "Uno Platform renderer", IsBackground = true };
        _thread.Start();
    }

    /// <summary>The size of the last frame, in pixels.</summary>
    public (int Width, int Height) FrameSize
    {
        get
        {
            lock (_surfaceGate)
            {
                return _size;
            }
        }
    }

    /// <summary>Asks for a frame to be presented, from any thread.</summary>
    public void Invalidate()
    {
        if (!_disposed)
        {
            _invalidated.Set();
        }
    }

    /// <summary>Copies the last frame into a buffer of 32-bit premultiplied BGRA pixels.</summary>
    /// <returns>Whether there was a frame to copy.</returns>
    public bool CopyTo(IntPtr pixels, int width, int height, int rowBytes)
    {
        lock (_surfaceGate)
        {
            return _surface is not null
                && _surface.ReadPixels(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul), pixels, rowBytes, 0, 0);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _invalidated.Set();
        if (_thread.Join(TimeSpan.FromSeconds(1)))
        {
            lock (_surfaceGate)
            {
                _surface?.Dispose();
                _surface = null;
            }
        }
    }

    private void Run()
    {
        while (!_disposed)
        {
            _invalidated.WaitOne();
            if (_disposed)
            {
                break;
            }

            try
            {
                if (Present())
                {
                    _frameReady();
                }
            }
            catch (Exception ex)
            {
                PreviewHost.Report(PreviewLogLevel.Error, "The app preview could not draw a frame.", ex);
            }
        }
    }

    private bool Present()
    {
        // The visual tree may not be there yet for the first invalidations.
        if (_host.RootElement?.Visual.CompositionTarget is not CompositionTarget target)
        {
            return false;
        }

        lock (_surfaceGate)
        {
            target.OnNativePlatformFrameRequested(_surface?.Canvas, size =>
            {
                _surface?.Dispose();
                var info = new SKImageInfo(Math.Max(1, (int)size.Width), Math.Max(1, (int)size.Height), SKColorType.Bgra8888, SKAlphaType.Premul);
                _surface = SKSurface.Create(info);
                _size = (info.Width, info.Height);
                _surface.Canvas.Clear(SKColors.Transparent);
                return _surface.Canvas;
            });
            _surface?.Flush();
            return _surface is not null;
        }
    }
}
