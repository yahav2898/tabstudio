using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TabStudio.App.Controls;
using TabStudio.Compilation.Features;

namespace TabStudio.App.Views;

/// <summary>
/// Quick Actions in the editor: a light bulb appears after the caret line when fixes or refactorings are available;
/// tapping it or pressing Ctrl+. opens the menu, and picking an action applies its edits to every affected file.
/// </summary>
public sealed partial class CodeEditorView
{
    private LightBulbView? _lightBulb;
    private Popup? _lightBulbPopup;
    private IReadOnlyList<CodeActionItem> _codeActions = [];
    private (int Start, int Length, int Edits) _codeActionsFor = (-1, -1, -1);

    /// <summary>The quick fixes and refactorings at the caret, from the last time they were listed. For tests.</summary>
    public IReadOnlyList<CodeActionItem> CodeActions => _codeActions;

    /// <summary>Whether the light bulb is shown.</summary>
    public bool IsLightBulbVisible => _lightBulbPopup?.IsOpen == true;

    private void InitializeQuickActions()
    {
        _lightBulb = new LightBulbView();
        _lightBulb.AddHandler(TappedEvent, OnLightBulbTapped, RoutingStrategies.Bubble, handledEventsToo: true);
        _lightBulbPopup = CreatePopup(_lightBulb, PopupAnchor.Left, PopupGravity.Right);
    }

    private void DisposeQuickActions()
    {
        _lightBulb?.RemoveHandler(TappedEvent, OnLightBulbTapped);
        HideLightBulb();
    }

    private void OnLightBulbTapped(object? sender, TappedEventArgs e)
    {
        e.Handled = true;
        _ = ShowQuickActionsAsync();
    }

    /// <summary>Lists the actions at the caret once it rests, and shows the light bulb when there are any.</summary>
    private async Task UpdateLightBulbAsync(CancellationToken cancellationToken)
    {
        if (await ListCodeActionsAsync(cancellationToken) is not { } actions)
        {
            return;
        }

        if (actions.Count > 0 && _completion?.IsOpen != true && _signatureHelp?.IsOpen != true)
        {
            ShowLightBulb();
        }
        else
        {
            HideLightBulb();
        }
    }

    /// <summary>
    /// Lists the code actions for the selection or caret, reusing the last list while nothing changed.
    /// </summary>
    /// <returns>The actions, or <see langword="null"/> when the text changed while they were listed.</returns>
    internal async Task<IReadOnlyList<CodeActionItem>?> ListCodeActionsAsync(CancellationToken cancellationToken)
    {
        if (_features is null)
        {
            return null;
        }

        _document.SyncText();
        var span = (Start: _editor.SelectionLength > 0 ? _editor.SelectionStart : _editor.CaretOffset, Length: _editor.SelectionLength, Edits: _document.EditCount);
        if (span == _codeActionsFor)
        {
            return _codeActions;
        }

        var actions = await _features.GetCodeActionsAsync(_document.FilePath, span.Start, span.Length, cancellationToken);
        if (cancellationToken.IsCancellationRequested || span.Edits != _document.EditCount)
        {
            return null;
        }

        _codeActions = actions;
        _codeActionsFor = span;
        return actions;
    }

