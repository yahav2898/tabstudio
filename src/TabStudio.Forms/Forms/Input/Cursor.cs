using System.Drawing;

namespace System.Windows.Forms;

/// <summary>The image of the mouse pointer over a control. On a touch screen, there is no pointer to show.</summary>
public sealed class Cursor : IDisposable
{
    private static Point _position;

    internal Cursor(string name) => Name = name;

    /// <summary>The cursor shown now.</summary>
    public static Cursor? Current { get; set; }

    /// <summary>The pointer's last position, in screen coordinates.</summary>
    public static Point Position
    {
        get => _position;
        set => _position = value;
    }

    /// <summary>The cursor's name, such as <c>Hand</c>.</summary>
    internal string Name { get; }

    /// <summary>The cursor's size.</summary>
    public Size Size => new(32, 32);

    /// <summary>Data about the cursor.</summary>
    public object? Tag { get; set; }

    /// <summary>Hides the pointer; there is none on a touch screen.</summary>
    public static void Hide()
    {
        // The preview has no pointer of its own to hide.
    }

    /// <summary>Shows the pointer; there is none on a touch screen.</summary>
    public static void Show()
    {
        // The preview has no pointer of its own to show.
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // A cursor holds nothing to release.
    }

    /// <inheritdoc />
    public override string ToString() => $"[Cursor: {Name}]";
}

/// <summary>The standard cursors.</summary>
public static class Cursors
{
    /// <summary>The application starting cursor.</summary>
    public static Cursor AppStarting { get; } = new(nameof(AppStarting));

    /// <summary>The arrow cursor.</summary>
    public static Cursor Arrow { get; } = new(nameof(Arrow));

    /// <summary>The crosshair cursor.</summary>
    public static Cursor Cross { get; } = new(nameof(Cross));

    /// <summary>The default cursor: an arrow.</summary>
    public static Cursor Default { get; } = new(nameof(Default));

    /// <summary>The hand cursor, shown over links.</summary>
    public static Cursor Hand { get; } = new(nameof(Hand));

    /// <summary>The help cursor.</summary>
    public static Cursor Help { get; } = new(nameof(Help));

    /// <summary>The horizontal splitter cursor.</summary>
    public static Cursor HSplit { get; } = new(nameof(HSplit));

    /// <summary>The I-beam cursor, shown over text.</summary>
    public static Cursor IBeam { get; } = new(nameof(IBeam));

    /// <summary>The cursor that shows something is not allowed.</summary>
    public static Cursor No { get; } = new(nameof(No));

    /// <summary>The four-headed sizing cursor.</summary>
    public static Cursor SizeAll { get; } = new(nameof(SizeAll));

    /// <summary>The diagonal (north-east to south-west) sizing cursor.</summary>
    public static Cursor SizeNESW { get; } = new(nameof(SizeNESW));

    /// <summary>The vertical sizing cursor.</summary>
    public static Cursor SizeNS { get; } = new(nameof(SizeNS));

    /// <summary>The diagonal (north-west to south-east) sizing cursor.</summary>
    public static Cursor SizeNWSE { get; } = new(nameof(SizeNWSE));

    /// <summary>The horizontal sizing cursor.</summary>
    public static Cursor SizeWE { get; } = new(nameof(SizeWE));

    /// <summary>The up-arrow cursor.</summary>
    public static Cursor UpArrow { get; } = new(nameof(UpArrow));

    /// <summary>The vertical splitter cursor.</summary>
    public static Cursor VSplit { get; } = new(nameof(VSplit));

    /// <summary>The wait cursor: an hourglass.</summary>
    public static Cursor WaitCursor { get; } = new(nameof(WaitCursor));
}
