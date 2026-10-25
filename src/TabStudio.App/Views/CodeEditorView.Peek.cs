using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Views;

/// <summary>Peek Definition in the editor: the definition in a small editor under the caret line.</summary>
public sealed partial class CodeEditorView
{
    private const double PeekMaxHeight = 280;

    private PeekDefinitionView? _peekView;
    private Popup? _peekPopup;

    /// <summary>The definition being peeked at, or <see langword="null"/> when no peek is open. For tests.</summary>
    public PeekDefinitionViewModel? Peek => _peekPopup?.IsOpen == true ? _peekView?.DataContext as PeekDefinitionViewModel : null;

    /// <summary>The peek's view, for tests.</summary>
    internal PeekDefinitionView? PeekView => _peekView;

    private void InitializePeek() => _document.PeekRequested += OnPeekRequested;

    private void DisposePeek()
    {
        _document.PeekRequested -= OnPeekRequested;
        ClosePeek();
    }

    private void OnPeekRequested(object? sender, PeekDefinitionViewModel peek)
    {
        if (_popupHost is null || LineRect(_editor.CaretOffset) is not { } line)
        {
            return;
        }

        if (_peekView is null)
        {
            _peekView = new PeekDefinitionView();
            _peekView.CloseRequested += (_, _) => ClosePeek();
            _peekView.OpenRequested += async (_, _) =>
            {
                if (_peekView.DataContext is PeekDefinitionViewModel shown && Workspace is { } workspace)
                {
                    ClosePeek();
                    await workspace.OpenPeekedDefinitionAsync(shown.Current);
                }
            };
            _peekPopup = CreatePopup(_peekView, PopupAnchor.BottomLeft, PopupGravity.BottomRight);
            _peekPopup.IsLightDismissEnabled = true;
        }

        _peekView.Width = Math.Max(320, TextView.Bounds.Width - 24);
        _peekView.Height = Math.Min(PeekMaxHeight, Math.Max(160, TextView.Bounds.Height * 0.5));
        _peekView.DataContext = peek;
        _peekPopup!.PlacementRect = line.WithX(0);
        _peekPopup.HorizontalOffset = 12;
        _peekPopup.IsOpen = true;
        _peekView.Focus();
    }

    /// <summary>Closes the peek.</summary>
    /// <returns>Whether it was open.</returns>
    private bool ClosePeek()
    {
        if (_peekPopup is not { IsOpen: true } popup)
        {
            return false;
        }

        popup.IsOpen = false;
        _editor.TextArea.Focus();
        return true;
    }
}
