using Avalonia.Controls;

namespace TabStudio.App.Platform;

/// <summary>
/// The <see cref="IExternalDisplayService"/> of platforms without external displays, such as the desktop host, where
/// the IDE is already in a window on a monitor.
/// </summary>
public sealed class NoExternalDisplayService : IExternalDisplayService
{
    /// <inheritdoc />
    public bool IsSupported => false;

    /// <inheritdoc />
    public ExternalDisplay? Display => null;

    /// <inheritdoc />
    public bool IsPresenting => false;

    /// <inheritdoc />
    public DesktopCursor Cursor { get; } = new();

    /// <inheritdoc />
    public bool HasKeyboard => true;

    /// <inheritdoc />
    public event EventHandler? KeyboardChanged
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public event EventHandler? DisplayChanged
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public event EventHandler? PresentationEnded
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public bool Present(Control content, double scale) => false;

    /// <inheritdoc />
    public void Dismiss()
    {
    }
}
