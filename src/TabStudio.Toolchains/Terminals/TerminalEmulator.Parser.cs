using System.Text;

namespace TabStudio.Toolchains.Terminals;

/// <summary>The escape sequence parser, after the state machine of DEC's VT500 series that xterm follows.</summary>
public sealed partial class TerminalEmulator
{
    private const int MaxParameters = 32;
    private const int MaxStringLength = 4096;

    private readonly int[] _parameters = new int[MaxParameters];
    private readonly StringBuilder _string = new();
    private ParserState _state;
    private int _parameterCount;
    private int _parameter = -1;
    private char _private;
    private char _intermediate;

    private enum ParserState
    {
        Ground,
        Escape,
        EscapeIntermediate,
        Csi,
        CsiIgnore,
        Osc,
        IgnoreString,
    }

    private void Process(int c)
    {
        switch (_state)
        {
            case ParserState.Osc:
                OscCharacter(c);
                return;
            case ParserState.IgnoreString:
                if (c == 0x1B)
                {
                    _state = ParserState.Escape;
                    _intermediate = '\0';
                }
                else if (c == 0x07)
                {
                    _state = ParserState.Ground;
                }

                return;
        }

        if (c == 0x1B)
        {
            _state = ParserState.Escape;
            _intermediate = '\0';
            return;
        }

        if (c is < 0x20 or 0x7F)
        {
            if (c is 0x18 or 0x1A)
            {
                _state = ParserState.Ground;
            }
            else if (c != 0x7F)
            {
                Execute(c);
            }

            return;
        }

        switch (_state)
        {
            case ParserState.Ground:
                Print(c);
                break;
            case ParserState.Escape:
                if (c is >= 0x20 and <= 0x2F)
                {
                    _intermediate = (char)c;
                    _state = ParserState.EscapeIntermediate;
                }
                else
                {
                    EscapeDispatch('\0', c);
                }

                break;
            case ParserState.EscapeIntermediate:
                if (c is >= 0x20 and <= 0x2F)
                {
                    _intermediate = (char)c;
                }
                else
                {
                    EscapeDispatch(_intermediate, c);
                }

                break;
            case ParserState.Csi:
                CsiCharacter(c);
                break;
            case ParserState.CsiIgnore:
                if (c is >= 0x40 and <= 0x7E)
                {
                    _state = ParserState.Ground;
                }

                break;
        }
    }

    /// <summary>A C0 control character.</summary>
    private void Execute(int c)
    {
        switch (c)
        {
            case 0x07:
                BellCount++;
                break;
            case 0x08:
                _wrapPending = false;
                if (_column > 0)
                {
                    _column--;
                }

                break;
            case 0x09:
                TabForward(1);
                break;
            case 0x0A or 0x0B or 0x0C:
                LineFeed();
                break;
            case 0x0D:
                _column = 0;
                _wrapPending = false;
                break;
            case 0x0E:
                _shiftOut = true;
                break;
            case 0x0F:
                _shiftOut = false;
                break;
        }
    }

    private void EscapeDispatch(char intermediate, int c)
    {
        _state = ParserState.Ground;
        switch (intermediate)
        {
            case '(':
                _g0Graphics = c == '0';
                return;
            case ')':
                _g1Graphics = c == '0';
                return;
            case '#':
                if (c == '8')
                {
                    AlignmentTest();
                }

                return;
            case not '\0':
                return;
        }

        switch (c)
        {
            case '[':
                _state = ParserState.Csi;
                _parameterCount = 0;
                _parameter = -1;
                _private = '\0';
                _intermediate = '\0';
                break;
            case ']':
                _state = ParserState.Osc;
                _string.Clear();
                break;
            case 'P' or 'X' or '^' or '_':
                _state = ParserState.IgnoreString;
                break;
            case '7':
                SaveCursor();
                break;
            case '8':
                RestoreCursor();
                break;
            case 'D':
                Index();
                break;
            case 'E':
                _column = 0;
                LineFeed();
                break;
            case 'H':
                _tabStops[_column] = true;
                break;
            case 'M':
                ReverseIndex();
                break;
            case 'c':
                Reset();
                break;
            case '=' or '>':
                // Application or normal keypad: the on-screen keys always send normal keypad codes.
                break;
        }
    }

    private void CsiCharacter(int c)
    {
        switch (c)
        {
            case >= '0' and <= '9':
                _parameter = Math.Min(65535, (Math.Max(0, _parameter) * 10) + (c - '0'));
                break;
            case ';' or ':':
                PushParameter();
                break;
            case >= 0x3C and <= 0x3F:
                if (_parameterCount == 0 && _parameter < 0 && _private == '\0')
                {
                    _private = (char)c;
                }
                else
                {
                    _state = ParserState.CsiIgnore;
                }

                break;
            case >= 0x20 and <= 0x2F:
                _intermediate = (char)c;
                break;
            case >= 0x40 and <= 0x7E:
                PushParameter();
                _state = ParserState.Ground;
                CsiDispatch((char)c);
                break;
            default:
                _state = ParserState.CsiIgnore;
                break;
        }
    }

    private void PushParameter()
    {
        if (_parameterCount < MaxParameters)
        {
            _parameters[_parameterCount++] = _parameter;
        }

        _parameter = -1;
    }

    /// <summary>A parameter, or a default when it is missing or 0.</summary>
    private int Parameter(int index, int fallback) =>
        index < _parameterCount && _parameters[index] > 0 ? _parameters[index] : fallback;

    /// <summary>A parameter as given, or -1 when it is missing.</summary>
    private int RawParameter(int index) => index < _parameterCount ? _parameters[index] : -1;

    private void OscCharacter(int c)
    {
        if (c == 0x07)
        {
            OscDispatch();
            _state = ParserState.Ground;
        }
        else if (c == 0x1B)
        {
            OscDispatch();
            _state = ParserState.Escape;
            _intermediate = '\0';
        }
        else if (_string.Length < MaxStringLength)
        {
            _string.Append(char.ConvertFromUtf32(c));
        }
    }

    private void OscDispatch()
    {
        var text = _string.ToString();
        _string.Clear();
        var separator = text.IndexOf(';', StringComparison.Ordinal);
        if (separator > 0 && text[..separator] is "0" or "2")
        {
            Title = text[(separator + 1)..];
        }
    }
}
