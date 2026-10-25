using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Interactivity;
using TabStudio.App.ViewModels;
using TabStudio.Compilation.Features;

namespace TabStudio.App.Views;

/// <summary>
/// Parameter Info in the editor: typing <c>(</c> or <c>,</c> in a call shows the signature above the line, the
/// current parameter in bold; Up and Down go through the overloads; it follows the caret and closes outside the call.
/// </summary>
public sealed partial class CodeEditorView
{
    private static readonly TimeSpan SignatureHelpUpdateDelay = TimeSpan.FromMilliseconds(60);

    private SignatureHelpViewModel? _signatureHelp;
    private Popup? _signatureHelpPopup;
    private CancellationTokenSource? _signatureHelpRequest;

    /// <summary>Parameter Info of this editor, or <see langword="null"/> for files without IntelliSense.</summary>
    public SignatureHelpViewModel? SignatureHelp => _signatureHelp;

    private void InitializeSignatureHelp()
    {
        _signatureHelp = new SignatureHelpViewModel();
        var view = new SignatureHelpView { DataContext = _signatureHelp };
        view.AddHandler(PointerReleasedEvent, OnPopupPointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);
        _signatureHelpPopup = CreatePopup(view, PopupAnchor.TopLeft, PopupGravity.TopRight);
        _editor.TextArea.TextEntered += OnSignatureHelpTextEntered;
    }

    private void DisposeSignatureHelp()
    {
        _editor.TextArea.TextEntered -= OnSignatureHelpTextEntered;
        CloseSignatureHelp();
    }

    private void OnSignatureHelpTextEntered(object? sender, TextInputEventArgs e)
    {
        if (e.Text is { Length: 1 } text && text[0] is '(' or ',' or '<' or ')')
        {
            _ = RequestSignatureHelpAsync(explicitRequest: false);
        }
    }

    private void OnSignatureHelpCaretMoved()
    {
        if (_signatureHelp is { IsOpen: true } && _signatureHelpRequest is null)
        {
            _ = RequestSignatureHelpAsync(explicitRequest: false, SignatureHelpUpdateDelay);
        }
    }

    /// <summary>Asks for signature help at the caret and shows it, or closes it outside a call.</summary>
    private async Task RequestSignatureHelpAsync(bool explicitRequest, TimeSpan delay = default)
    {
        if (_features is null || _signatureHelp is null)
        {
            return;
        }

        _signatureHelpRequest?.Cancel();
        var request = new CancellationTokenSource();
        _signatureHelpRequest = request;
        SignatureHelpResult? result;
        int edits;
        try
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, request.Token);
            }

            _document.SyncText();
            edits = _document.EditCount;
            result = await _features.GetSignatureHelpAsync(_document.FilePath, _editor.CaretOffset, request.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            if (ReferenceEquals(_signatureHelpRequest, request))
            {
                _signatureHelpRequest = null;
            }

            request.Dispose();
        }

        if (request.IsCancellationRequested)
        {
            return;
        }

        if (edits != _document.EditCount)
        {
            // The user typed on; ask again about the current text.
            _ = RequestSignatureHelpAsync(explicitRequest);
            return;
        }

        if (result is null || result.Signatures.Count == 0)
        {
            CloseSignatureHelp();
            return;
        }

        _signatureHelp.Show(result);
        PlaceSignatureHelpPopup();
    }

    private bool HandleSignatureHelpKey(KeyEventArgs e)
    {
        if (_signatureHelp is not { IsOpen: true } help || e.KeyModifiers != KeyModifiers.None)
        {
            return false;
        }

        switch (e.Key)
        {
            case Key.Up when help.HasOverloads:
                help.PreviousOverload();
                return true;
            case Key.Down when help.HasOverloads:
                help.NextOverload();
                return true;
            case Key.Escape:
                CloseSignatureHelp();
                return true;
            default:
                return false;
        }
    }

    /// <summary>Places Parameter Info above the line of the call, or below it at the top of the editor.</summary>
    private void PlaceSignatureHelpPopup()
    {
        if (_signatureHelpPopup is null || _signatureHelp is not { IsOpen: true } help)
        {
            return;
        }

        if (LineRect(Math.Max(0, help.ApplicableSpanStart - 1)) is not { } anchor)
        {
            _signatureHelpPopup.IsOpen = false;
            return;
        }

        // When the completion list had to open above the line, Parameter Info goes above the list.
        _signatureHelpPopup.VerticalOffset = _completionPopup?.IsOpen == true && _completionAbove ? -(CompletionHeight() + 4) : 0;
        _signatureHelpPopup.PlacementRect = anchor;
        _signatureHelpPopup.IsOpen = true;
    }

    private void CloseSignatureHelp()
    {
        _signatureHelpRequest?.Cancel();
        _signatureHelpRequest = null;
        _signatureHelp?.Close();
        if (_signatureHelpPopup is not null)
        {
            _signatureHelpPopup.IsOpen = false;
        }
    }

    /// <summary>Gives keyboard focus back to the editor after a tap on a popup, such as an overload arrow.</summary>
    private void OnPopupPointerReleased(object? sender, PointerReleasedEventArgs e) => _editor.TextArea.Focus();
}