    /// <summary>Opens the Quick Actions menu at the light bulb (Ctrl+., or a tap on the bulb).</summary>
    private async Task ShowQuickActionsAsync()
    {
        if (_features is null || _lightBulb is null)
        {
            return;
        }

        _caretIdle?.Cancel();
        IReadOnlyList<CodeActionItem>? actions;
        try
        {
            actions = await ListCodeActionsAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (actions is null || !ShowLightBulb())
        {
            return;
        }

        var menu = CreateQuickActionsMenu(actions);
        menu.Closed += (_, _) =>
        {
            if (_codeActions.Count == 0)
            {
                HideLightBulb();
            }

            _editor.TextArea.Focus();
        };
        menu.ShowAt(_lightBulb.Anchor);

        // With a keyboard, the arrows and Enter work in the menu at once, as after Ctrl+. in Visual Studio.
        Dispatcher.UIThread.Post(() => menu.Items.OfType<MenuItem>().FirstOrDefault(i => i.IsEnabled)?.Focus(), DispatcherPriority.Background);
    }

    private MenuFlyout CreateQuickActionsMenu(IReadOnlyList<CodeActionItem> actions)
    {
        var menu = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedLeft };
        if (actions.Count == 0)
        {
            menu.Items.Add(new MenuItem { Header = "No quick actions available here", IsEnabled = false });
            return menu;
        }

        var groups = new Dictionary<string, MenuItem>(StringComparer.Ordinal);
        foreach (var action in actions)
        {
            var item = new MenuItem
            {
                Header = action.Title,
                Icon = new StrokeIcon { Data = action.Kind == CodeActionKind.QuickFix ? IntelliSenseIcons.LightBulb : IntelliSenseIcons.Screwdriver, Size = 15 },
            };
            item.Click += (_, _) => _ = ApplyCodeActionAsync(action);
            if (action.Group is not { } group)
            {
                menu.Items.Add(item);
                continue;
            }

            if (!groups.TryGetValue(group, out var parent))
            {
                parent = new MenuItem { Header = group };
                groups[group] = parent;
                menu.Items.Add(parent);
            }

            parent.Items.Add(item);
        }

        return menu;
    }

    /// <summary>Applies a code action, to every file it changes.</summary>
    /// <param name="action">An action from <see cref="CodeActions"/>.</param>
    internal async Task ApplyCodeActionAsync(CodeActionItem action)
    {
        if (_features is null)
        {
            return;
        }

        HideLightBulb();
        var listedFor = _codeActionsFor;
        var edits = await _features.GetCodeActionEditsAsync(action.Id, CancellationToken.None);
        if (edits.Count == 0)
        {
            // Another list replaced this one in the language service: list again and pick the same action.
            _codeActionsFor = (-1, -1, -1);
            if (await ListCodeActionsAsync(CancellationToken.None) is { } again
                && again.FirstOrDefault(a => a.Title == action.Title && a.Group == action.Group) is { } same)
            {
                listedFor = _codeActionsFor;
                edits = await _features.GetCodeActionEditsAsync(same.Id, CancellationToken.None);
            }
        }

        _codeActionsFor = (-1, -1, -1);
        if (edits.Count == 0 || listedFor.Edits != _document.EditCount)
        {
            Workspace?.ShowStatus($"'{action.Title}' could not be applied.");
            return;
        }

        if (Workspace is { } workspace)
        {
            await workspace.ApplyDocumentEditsAsync(edits);
        }
        else
        {
            foreach (var edit in edits.Where(e => e.Kind == DocumentEditKind.Modified && e.FilePath == _document.FilePath))
            {
                _document.ApplyEdits(edit.Edits);
            }
        }

        _editor.TextArea.Focus();
    }

    /// <summary>Shows the light bulb after the text of the caret line.</summary>
    /// <returns>Whether the line is in view, so the bulb is shown.</returns>
    private bool ShowLightBulb()
    {
        if (_lightBulb is null || _lightBulbPopup is null)
        {
            return false;
        }

        var line = _editor.Document.GetLineByOffset(_editor.CaretOffset);
        if (LineRect(line.EndOffset) is not { } end)
        {
            HideLightBulb();
            return false;
        }

        var x = Math.Min(end.X + 16, Math.Max(0, TextView.Bounds.Width - 40));
        _lightBulb.ShowFor(_codeActions.Any(a => a.Kind == CodeActionKind.QuickFix));
        _lightBulbPopup.PlacementRect = new Rect(x, end.Y, 1, end.Height);
        _lightBulbPopup.IsOpen = true;
        return true;
    }

    private void HideLightBulb()
    {
        if (_lightBulbPopup is not null)
        {
            _lightBulbPopup.IsOpen = false;
        }
    }
}
