using System.Collections.Specialized;
using System.ComponentModel;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Threading;
using TabStudio.App.Editing;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.Compilation.Features;

namespace TabStudio.App.Views;

/// <summary>
/// The completion list in the editor: typing a trigger character such as <c>.</c> or the first letter of a word
/// opens it, typing filters it, Tab, Enter, a tap or a commit character such as <c>(</c> commits, Escape closes,
/// and Ctrl+Space or Ctrl+J opens it on request.
/// </summary>
public sealed partial class CodeEditorView
{
    // The list moves left by its padding and icon column, so the items' text lines up with the typed word.
    private const double CompletionTextIndent = 39;
    private const double CompletionRowMinimum = 34;
    private const int CompletionVisibleRows = 8;

    private readonly StringBuilder _typedWhileCommitting = new();
    private bool _completionAbove;
    private CompletionListViewModel? _completion;
    private CompletionListView? _completionView;
    private Popup? _completionPopup;

    /// <summary>The completion list of this editor, or <see langword="null"/> for files without IntelliSense.</summary>
    public CompletionListViewModel? Completion => _completion;

    private void InitializeCompletion(ILanguageFeatures features)
    {
        _completion = new CompletionListViewModel(_document, features, () => _editor.CaretOffset);
        _completionView = new CompletionListView { DataContext = _completion };
        _completionView.ItemChosen += OnCompletionItemChosen;
        _completionPopup = CreatePopup(_completionView, PopupAnchor.BottomLeft, PopupGravity.BottomRight);
        _completion.PropertyChanged += OnCompletionPropertyChanged;
        _completion.Items.CollectionChanged += OnCompletionItemsChanged;
        _editor.TextArea.TextEntering += OnCompletionTextEntering;
        _editor.TextArea.TextEntered += OnCompletionTextEntered;
    }

    private void DisposeCompletion()
    {
        if (_completion is null)
        {
            return;
        }

        _completion.PropertyChanged -= OnCompletionPropertyChanged;
        _completion.Items.CollectionChanged -= OnCompletionItemsChanged;
        TextView.VisualLinesChanged -= OnVisualLinesChangedForCompletion;
        _editor.TextArea.TextEntering -= OnCompletionTextEntering;
        _editor.TextArea.TextEntered -= OnCompletionTextEntered;
        if (_completionView is not null)
        {
            _completionView.ItemChosen -= OnCompletionItemChosen;
        }

        _completion.Dispose();
        if (_completionPopup is not null)
        {
            _completionPopup.IsOpen = false;
        }
    }

    /// <summary>Opens the list on request: Ctrl+J lists members; Ctrl+Space also completes a word with one match.</summary>
    private async Task OpenCompletionAsync(bool completeWord)
    {
        if (_completion is not { } completion)
        {
            return;
        }

        _editor.TextArea.Focus();
        var open = await completion.RequestAsync(CompletionTrigger.Invoke);
        if (open && completeWord && completion.FilterText.Length > 0 && completion.Items.Count == 1)
        {
            await CommitCompletionAsync(null, commitCharacter: null);
        }
    }

