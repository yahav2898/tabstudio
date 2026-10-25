using System.Globalization;
using System.Text;

namespace TabStudio.Toolchains.Terminals;

/// <summary>What the control functions do to the screen.</summary>
public sealed partial class TerminalEmulator
{
    // DEC Special Graphics, for the characters 0x5F to 0x7E after ESC ( 0: the line drawing set.
    private static readonly int[] LineDrawing =
    [
        0x00A0, 0x25C6, 0x2592, 0x2409, 0x240C, 0x240D, 0x240A, 0x00B0, 0x00B1, 0x2424, 0x240B, 0x2518, 0x2510, 0x250C,
        0x2514, 0x253C, 0x23BA, 0x23BB, 0x2500, 0x23BC, 0x23BD, 0x251C, 0x2524, 0x2534, 0x252C, 0x2502, 0x2264, 0x2265,
        0x03C0, 0x2260, 0x00A3, 0x00B7,
    ];

    private int _row;
    private int _column;
    private bool _wrapPending;
    private TerminalStyle _style;
    private int _top;
    private int _bottom;
    private bool _originMode;
    private bool _autoWrap = true;
    private bool _insertMode;
    private bool _newLineMode;
    private bool _g0Graphics;
    private bool _g1Graphics;
    private bool _shiftOut;
    private int _lastRune = ' ';
    private SavedCursor _savedMain = new(0, 0, default, false, true, false, false, false);
    private SavedCursor _savedAlternate = new(0, 0, default, false, true, false, false, false);

    private readonly record struct SavedCursor(int Row, int Column, TerminalStyle Style, bool OriginMode, bool AutoWrap, bool G0Graphics, bool G1Graphics, bool ShiftOut);

    private TerminalLine CurrentLine => Screen[_row];

    private TerminalCell BlankCell => TerminalCell.Blank(_style);

    private void Print(int c)
    {
        var rune = c is >= 0x5F and <= 0x7E && (_shiftOut ? _g1Graphics : _g0Graphics) ? LineDrawing[c - 0x5F] : c;
        if (!Rune.IsValid(rune))
        {
            return;
        }

        var width = CharacterWidth.Of(new Rune(rune));
        if (width == 0)
        {
            // A combining mark: the cell before it already shows its base character.
            return;
        }

        if (_wrapPending)
        {
            WrapToNextLine();
        }

        if (width == 2 && _column == Columns - 1)
        {
            if (_autoWrap)
            {
                CurrentLine[_column] = BlankCell;
                WrapToNextLine();
            }
            else
            {
                _column = Columns - 2;
            }
        }

        var line = CurrentLine;
        if (_insertMode)
        {
            line.Insert(_column, width, BlankCell);
        }

        ClearWideCharacterAt(line, _column);
        if (width == 2)
        {
            ClearWideCharacterAt(line, _column + 1);
        }

        line[_column] = new TerminalCell(rune, (byte)width, _style);
        if (width == 2)
        {
            line[_column + 1] = new TerminalCell(0, 0, _style);
        }

        _column += width;
        if (_column >= Columns)
        {
            _column = Columns - 1;
            _wrapPending = _autoWrap;
        }

        _lastRune = rune;
    }

    private void WrapToNextLine()
    {
        CurrentLine.IsWrapped = true;
        _column = 0;
        _wrapPending = false;
        Index();
    }

    /// <summary>Overwriting either half of a wide character leaves blanks where the other half was.</summary>
    private void ClearWideCharacterAt(TerminalLine line, int column)
    {
        if (column >= line.Length)
        {
            return;
        }

        var cell = line[column];
        if (cell.IsContinuation && column > 0)
        {
            line[column - 1] = BlankCell;
        }
        else if (cell.Width == 2 && column + 1 < line.Length)
        {
            line[column + 1] = BlankCell;
        }
    }

    private void LineFeed()
    {
        Index();
        if (_newLineMode)
        {
            _column = 0;
        }
    }

    private void Index()
    {
        _wrapPending = false;
        if (_row == _bottom)
        {
            ScrollUp(1);
        }
        else if (_row < Rows - 1)
        {
            _row++;
        }
    }

    private void ReverseIndex()
    {
        _wrapPending = false;
        if (_row == _top)
        {
            ScrollDown(1);
        }
        else if (_row > 0)
        {
            _row--;
        }
    }

