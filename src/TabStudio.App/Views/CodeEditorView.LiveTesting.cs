using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using AvaloniaEdit.Editing;
using TabStudio.App.Editing;
using TabStudio.App.ViewModels;
using TabStudio.LiveTesting.Results;

namespace TabStudio.App.Views;

/// <summary>
/// Live Unit Testing in the code editor: the margin of markers between the line numbers and the code, and the
/// popup of a marker, which lists the tests that run its line (tap a marker, long-press it, or click it).
/// </summary>
public sealed partial class CodeEditorView
{
    // The popup draws in the window's overlay layer, like the DataTips, so it belongs to the window.
    private readonly Popup _liveTestPopup = new()
    {
        Placement = PlacementMode.RightEdgeAlignedTop,
        ShouldUseOverlayLayer = true,
        IsLightDismissEnabled = true,
        OverlayDismissEventPassThrough = true,
    };

    private readonly LiveTestPopupView _liveTestPopupView = new();
    private LiveTestingMargin? _liveTestingMargin;

    /// <summary>The Live Unit Testing margin of a C# document, for tests.</summary>
    internal LiveTestingMargin? LiveTestingMargin => _liveTestingMargin;

    /// <summary>Whether a marker's popup is open, for tests.</summary>
    internal bool IsLiveTestPopupOpen => _liveTestPopup.IsOpen;

    /// <summary>The open popup's content, for tests.</summary>
    internal LiveTestPopupViewModel? LiveTestPopup => _liveTestPopup.IsOpen ? _liveTestPopupView.DataContext as LiveTestPopupViewModel : null;

    /// <summary>Opens the popup of a line's marker, as a tap on it does.</summary>
    /// <param name="line">The one-based line.</param>
    /// <returns><see langword="false"/> when the line shows no marker.</returns>
    internal bool ShowLiveTestPopup(int line)
    {
        if (_liveTestingMargin?.MarkerOn(line) is not { } found)
        {
            return false;
        }

        ShowLiveTestPopup(line, found.Marker, found.Bounds);
        return true;
    }

    /// <summary>Adds the Live Unit Testing margin to C# documents. Called once, by the constructor.</summary>
    private void InitializeLiveTesting()
    {
        if (!_document.FileType.IsRoslyn)
        {
            return;
        }

        var margin = new LiveTestingMargin(() => _document.LiveTestMarkers) { IsVisible = _document.LiveTestMarkers is not null };
        // Right of the line numbers, as in Visual Studio.
        var margins = _editor.TextArea.LeftMargins;
        var index = margins.Count;
        for (var i = 0; i < margins.Count; i++)
        {
            if (margins[i] is LineNumberMargin)
            {
                index = i + 1;
                break;
            }
        }

        margins.Insert(index, margin);
        margin.MarkerTapped += OnLiveTestMarkerTapped;
        _liveTestingMargin = margin;

        // A logical child of the editor, so the popup gets the app's styles; it is placed at the marker.
        _liveTestPopup.PlacementTarget = margin;
        _liveTestPopup.Child = _liveTestPopupView;
        LogicalChildren.Add(_liveTestPopup);
        _document.LiveTestMarkersChanged += OnLiveTestMarkersChanged;
    }

    /// <summary>Removes the handlers and closes the popup. Called by <see cref="Dispose"/>.</summary>
    private void DisposeLiveTesting()
    {
        _document.LiveTestMarkersChanged -= OnLiveTestMarkersChanged;
        if (_liveTestingMargin is { } margin)
        {
            margin.MarkerTapped -= OnLiveTestMarkerTapped;
        }

        _liveTestPopup.IsOpen = false;
    }

    private void OnLiveTestMarkersChanged(object? sender, EventArgs e)
    {
        if (_liveTestingMargin is not { } margin)
        {
            return;
        }

        margin.IsVisible = _document.LiveTestMarkers is not null;
        margin.InvalidateVisual();

        // An open popup keeps what it showed, so a tap on it is never lost to an update; it closes when its marker goes.
        if (_document.LiveTestMarkers is null || (LiveTestPopup is { } open && margin.MarkerOn(open.Line) is null))
        {
            _liveTestPopup.IsOpen = false;
        }
    }

    private void OnLiveTestMarkerTapped(object? sender, LiveTestMarkerTappedEventArgs e) => ShowLiveTestPopup(e.Line, e.Marker, e.Bounds);

    private void ShowLiveTestPopup(int line, LiveTestMarker marker, Rect bounds)
    {
        _liveTestPopupView.DataContext = new LiveTestPopupViewModel(line, marker, new PopupActions(this));
        _liveTestPopup.PlacementRect = bounds;
        _liveTestPopup.IsOpen = true;
    }

    /// <summary>Runs and opens tests from the popup, closing it first.</summary>
    private sealed class PopupActions(CodeEditorView view) : ILiveTestingEditor
    {
        public Task RunTestsAsync(IReadOnlyList<TestKey> tests)
        {
            view._liveTestPopup.IsOpen = false;
            return view._document.LiveTesting?.RunTestsAsync(tests) ?? Task.CompletedTask;
        }

        public Task GoToTestAsync(TestKey test)
        {
            view._liveTestPopup.IsOpen = false;
            return view._document.LiveTesting?.GoToTestAsync(test) ?? Task.CompletedTask;
        }
    }
}
