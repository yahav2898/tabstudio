using System.Text;

namespace TabStudio.Toolchains.Terminals;

/// <summary>
/// A terminal emulator: turns what programs write to a pseudo terminal into a screen of styled character cells,
/// as xterm does, for the Terminal panel to draw.
/// </summary>
/// <remarks>
/// <para>
/// It implements the parts of VT100, VT220 and xterm that shells and full-screen programs use: cursor movement,
/// erasing, insert and delete, scroll regions, the alternate screen (<c>top</c>, <c>vi</c>, <c>less</c>), 16, 256
/// and 24-bit colors and text attributes, application cursor keys, bracketed paste, line drawing characters, wide
/// characters, the window title, and the reports programs ask for (cursor position, device attributes, size).
/// Lines scrolled off the top of the main screen are kept in a scrollback of a fixed size.
/// </para>
/// <para>
/// It is not thread-safe: feed it and read it on one thread, or lock around both.
/// </para>
/// </remarks>
public sealed partial class TerminalEmulator
{
    private readonly Decoder _decoder = new UTF8Encoding(false).GetDecoder();
    private readonly TerminalLine[] _scrollback;
    private int _scrollbackStart;
    private int _scrollbackCount;
    private TerminalLine[] _main;
    private TerminalLine[] _alternate;
    private bool[] _tabStops;
    private char _highSurrogate;

    /// <summary>Creates a terminal with a blank screen.</summary>
    /// <param name="rows">The screen's height in lines.</param>
    /// <param name="columns">The screen's width in columns.</param>
    /// <param name="scrollbackLines">How many lines scrolled off the screen to keep.</param>
    public TerminalEmulator(int rows = 24, int columns = 80, int scrollbackLines = 5000)
    {
        Rows = Math.Max(1, rows);
        Columns = Math.Max(2, columns);
        _scrollback = new TerminalLine[Math.Max(0, scrollbackLines)];
        _main = NewScreen(Rows, Columns);
        _alternate = NewScreen(Rows, Columns);
        _tabStops = NewTabStops(Columns);
        _bottom = Rows - 1;
    }

    /// <summary>Raised when the terminal answers a program, such as with the cursor position; the bytes go to its input.</summary>
    public event EventHandler<byte[]>? Responded;

    /// <summary>The screen's height in lines.</summary>
    public int Rows { get; private set; }

    /// <summary>The screen's width in columns.</summary>
    public int Columns { get; private set; }

    /// <summary>The cursor's line, from 0 at the top of the screen.</summary>
    public int CursorRow => _row;

    /// <summary>The cursor's column, from 0.</summary>
    public int CursorColumn => _column;

    /// <summary>Whether the program shows the cursor.</summary>
    public bool CursorVisible { get; private set; } = true;

    /// <summary>Whether the alternate screen is shown, as full-screen programs such as <c>vi</c> use it.</summary>
    public bool IsAlternateScreen { get; private set; }

    /// <summary>Whether the arrow keys send application sequences (<c>ESC O A</c>), as full-screen programs ask.</summary>
    public bool ApplicationCursorKeys { get; private set; }

    /// <summary>Whether pasted text is wrapped in <c>ESC [200~</c> and <c>ESC [201~</c>.</summary>
    public bool BracketedPaste { get; private set; }

    /// <summary>Whether the program asked for mouse reports, which the Terminal does not send.</summary>
    public bool MouseReporting { get; private set; }

    /// <summary>The window title a program set, or an empty string.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>How many lines the scrollback holds now.</summary>
    public int ScrollbackCount => _scrollbackCount;

    /// <summary>All lines: the scrollback, then the screen.</summary>
    public int TotalLines => _scrollbackCount + Rows;

    /// <summary>Counts changes, so a view knows when to draw again.</summary>
    public long Version { get; private set; }

    /// <summary>How many times a program rang the bell.</summary>
    public int BellCount { get; private set; }

    private TerminalLine[] Screen => IsAlternateScreen ? _alternate : _main;

    /// <summary>A line of the screen.</summary>
    /// <param name="row">From 0 at the top.</param>
    public TerminalLine ScreenLine(int row) => Screen[row];

