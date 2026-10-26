#nullable enable

using System;
using Microsoft.UI.Xaml;
using Uno.UI.Hosting;
using Uno.UI.Xaml.Controls;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Display;

namespace TabStudio.UnoHost;

/// <summary>
/// The native side of an Uno window. The host's own window is the app preview: TabStudio decides its size and scale,
/// and its frames are drawn for TabStudio to show. A window an app creates shows its content in that window instead
/// (Uno's secondary app support moves it there), so its own native side draws nothing.
/// </summary>
internal sealed class PreviewWindow : NativeWindowWrapperBase, IXamlRootHost, IDisplayInformationExtension
{
    private readonly PreviewRenderer? _renderer;
    private double _width;
    private double _height;
    private float _scale;
    private bool _closed;

    public PreviewWindow(Window window, XamlRoot xamlRoot, bool isPreview, double width, double height, float scale, Action frameReady)
        : base(window, xamlRoot)
    {
        IsPreview = isPreview;
        XamlRootMap.Register(xamlRoot, this);

        // Setting the bounds can render right away, through this window as the root's host.
        _renderer = isPreview ? new PreviewRenderer(this, frameReady) : null;
        ApplySize(width, height, scale);
    }

    /// <summary>Whether this is the host's window, the one TabStudio shows.</summary>
    public bool IsPreview { get; }

    public override object? NativeWindow => null;

    /// <summary>The renderer of the host's window.</summary>
    public PreviewRenderer? Renderer => _renderer;

    UIElement? IXamlRootHost.RootElement => Window?.RootElement;

    DisplayOrientations IDisplayInformationExtension.CurrentOrientation =>
        _width >= _height ? DisplayOrientations.Landscape : DisplayOrientations.Portrait;

    uint IDisplayInformationExtension.ScreenWidthInRawPixels => (uint)Math.Round(_width * _scale);

    uint IDisplayInformationExtension.ScreenHeightInRawPixels => (uint)Math.Round(_height * _scale);

    float IDisplayInformationExtension.LogicalDpi => _scale * DisplayInformation.BaseDpi;

    double IDisplayInformationExtension.RawPixelsPerViewPixel => _scale;

    ResolutionScale IDisplayInformationExtension.ResolutionScale => (ResolutionScale)(int)(Math.Round(_scale * 4) / 4 * 100);

    double? IDisplayInformationExtension.DiagonalSizeInInches => null;

    /// <summary>Sizes the window as the app preview's pane, in device-independent pixels, at a pixel density.</summary>
    public void ApplySize(double width, double height, float scale)
    {
        _width = Math.Max(1, width);
        _height = Math.Max(1, height);
        _scale = scale > 0 && float.IsFinite(scale) ? scale : 1;

        RasterizationScale = _scale;
        var bounds = new Rect(0, 0, _width, _height);
        SetBoundsAndVisibleBounds(bounds, bounds);
        var pixels = new SizeInt32((int)Math.Round(_width * _scale), (int)Math.Round(_height * _scale));
        SetSizes(pixels, pixels);
    }

    /// <summary>The app preview decides the window's size; an app asking for another one keeps the pane's.</summary>
    public override void Resize(SizeInt32 size)
    {
    }

    void IXamlRootHost.InvalidateRender() => _renderer?.Invalidate();

    protected override void ShowCore() => _renderer?.Invalidate();

    protected override void CloseCore() => TearDown();

    /// <summary>Stops rendering and forgets the window's root.</summary>
    public void TearDown()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        if (XamlRoot is { } root)
        {
            XamlRootMap.Unregister(root);
        }

        _renderer?.Dispose();
    }
}
