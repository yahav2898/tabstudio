using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace TabStudio.App.Editing.Extensions.Svg;

/// <summary>
/// Reads SVG path data, such as <c>M2 2h20v20H2z</c>, into a geometry, by the SVG grammar: absolute and relative
/// commands, repeated parameters, smooth curves, elliptical arcs and the compact forms optimizers write, such as
/// <c>a1 1 0 011 1</c> or <c>1.5.5</c>. As browsers do, drawing stops at the first error and keeps what came before.
/// </summary>
internal sealed class SvgPathReader
{
    private readonly string _data;
    private readonly StreamGeometryContext _context;
    private int _position;
    private Point _current;
    private Point _start;
    private Point _control;
    private char _previous;
    private bool _figureOpen;

    private SvgPathReader(string data, StreamGeometryContext context)
    {
        _data = data;
        _context = context;
    }

    /// <summary>Reads path data into a new geometry.</summary>
    /// <param name="data">The path data, from a <c>d</c> attribute.</param>
    /// <param name="fillRule">How the path is filled.</param>
    public static StreamGeometry Read(string data, FillRule fillRule = FillRule.NonZero)
    {
        ArgumentNullException.ThrowIfNull(data);
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.SetFillRule(fillRule);
            new SvgPathReader(data, context).Read();
        }