    /// <summary>A line of the scrollback or the screen.</summary>
    /// <param name="index">From 0 for the oldest scrollback line; <see cref="ScrollbackCount"/> is the screen's top line.</param>
    public TerminalLine Line(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, TotalLines);
        return index < _scrollbackCount
            ? _scrollback[(_scrollbackStart + index) % _scrollback.Length]
            : Screen[index - _scrollbackCount];
    }

    /// <summary>The screen's text, line by line, without trailing blanks; for tests and copying.</summary>
    public string ScreenText => string.Join('\n', Screen.Select(l => l.Text)).TrimEnd('\n');

    /// <summary>Processes what a program wrote.</summary>
    /// <param name="data">Its bytes, UTF-8; a character split between two calls is kept until its end arrives.</param>
    public void Feed(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return;
        }

        var chars = new char[_decoder.GetCharCount(data, flush: false)];
        var count = _decoder.GetChars(data, chars, flush: false);
        Feed(chars.AsSpan(0, count));
    }

    /// <summary>Processes text a program wrote.</summary>
    /// <param name="text">The text.</param>
    public void Feed(ReadOnlySpan<char> text)
    {
        foreach (var c in text)
        {
            if (char.IsHighSurrogate(c))
            {
                _highSurrogate = c;
                continue;
            }

            if (char.IsLowSurrogate(c))
            {
                if (_highSurrogate != '\0')
                {
                    Process(char.ConvertToUtf32(_highSurrogate, c));
                }

                _highSurrogate = '\0';
                continue;
            }

            _highSurrogate = '\0';
            Process(c);
        }

        Version++;
    }

    /// <summary>Processes text a program wrote.</summary>
    /// <param name="text">The text.</param>
    public void Feed(string text) => Feed(text.AsSpan());

    /// <summary>
    /// Changes the screen's size. Lines keep their text, cut or padded; when the screen gets shorter, lines above the
    /// cursor move to the scrollback so the cursor's line stays in view.
    /// </summary>
    /// <param name="rows">The new height.</param>
    /// <param name="columns">The new width.</param>
    public void Resize(int rows, int columns)
    {
        rows = Math.Max(1, rows);
        columns = Math.Max(2, columns);
        if (rows == Rows && columns == Columns)
        {
            return;
        }

        foreach (var line in _main.Concat(_alternate))
        {
            line.Resize(columns, TerminalStyle.Default);
        }

        var mainRow = IsAlternateScreen ? _savedMain.Row : _row;
        var alternateRow = IsAlternateScreen ? _row : _savedAlternate.Row;
        _main = ResizeRows(_main, rows, columns, ref mainRow, toScrollback: true);
        _alternate = ResizeRows(_alternate, rows, columns, ref alternateRow, toScrollback: false);
        Rows = rows;
        Columns = columns;
        _row = IsAlternateScreen ? alternateRow : mainRow;
        _column = Math.Min(_column, columns - 1);
        _savedMain = _savedMain with { Row = Math.Clamp(IsAlternateScreen ? mainRow : _savedMain.Row, 0, rows - 1), Column = Math.Min(_savedMain.Column, columns - 1) };
        _savedAlternate = _savedAlternate with { Row = Math.Clamp(_savedAlternate.Row, 0, rows - 1), Column = Math.Min(_savedAlternate.Column, columns - 1) };
        _top = 0;
        _bottom = rows - 1;
        _wrapPending = false;
        _tabStops = NewTabStops(columns);
        Version++;
    }

    /// <summary>The text between two positions, as the user selected it: wrapped lines joined, others on lines of their own.</summary>
    /// <param name="startLine">The first line, as <see cref="Line"/> counts.</param>
    /// <param name="startColumn">The first column.</param>
    /// <param name="endLine">The last line.</param>
    /// <param name="endColumn">The column after the last.</param>
    public string GetText(int startLine, int startColumn, int endLine, int endColumn)
    {
        if (endLine < startLine || (endLine == startLine && endColumn < startColumn))
        {
            (startLine, startColumn, endLine, endColumn) = (endLine, endColumn, startLine, startColumn);
        }

        startLine = Math.Clamp(startLine, 0, TotalLines - 1);
        endLine = Math.Clamp(endLine, 0, TotalLines - 1);
        var text = new StringBuilder();
        for (var index = startLine; index <= endLine; index++)
        {
            var line = Line(index);
            var from = index == startLine ? startColumn : 0;
            var to = index == endLine ? endColumn : int.MaxValue;
            text.Append(line.TextBetween(from, to));
            if (index < endLine && !line.IsWrapped)
            {
                text.Append('\n');
            }
        }

        return text.ToString();
    }

    /// <summary>Forgets the scrollback.</summary>
    public void ClearScrollback()
    {
        Array.Clear(_scrollback);
        _scrollbackStart = 0;
        _scrollbackCount = 0;
        Version++;
    }

    private void Respond(string text) => Responded?.Invoke(this, Encoding.UTF8.GetBytes(text));

    private void AddToScrollback(TerminalLine line)
    {
        if (_scrollback.Length == 0)
        {
            return;
        }

        if (_scrollbackCount < _scrollback.Length)
        {
            _scrollback[(_scrollbackStart + _scrollbackCount) % _scrollback.Length] = line;
            _scrollbackCount++;
        }
        else
        {
            _scrollback[_scrollbackStart] = line;
            _scrollbackStart = (_scrollbackStart + 1) % _scrollback.Length;
        }
    }

    private TerminalLine[] ResizeRows(TerminalLine[] screen, int rows, int columns, ref int cursorRow, bool toScrollback)
    {
        var lines = screen.ToList();
        if (rows < lines.Count)
        {
            // Keep the cursor's line in view: lines above it go first, then blank lines below it.
            var above = Math.Max(0, cursorRow + 1 - rows);
            for (var i = 0; i < above; i++)
            {
                if (toScrollback)
                {
                    AddToScrollback(lines[0].ForScrollback());
                }

                lines.RemoveAt(0);
            }

            cursorRow -= above;
            lines.RemoveRange(rows, lines.Count - rows);
        }

        while (lines.Count < rows)
        {
            lines.Add(new TerminalLine(columns));
        }

        cursorRow = Math.Clamp(cursorRow, 0, rows - 1);
        return [.. lines];
    }

    private static TerminalLine[] NewScreen(int rows, int columns)
    {
        var screen = new TerminalLine[rows];
        for (var i = 0; i < rows; i++)
        {
            screen[i] = new TerminalLine(columns);
        }

        return screen;
    }

    private static bool[] NewTabStops(int columns)
    {
        var stops = new bool[columns];
        for (var column = 8; column < columns; column += 8)
        {
            stops[column] = true;
        }

        return stops;
    }
}