    /// <summary>Moves the scroll region's lines up; the top ones of the main screen go to the scrollback.</summary>
    private void ScrollUp(int count)
    {
        var screen = Screen;
        count = Math.Min(count, _bottom - _top + 1);
        for (var i = 0; i < count; i++)
        {
            var removed = screen[_top];
            if (_top == 0 && !IsAlternateScreen)
            {
                AddToScrollback(removed.ForScrollback());
            }

            Array.Copy(screen, _top + 1, screen, _top, _bottom - _top);
            removed.Clear(_style);
            removed.IsWrapped = false;
            screen[_bottom] = removed;
        }
    }

    private void ScrollDown(int count)
    {
        var screen = Screen;
        count = Math.Min(count, _bottom - _top + 1);
        for (var i = 0; i < count; i++)
        {
            var removed = screen[_bottom];
            Array.Copy(screen, _top, screen, _top + 1, _bottom - _top);
            removed.Clear(_style);
            removed.IsWrapped = false;
            screen[_top] = removed;
        }
    }

    private void TabForward(int count)
    {
        _wrapPending = false;
        for (var i = 0; i < count && _column < Columns - 1; i++)
        {
            do
            {
                _column++;
            }
            while (_column < Columns - 1 && !_tabStops[_column]);
        }
    }

    private void TabBackward(int count)
    {
        _wrapPending = false;
        for (var i = 0; i < count && _column > 0; i++)
        {
            do
            {
                _column--;
            }
            while (_column > 0 && !_tabStops[_column]);
        }
    }

    private void SaveCursor()
    {
        var saved = new SavedCursor(_row, _column, _style, _originMode, _autoWrap, _g0Graphics, _g1Graphics, _shiftOut);
        if (IsAlternateScreen)
        {
            _savedAlternate = saved;
        }
        else
        {
            _savedMain = saved;
        }
    }

    private void RestoreCursor()
    {
        var saved = IsAlternateScreen ? _savedAlternate : _savedMain;
        _row = Math.Clamp(saved.Row, 0, Rows - 1);
        _column = Math.Clamp(saved.Column, 0, Columns - 1);
        _style = saved.Style;
        _originMode = saved.OriginMode;
        _autoWrap = saved.AutoWrap;
        _g0Graphics = saved.G0Graphics;
        _g1Graphics = saved.G1Graphics;
        _shiftOut = saved.ShiftOut;
        _wrapPending = false;
    }

    /// <summary>RIS: everything back as it was when the terminal started, but the scrollback.</summary>
    private void Reset()
    {
        IsAlternateScreen = false;
        foreach (var line in _main.Concat(_alternate))
        {
            line.Clear(TerminalStyle.Default);
            line.IsWrapped = false;
        }

        SoftReset();
        _row = 0;
        _column = 0;
        _tabStops = NewTabStops(Columns);
        Title = string.Empty;
    }

    /// <summary>DECSTR: modes, margins and the style back to their defaults.</summary>
    private void SoftReset()
    {
        _style = TerminalStyle.Default;
        _top = 0;
        _bottom = Rows - 1;
        _originMode = false;
        _autoWrap = true;
        _insertMode = false;
        _newLineMode = false;
        _g0Graphics = false;
        _g1Graphics = false;
        _shiftOut = false;
        _wrapPending = false;
        CursorVisible = true;
        ApplicationCursorKeys = false;
        BracketedPaste = false;
        MouseReporting = false;
        _savedMain = new(0, 0, default, false, true, false, false, false);
        _savedAlternate = _savedMain;
    }

    /// <summary>DECALN: fills the screen with E, a test pattern.</summary>
    private void AlignmentTest()
    {
        foreach (var line in Screen)
        {
            line.Fill(0, Columns, new TerminalCell('E', 1, TerminalStyle.Default));
        }

        _row = 0;
        _column = 0;
    }

