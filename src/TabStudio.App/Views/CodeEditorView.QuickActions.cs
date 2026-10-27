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
/// <remarks>
/// There is one light bulb and one Quick Actions menu in the whole app, at the caret line of the editor in use. Every
/// open document keeps its editor alive while hidden, and the bulb and menu live in the window's overlay layer, so an
/// editor that shows them takes them from any other, and an editor that is hidden, or whose caret moves, scrolls or
/// changes text, closes its own. Escape and a tap elsewhere close them too.
/// </remarks>
public sealed partial class CodeEditorView
{
    /// <summary>The editor whose light bulb or Quick Actions menu is showing, if any: there is never more than one.</summary>
    private static CodeEditorView? _quickActionsOwner;

    private static int _openQuickActionsMenus;

    private LightBulbView? _lightBulb;
    private Popup? _lightBulbPopup;
    private MenuFlyout? _quickActionsMenu;
    private IReadOnlyList<CodeActionItem> _codeActions = [];
    private (int Start, int Length, int Edits) _codeActionsFor = (-1, -1, -1);
    private int _codeActionsRegisterRevision;

    /// <summary>The quick fixes and refactorings at the caret, from the last time they were listed. For tests.</summary>
    public IReadOnlyList<CodeActionItem> CodeActions => _codeActions;

    /// <summary>Whether the light bulb is shown.</summary>
    public bool IsLightBulbVisible => _lightBulbPopup?.IsOpen == true;

    /// <summary>Whether this editor's Quick Actions menu is open.</summary>
    public bool IsQuickActionsMenuOpen => _quickActionsMenu?.IsOpen == true;

    /// <summary>How many Quick Actions menus are open in the app, which is never more than one. For tests.</summary>
    internal static int OpenQuickActionsMenus => _openQuickActionsMenus;

    private void InitializeQuickActions()
    {
        _lightBulb = new LightBulbView();
        _lightBulb.AddHandler(TappedEvent, OnLightBulbTapped, RoutingStrategies.Bubble, handledEventsToo: true);
        _lightBulbPopup = CreatePopup(_lightBulb, PopupAnchor.Left, PopupGravity.Right);
    }

    private void DisposeQuickActions()
    {
        _lightBulb?.RemoveHandler(TappedEvent, OnLightBulbTapped);
        CloseQuickActions();
        if (_quickActionsOwner == this)
        {
            _quickActionsOwner = null;
        }
    }

    /// <summary>
    /// Closes the light bulb and the Quick Actions menu: the caret moved, the text changed or scrolled, Escape was
    /// pressed, or the editor is hidden.
    /// </summary>
    /// <returns>Whether either was showing.</returns>
    private bool CloseQuickActions()
    {
        var wasOpen = IsLightBulbVisible || IsQuickActionsMenuOpen;
        var menu = _quickActionsMenu;
        _quickActionsMenu = null;
        menu?.Hide();
        HideLightBulb();
        return wasOpen;
    }

    /// <summary>Makes this editor the one showing Quick Actions, closing any other editor's light bulb and menu.</summary>
    private void TakeQuickActions()
    {
        if (_quickActionsOwner is { } other && other != this)
        {
            other.CloseQuickActions();
        }

        _quickActionsOwner = this;
    }

    /// <summary>Whether this editor is on screen, so a popup at its caret belongs there.</summary>
    private bool IsShownOnScreen => IsEffectivelyVisible && TopLevel.GetTopLevel(this) is not null && TextView.Bounds.Width > 0;

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
        var registerRevision = Workspace?.EmbeddedRegisterMetadataRevision ?? 0;
        if (span == _codeActionsFor && registerRevision == _codeActionsRegisterRevision)
        {
            return _codeActions;
        }

        var actions = await _features.GetCodeActionsAsync(_document.FilePath, span.Start, span.Length, cancellationToken);
        if (!IsInStringOrComment(span.Start) && Workspace is { } workspace)
        {
            actions = [.. actions, .. workspace.GetEmbeddedMaskCodeActions(_document.FilePath, _document.Document.Text, span.Start)];
        }
        if (cancellationToken.IsCancellationRequested || span.Edits != _document.EditCount)
        {
            return null;
        }

        _codeActions = actions;
        _codeActionsFor = span;
        _codeActionsRegisterRevision = registerRevision;
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

        // One menu at a time: Ctrl+. again replaces the menu rather than opening a second one.
        var previous = _quickActionsMenu;
        _quickActionsMenu = null;
        previous?.Hide();
        var menu = CreateQuickActionsMenu(actions);
        menu.Opened += (_, _) => _openQuickActionsMenus++;
        menu.Closed += (_, _) =>
        {
            _openQuickActionsMenus = Math.Max(0, _openQuickActionsMenus - 1);
            if (_quickActionsMenu != menu)
            {
                // Closed because the caret moved, the editor was hidden or another menu replaced it.
                return;
            }

            _quickActionsMenu = null;
            if (_codeActions.Count == 0)
            {
                HideLightBulb();
            }

            _editor.TextArea.Focus();
        };
        _quickActionsMenu = menu;
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
                Header = new TextBlock { Text = action.Title, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center },
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
        if (action.Id.StartsWith("embedded-mask:", StringComparison.Ordinal))
        {
            HideLightBulb();
            if (_codeActionsFor.Edits == _document.EditCount && Workspace is { } owner &&
                owner.GetEmbeddedMaskCodeActionEdit(action.Id, _document.FilePath, _document.Document.Text) is { } edit)
            {
                await owner.ApplyDocumentEditsAsync([edit]);
            }
            _codeActionsFor = (-1, -1, -1);
            _editor.TextArea.Focus();
            return;
        }
        if (_features is null)
        {
            return;
        }

        HideLightBulb();
        var listedFor = _codeActionsFor;
        var applied = action.Id;
        var edits = await _features.GetCodeActionEditsAsync(applied, CancellationToken.None);
        if (edits.Count == 0)
        {
            // Another list replaced this one in the language service: list again and pick the same action.
            _codeActionsFor = (-1, -1, -1);
            if (await ListCodeActionsAsync(CancellationToken.None) is { } again
                && again.FirstOrDefault(a => a.Title == action.Title && a.Group == action.Group) is { } same)
            {
                listedFor = _codeActionsFor;
                applied = same.Id;
                edits = await _features.GetCodeActionEditsAsync(applied, CancellationToken.None);
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

        if (_features is Services.ICodeActionFollowUp followUp)
        {
            await followUp.RunAfterEditsAsync(applied, CancellationToken.None);
        }

        _editor.TextArea.Focus();
    }

    /// <summary>Shows the light bulb after the text of the caret line, the only one in the app.</summary>
    /// <returns>Whether the editor and the line are in view, so the bulb is shown.</returns>
    private bool ShowLightBulb()
    {
        if (_lightBulb is null || _lightBulbPopup is null)
        {
            return false;
        }

        if (!IsShownOnScreen)
        {
            CloseQuickActions();
            return false;
        }

        TakeQuickActions();
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

        if (_quickActionsOwner == this && _quickActionsMenu is null)
        {
            _quickActionsOwner = null;
        }
    }
}