    private void OnCompletionTextEntering(object? sender, TextInputEventArgs e)
    {
        if (_completion is not { } completion || string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        if (completion.IsCommitting)
        {
            // Keys typed while an item is being committed are typed after it, in order.
            _typedWhileCommitting.Append(e.Text);
            e.Handled = true;
            return;
        }

        if (!completion.IsOpen)
        {
            return;
        }

        if (e.Text.Length != 1)
        {
            completion.Close();
            return;
        }

        var typed = e.Text[0];
        if (CompletionMatcher.IsIdentifierCharacter(typed))
        {
            return;
        }

        // On-screen keyboards send Enter as a typed line break: it commits like the Enter key.
        if (typed is '\n' or '\r' && !completion.IsSoftSelection && completion.SelectedItem is { } selected)
        {
            e.Handled = true;
            _ = CommitCompletionAsync(null, commitCharacter: null, newLine: string.Equals(completion.FilterText, selected.Entry.DisplayText, StringComparison.Ordinal));
            return;
        }

        if (!completion.IsSoftSelection && completion.SelectedItem is { } item
            && (item.Entry.CommitCharacters?.Contains(typed) ?? CompletionMatcher.IsCommitCharacter(typed)))
        {
            e.Handled = true;
            _ = CommitCompletionAsync(null, typed);
            return;
        }

        completion.Close();
    }

    private void OnCompletionTextEntered(object? sender, TextInputEventArgs e)
    {
        if (_completion is not { } completion || _features is null || e.Text is not { Length: 1 } text || completion.IsOpen)
        {
            return;
        }

        var typed = text[0];
        if (completion.IsRequesting && CompletionMatcher.IsIdentifierCharacter(typed))
        {
            // The list on its way filters by this letter too.
            return;
        }

        _document.SyncText();
        if (_features.ShouldTriggerCompletion(_document.FilePath, _editor.CaretOffset, typed))
        {
            _ = completion.RequestAsync(CompletionTrigger.Insertion(typed));
        }
        else if (completion.IsRequesting)
        {
            completion.Close();
        }
    }

    private bool HandleCompletionKey(KeyEventArgs e)
    {
        if (_completion is not { IsOpen: true } completion)
        {
            return false;
        }

        if (e.KeyModifiers != KeyModifiers.None)
        {
            return false;
        }

        switch (e.Key)
        {
            case Key.Down:
                completion.MoveSelection(1);
                return true;
            case Key.Up:
                completion.MoveSelection(-1);
                return true;
            case Key.PageDown:
                completion.MoveSelection(CompletionListViewModel.PageSize);
                return true;
            case Key.PageUp:
                completion.MoveSelection(-CompletionListViewModel.PageSize);
                return true;
            case Key.Escape:
                completion.Close();
                return true;
            case Key.Tab when completion.SelectedItem is not null:
                _ = CommitCompletionAsync(null, commitCharacter: null);
                return true;
            case Key.Enter when completion.SelectedItem is { } selected && !completion.IsSoftSelection:
                // As in Visual Studio, Enter only commits, unless the whole word was already typed.
                var fullyTyped = string.Equals(completion.FilterText, selected.Entry.DisplayText, StringComparison.Ordinal);
                _ = CommitCompletionAsync(null, commitCharacter: null, newLine: fullyTyped);
                return true;
            case Key.Enter:
                completion.Close();
                return false;
            default:
                return false;
        }
    }

    private void OnCompletionItemChosen(object? sender, CompletionItemViewModel item)
    {
        _editor.TextArea.Focus();
        _ = CommitCompletionAsync(item, commitCharacter: null);
    }

    /// <summary>Commits an item, then types the commit character and anything typed in the meantime.</summary>
    private async Task CommitCompletionAsync(CompletionItemViewModel? item, char? commitCharacter, bool newLine = false)
    {
        if (_completion is not { } completion)
        {
            return;
        }

        var outcome = await completion.CommitAsync(item, commitCharacter);
        if (outcome is not null)
        {
            _editor.CaretOffset = Math.Clamp(outcome.Caret, 0, _editor.Document.TextLength);
            if (outcome.SelectionLength > 0 && _editor.CaretOffset + outcome.SelectionLength <= _editor.Document.TextLength)
            {
                // A snippet's first placeholder is selected, so typing replaces it.
                _editor.Select(_editor.CaretOffset, outcome.SelectionLength);
            }
        }

        if (commitCharacter is { } character && (outcome is null || outcome.TypeCommitCharacter))
        {
            _editor.TextArea.PerformTextInput(char.ToString(character));
        }

        if (newLine && outcome is not null)
        {
            _editor.TextArea.PerformTextInput("\n");
        }

        var pending = _typedWhileCommitting.ToString();
        _typedWhileCommitting.Clear();
        foreach (var typed in pending)
        {
            _editor.TextArea.PerformTextInput(char.ToString(typed));
        }

        _editor.TextArea.Caret.BringCaretToView();
    }

    private void OnCompletionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(CompletionListViewModel.IsOpen):
                if (_completion?.IsOpen == true)
                {
                    PlaceCompletionPopup();
                }
                else if (_completionPopup is not null)
                {
                    _completionPopup.IsOpen = false;
                    PlaceSignatureHelpPopup();
                }

                break;
            case nameof(CompletionListViewModel.SelectedItem) when _completion?.SelectedItem is { } selected:
                Dispatcher.UIThread.Post(() => _completionView?.ItemList.ScrollIntoView(selected), DispatcherPriority.Background);
                break;
        }
    }

    private void OnCompletionItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Above the line, the list grows and shrinks upwards; Parameter Info stays on top of it.
        if (_completionAbove)
        {
            PlaceSignatureHelpPopup();
        }
    }

    /// <summary>
    /// Places the list under the start of the word being completed, or above the line when it does not fit below.
    /// The side is chosen when the list opens and kept while it is open, so it does not jump while the user types.
    /// </summary>
    private void PlaceCompletionPopup()
    {
        if (_completionPopup is null || _completion is not { IsOpen: true } completion)
        {
            return;
        }

        if (LineRect(completion.SpanStart) is not { } anchor)
        {
            _completionPopup.IsOpen = false;
            if (!TextView.VisualLinesValid)
            {
                // A list that arrives before the edit that asked for it is laid out is placed once it is.
                TextView.VisualLinesChanged -= OnVisualLinesChangedForCompletion;
                TextView.VisualLinesChanged += OnVisualLinesChangedForCompletion;
            }

            return;
        }

        if (!_completionPopup.IsOpen)
        {
            _completionAbove = !FitsBelow(anchor, CompletionHeight());
        }

        _completionPopup.PlacementAnchor = _completionAbove ? PopupAnchor.TopLeft : PopupAnchor.BottomLeft;
        _completionPopup.PlacementGravity = _completionAbove ? PopupGravity.TopRight : PopupGravity.BottomRight;
        _completionPopup.HorizontalOffset = -CompletionTextIndent;
        _completionPopup.PlacementRect = anchor;
        _completionPopup.IsOpen = true;
        PlaceSignatureHelpPopup();
    }

    private void OnVisualLinesChangedForCompletion(object? sender, EventArgs e)
    {
        TextView.VisualLinesChanged -= OnVisualLinesChangedForCompletion;
        PlaceCompletionPopup();
    }

    /// <summary>A completion row's height: the window's touch target (docs/design/README.md, "Touch targets").</summary>
    private double CompletionRowHeight => this.FindResource("Target.Size") is double target ? Math.Max(CompletionRowMinimum, target) : CompletionRowMinimum;

    /// <summary>The height of the list as shown now.</summary>
    private double CompletionHeight()
    {
        if (_completionView is not null)
        {
            // The list shows eight rows at most, as tall as the touch target and the text make them.
            _completionView.ItemList.MaxHeight = CompletionVisibleRows * CompletionRowHeight * Math.Max(1, Accessibility.TypeScale.Factor);
        }

        return (Math.Min(_completion?.Items.Count ?? 0, CompletionVisibleRows) * CompletionRowHeight * Math.Max(1, Accessibility.TypeScale.Factor)) + 10;
    }

    /// <summary>Whether something this tall fits between a line and the bottom of the window, or does not fit above it either.</summary>
    private bool FitsBelow(Rect line, double height)
    {
        if (TopLevel.GetTopLevel(this) is not { } topLevel
            || TextView.TranslatePoint(line.BottomLeft, topLevel) is not { } bottom
            || TextView.TranslatePoint(line.TopLeft, topLevel) is not { } top)
        {
            return true;
        }

        return bottom.Y + height <= topLevel.Bounds.Height || top.Y < height;
    }
}