    private void CsiDispatch(char final)
    {
        if (_private == '?')
        {
            switch (final)
            {
                case 'h':
                    SetPrivateModes(true);
                    break;
                case 'l':
                    SetPrivateModes(false);
                    break;
                case 'J':
                    // Selective erase: there are no protected cells, so it erases as ED and EL do.
                    EraseInDisplay(Math.Max(0, RawParameter(0)));
                    break;
                case 'K':
                    EraseInLine(Math.Max(0, RawParameter(0)));
                    break;
            }

            return;
        }

        if (_private == '>')
        {
            if (final == 'c')
            {
                // Secondary device attributes: a VT220-class terminal, as xterm answers.
                Respond("\u001b[>1;10;0c");
            }

            return;
        }

        if (_private != '\0')
        {
            return;
        }

        if (_intermediate != '\0')
        {
            if (_intermediate == '!' && final == 'p')
            {
                SoftReset();
            }

            // Cursor style (DECSCUSR, " q) and the rest: the Terminal draws one cursor.
            return;
        }

        switch (final)
        {
            case '@':
                CurrentLine.Insert(_column, Parameter(0, 1), BlankCell);
                break;
            case 'A':
                MoveCursorVertically(-Parameter(0, 1));
                break;
            case 'B' or 'e':
                MoveCursorVertically(Parameter(0, 1));
                break;
            case 'C' or 'a':
                _column = Math.Min(Columns - 1, _column + Parameter(0, 1));
                _wrapPending = false;
                break;
            case 'D':
                _column = Math.Max(0, Math.Min(_column, Columns - 1) - Parameter(0, 1));
                _wrapPending = false;
                break;
            case 'E':
                MoveCursorVertically(Parameter(0, 1));
                _column = 0;
                break;
            case 'F':
                MoveCursorVertically(-Parameter(0, 1));
                _column = 0;
                break;
            case 'G' or '`':
                _column = Math.Clamp(Parameter(0, 1) - 1, 0, Columns - 1);
                _wrapPending = false;
                break;
            case 'H' or 'f':
                MoveCursorTo(Parameter(0, 1) - 1, Parameter(1, 1) - 1);
                break;
            case 'I':
                TabForward(Parameter(0, 1));
                break;
            case 'J':
                EraseInDisplay(Math.Max(0, RawParameter(0)));
                break;
            case 'K':
                EraseInLine(Math.Max(0, RawParameter(0)));
                break;
            case 'L':
                InsertLines(Parameter(0, 1));
                break;
            case 'M':
                DeleteLines(Parameter(0, 1));
                break;
            case 'P':
                CurrentLine.Delete(_column, Parameter(0, 1), BlankCell);
                _wrapPending = false;
                break;
            case 'S':
                ScrollUp(Parameter(0, 1));
                break;
            case 'T':
                if (_parameterCount <= 1)
                {
                    ScrollDown(Parameter(0, 1));
                }

                break;
            case 'X':
                CurrentLine.Fill(_column, _column + Parameter(0, 1), BlankCell);
                _wrapPending = false;
                break;
            case 'Z':
                TabBackward(Parameter(0, 1));
                break;
            case 'b':
                for (var i = Math.Min(Parameter(0, 1), Rows * Columns); i > 0; i--)
                {
                    Print(_lastRune);
                }

                break;
            case 'c':
                // Primary device attributes: a VT100 with advanced video.
                Respond("\u001b[?1;2c");
                break;
            case 'd':
                MoveCursorTo(Parameter(0, 1) - 1 - (_originMode ? _top : 0), _column);
                break;
            case 'g':
                ClearTabStops(Math.Max(0, RawParameter(0)));
                break;
            case 'h':
                SetModes(true);
                break;
            case 'l':
                SetModes(false);
                break;
            case 'm':
                SelectGraphicRendition();
                break;
            case 'n':
                DeviceStatusReport(Parameter(0, 0));
                break;
            case 'r':
                SetScrollRegion(Parameter(0, 1) - 1, Parameter(1, Rows) - 1);
                break;
            case 's':
                SaveCursor();
                break;
            case 'u':
                RestoreCursor();
                break;
            case 't':
                if (Parameter(0, 0) == 18)
                {
                    Respond(string.Create(CultureInfo.InvariantCulture, $"\u001b[8;{Rows};{Columns}t"));
                }

                break;
        }
    }

    private void MoveCursorVertically(int delta)
    {
        _wrapPending = false;
        var (top, bottom) = _row >= _top && _row <= _bottom ? (_top, _bottom) : (0, Rows - 1);
        _row = Math.Clamp(_row + delta, top, bottom);
        _column = Math.Min(_column, Columns - 1);
    }

    /// <summary>Moves the cursor; with origin mode on, rows count from the top of the scroll region and stay in it.</summary>
    private void MoveCursorTo(int row, int column)
    {
        _wrapPending = false;
        _row = _originMode ? Math.Clamp(row + _top, _top, _bottom) : Math.Clamp(row, 0, Rows - 1);
        _column = Math.Clamp(column, 0, Columns - 1);
    }

