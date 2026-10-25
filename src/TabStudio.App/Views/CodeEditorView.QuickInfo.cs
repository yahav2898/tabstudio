using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TabStudio.App.ViewModels;
using TabStudio.Compilation.Features;
using TabStudio.Core.Diagnostics;

namespace TabStudio.App.Views;

/// <summary>
/// Quick Info in the editor: resting the pointer on a symbol, pressing and holding it on a touch screen, or
/// Ctrl+K, Ctrl+I shows its signature and documentation, together with any error or warning at that place.
/// </summary>
public sealed partial class CodeEditorView
{
    private static readonly TimeSpan QuickInfoCloseDelay = TimeSpan.FromMilliseconds(300);

    private QuickInfoView? _quickInfoView;
    private Popup? _quickInfoPopup;
    private CancellationTokenSource? _quickInfoRequest;
    private DispatcherTimer? _quickInfoCloseTimer;

    /// <summary>The Quick Info shown, or <see langword="null"/> when it is closed. For tests.</summary>
    public QuickInfoViewModel? QuickInfo => _quickInfoPopup?.IsOpen == true ? _quickInfoView?.DataContext as QuickInfoViewModel : null;

    private void InitializeQuickInfo()
    {
        _quickInfoView = new QuickInfoView();
        _quickInfoView.PointerExited += OnQuickInfoViewPointerExited;
        _quickInfoPopup = CreatePopup(_quickInfoView, PopupAnchor.BottomLeft, PopupGravity.BottomRight);
        _quickInfoCloseTimer = new DispatcherTimer { Interval = QuickInfoCloseDelay };
        _quickInfoCloseTimer.Tick += OnQuickInfoCloseTimerTick;

        // Quick Info replaces the tooltip that only showed errors: it shows both, as Visual Studio does.
        TextView.PointerHover -= OnPointerHover;
        TextView.PointerHoverStopped -= OnPointerHoverStopped;
        TextView.PointerHover += OnQuickInfoPointerHover;
        TextView.PointerMoved += OnQuickInfoPointerMoved;
        TextView.PointerExited += OnQuickInfoPointerExited;
        TextView.AddHandler(HoldingEvent, OnQuickInfoHolding, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private void DisposeQuickInfo()
    {
        TextView.PointerHover -= OnQuickInfoPointerHover;
        TextView.PointerMoved -= OnQuickInfoPointerMoved;
        TextView.PointerExited -= OnQuickInfoPointerExited;
        TextView.RemoveHandler(HoldingEvent, OnQuickInfoHolding);
        if (_quickInfoView is not null)
        {
            _quickInfoView.PointerExited -= OnQuickInfoViewPointerExited;
        }

        if (_quickInfoCloseTimer is not null)
        {
            _quickInfoCloseTimer.Stop();
            _quickInfoCloseTimer.Tick -= OnQuickInfoCloseTimerTick;
        }

        CloseQuickInfo();
    }

    private Task ShowQuickInfoAtCaretAsync() => ShowQuickInfoAsync(_editor.CaretOffset, above: false);

    /// <summary>Shows Quick Info for the symbol at an offset, below its line or, for a long press, above it.</summary>
    private async Task ShowQuickInfoAsync(int offset, bool above)
    {
        if (_features is null || _quickInfoView is null || _quickInfoPopup is null)
        {
            return;
        }

        _quickInfoRequest?.Cancel();
        var request = new CancellationTokenSource();
        _quickInfoRequest = request;
        var diagnostic = _diagnostics?.DiagnosticAt(offset);
        var diagnostics = diagnostic is null ? Array.Empty<BuildDiagnostic>() : new[] { diagnostic };
        _document.SyncText();
        var edits = _document.EditCount;
        QuickInfoResult? info;
        try
        {
            info = await _features.GetQuickInfoAsync(_document.FilePath, offset, request.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            if (ReferenceEquals(_quickInfoRequest, request))
            {
                _quickInfoRequest = null;
            }

            request.Dispose();
        }

        if (request.IsCancellationRequested || edits != _document.EditCount)
        {
            return;
        }

        if (QuickInfoViewModel.Create(info, diagnostics) is not { } model || LineRect(info?.SpanStart ?? offset) is not { } line)
        {
            CloseQuickInfo();
            return;
        }

        _quickInfoView.DataContext = model;
        _quickInfoPopup.PlacementAnchor = above ? PopupAnchor.TopLeft : PopupAnchor.BottomLeft;
        _quickInfoPopup.PlacementGravity = above ? PopupGravity.TopRight : PopupGravity.BottomRight;
        _quickInfoPopup.PlacementRect = line;
        _quickInfoPopup.IsOpen = true;
    }

    /// <summary>Closes Quick Info.</summary>
    /// <returns>Whether it was open.</returns>
    private bool CloseQuickInfo()
    {
        _quickInfoRequest?.Cancel();
        _quickInfoRequest = null;
        _quickInfoCloseTimer?.Stop();
        if (_quickInfoPopup is not { IsOpen: true } popup)
        {
            return false;
        }

        popup.IsOpen = false;
        return true;
    }

    private void OnQuickInfoPointerHover(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Touch || _completion?.IsOpen == true || OffsetAt(e.GetPosition(TextView)) is not { } offset)
        {
            return;
        }

        _quickInfoCloseTimer?.Stop();
        if (QuickInfo is { } shown && shown.Covers(offset))
        {
            return;
        }

        _ = ShowQuickInfoAsync(offset, above: false);
    }

    /// <summary>Quick Info stays open while the pointer is on its symbol or on the tooltip itself.</summary>
    private void OnQuickInfoPointerMoved(object? sender, PointerEventArgs e)
    {
        if (QuickInfo is not { } shown || e.Pointer.Type == PointerType.Touch)
        {
            return;
        }

        if (OffsetAt(e.GetPosition(TextView)) is { } offset && shown.Covers(offset))
        {
            _quickInfoCloseTimer?.Stop();
        }
        else
        {
            ScheduleQuickInfoClose();
        }
    }

    private void OnQuickInfoPointerExited(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Touch)
        {
            ScheduleQuickInfoClose();
        }
    }

    private void OnQuickInfoViewPointerExited(object? sender, PointerEventArgs e) => ScheduleQuickInfoClose();

    private void ScheduleQuickInfoClose()
    {
        if (_quickInfoPopup?.IsOpen == true && _quickInfoCloseTimer is { } timer)
        {
            timer.Stop();
            timer.Start();
        }
    }

    private void OnQuickInfoCloseTimerTick(object? sender, EventArgs e)
    {
        _quickInfoCloseTimer?.Stop();
        if (_quickInfoView?.IsPointerOver != true)
        {
            CloseQuickInfo();
        }
    }

    /// <summary>A long press on a touch screen shows Quick Info above the line, next to the context menu.</summary>
    private void OnQuickInfoHolding(object? sender, HoldingRoutedEventArgs e)
    {
        if (e.HoldingState != HoldingState.Started)
        {
            return;
        }

        var point = (e.Source as Visual)?.TranslatePoint(e.Position, TextView) ?? e.Position;
        if (OffsetAt(point) is { } offset)
        {
            _ = ShowQuickInfoAsync(offset, above: true);
        }
    }
}
