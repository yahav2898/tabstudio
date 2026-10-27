using System.Drawing;
using System.Numerics;

namespace TabStudio.Forms.Rendering;

/// <summary>
/// What a window, a popup or a designer surface looks like: the drawing commands its controls recorded as they
/// painted, in order. Immutable once built, so the Forms thread records it and the UI thread draws it.
/// </summary>
internal sealed class DisplayList
{
    private DisplayList(IReadOnlyList<DrawCommand> commands) => Commands = commands;

    /// <summary>A display list that draws nothing.</summary>
    public static DisplayList Empty { get; } = new([]);

    /// <summary>The commands, in drawing order.</summary>
    public IReadOnlyList<DrawCommand> Commands { get; }

    /// <summary>Creates a display list from recorded commands.</summary>
    /// <param name="commands">The commands; the list is not copied, so it must not change afterwards.</param>
    public static DisplayList From(List<DrawCommand> commands) => new(commands);

    /// <summary>The texts of the display list, in drawing order, for tests and the self test.</summary>
    public IEnumerable<string> Texts => Commands.OfType<TextCommand>().Select(command => command.Text);
}

/// <summary>
/// Records drawing commands, inserting a <see cref="StateCommand"/> whenever the transform, clip or edge mode of the
/// next command differs from the last one recorded.
/// </summary>
internal sealed class DisplayListBuilder
{
    private readonly List<DrawCommand> _commands = [];
    private StateCommand? _state;

    /// <summary>How many commands were recorded so far.</summary>
    public int Count => _commands.Count;

    /// <summary>Records a command drawn in a state.</summary>
    /// <param name="transform">From the command's coordinates to the window's client area.</param>
    /// <param name="clip">The clip, in the window's client area.</param>
    /// <param name="antialias">Whether shapes are antialiased.</param>
    /// <param name="command">The command.</param>
    /// <param name="clipPaths">Shapes the drawing is further clipped to, in the window's client area.</param>
    /// <param name="sourceCopy">Whether shapes and images replace the pixels under them.</param>
    public void Add(Matrix3x2 transform, RectangleF clip, bool antialias, DrawCommand command, IReadOnlyList<PathData>? clipPaths = null, bool sourceCopy = false)
    {
        if (clip.Width <= 0 || clip.Height <= 0)
        {
            // Entirely clipped away.
            return;
        }

        if (_state is null || _state.Transform != transform || _state.Clip != clip || _state.Antialias != antialias
            || !ReferenceEquals(_state.ClipPaths, clipPaths) || _state.SourceCopy != sourceCopy)
        {
            _state = new StateCommand(transform, clip, antialias) { ClipPaths = clipPaths, SourceCopy = sourceCopy };
            _commands.Add(_state);
        }

        _commands.Add(command);
    }

    /// <summary>Appends another display list's commands, as they are: they carry their own states.</summary>
    /// <param name="list">The display list.</param>
    public void AddRange(DisplayList list)
    {
        foreach (var command in list.Commands)
        {
            _commands.Add(command);
        }

        // The next command states its own state again.
        _state = null;
    }

    /// <summary>Builds the display list. The builder must not be used afterwards.</summary>
    public DisplayList Build() => DisplayList.From(_commands);

    /// <summary>A display list of the commands recorded so far; the builder can go on recording.</summary>
    public DisplayList Snapshot() => DisplayList.From([.. _commands]);

    /// <summary>Takes the commands recorded so far, leaving the builder empty and ready to record again.</summary>
    public DisplayList Drain()
    {
        var list = DisplayList.From([.. _commands]);
        Clear();
        return list;
    }

    /// <summary>Forgets every command.</summary>
    public void Clear()
    {
        _commands.Clear();
        _state = null;
    }
}