    private void EraseInDisplay(int mode)
    {
        _wrapPending = false;
        var screen = Screen;
        switch (mode)
        {
            case 0:
                EraseInLine(0);
                for (var row = _row + 1; row < Rows; row++)
                {
                    screen[row].Clear(_style);
                    screen[row].IsWrapped = false;
                }

                break;
            case 1:
                for (var row = 0; row < _row; row++)
                {
                    screen[row].Clear(_style);
                    screen[row].IsWrapped = false;
                }

                EraseInLine(1);
                break;
            case 2:
                foreach (var line in screen)
                {
                    line.Clear(_style);
                    line.IsWrapped = false;
                }

                break;
            case 3:
                ClearScrollback();
                break;
        }
    }

    private void EraseInLine(int mode)
    {
        _wrapPending = false;
        var line = CurrentLine;
        switch (mode)
        {
            case 0:
                line.Fill(_column, Columns, BlankCell);
                line.IsWrapped = false;
                break;
            case 1:
                line.Fill(0, _column + 1, BlankCell);
                break;
            case 2:
                line.Clear(_style);
                line.IsWrapped = false;
                break;
        }
    }

    private void InsertLines(int count)
    {
        if (_row < _top || _row > _bottom)
        {
            return;
        }

        var saved = _top;
        _top = _row;
        ScrollDown(count);
        _top = saved;
        _column = 0;
        _wrapPending = false;
    }

    private void DeleteLines(int count)
    {
        if (_row < _top || _row > _bottom)
        {
            return;
        }

        var saved = _top;
        _top = _row;
        var screen = Screen;
        count = Math.Min(count, _bottom - _top + 1);
        for (var i = 0; i < count; i++)
        {
            // Deleted lines do not go to the scrollback, even at the top of the screen.
            var removed = screen[_top];
            Array.Copy(screen, _top + 1, screen, _top, _bottom - _top);
            removed.Clear(_style);
            removed.IsWrapped = false;
            screen[_bottom] = removed;
        }

        _top = saved;
        _column = 0;
        _wrapPending = false;
    }

    private void SetScrollRegion(int top, int bottom)
    {
        top = Math.Clamp(top, 0, Rows - 1);
        bottom = Math.Clamp(bottom, 0, Rows - 1);
        if (top >= bottom)
        {
            return;
        }

        _top = top;
        _bottom = bottom;
        MoveCursorTo(0, 0);
    }

    private void ClearTabStops(int mode)
    {
        if (mode == 0)
        {
            _tabStops[_column] = false;
        }
        else if (mode == 3)
        {
            Array.Clear(_tabStops);
        }
    }

    private void DeviceStatusReport(int request)
    {
        switch (request)
        {
            case 5:
                Respond("\u001b[0n");
                break;
            case 6:
                var row = _originMode ? _row - _top : _row;
                Respond(string.Create(CultureInfo.InvariantCulture, $"\u001b[{row + 1};{_column + 1}R"));
                break;
        }
    }

    private void SetModes(bool on)
    {
        for (var i = 0; i < Math.Max(1, _parameterCount); i++)
        {
            switch (RawParameter(i))
            {
                case 4:
                    _insertMode = on;
                    break;
                case 20:
                    _newLineMode = on;
                    break;
            }
        }
    }

    private void SetPrivateModes(bool on)
    {
        for (var i = 0; i < Math.Max(1, _parameterCount); i++)
        {
            switch (RawParameter(i))
            {
                case 1:
                    ApplicationCursorKeys = on;
                    break;
                case 6:
                    _originMode = on;
                    MoveCursorTo(0, 0);
                    break;
                case 7:
                    _autoWrap = on;
                    _wrapPending = false;
                    break;
                case 25:
                    CursorVisible = on;
                    break;
                case 47:
                    SwitchScreen(on, clear: false);
                    break;
                case 1047:
                    if (!on && IsAlternateScreen)
                    {
                        ClearScreen(_alternate);
                    }

                    SwitchScreen(on, clear: false);
                    break;
                case 1048:
                    if (on)
                    {
                        SaveCursor();
                    }
                    else
                    {
                        RestoreCursor();
                    }

                    break;
                case 1049:
                    if (on && !IsAlternateScreen)
                    {
                        SaveCursor();
                        SwitchScreen(true, clear: true);
                    }
                    else if (!on && IsAlternateScreen)
                    {
                        SwitchScreen(false, clear: false);
                        RestoreCursor();
                    }

                    break;
                case 1000 or 1002 or 1003:
                    MouseReporting = on;
                    break;
                case 2004:
                    BracketedPaste = on;
                    break;
            }
        }
    }

