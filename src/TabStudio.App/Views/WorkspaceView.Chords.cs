using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using TabStudio.App.Input;

namespace TabStudio.App.Views;

/// <summary>
/// Visual Studio's two-key shortcuts, in one map for the whole shell so that chords sharing a first key, such as
/// Ctrl+R, Ctrl+R (rename) and Ctrl+R, A (run all tests), work together.
/// </summary>
/// <remarks>
/// Editor chords only act while a code editor has the keyboard focus: Ctrl+R, Ctrl+R renames, and Ctrl+K, Ctrl+I,
/// Ctrl+K, Ctrl+P, Ctrl+K, Ctrl+L and Ctrl+K, Ctrl+W show Quick Info, Parameter Info, the member list and the
/// completed word; Ctrl+K, Ctrl+D formats the document. Their second key works with or without Ctrl.
/// </remarks>
public sealed partial class WorkspaceView
{
    private readonly KeyChordMap _chords = new();

    /// <summary>Registers the editor chords and starts listening for keys. Called by the constructor.</summary>
    private void InitializeKeyChords()
    {
        AddEditorChord(Key.R, Key.R, () => _viewModel?.RenameSymbolCommand);
        AddEditorChord(Key.K, Key.I, () => _viewModel?.QuickInfoCommand);
        AddEditorChord(Key.K, Key.P, () => _viewModel?.ParameterInfoCommand);
        AddEditorChord(Key.K, Key.L, () => _viewModel?.ListMembersCommand);
        AddEditorChord(Key.K, Key.W, () => _viewModel?.CompleteWordCommand);
        AddEditorChord(Key.K, Key.D, () => _viewModel?.FormatDocumentCommand);

        _chords.PendingChanged += (_, text) => _viewModel?.ShowKeyChordStatus(text);
        _chords.NotACommand += (_, message) => _viewModel?.ShowStatus(message);

        // Tunnelling, so the chord's keys reach the map before an editor types them.
        AddHandler(KeyDownEvent, OnChordKeyDown, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, (_, _) => _chords.Cancel(), RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void AddEditorChord(Key first, Key second, Func<System.Windows.Input.ICommand?> command)
    {
        var start = new KeyGesture(first, KeyModifiers.Control);
        System.Windows.Input.ICommand? InEditor() => IsCodeEditorFocused() ? command() : null;
        _chords.Add(start, new KeyGesture(second, KeyModifiers.Control), InEditor);
        _chords.Add(start, new KeyGesture(second), InEditor);
    }

    private bool IsCodeEditorFocused() =>
        TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is Visual focused
        && (focused as CodeEditorView ?? focused.FindAncestorOfType<CodeEditorView>()) is not null;

    private bool IsTerminalFocused() =>
        TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is Visual focused
        && (focused as TabStudio.App.Controls.TerminalView ?? focused.FindAncestorOfType<TabStudio.App.Controls.TerminalView>()) is not null;

    private void OnChordKeyDown(object? sender, KeyEventArgs e)
    {
        // The shell's Ctrl+R, Ctrl+E and Ctrl+K are reverse search, end of line and kill line: the chords stay out of
        // the terminal's way, as its own key handling promises.
        if (!_chords.IsPending && IsTerminalFocused())
        {
            return;
        }

        if (_chords.HandleKeyDown(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
        }
    }
}
