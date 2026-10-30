using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using TabStudio.App.Editing;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Views;

/// <summary>
/// The debugger in the code editor: the breakpoint margin, the highlighted statements, DataTips while the program is
/// paused (hover with a mouse, long-press on touch), and the exception helper at the line that threw.
/// </summary>
public sealed partial class CodeEditorView
{
    // The popups draw in the window's overlay layer, so they are part of the window like the rest of the IDE.
    private readonly Popup _dataTip = new()
    {
        Placement = PlacementMode.BottomEdgeAlignedLeft,
        ShouldUseOverlayLayer = true,
        IsLightDismissEnabled = true,
        OverlayDismissEventPassThrough = true,
    };

    private readonly ExceptionHelperView _exceptionView = new();
    private readonly Popup _exceptionPopup = new() { Placement = PlacementMode.BottomEdgeAlignedLeft, ShouldUseOverlayLayer = true };
    private readonly DispatcherTimer _dataTipCloseTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private DateTime _dataTipHoldAt = DateTime.MinValue;
    private BreakpointMargin _breakpointMargin;
    private DebugLineRenderer _debugLines;
    private ExecutionPoint? _shownCurrent;
    private ExecutionPoint? _shownCaller;
    private int _dataTipVersion;

    /// <summary>The breakpoint margin, for tests.</summary>
    internal BreakpointMargin GlyphMargin => _breakpointMargin;

    /// <summary>Whether the exception helper is open, for tests.</summary>
    internal bool IsExceptionHelperOpen => _exceptionPopup.IsOpen;

    /// <summary>Whether a DataTip is open, for tests.</summary>
    internal bool IsDataTipOpen => _dataTip.IsOpen;

    /// <summary>Adds the debugger's margin, highlights and popups to the editor. Called once, by the constructor.</summary>
    [MemberNotNull(nameof(_breakpointMargin), nameof(_debugLines))]
    private void InitializeDebugging()
    {
        var textView = _editor.TextArea.TextView;
        _debugLines = new DebugLineRenderer(() => _document.Breakpoints.Select(b => b.Line));
        textView.BackgroundRenderers.Add(_debugLines);

        var lines = _debugLines;
        _breakpointMargin = new BreakpointMargin(
            () => _document.Breakpoints.Select(b => new BreakpointMark(b.Line, b.Glyph)).ToList(),
            () => lines.CurrentLine(_editor.Document),
            () => lines.CallerLine(_editor.Document));
        _editor.TextArea.LeftMargins.Insert(0, _breakpointMargin);
        _breakpointMargin.LineTapped += OnBreakpointMarginTapped;
        _breakpointMargin.LineMenuRequested += OnBreakpointMarginMenuRequested;

        // Logical children of the editor, so the popups get the app's styles; they are placed at the text.
        _dataTip.PlacementTarget = textView;
        _exceptionPopup.PlacementTarget = textView;
        _exceptionPopup.Child = _exceptionView;
        LogicalChildren.Add(_dataTip);
        LogicalChildren.Add(_exceptionPopup);

        _document.DebugStateChanged += OnDebugStateChanged;
        _document.Document.Changed += OnDocumentChangedForDebugging;
        textView.PointerHover += OnDebugPointerHover;
        textView.PointerHoverStopped += OnDebugPointerHoverStopped;
        // The text area captures touches, so a long press is raised on it and bubbles to the editor.
        _editor.AddHandler(HoldingEvent, OnEditorHolding, RoutingStrategies.Bubble, handledEventsToo: true);
        textView.VisualLinesChanged += OnVisualLinesChangedForDebugging;
        _dataTipCloseTimer.Tick += OnDataTipCloseTimerTick;
        PropertyChanged += OnPropertyChangedForDebugging;
        ApplyDebugState();
    }