    private void SwitchScreen(bool alternate, bool clear)
    {
        if (alternate == IsAlternateScreen)
        {
            return;
        }

        IsAlternateScreen = alternate;
        if (clear)
        {
            ClearScreen(_alternate);
        }

        _top = 0;
        _bottom = Rows - 1;
        _wrapPending = false;
    }

    private void ClearScreen(TerminalLine[] screen)
    {
        foreach (var line in screen)
        {
            line.Clear(_style);
            line.IsWrapped = false;
        }
    }

    private void SelectGraphicRendition()
    {
        if (_parameterCount == 0)
        {
            _style = TerminalStyle.Default;
            return;
        }

        for (var i = 0; i < _parameterCount; i++)
        {
            var code = Math.Max(0, _parameters[i]);
            switch (code)
            {
                case 0:
                    _style = TerminalStyle.Default;
                    break;
                case 1:
                    SetFlag(TerminalAttributes.Bold, true);
                    break;
                case 2:
                    SetFlag(TerminalAttributes.Dim, true);
                    break;
                case 3:
                    SetFlag(TerminalAttributes.Italic, true);
                    break;
                case 4 or 21:
                    SetFlag(TerminalAttributes.Underline, true);
                    break;
                case 5 or 6:
                    SetFlag(TerminalAttributes.Blink, true);
                    break;
                case 7:
                    SetFlag(TerminalAttributes.Inverse, true);
                    break;
                case 8:
                    SetFlag(TerminalAttributes.Hidden, true);
                    break;
                case 9:
                    SetFlag(TerminalAttributes.Strikethrough, true);
                    break;
                case 22:
                    SetFlag(TerminalAttributes.Bold | TerminalAttributes.Dim, false);
                    break;
                case 23:
                    SetFlag(TerminalAttributes.Italic, false);
                    break;
                case 24:
                    SetFlag(TerminalAttributes.Underline, false);
                    break;
                case 25:
                    SetFlag(TerminalAttributes.Blink, false);
                    break;
                case 27:
                    SetFlag(TerminalAttributes.Inverse, false);
                    break;
                case 28:
                    SetFlag(TerminalAttributes.Hidden, false);
                    break;
                case 29:
                    SetFlag(TerminalAttributes.Strikethrough, false);
                    break;
                case >= 30 and <= 37:
                    _style = _style with { Foreground = TerminalColor.Indexed(code - 30) };
                    break;
                case 38:
                    if (ExtendedColor(ref i) is { } foreground)
                    {
                        _style = _style with { Foreground = foreground };
                    }

                    break;
                case 39:
                    _style = _style with { Foreground = TerminalColor.Default };
                    break;
                case >= 40 and <= 47:
                    _style = _style with { Background = TerminalColor.Indexed(code - 40) };
                    break;
                case 48:
                    if (ExtendedColor(ref i) is { } background)
                    {
                        _style = _style with { Background = background };
                    }

                    break;
                case 49:
                    _style = _style with { Background = TerminalColor.Default };
                    break;
                case >= 90 and <= 97:
                    _style = _style with { Foreground = TerminalColor.Indexed(code - 90 + 8) };
                    break;
                case >= 100 and <= 107:
                    _style = _style with { Background = TerminalColor.Indexed(code - 100 + 8) };
                    break;
            }
        }
    }

    /// <summary>The color after 38 or 48: <c>5;n</c> for an indexed color, <c>2;r;g;b</c> for a 24-bit one.</summary>
    private TerminalColor? ExtendedColor(ref int i)
    {
        switch (RawParameter(i + 1))
        {
            case 5 when i + 2 < _parameterCount:
                i += 2;
                return TerminalColor.Indexed(Math.Max(0, _parameters[i]));
            case 2 when i + 4 < _parameterCount:
                i += 4;
                return TerminalColor.FromRgb(Math.Max(0, _parameters[i - 2]), Math.Max(0, _parameters[i - 1]), Math.Max(0, _parameters[i]));
            default:
                i = _parameterCount;
                return null;
        }
    }

    private void SetFlag(TerminalAttributes flag, bool on) =>
        _style = _style with { Attributes = on ? _style.Attributes | flag : _style.Attributes & ~flag };
}
