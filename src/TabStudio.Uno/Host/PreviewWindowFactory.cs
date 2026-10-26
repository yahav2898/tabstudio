#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Uno.UI.Xaml.Controls;

namespace TabStudio.UnoHost;

/// <summary>
/// Creates the native side of Uno's windows: the first window is the host's own, which is the app preview; the
/// windows apps create afterwards show their content in it.
/// </summary>
internal sealed class PreviewWindowFactory : INativeWindowFactoryExtension
{
    private readonly Func<(double Width, double Height, float Scale)> _size;
    private readonly Action _frameReady;
    private readonly List<WeakReference<Window>> _appWindows = new();

    public PreviewWindowFactory(Func<(double Width, double Height, float Scale)> size, Action frameReady)
    {
        _size = size;
        _frameReady = frameReady;
    }

    /// <summary>The host's window, once Uno created it.</summary>
    public PreviewWindow? Preview { get; private set; }

    public bool SupportsClosingCancellation => true;

    public bool SupportsMultipleWindows => true;

    /// <summary>The windows the running apps created, newest last.</summary>
    public IEnumerable<Window> AppWindows
    {
        get
        {
            foreach (var reference in _appWindows)
            {
                if (reference.TryGetTarget(out var window))
                {
                    yield return window;
                }
            }
        }
    }

    public INativeWindowWrapper CreateWindow(Window window, XamlRoot xamlRoot)
    {
        var (width, height, scale) = _size();
        if (Preview is null)
        {
            Preview = new PreviewWindow(window, xamlRoot, isPreview: true, width, height, scale, _frameReady);
            return Preview;
        }

        _appWindows.RemoveAll(reference => !reference.TryGetTarget(out _));
        _appWindows.Add(new WeakReference<Window>(window));
        return new PreviewWindow(window, xamlRoot, isPreview: false, width, height, scale, _frameReady);
    }
}
