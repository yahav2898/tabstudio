using Avalonia.Controls.Primitives;
using TabStudio.App.Platform;

namespace TabStudio.App.Views.DesktopMode;

/// <summary>The mouse pointer TabStudio draws on the display, in the display's overlay layer, above menus and popups.</summary>
public sealed partial class DesktopScreenView
{
    private readonly DesktopPointerView _pointer = new();
    private OverlayLayer? _overlay;

    /// <summary>The pointer the platform moves on the display while it has the tablet's mouse.</summary>
    internal DesktopCursor? MousePointer
    {
        get => _pointer.MousePointer;
        set => _pointer.MousePointer = value;
    }

    /// <summary>Puts the pointer into the overlay layer of the display's window, once the window has one.</summary>
    private void AddPointer()
    {
        if (_overlay is not null || OverlayLayer.GetOverlayLayer(this) is not { } overlay)
        {
            return;
        }

        _overlay = overlay;
        overlay.Children.Add(_pointer);
    }

    private void RemovePointer()
    {
        _overlay?.Children.Remove(_pointer);
        _overlay = null;
    }
}
