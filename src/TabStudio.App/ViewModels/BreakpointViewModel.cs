using AvaloniaEdit.Document;
using TabStudio.Debugging;

namespace TabStudio.App.ViewModels;

/// <summary>How a breakpoint is drawn in the editor's margin.</summary>
public enum BreakpointGlyph
{
    /// <summary>A red dot: the program stops here.</summary>
    Normal,

    /// <summary>A red dot with a mark: the program stops here when the condition is true.</summary>
    Conditional,

    /// <summary>A hollow dot: while debugging, no code follows the line in its file, so it never hits.</summary>
    Unbound,
}

/// <summary>
/// A breakpoint the user set on a line. While its file is open it follows edits, so lines inserted above it move it
/// down, as in Visual Studio; while a debug session runs, it knows the engine breakpoint it is bound to.
/// </summary>
/// <remarks>
/// The line is tracked by an anchor at the start of the line. When an edit deletes the text the anchor was in, or
/// replaces the whole text, the breakpoint goes back to the line number it last had. Use it on the UI thread.
/// </remarks>
public sealed class BreakpointViewModel
{
    private TextAnchor? _anchor;
    private int _line;

    /// <summary>Creates a breakpoint.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="line">One-based line.</param>
    /// <param name="condition">When to stop, or <see langword="null"/> to stop every time.</param>
    public BreakpointViewModel(string filePath, int line, string? condition = null)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        FilePath = filePath;
        _line = Math.Max(1, line);
        Condition = string.IsNullOrWhiteSpace(condition) ? null : condition.Trim();
    }

    /// <summary>Full path of the file.</summary>
    public string FilePath { get; }

    /// <summary>The file name, for lists and messages.</summary>
    public string FileName => Path.GetFileName(FilePath);

    /// <summary>The one-based line it is on now, following edits while the file is open.</summary>
    public int Line => _anchor is { IsDeleted: false } anchor ? anchor.Line : _line;

    /// <summary>When the program stops here, such as <c>count == 3</c>; <see langword="null"/> to stop every time.</summary>
    public string? Condition { get; private set; }

    /// <summary>The engine's breakpoint while a debug session runs, otherwise <see langword="null"/>.</summary>
    public Breakpoint? Binding { get; private set; }

    /// <summary>Whether a debug session runs and found no code at or after this line to stop at.</summary>
    public bool IsUnbound => Binding is { IsBound: false };

    /// <summary>How the margin draws it.</summary>
    public BreakpointGlyph Glyph => IsUnbound ? BreakpointGlyph.Unbound
        : Condition is null ? BreakpointGlyph.Normal
        : BreakpointGlyph.Conditional;

    /// <summary>Starts following edits in the open document of its file.</summary>
    /// <param name="document">The document.</param>
    internal void Attach(TextDocument document)
    {
        _line = Math.Clamp(_line, 1, document.LineCount);
        var anchor = document.CreateAnchor(document.GetLineByNumber(_line).Offset);

        // Text typed at the very start of the line, including Enter, pushes the line and the breakpoint down together.
        anchor.MovementType = AnchorMovementType.AfterInsertion;
        anchor.SurviveDeletion = false;
        _anchor = anchor;
    }

    /// <summary>Stops following edits, when its document closes, and keeps the line it is on.</summary>
    internal void Detach()
    {
        _line = Line;
        _anchor = null;
    }

    /// <summary>After an edit: keeps the line it is on, or goes back to its last line when the edit deleted its anchor.</summary>
    /// <param name="document">Its open document.</param>
    internal void Refresh(TextDocument document)
    {
        if (_anchor is { IsDeleted: false } anchor)
        {
            _line = anchor.Line;
        }
        else if (_anchor is not null)
        {
            Attach(document);
        }
    }

    /// <summary>Moves it to another line, such as the line of the statement it bound to.</summary>
    /// <param name="line">One-based line.</param>
    /// <param name="document">Its open document, or <see langword="null"/> when the file is not open.</param>
    internal void MoveTo(int line, TextDocument? document)
    {
        _line = Math.Max(1, line);
        _anchor = null;
        if (document is not null)
        {
            Attach(document);
        }
    }

    /// <summary>Changes the condition.</summary>
    /// <param name="condition">The condition, or <see langword="null"/> or blank to stop every time.</param>
    internal void SetCondition(string? condition) =>
        Condition = string.IsNullOrWhiteSpace(condition) ? null : condition.Trim();

    /// <summary>Remembers the engine breakpoint of the running debug session, or forgets it when the session ends.</summary>
    /// <param name="binding">The engine breakpoint, or <see langword="null"/>.</param>
    internal void Bind(Breakpoint? binding) => Binding = binding;
}
