using System.Text;

namespace TabStudio.Toolchains.Terminals;

/// <summary>One character cell of the screen.</summary>
/// <param name="Rune">The character's code point, or 0 for an empty cell and for the second half of a wide character.</param>
/// <param name="Width">1, or 2 for the first half of a wide character, or 0 for its second half.</param>
/// <param name="Style">Its colors and flags.</param>
public readonly record struct TerminalCell(int Rune, byte Width, TerminalStyle Style)
{
    /// <summary>An empty cell with a style's background, as erasing leaves it.</summary>
    /// <param name="style">The style whose background it keeps.</param>
    public static TerminalCell Blank(TerminalStyle style) => new(0, 1, new TerminalStyle(TerminalColor.Default, style.Background, TerminalTextFlags.None));

    /// <summary>Whether the cell shows nothing: empty, or a space.</summary>
    public bool IsEmpty => Rune is 0 or ' ';

    /// <summary>Whether it is the second half of a wide character, drawn by the cell before it.</summary>
    public bool IsContinuation => Width == 0;

    /// <summary>The character as text; a space for an empty cell.</summary>
    public string Text => Rune <= 0 ? " " : char.ConvertFromUtf32(Rune);
}

/// <summary>One line of the screen or the scrollback.</summary>
public sealed class TerminalLine
{
    private TerminalCell[] _cells;

    /// <summary>Creates a blank line.</summary>
    /// <param name="columns">Its width.</param>
    /// <param name="style">The style whose background fills it.</param>
    public TerminalLine(int columns, TerminalStyle style = default)
    {
        _cells = new TerminalCell[Math.Max(1, columns)];
        Clear(style);
    }

    private TerminalLine(TerminalCell[] cells, bool wrapped)
    {
        _cells = cells;
        IsWrapped = wrapped;
    }

    /// <summary>The cells, one per column.</summary>
    public ReadOnlySpan<TerminalCell> Cells => _cells;

    /// <summary>How many columns it has.</summary>
    public int Length => _cells.Length;

    /// <summary>Whether the text continues on the next line, because it was longer than the screen is wide.</summary>
    public bool IsWrapped { get; internal set; }

    /// <summary>A cell.</summary>
    /// <param name="column">The 0-based column.</param>
    public TerminalCell this[int column]
    {
        get => _cells[column];
        internal set => _cells[column] = value;
    }

    /// <summary>The line's text, without trailing blanks; wide characters once.</summary>
    public string Text => TextBetween(0, _cells.Length);

    /// <summary>The text of some columns, without trailing blanks.</summary>
    /// <param name="start">The first column.</param>
    /// <param name="end">The column after the last.</param>
    public string TextBetween(int start, int end)
    {
        var text = new StringBuilder();
        end = Math.Min(end, _cells.Length);
        for (var column = Math.Max(0, start); column < end; column++)
        {
            var cell = _cells[column];
            if (!cell.IsContinuation)
            {
                text.Append(cell.Rune <= 0 ? " " : char.ConvertFromUtf32(cell.Rune));
            }
        }

        return text.ToString().TrimEnd(' ');
    }

    internal void Clear(TerminalStyle style) => Array.Fill(_cells, TerminalCell.Blank(style));

    internal void Fill(int start, int end, TerminalCell cell)
    {
        start = Math.Clamp(start, 0, _cells.Length);
        end = Math.Clamp(end, start, _cells.Length);
        _cells.AsSpan(start, end - start).Fill(cell);
    }

    internal void Insert(int column, int count, TerminalCell blank)
    {
        if (column >= _cells.Length || count <= 0)
        {
            return;
        }

        count = Math.Min(count, _cells.Length - column);
        Array.Copy(_cells, column, _cells, column + count, _cells.Length - column - count);
        _cells.AsSpan(column, count).Fill(blank);
    }

    internal void Delete(int column, int count, TerminalCell blank)
    {
        if (column >= _cells.Length || count <= 0)
        {
            return;
        }

        count = Math.Min(count, _cells.Length - column);
        Array.Copy(_cells, column + count, _cells, column, _cells.Length - column - count);
        _cells.AsSpan(_cells.Length - count, count).Fill(blank);
    }

    internal void Resize(int columns, TerminalStyle style)
    {
        if (columns == _cells.Length)
        {
            return;
        }

        var old = _cells;
        _cells = new TerminalCell[Math.Max(1, columns)];
        var kept = Math.Min(old.Length, _cells.Length);
        Array.Copy(old, _cells, kept);
        _cells.AsSpan(kept).Fill(TerminalCell.Blank(style));

        // A wide character cut in half by the new width leaves only blanks.
        if (kept > 0 && _cells[kept - 1].Width == 2 && kept == _cells.Length)
        {
            _cells[kept - 1] = TerminalCell.Blank(style);
        }
    }

    /// <summary>A copy for the scrollback, without the trailing empty cells, which take memory and show nothing.</summary>
    internal TerminalLine ForScrollback()
    {
        var length = _cells.Length;
        while (length > 0 && _cells[length - 1] is { Rune: 0, Style.Background.IsDefault: true })
        {
            length--;
        }

        return new TerminalLine(_cells.AsSpan(0, length).ToArray(), IsWrapped);
    }

    /// <summary>A copy as wide as the screen, for a scrollback line that scrolls back onto it.</summary>
    internal TerminalLine Widened(int columns)
    {
        var copy = new TerminalLine([.. _cells], IsWrapped);
        copy.Resize(columns, TerminalStyle.Default);
        return copy;
    }
}