    /// <summary>Removes the debugger's handlers and closes its popups. Called by <see cref="Dispose"/>.</summary>
    private void DisposeDebugging()
    {
        var textView = _editor.TextArea.TextView;
        _document.DebugStateChanged -= OnDebugStateChanged;
        _document.Document.Changed -= OnDocumentChangedForDebugging;
        textView.PointerHover -= OnDebugPointerHover;
        textView.PointerHoverStopped -= OnDebugPointerHoverStopped;
        _editor.RemoveHandler(HoldingEvent, OnEditorHolding);
        textView.VisualLinesChanged -= OnVisualLinesChangedForDebugging;
        _dataTipCloseTimer.Stop();
        _dataTipCloseTimer.Tick -= OnDataTipCloseTimerTick;
        PropertyChanged -= OnPropertyChangedForDebugging;
        _breakpointMargin.LineTapped -= OnBreakpointMarginTapped;
        _breakpointMargin.LineMenuRequested -= OnBreakpointMarginMenuRequested;
        _dataTip.IsOpen = false;
        _exceptionPopup.IsOpen = false;
    }

    private void OnDebugStateChanged(object? sender, EventArgs e) => ApplyDebugState();

    private void ApplyDebugState()
    {
        // Statements are re-marked only when they change, so highlights that followed edits stay where they are.
        if (_document.CurrentStatement != _shownCurrent || _document.CallerStatement != _shownCaller)
        {
            _shownCurrent = _document.CurrentStatement;
            _shownCaller = _document.CallerStatement;
            _debugLines.SetStatements(Span(_shownCurrent), Span(_shownCaller), _editor.Document.TextLength);
        }

        _editor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
        _breakpointMargin.InvalidateVisual();
        if (_document.CurrentStatement is null)
        {
            CloseDataTip();
        }

        UpdateExceptionHelper();
    }

    private void OnDocumentChangedForDebugging(object? sender, DocumentChangeEventArgs e) => _debugLines.OnDocumentChanged(e);

    private void OnVisualLinesChangedForDebugging(object? sender, EventArgs e)
    {
        // The exception helper follows its line as the editor scrolls; it is placed after layout.
        if (_document.ExceptionHelper is not null || _exceptionPopup.IsOpen)
        {
            Dispatcher.UIThread.Post(UpdateExceptionHelper, DispatcherPriority.Background);
        }
    }

    private void OnPropertyChangedForDebugging(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != IsVisibleProperty)
        {
            return;
        }

