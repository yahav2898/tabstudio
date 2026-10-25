using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Input.TextInput;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using TabStudio.App.ViewModels;
using TabStudio.Toolchains.Terminals;

namespace TabStudio.App.Controls;

/// <summary>
/// Draws a <see cref="TerminalEmulator"/>'s screen and scrollback, and turns keys, typing, pastes and touches into
/// what a terminal sends: the Terminal panel's screen.
/// </summary>
/// <remarks>
/// <para>
/// With a mouse, dragging selects text and the wheel scrolls back. With a finger, dragging scrolls, a long press
/// starts a selection, and a tap brings up the on-screen keyboard. Ctrl+Shift+C (or Ctrl+C with a selection) copies,
/// Ctrl+Shift+V, Ctrl+V and Shift+Insert paste; the context menu has the same.
/// </para>
/// <para>
/// IDE shortcuts that a shell has no use for stay with the IDE: the function keys (F5 runs), Ctrl+S, Ctrl+Tab and
/// Ctrl+`.
/// </para>
/// </remarks>
public sealed class TerminalView : Control
{
    /// <summary>The panel whose screen this shows.</summary>
    public static readonly StyledProperty<TerminalViewModel?> TerminalProperty =
        AvaloniaProperty.Register<TerminalView, TerminalViewModel?>(nameof(Terminal));

    /// <summary>The font size.</summary>
    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<TerminalView, double>(nameof(FontSize), 13);

