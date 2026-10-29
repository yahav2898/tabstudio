using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using TabStudio.App.Controls;

namespace TabStudio.App.Views;

/// <summary>
/// The editor's context menu, opened by a right click or a long press: IntelliSense commands for C# files, then
/// clipboard and formatting commands, with Visual Studio's shortcuts shown.
/// </summary>
public sealed partial class CodeEditorView
{
    private MenuItem? _cutItem;
    private MenuItem? _copyItem;

    private ContextMenu CreateContextMenu()
    {
        var menu = new ContextMenu();
        if (_features is not null)
        {
            menu.Items.Add(MenuEntry("Quick Actions and Refactorings...", new KeyGesture(Key.OemPeriod, KeyModifiers.Control), IntelliSenseIcons.LightBulb, () => _ = ShowQuickActionsAsync()));
            menu.Items.Add(MenuEntry("Rename...", new KeyGesture(Key.F2), null, () => _ = Workspace?.RenameSymbolCommand.ExecuteAsync(null)));
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuEntry("Go To Definition", new KeyGesture(Key.F12), null, () => _ = Workspace?.GoToDefinitionCommand.ExecuteAsync(null)));
            menu.Items.Add(MenuEntry("Peek Definition", new KeyGesture(Key.F12, KeyModifiers.Alt), null, () => _ = Workspace?.PeekDefinitionCommand.ExecuteAsync(null)));
            menu.Items.Add(MenuEntry("Go To Implementation", new KeyGesture(Key.F12, KeyModifiers.Control), null, () => _ = Workspace?.GoToImplementationCommand.ExecuteAsync(null)));
            AddLanguageServerMenuItems(menu);
            menu.Items.Add(MenuEntry("Find All References", new KeyGesture(Key.F12, KeyModifiers.Shift), null, () => _ = Workspace?.FindAllReferencesCommand.ExecuteAsync(null)));
            menu.Items.Add(new Separator());
        }

        _cutItem = MenuEntry("Cut", new KeyGesture(Key.X, KeyModifiers.Control), null, _editor.Cut);
        _copyItem = MenuEntry("Copy", new KeyGesture(Key.C, KeyModifiers.Control), null, _editor.Copy);
        menu.Items.Add(_cutItem);
        menu.Items.Add(_copyItem);
        menu.Items.Add(MenuEntry("Paste", new KeyGesture(Key.V, KeyModifiers.Control), null, _editor.Paste));
        menu.Items.Add(new Separator());
        menu.Items.Add(MenuEntry("Format Document", new KeyGesture(Key.F, KeyModifiers.Shift | KeyModifiers.Alt), null, () => _ = Workspace?.FormatDocumentCommand.ExecuteAsync(null)));
        menu.Items.Add(MenuEntry("Toggle Line Comment", new KeyGesture(Key.OemQuestion, KeyModifiers.Control), null, ToggleComment));

        menu.Opening += OnContextMenuOpening;
        menu.Closed += OnContextMenuClosed;
        return menu;
    }

    private void OnContextMenuOpening(object? sender, CancelEventArgs e)
    {
        if (IsDataTipHold)
        {
            e.Cancel = true;
            return;
        }

        var hasSelection = _editor.SelectionLength > 0;
        _cutItem!.IsEnabled = hasSelection;
        _copyItem!.IsEnabled = hasSelection;
        _completion?.Close();
        CloseSignatureHelp();
    }

    private void OnContextMenuClosed(object? sender, RoutedEventArgs e)
    {
        CloseQuickInfo();
        _editor.TextArea.Focus();
    }

    private static MenuItem MenuEntry(string header, KeyGesture gesture, Geometry? icon, Action action)
    {
        var item = new MenuItem { Header = header, InputGesture = gesture };
        if (icon is not null)
        {
            item.Icon = new StrokeIcon { Data = icon, Size = 15 };
        }

        item.Click += (_, _) => action();
        return item;
    }
}