        return geometry;
    }

    /// <summary>A geometry through a list of points, as <c>polyline</c> and <c>polygon</c> give them.</summary>
    /// <param name="points">The <c>points</c> attribute.</param>
    /// <param name="closed">Whether the last point joins the first.</param>
    /// <param name="fillRule">How the shape is filled.</param>
    public static StreamGeometry ReadPoints(string points, bool closed, FillRule fillRule = FillRule.NonZero)
    {
        ArgumentNullException.ThrowIfNull(points);
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.SetFillRule(fillRule);
        var reader = new SvgPathReader(points, context);
        var first = true;
        while (reader.TryReadNumber(out var x) && reader.TryReadNumber(out var y))
        {
            if (first)
            {
                context.BeginFigure(new Point(x, y), isFilled: true);
                first = false;
            }
            else
            {
                context.LineTo(new Point(x, y));
            }
        }

        if (!first)
        {
            context.EndFigure(closed);
        }

        return geometry;
    }

    private void Read()
    {
        var command = '\0';
        while (true)
        {
            SkipSeparators();
            if (_position >= _data.Length)
            {
                break;
            }

            var next = _data[_position];
            if (IsCommand(next))
            {
                command = next;
                _position++;
            }
            else if (command is '\0' or 'Z' or 'z' || !IsNumberStart(next))
            {
                break;
            }

            if (!Execute(command))
            {
                break;
            }

            // Numbers after a move are lines.
            command = command switch
            {
                'M' => 'L',
                'm' => 'l',
                _ => command,
            };
        }

        if (_figureOpen)
        {
            _context.EndFigure(false);
        }
    }

    private bool Execute(char command)
    {
        var relative = char.IsLower(command);
        var kind = char.ToUpperInvariant(command);
        switch (kind)
        {
            case 'M':
            {
                if (!TryReadPoint(relative, out var point))
                {
                    return false;
                }

                if (_figureOpen)
                {
                    _context.EndFigure(false);
                }

                _context.BeginFigure(point, isFilled: true);
                _figureOpen = true;
                _current = _start = _control = point;
                break;
            }

            case 'L':
            {
                if (!TryReadPoint(relative, out var point))
                {
                    return false;
                }

                LineTo(point);
                break;
            }

            case 'H':
            {
                if (!TryReadNumber(out var x))
                {
                    return false;
                }

                LineTo(new Point(relative ? _current.X + x : x, _current.Y));
                break;
            }

            case 'V':
            {
                if (!TryReadNumber(out var y))
                {
                    return false;
                }

                LineTo(new Point(_current.X, relative ? _current.Y + y : y));
                break;
            }

            case 'C':
            {
                if (!TryReadPoint(relative, out var first) || !TryReadPoint(relative, out var second) || !TryReadPoint(relative, out var end))
                {
                    return false;
                }

                CurveTo(first, second, end);
                break;
            }

            case 'S':
            {
                var first = _previous is 'C' or 'S' ? Reflect(_control) : _current;
                if (!TryReadPoint(relative, out var second) || !TryReadPoint(relative, out var end))
                {
                    return false;
                }

                CurveTo(first, second, end);
                break;
            }

            case 'Q':
            {
                if (!TryReadPoint(relative, out var control) || !TryReadPoint(relative, out var end))
                {
                    return false;
                }

                QuadraticTo(control, end);
                break;
            }

            case 'T':
            {
                var control = _previous is 'Q' or 'T' ? Reflect(_control) : _current;
                if (!TryReadPoint(relative, out var end))
                {
                    return false;
                }

                QuadraticTo(control, end);
                break;
            }

            case 'A':
            {
                if (!TryReadNumber(out var rx) || !TryReadNumber(out var ry) || !TryReadNumber(out var rotation)
                    || !TryReadFlag(out var largeArc) || !TryReadFlag(out var sweep) || !TryReadPoint(relative, out var end))
                {
                    return false;
                }

                OpenFigure();
                if (rx == 0 || ry == 0)
                {
                    _context.LineTo(end);
                }
                else
                {
                    _context.ArcTo(end, new Size(Math.Abs(rx), Math.Abs(ry)), rotation, largeArc, sweep ? SweepDirection.Clockwise : SweepDirection.CounterClockwise);
                }

                _current = _control = end;
                break;
            }

            case 'Z':
                if (_figureOpen)
                {
                    _context.EndFigure(true);
                    _figureOpen = false;
                }

                _current = _control = _start;
                break;

            default:
                return false;
        }

        _previous = kind;
        return true;
    }

    private void LineTo(Point point)
    {
        OpenFigure();
        _context.LineTo(point);
        _current = _control = point;
    }

    private void CurveTo(Point first, Point second, Point end)
    {
        OpenFigure();
        _context.CubicBezierTo(first, second, end);
        _control = second;
        _current = end;
    }

    private void QuadraticTo(Point control, Point end)
    {
        OpenFigure();
        _context.QuadraticBezierTo(control, end);
        _control = control;
        _current = end;
    }

    /// <summary>After a close, the next segment starts a new figure where the closed one began.</summary>
    private void OpenFigure()
    {
        if (!_figureOpen)
        {
            _context.BeginFigure(_current, isFilled: true);
            _figureOpen = true;
        }
    }

    private Point Reflect(Point control) => new((2 * _current.X) - control.X, (2 * _current.Y) - control.Y);

    private bool TryReadPoint(bool relative, out Point point)
    {
        point = default;
        if (!TryReadNumber(out var x) || !TryReadNumber(out var y))
        {
            return false;
        }

        point = relative ? new Point(_current.X + x, _current.Y + y) : new Point(x, y);
        return true;
    }

    /// <summary>Reads an arc flag, a single 0 or 1 that may touch the next number.</summary>
    private bool TryReadFlag(out bool flag)
    {
        SkipSeparators();
        flag = false;
        if (_position >= _data.Length || _data[_position] is not ('0' or '1'))
        {
            return false;
        }

        flag = _data[_position] == '1';
        _position++;
        return true;
    }

    /// <summary>Reads a number: a sign, digits with at most one point, and an exponent; a second point starts the next number.</summary>
    private bool TryReadNumber(out double value)
    {
        SkipSeparators();
        value = 0;
        var start = _position;
        if (_position < _data.Length && _data[_position] is '+' or '-')
        {
            _position++;
        }

        var digits = 0;
        while (_position < _data.Length && char.IsAsciiDigit(_data[_position]))
        {
            _position++;
            digits++;
        }

        if (_position < _data.Length && _data[_position] == '.')
        {
            _position++;
            while (_position < _data.Length && char.IsAsciiDigit(_data[_position]))
            {
                _position++;
                digits++;
            }
        }

        if (digits == 0)
        {
            _position = start;
            return false;
        }

        if (_position < _data.Length && _data[_position] is 'e' or 'E')
        {
            var exponent = _position + 1;
            if (exponent < _data.Length && _data[exponent] is '+' or '-')
            {
                exponent++;
            }

            if (exponent < _data.Length && char.IsAsciiDigit(_data[exponent]))
            {
                _position = exponent;
                while (_position < _data.Length && char.IsAsciiDigit(_data[_position]))
                {
                    _position++;
                }
            }
        }

        return double.TryParse(_data.AsSpan(start, _position - start), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private void SkipSeparators()
    {
        while (_position < _data.Length && (char.IsWhiteSpace(_data[_position]) || _data[_position] == ','))
        {
            _position++;
        }
    }

    private static bool IsCommand(char c) => c is 'M' or 'm' or 'L' or 'l' or 'H' or 'h' or 'V' or 'v' or 'C' or 'c' or 'S' or 's'
        or 'Q' or 'q' or 'T' or 't' or 'A' or 'a' or 'Z' or 'z';

    private static bool IsNumberStart(char c) => char.IsAsciiDigit(c) || c is '-' or '+' or '.';
}