    private static readonly TimeSpan LongPress = TimeSpan.FromMilliseconds(550);
    private const uint DefaultForeground = 0xD7DAE0;
    private const uint DefaultBackground = 0x16181D;
    private const uint CursorColor = 0x6FD6C4;
    private static readonly IPen CursorPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(0x6F, 0xD6, 0xC4)), 1);

    private readonly Dictionary<uint, IBrush> _brushes = [];
    private readonly InputClient _client;
    private readonly DispatcherTimer _longPress;
    private Typeface _regular;
    private Typeface _bold;
    private IGlyphTypeface? _glyphs;
    private double _cellWidth = 8;
    private double _cellHeight = 16;
    private int _scrollBack;
    private int _knownScrollback;
    private (int Line, int Column)? _anchor;
    private (int Line, int Column) _caret;
    private bool _selecting;
    private Point _touchStart;
    private double _touchScrolled;
    private bool _touchMoved;

    static TerminalView()
    {
        FocusableProperty.OverrideDefaultValue<TerminalView>(true);
        ClipToBoundsProperty.OverrideDefaultValue<TerminalView>(true);
        AffectsRender<TerminalView>(FontSizeProperty);
    }

    /// <summary>Creates the view.</summary>
    public TerminalView()
    {
        _client = new InputClient(this);
        _longPress = new DispatcherTimer { Interval = LongPress };
        _longPress.Tick += OnLongPress;

        // The on-screen keyboard: no suggestions or corrections, which would rewrite what was typed, and an Enter
        // key that sends Enter rather than closing the keyboard.
        TextInputOptions.SetShowSuggestions(this, false);
        TextInputOptions.SetAutoCapitalization(this, false);
        TextInputOptions.SetMultiline(this, false);
        TextInputOptions.SetReturnKeyType(this, TextInputReturnKeyType.Return);
        TextInputMethodClientRequested += (_, e) => e.Client = _client;
        ContextMenu = CreateContextMenu();
        UpdateFont();
    }

    /// <summary>The panel whose screen this shows.</summary>
    public TerminalViewModel? Terminal
    {
        get => GetValue(TerminalProperty);
        set => SetValue(TerminalProperty, value);
    }

    /// <summary>The font size.</summary>
    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>The size of one character cell.</summary>
    public Size CellSize => new(_cellWidth, _cellHeight);

    /// <summary>Whether text is selected.</summary>
    public bool HasSelection => _anchor is { } anchor && anchor != _caret;

    /// <summary>The selected text, or an empty string.</summary>
    public string SelectedText => _anchor is { } anchor && HasSelection && Terminal is { } terminal
        ? terminal.Emulator.GetText(anchor.Line, anchor.Column, _caret.Line, _caret.Column)
        : string.Empty;

    private Rect CursorRectangle => Terminal is { } terminal
        ? new Rect(terminal.Emulator.CursorColumn * _cellWidth, (terminal.Emulator.CursorRow + _scrollBack) * _cellHeight, _cellWidth, _cellHeight)
        : default;

    /// <summary>Selects everything in the scrollback and on the screen.</summary>
    public void SelectAll()
    {
        if (Terminal is { } terminal)
        {
            _anchor = (0, 0);
            _caret = (terminal.Emulator.TotalLines - 1, terminal.Emulator.Columns);
            InvalidateVisual();
        }
    }

    /// <summary>Copies the selection to the clipboard.</summary>
    public async Task CopyAsync()
    {
        var text = SelectedText;
        if (text.Length > 0 && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }

    /// <summary>Pastes the clipboard's text into the shell.</summary>
    public async Task PasteAsync()
    {
        if (Terminal is { } terminal && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard && await clipboard.TryGetTextAsync() is { Length: > 0 } text)
        {
            terminal.Paste(text);
            ScrollToBottom();
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TerminalProperty)
        {
            if (change.OldValue is TerminalViewModel old)
            {
                old.Changed -= OnTerminalChanged;
                old.FocusRequested -= OnFocusRequested;
            }

            if (change.NewValue is TerminalViewModel terminal)
            {
                terminal.Changed += OnTerminalChanged;
                terminal.FocusRequested += OnFocusRequested;
                _knownScrollback = terminal.Emulator.ScrollbackCount;
                ResizeTerminal(Bounds.Size);
            }

            InvalidateVisual();
        }
        else if (change.Property == FontSizeProperty)
        {
            UpdateFont();
            ResizeTerminal(Bounds.Size);
        }
        else if (change.Property == BoundsProperty)
        {
            ResizeTerminal(Bounds.Size);
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateFont();
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize) => new(
        double.IsInfinity(availableSize.Width) ? 80 * _cellWidth : availableSize.Width,
        double.IsInfinity(availableSize.Height) ? 24 * _cellHeight : availableSize.Height);

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(Brush(DefaultBackground), bounds);
        if (Terminal is not { } terminal)
        {
            return;
        }

        var emulator = terminal.Emulator;
        _scrollBack = Math.Clamp(_scrollBack, 0, emulator.ScrollbackCount);
        var top = emulator.ScrollbackCount - _scrollBack;
        for (var row = 0; row < emulator.Rows && row * _cellHeight < bounds.Height; row++)
        {
            var index = top + row;
            if (index >= 0 && index < emulator.TotalLines)
            {
                DrawLine(context, emulator.Line(index), index, row);
            }
        }

        DrawCursor(context, terminal);
        DrawScrollbar(context, emulator, bounds);
    }

    /// <inheritdoc />
    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || Terminal is not { } terminal)
        {
            return;
        }

        var control = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        if ((control && shift && e.Key == Key.C) || (control && !shift && e.Key == Key.C && HasSelection))
        {
            _ = CopyAsync();
            e.Handled = true;
            return;
        }

        if ((control && e.Key == Key.V) || (shift && e.Key == Key.Insert))
        {
            _ = PasteAsync();
            e.Handled = true;
            return;
        }

        if (IsIdeShortcut(e))
        {
            return;
        }

        var modifiers = Modifiers(e.KeyModifiers);
        if (KeyOf(e.Key) is { } key)
        {
            terminal.SendKey(key, modifiers);
        }
        else if (control && CharacterOf(e.Key, shift) is { } character)
        {
            terminal.SendText(character.ToString(), modifiers & ~TerminalModifiers.Shift);
        }
        else
        {
            return;
        }

        ClearSelection();
        ScrollToBottom();
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (!e.Handled && Terminal is { } terminal && !string.IsNullOrEmpty(e.Text))
        {
            terminal.SendText(e.Text);
            ClearSelection();
            ScrollToBottom();
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var point = e.GetCurrentPoint(this);
        if (e.Pointer.Type == PointerType.Touch)
        {
            _touchStart = point.Position;
            _touchScrolled = 0;
            _touchMoved = false;
            _selecting = false;
            _longPress.Start();
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        var position = PositionAt(point.Position);
        if (e.ClickCount == 2)
        {
            SelectWord(position);
        }
        else
        {
            _anchor = position;
            _caret = position;
            _selecting = true;
            e.Pointer.Capture(this);
        }

        InvalidateVisual();
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var position = e.GetPosition(this);
        if (e.Pointer.Type == PointerType.Touch && !_selecting)
        {
            var dy = position.Y - _touchStart.Y;
            if (!_touchMoved && Math.Abs(dy) < 8 && Math.Abs(position.X - _touchStart.X) < 8)
            {
                return;
            }

            _touchMoved = true;
            _longPress.Stop();
            var lines = (int)((dy - _touchScrolled) / _cellHeight);
            if (lines != 0)
            {
                _touchScrolled += lines * _cellHeight;
                ScrollBy(lines);
            }

            e.Handled = true;
            return;
        }

        if (_selecting)
        {
            _caret = PositionAt(position);
            InvalidateVisual();
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _longPress.Stop();
        if (e.Pointer.Type == PointerType.Touch && !_selecting && !_touchMoved)
        {
            // A tap: the keyboard comes up again if it was put away, and a selection goes.
            ClearSelection();
            if (IsFocused && TopLevel.GetTopLevel(this)?.FocusManager is { } focus)
            {
                focus.ClearFocus();
                Focus();
            }
        }

        if (_selecting && !HasSelection)
        {
            _anchor = null;
        }

        _selecting = false;
        e.Pointer.Capture(null);
    }

    /// <inheritdoc />
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (Terminal is not { } terminal)
        {
            return;
        }

        var lines = (int)Math.Round(e.Delta.Y * 3);
        if (terminal.Emulator.IsAlternateScreen)
        {
            // Full-screen programs scroll their own text: the wheel sends arrow keys, as xterm's alternate scroll mode does.
            for (var i = 0; i < Math.Abs(lines); i++)
            {
                terminal.SendKey(lines > 0 ? TerminalKey.Up : TerminalKey.Down);
            }
        }
        else
        {
            ScrollBy(lines);
        }

        e.Handled = true;
    }

    private static bool IsIdeShortcut(KeyEventArgs e) =>
        e.Key is >= Key.F1 and <= Key.F12
        || (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key is Key.S or Key.Tab or Key.OemTilde or Key.F4);

    private static TerminalModifiers Modifiers(KeyModifiers modifiers) =>
        (modifiers.HasFlag(KeyModifiers.Shift) ? TerminalModifiers.Shift : TerminalModifiers.None)
        | (modifiers.HasFlag(KeyModifiers.Alt) ? TerminalModifiers.Alt : TerminalModifiers.None)
        | (modifiers.HasFlag(KeyModifiers.Control) ? TerminalModifiers.Control : TerminalModifiers.None);

    private static TerminalKey? KeyOf(Key key) => key switch
    {
        Key.Enter => TerminalKey.Enter,
        Key.Back => TerminalKey.Backspace,
        Key.Tab => TerminalKey.Tab,
        Key.Escape => TerminalKey.Escape,
        Key.Up => TerminalKey.Up,
        Key.Down => TerminalKey.Down,
        Key.Left => TerminalKey.Left,
        Key.Right => TerminalKey.Right,
        Key.Home => TerminalKey.Home,
        Key.End => TerminalKey.End,
        Key.Insert => TerminalKey.Insert,
        Key.Delete => TerminalKey.Delete,
        Key.PageUp => TerminalKey.PageUp,
        Key.PageDown => TerminalKey.PageDown,
        _ => null,
    };

    /// <summary>The character a key makes, for Control combinations, which bring no text input.</summary>
    private static char? CharacterOf(Key key, bool shift) => key switch
    {
        >= Key.A and <= Key.Z => (char)((shift ? 'A' : 'a') + (key - Key.A)),
        >= Key.D0 and <= Key.D9 => (char)('0' + (key - Key.D0)),
        Key.Space => ' ',
        Key.OemOpenBrackets => '[',
        Key.OemCloseBrackets => ']',
        Key.OemPipe or Key.OemBackslash => '\\',
        Key.OemMinus => shift ? '_' : '-',
        Key.OemQuestion => shift ? '?' : '/',
        _ => null,
    };

    private void OnTerminalChanged(object? sender, EventArgs e)
    {
        if (Terminal is { } terminal)
        {
            // Scrolled back, the view stays on the same lines while new ones arrive below.
            var count = terminal.Emulator.ScrollbackCount;
            if (_scrollBack > 0 && count > _knownScrollback)
            {
                _scrollBack += count - _knownScrollback;
            }

            _knownScrollback = count;
        }

        InvalidateVisual();
    }

    private void OnFocusRequested(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() => Focus(), DispatcherPriority.Background);

    private void OnLongPress(object? sender, EventArgs e)
    {
        _longPress.Stop();
        if (_touchMoved)
        {
            return;
        }

        // A long press starts a selection at the word under the finger; dragging then extends it.
        SelectWord(PositionAt(_touchStart));
        _selecting = true;
        InvalidateVisual();
    }

    private void UpdateFont()
    {
        var family = this.FindResource("CodeFont") as FontFamily ?? new FontFamily("monospace");
        _regular = new Typeface(family);
        _bold = new Typeface(family, FontStyle.Normal, FontWeight.Bold);
        _glyphs = _regular.GlyphTypeface;
        var sample = new FormattedText("MMMMMMMMMM", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, _regular, FontSize, Brushes.White);
        _cellWidth = Math.Max(1, sample.WidthIncludingTrailingWhitespace / 10);
        _cellHeight = Math.Max(1, Math.Ceiling(sample.Height));
        InvalidateVisual();
    }

    private void ResizeTerminal(Size size)
    {
        if (Terminal is { } terminal && size.Width > 0 && size.Height > 0)
        {
            terminal.Resize((int)(size.Height / _cellHeight), (int)((size.Width - 6) / _cellWidth));
        }
    }

    private void ScrollBy(int lines)
    {
        if (Terminal is { } terminal)
        {
            _scrollBack = Math.Clamp(_scrollBack + lines, 0, terminal.Emulator.ScrollbackCount);
            InvalidateVisual();
        }
    }

    private void ScrollToBottom()
    {
        if (_scrollBack != 0)
        {
            _scrollBack = 0;
            InvalidateVisual();
        }
    }

    private void ClearSelection()
    {
        if (_anchor is not null)
        {
            _anchor = null;
            InvalidateVisual();
        }
    }

    private (int Line, int Column) PositionAt(Point point)
    {
        if (Terminal is not { } terminal)
        {
            return (0, 0);
        }

        var emulator = terminal.Emulator;
        var row = Math.Clamp((int)(point.Y / _cellHeight), 0, emulator.Rows - 1);
        var column = Math.Clamp((int)Math.Round(point.X / _cellWidth), 0, emulator.Columns);
        return (Math.Clamp(emulator.ScrollbackCount - _scrollBack + row, 0, emulator.TotalLines - 1), column);
    }

    private void SelectWord((int Line, int Column) position)
    {
        if (Terminal is not { } terminal)
        {
            return;
        }

        var line = terminal.Emulator.Line(position.Line);
        bool IsWord(int column) => column >= 0 && column < line.Length && !line[column].IsEmpty;
        var start = Math.Min(position.Column, line.Length - 1);
        var end = start;
        while (IsWord(start - 1))
        {
            start--;
        }

        while (IsWord(end))
        {
            end++;
        }

        _anchor = (position.Line, start);
        _caret = (position.Line, Math.Max(end, start + 1));
    }

    private bool IsSelected(int line, int column)
    {
        if (_anchor is not { } anchor || !HasSelection)
        {
            return false;
        }

        var (start, end) = anchor.CompareTo(_caret) <= 0 ? (anchor, _caret) : (_caret, anchor);
        var here = (line, column);
        return here.CompareTo(start) >= 0 && here.CompareTo((end.Line, end.Column - 1)) <= 0;
    }

    private void DrawLine(DrawingContext context, TerminalLine line, int index, int row)
    {
        var y = row * _cellHeight;
        var columns = Math.Min(line.Length, Terminal!.Emulator.Columns);

        // Backgrounds first, in runs of one color, then the selection over them.
        var runStart = 0;
        var runColor = uint.MaxValue;
        for (var column = 0; column <= columns; column++)
        {
            var color = column < columns ? Colors(line[column]).Background : uint.MaxValue;
            if (column < columns && IsSelected(index, column))
            {
                color = 0x2B4A6F;
            }

            if (color != runColor || column == columns)
            {
                if (runColor != uint.MaxValue && runColor != DefaultBackground)
                {
                    context.FillRectangle(Brush(runColor), new Rect(runStart * _cellWidth, y, (column - runStart) * _cellWidth, _cellHeight));
                }

                runStart = column;
                runColor = color;
            }
        }

        // Text, in runs of one style that the terminal font draws; other characters one cell at a time.
        var text = new StringBuilder();
        var textStart = 0;
        var textStyle = default(TerminalStyle);
        void FlushText()
        {
            if (text.Length > 0)
            {
                DrawText(context, text.ToString(), textStyle, textStart, y);
                text.Clear();
            }
        }

        for (var column = 0; column < columns; column++)
        {
            var cell = line[column];
            if (cell.IsContinuation)
            {
                continue;
            }

            if (cell.IsEmpty || cell.Style.Has(TerminalAttributes.Hidden))
            {
                FlushText();
                continue;
            }

            var simple = cell.Width == 1 && cell.Rune < 0x10000 && _glyphs?.TryGetGlyph((uint)cell.Rune, out _) == true;
            if (!simple)
            {
                FlushText();
                DrawText(context, cell.Text, cell.Style, column, y);
                continue;
            }

            if (text.Length > 0 && (cell.Style != textStyle || textStart + text.Length != column))
            {
                FlushText();
            }

            if (text.Length == 0)
            {
                textStart = column;
                textStyle = cell.Style;
            }

            text.Append((char)cell.Rune);
        }

        FlushText();
    }

    private void DrawText(DrawingContext context, string text, TerminalStyle style, int column, double y)
    {
        var foreground = Colors(new TerminalCell(' ', 1, style)).Foreground;
        var brush = Brush(foreground);
        var typeface = style.Has(TerminalAttributes.Bold) ? _bold : _regular;
        if (style.Has(TerminalAttributes.Italic))
        {
            typeface = new Typeface(typeface.FontFamily, FontStyle.Italic, typeface.Weight);
        }

        var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, FontSize, brush);
        if (style.Has(TerminalAttributes.Underline) || style.Has(TerminalAttributes.Strikethrough))
        {
            var decorations = new TextDecorationCollection();
            if (style.Has(TerminalAttributes.Underline))
            {
                decorations.AddRange(TextDecorations.Underline);
            }

            if (style.Has(TerminalAttributes.Strikethrough))
            {
                decorations.AddRange(TextDecorations.Strikethrough);
            }

            formatted.SetTextDecorations(decorations);
        }

        using (context.PushOpacity(style.Has(TerminalAttributes.Dim) ? 0.6 : 1))
        {
            context.DrawText(formatted, new Point(column * _cellWidth, y + Math.Max(0, (_cellHeight - formatted.Height) / 2)));
        }
    }

    private void DrawCursor(DrawingContext context, TerminalViewModel terminal)
    {
        var emulator = terminal.Emulator;
        if (!terminal.IsRunning || !emulator.CursorVisible || _scrollBack > 0)
        {
            return;
        }

        var rect = new Rect(emulator.CursorColumn * _cellWidth, emulator.CursorRow * _cellHeight, _cellWidth, _cellHeight);
        if (IsFocused)
        {
            context.FillRectangle(Brush(CursorColor), rect);
            var cell = emulator.ScreenLine(emulator.CursorRow)[Math.Min(emulator.CursorColumn, emulator.Columns - 1)];
            if (!cell.IsEmpty && !cell.IsContinuation)
            {
                var formatted = new FormattedText(cell.Text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, cell.Style.Has(TerminalAttributes.Bold) ? _bold : _regular, FontSize, Brush(DefaultBackground));
                context.DrawText(formatted, rect.TopLeft);
            }
        }
        else
        {
            context.DrawRectangle(null, CursorPen, rect.Deflate(0.5));
        }
    }

    private void DrawScrollbar(DrawingContext context, TerminalEmulator emulator, Rect bounds)
    {
        if (emulator.ScrollbackCount == 0 || _scrollBack == 0)
        {
            return;
        }

        var total = (double)emulator.TotalLines;
        var height = Math.Max(24, bounds.Height * emulator.Rows / total);
        var top = (bounds.Height - height) * (emulator.ScrollbackCount - _scrollBack) / emulator.ScrollbackCount;
        context.FillRectangle(Brush(0x4A5263), new Rect(bounds.Width - 5, top, 3, height), 1.5f);
    }

    /// <summary>A cell's colors as drawn: inverse swaps them, and the defaults are the Terminal's own.</summary>
    private static (uint Foreground, uint Background) Colors(TerminalCell cell)
    {
        var foreground = cell.Style.Foreground.ToRgb(DefaultForeground);
        var background = cell.Style.Background.ToRgb(DefaultBackground);
        return cell.Style.Has(TerminalAttributes.Inverse) ? (background, foreground) : (foreground, background);
    }

    private IBrush Brush(uint rgb)
    {
        if (!_brushes.TryGetValue(rgb, out var brush))
        {
            brush = new ImmutableSolidColorBrush(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
            _brushes[rgb] = brush;
        }

        return brush;
    }

    private ContextMenu CreateContextMenu()
    {
        MenuItem Item(string header, Action action, string? gesture = null)
        {
            var item = new MenuItem { Header = header, InputGesture = gesture is null ? null : KeyGesture.Parse(gesture) };
            item.Click += (_, _) => action();
            return item;
        }

        return new ContextMenu
        {
            ItemsSource = new Control[]
            {
                Item("Copy", () => _ = CopyAsync(), "Ctrl+Shift+C"),
                Item("Paste", () => _ = PasteAsync(), "Ctrl+Shift+V"),
                Item("Select All", SelectAll),
                new Separator(),
                Item("Clear Terminal", () => Terminal?.ClearCommand.Execute(null)),
                Item("Restart Shell", () => Terminal?.RestartCommand.Execute(null)),
            },
        };
    }

    /// <summary>
    /// Lets the on-screen keyboard type into the Terminal. It reports no surrounding text, so keyboards send
    /// Backspace as a key rather than editing text the Terminal does not have.
    /// </summary>
    private sealed class InputClient(TerminalView view) : TextInputMethodClient
    {
        public override Visual TextViewVisual => view;

        public override bool SupportsPreedit => false;

        public override bool SupportsSurroundingText => false;

        public override string SurroundingText => string.Empty;

        public override Rect CursorRectangle => view.CursorRectangle;

        public override TextSelection Selection { get; set; }
    }
}