        if (IsVisible)
        {
            Dispatcher.UIThread.Post(UpdateExceptionHelper, DispatcherPriority.Background);
        }
        else
        {
            CloseDataTip();
            _exceptionPopup.IsOpen = false;
        }
    }

    /// <summary>Shows the exception helper below the current statement while that line is in view.</summary>
    private void UpdateExceptionHelper()
    {
        var helper = _document.ExceptionHelper;
        var anchor = helper is { IsOpen: true } && IsVisible ? _debugLines.CurrentStatementRect(_editor.TextArea.TextView) : null;
        if (helper is null || anchor is not { } rect)
        {
            _exceptionPopup.IsOpen = false;
            return;
        }

        _exceptionView.DataContext = helper;
        _exceptionPopup.PlacementRect = rect;
        _exceptionPopup.IsOpen = true;
    }

    private void OnBreakpointMarginTapped(object? sender, int line) => _document.Debugger?.ToggleBreakpoint(_document.FilePath, line);

    private void OnBreakpointMarginMenuRequested(object? sender, int line)
    {
        if (_document.Debugger is not { } debugger)
        {
            return;
        }

        var path = _document.FilePath;
        var breakpoint = _document.Breakpoints.FirstOrDefault(b => b.Line == line);
        var menu = new MenuFlyout();
        if (breakpoint is null)
        {
            menu.Items.Add(CreateMenuItem("Insert Breakpoint", () => debugger.ToggleBreakpoint(path, line)));
            menu.Items.Add(CreateMenuItem("Insert Conditional Breakpoint...", () => _ = debugger.EditBreakpointConditionAsync(path, line)));
        }
        else
        {
            menu.Items.Add(CreateMenuItem("Conditions...", () => _ = debugger.EditBreakpointConditionAsync(path, line)));
            if (breakpoint.Condition is not null)
            {
                menu.Items.Add(CreateMenuItem("Remove Condition", () => debugger.ClearBreakpointCondition(path, line)));
            }

            menu.Items.Add(CreateMenuItem("Delete Breakpoint", () => debugger.ToggleBreakpoint(path, line)));
        }

        AddAdapterBreakpointItems(menu, path, line, breakpoint);
        menu.ShowAt(_breakpointMargin, showAtPointer: true);
    }

    private void OnDebugPointerHover(object? sender, PointerEventArgs e)
    {
        if (_document.Debugger is { IsPaused: true })
        {
            _dataTipCloseTimer.Stop();
            _ = ShowDataTipAsync(e.GetPosition(_editor));
        }
    }

    private void OnDebugPointerHoverStopped(object? sender, PointerEventArgs e) => RestartDataTipCloseTimer();

    /// <summary>Whether a DataTip shows for the text at an offset; while paused it takes the place of Quick Info there.</summary>
    private bool HasDataTipAt(int offset)
    {
        if (_document.Debugger is not { IsPaused: true } || IsInStringOrComment(offset))
        {
            return false;
        }

        var line = _editor.Document.GetLineByOffset(offset);
        return DataTipExpression.Find(_editor.Document.GetText(line), offset - line.Offset) is not null;
    }

    private void OnEditorHolding(object? sender, HoldingRoutedEventArgs e)
    {
        // A long press on touch shows the DataTip a mouse shows on hover; in the margin it opens the breakpoint menu.
        if (e.HoldingState == HoldingState.Started
            && _document.Debugger is { IsPaused: true }
            && e.Source is Visual source and not BreakpointMargin
            && source.TranslatePoint(e.Position, _editor) is { } point)
        {
            // On a value it is the DataTip's gesture: the context menu the same press raised does not open over it.
            if (_editor.GetPositionFromPoint(point) is { } position && HasDataTipAt(_editor.Document.GetOffset(position.Location)))
            {
                _dataTipHoldAt = DateTime.UtcNow;
                _editor.ContextMenu?.Close();
            }

            _ = ShowDataTipAsync(point);
        }
    }

    /// <summary>Whether a long press that shows a DataTip just happened, so its context menu stays closed.</summary>
    private bool IsDataTipHold => DateTime.UtcNow - _dataTipHoldAt < TimeSpan.FromSeconds(1);

    private async Task ShowDataTipAsync(Point point)
    {
        if (_document.Debugger is not { IsPaused: true } debugger || _editor.GetPositionFromPoint(point) is not { } position)
        {
            return;
        }

        var document = _editor.Document;
        var line = document.GetLineByNumber(position.Line);
        if (IsInStringOrComment(document.GetOffset(position.Location))
            || DataTipExpression.Find(document.GetText(line), position.Column - 1) is not { } target)
        {
            return;
        }

        var version = ++_dataTipVersion;
        var tree = await debugger.EvaluateDataTipAsync(target.Expression);
        if (tree is null || version != _dataTipVersion || !IsVisible)
        {
            return;
        }

        var word = new TextSegment { StartOffset = line.Offset + target.Start, Length = target.Length };
        var view = new DataTipView { DataContext = tree };
        view.PointerExited += (_, _) => RestartDataTipCloseTimer();
        _dataTip.Child = view;
        _dataTip.PlacementRect = BackgroundGeometryBuilder.GetRectsForSegment(_editor.TextArea.TextView, word).FirstOrDefault();
        _dataTip.IsOpen = true;
    }

    private void RestartDataTipCloseTimer()
    {
        if (_dataTip.IsOpen)
        {
            _dataTipCloseTimer.Stop();
            _dataTipCloseTimer.Start();
        }
    }

    private void OnDataTipCloseTimerTick(object? sender, EventArgs e)
    {
        _dataTipCloseTimer.Stop();

        // A DataTip stays open while the pointer is on it, so its members can be expanded.
        if (_dataTip.Child?.IsPointerOver != true)
        {
            CloseDataTip();
        }
    }

    private void CloseDataTip()
    {
        _dataTipVersion++;
        _dataTip.IsOpen = false;
        _dataTip.Child = null;
    }

    private bool IsInStringOrComment(int offset) => _document.Classifications.Any(c =>
        c.Start <= offset && offset < c.Start + c.Length
        && (c.Classification.Contains("comment", StringComparison.Ordinal) || c.Classification.StartsWith("string", StringComparison.Ordinal)));

    private static (int Start, int Length)? Span(ExecutionPoint? point) => point is null ? null : (point.Start, point.Length);

    private static MenuItem CreateMenuItem(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }
}
