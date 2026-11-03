using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Accessibility;
using TabStudio.App.Controls;
using TabStudio.App.Services.Appearance;
using TabStudio.App.Services.Search;
using TabStudio.App.Tests.Theming;
using TabStudio.App.Tests.Views.Scrolling;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;

namespace TabStudio.App.Tests.Sweep;

/// <summary>
/// Every command a person can reach in the IDE, run on a real opened solution (the sample) on a tablet's screen: each
/// menu command and Ctrl+Q entry, each key gesture the IDE shows, each toolbar, status bar, activity bar and tool
/// window button tapped with a finger, each context menu's items, and the Start window's buttons. Each one must run
/// without an exception, an error in the Output or a frozen UI thread, must leave no dialog a person cannot close, and
/// every screen it leads to must meet the accessibility and layout rules. CI runs these in the dark and the light
/// theme; what they find is in <c>sweep/</c> beside the screenshots (<see cref="SweepHarness"/>).
/// </summary>
[Collection(AppearanceTestGroup.Name)]
public sealed partial class EveryCommandTests
{
    /// <summary>Commands that end what the sweep runs in, which run last or on their own.</summary>
    private static readonly string[] RunLast = ["Close Solution"];

    /// <summary>
    /// Buttons a tap of the sweep leaves alone: they delete or discard the user's work, sign out, or download and
    /// install toolchains and packages over the network. Each is run by its own tests.
    /// </summary>
    [GeneratedRegex(@"\b(delete|remove|discard|uninstall|sign out|log out|install|download|reset|undo commit|force|wipe|erase|format chip|flash|upload|publish|push|merge|close pull request|approve)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Destructive();

    [AvaloniaFact]
    public async Task Every_menu_and_search_command_runs_without_an_error_on_a_tablet()
    {
        using var sweep = new SweepHarness("menus");
        sweep.Show(1280, 800);
        var workspace = await sweep.OpenSampleAsync();
        sweep.Audit("the sample, open");
        var unavailable = new List<CommandEntry>();
        var entries = workspace.Commands.Commands.ToList();
        sweep.Note(string.Create(CultureInfo.InvariantCulture, $"commands\t{entries.Count}"));
        foreach (var entry in entries.Where(e => !RunLast.Contains(e.Title)))
        {
            await RunEntryAsync(sweep, entry, unavailable);
        }

        // With a debug session paused at a breakpoint, every command that only applies then.
        await DebugSweepAsync(sweep, unavailable);

        foreach (var entry in entries.Where(e => RunLast.Contains(e.Title)))
        {
            await RunEntryAsync(sweep, entry, unavailable);
        }

        sweep.Note("unavailable on the sample\t" + string.Join(", ", unavailable.Select(Title)));
        Finish(sweep);
    }

    [AvaloniaFact]
    public async Task Every_key_gesture_the_IDE_shows_runs_its_command()
    {
        using var sweep = new SweepHarness("shortcuts");
        sweep.Show(1280, 800);
        await sweep.OpenSampleAsync();
        var greeter = Directory.EnumerateFiles(Path.GetDirectoryName(sweep.SolutionPath)!, "Greeter.cs", SearchOption.AllDirectories).First();
        foreach (var (gesture, title, command, parameter) in Gestures(sweep))
        {
            // Two documents, so the next and previous document have somewhere to go, and the caret on a symbol.
            await sweep.ResetAsync();
            await sweep.Workspace.OpenDocumentAsync(greeter);
            await sweep.Workspace.OpenDocumentAsync(sweep.ProgramPath);
            await sweep.SettleAsync(60);
            var editor = FocusEditor(sweep, "Greeter(");
            if (editor is null)
            {
                sweep.Add("shortcut", gesture, "The editor could not take the keyboard focus.");
                continue;
            }

            var keys = await sweep.RunAsync($"keys: {gesture} ({title})", () =>
            {
                foreach (var (key, modifiers) in Keys(gesture))
                {
                    sweep.PressKey(key, modifiers);
                }

                return null;
            });
            await sweep.DismissAsync($"keys: {gesture}");
            if (keys.Changed || command is null)
            {
                continue;
            }

            // The key did nothing: does the command it stands for do something from the same place?
            await sweep.ResetAsync();
            await sweep.Workspace.OpenDocumentAsync(greeter);
            await sweep.Workspace.OpenDocumentAsync(sweep.ProgramPath);
            FocusEditor(sweep, "Greeter(");
            var direct = await sweep.RunAsync($"command: {title}", () => Start(command, parameter));
            await sweep.DismissAsync($"command: {title}");
            if (direct.Changed)
            {
                sweep.Add("shortcut", $"keys: {gesture}", $"{gesture} does nothing in the editor, but {title}, which shows it, does ({direct.Change}).");
            }
        }

        Finish(sweep);
    }

    [AvaloniaFact]
    public async Task Every_button_of_the_shell_and_its_tool_windows_works_by_touch_in_both_orientations()
    {
        foreach (var (name, width, height) in new[] { ("landscape", 1280.0, 800.0), ("portrait", 800.0, 1280.0) })
        {
            using var sweep = new SweepHarness("touch-" + name);
            var window = sweep.Show(width, height);
            var workspace = await sweep.OpenSampleAsync();
            var finger = new HeadlessTouchScreen(window);
            var done = new HashSet<string>(StringComparer.Ordinal);
            workspace.IsTouchInput = true;
            sweep.Audit($"{name}: the sample, open");

            // The shell's own buttons: toolbar, status bar, activity bar, bottom tabs, the touch bar.
            await TapEverythingAsync(sweep, finger, name, done, () => { });
            await TapOverflowAsync(sweep, name);

            foreach (var panel in Enum.GetValues<SideBarPanel>())
            {
                await TapEverythingAsync(sweep, finger, $"{name}: {panel}", done, () => sweep.Workspace.SelectSideBarPanelCommand.Execute(panel));
            }

            foreach (var panel in Enum.GetValues<BottomPanel>())
            {
                await TapEverythingAsync(sweep, finger, $"{name}: {panel}", done, () => sweep.Workspace.SelectPanelCommand.Execute(panel));
            }

            sweep.Note(string.Create(CultureInfo.InvariantCulture, $"tapped\t{done.Count}"));
            Finish(sweep);
        }
    }

    [AvaloniaFact]
    public async Task Every_context_menu_item_runs_its_command()
    {
        using var sweep = new SweepHarness("context-menus");
        sweep.Show(1280, 800);
        await sweep.OpenSampleAsync();

        // The editor's, on a symbol, with a selection for Cut and Copy.
        for (var index = 0; ; index++)
        {
            await sweep.ResetAsync();
            var editor = FocusEditor(sweep, "Greeter(");
            if (editor is null)
            {
                sweep.Add("dead-end", "the editor's context menu", "The editor could not take the focus.");
                break;
            }

            editor.Editor.Select(editor.Editor.CaretOffset, 7);
            var menu = editor.Editor.ContextMenu;
            if (menu is null)
            {
                sweep.Add("dead-end", "the editor's context menu", "The editor has no context menu.");
                break;
            }

            editor.Editor.RaiseEvent(new ContextRequestedEventArgs());
            sweep.Pump();
            if (index == 0)
            {
                sweep.Audit("the editor's context menu");
            }

            var items = menu.Items.OfType<MenuItem>().Where(i => i.IsVisible).ToList();
            if (index >= items.Count)
            {
                menu.Close();
                break;
            }

            var item = items[index];
            var header = item.Header as string ?? "?";
            if (!item.IsEnabled)
            {
                menu.Close();
                continue;
            }

            var outcome = await sweep.RunAsync($"editor context menu: {header}", () =>
            {
                item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                menu.Close();
                return null;
            });
            await NoEffectAsync(sweep, $"editor context menu: {header}", outcome);
            await sweep.DismissAsync($"editor context menu: {header}");
        }

        // The tool windows' rows: Solution Explorer's file and project, and the others that have rows now.
        foreach (var panel in new[] { SideBarPanel.SolutionExplorer, SideBarPanel.TestExplorer, SideBarPanel.GitChanges })
        {
            await sweep.ResetAsync();
            sweep.Workspace.SelectSideBarPanelCommand.Execute(panel);
            await sweep.SettleAsync();
            var owners = sweep.Window.GetVisualDescendants().OfType<Control>()
                .Where(c => c.ContextFlyout is MenuFlyout && c.IsEffectivelyVisible).Take(4).ToList();
            foreach (var owner in owners)
            {
                await RunFlyoutAsync(sweep, $"{panel} row", owner);
            }
        }

        Finish(sweep);
    }

    [AvaloniaFact]
    public async Task Every_button_of_the_Start_window_works()
    {
        using var sweep = new SweepHarness("start");
        var window = sweep.Show(1280, 800);
        await sweep.SettleAsync(300);
        sweep.Audit("the Start window");
        var finger = new HeadlessTouchScreen(window);
        var done = new HashSet<string>(StringComparer.Ordinal);
        for (var round = 0; round < 60; round++)
        {
            if (sweep.Main.Workspace is not null)
            {
                sweep.Main.Workspace.CloseWorkspaceCommand.Execute(null);
                await sweep.SettleAsync(200);
            }

            var next = Buttons(window).FirstOrDefault(b => !done.Contains(NameOf(b)));
            if (next is null)
            {
                break;
            }

            var key = NameOf(next);
            done.Add(key);
            if (Destructive().IsMatch(key))
            {
                sweep.Note("skipped\t" + key);
                continue;
            }

            var outcome = await sweep.RunAsync("start: " + key, () =>
            {
                Tap(sweep, finger, next, "start: " + key);
                return null;
            });
            sweep.Audit("start: " + key);
            await NoEffectAsync(sweep, "start: " + key, outcome);
            await sweep.DismissAsync("start: " + key);
        }

        Finish(sweep);
    }

    private static void Finish(SweepHarness sweep)
    {
        sweep.Write();
        if (SweepAllowances.Enforced)
        {
            sweep.AssertClean();
        }
    }

    private static async Task RunEntryAsync(SweepHarness sweep, CommandEntry entry, List<CommandEntry> unavailable)
    {
        await sweep.ResetAsync();
        await PrepareAsync(sweep, entry);
        if (!entry.IsAvailable())
        {
            unavailable.Add(entry);
            return;
        }

        var where = "menu: " + Title(entry);
        var size = sweep.Workspace.CurrentTextSize;
        AnswerInput(sweep.Workspace);
        var outcome = await sweep.RunAsync(where, () => Start(entry.Command, entry.Parameter));
        sweep.Audit(where);
        await NoEffectAsync(sweep, where, outcome);
        await sweep.DismissAsync(where);

        // The theme and the text size are the app's: they go back for the next command.
        if (entry.Path?.EndsWith("Theme", StringComparison.Ordinal) == true)
        {
            AppearanceService.Preview(TestTheme.Baseline);
        }

        if (entry.Path?.EndsWith("Text Size", StringComparison.Ordinal) == true)
        {
            if (size is { } before && sweep.Main.Workspace is { } workspace)
            {
                workspace.UseTextSizeCommand.Execute(before);
            }

            TypeScale.Apply(1);
        }

        await sweep.SettleAsync(40);
    }

    /// <summary>Sets up what a command acts on, so that one that does nothing is told from one with nothing to do.</summary>
    private static async Task PrepareAsync(SweepHarness sweep, CommandEntry entry)
    {
        var workspace = sweep.Workspace;
        switch (entry.Title)
        {
            case "Save" or "Save All" or "Undo" or "Redo":
                var editor = FocusEditor(sweep, "Greeter(");
                editor?.Editor.Document.Insert(0, "// edited\n");
                if (entry.Title == "Redo")
                {
                    editor?.Editor.Undo();
                }

                break;
            case "Next Document" or "Previous Document" or "Navigate Backward" or "Navigate Forward":
                var greeter = Directory.EnumerateFiles(Path.GetDirectoryName(sweep.SolutionPath)!, "Greeter.cs", SearchOption.AllDirectories).First();
                await workspace.OpenDocumentAsync(greeter);
                await workspace.OpenDocumentAsync(sweep.ProgramPath);
                if (entry.Title == "Navigate Forward" && workspace.NavigateBackCommand.CanExecute(null))
                {
                    workspace.NavigateBackCommand.Execute(null);
                }

                break;
            case "Toggle Breakpoint" or "Run To Cursor" or "Go To Definition" or "Peek Definition" or "Go To Implementation"
                or "Find All References" or "Rename" or "Quick Info" or "Parameter Info" or "Quick Actions and Refactorings"
                or "Go To Declaration" or "Go To Type Definition":
                FocusEditor(sweep, "Greeter(");
                break;
            case "Delete All Breakpoints":
                workspace.Breakpoints.Toggle(sweep.ProgramPath, 16);
                break;
            default:
                FocusEditor(sweep, "Greeter(");
                break;
        }

        await sweep.SettleAsync(40);
    }

    /// <summary>The commands that apply only while debugging, run with the sample paused at a breakpoint.</summary>
    private static async Task DebugSweepAsync(SweepHarness sweep, List<CommandEntry> unavailable)
    {
        foreach (var entry in unavailable.ToList())
        {
            await sweep.ResetAsync();
            if (!await PauseAsync(sweep))
            {
                sweep.Add("exception", "debugging the sample", "The sample did not stop at its breakpoint, so the debugger's commands were not run.");
                return;
            }

            FocusEditor(sweep, "greeter.Greet");
            if (!entry.IsAvailable())
            {
                continue;
            }

            unavailable.Remove(entry);
            var where = "debugging: " + Title(entry);
            var outcome = await sweep.RunAsync(where, () => Start(entry.Command, entry.Parameter));
            sweep.Audit(where);
            await NoEffectAsync(sweep, where, outcome);
            await sweep.DismissAsync(where);
        }

        await sweep.ResetAsync();
    }

    private static async Task<bool> PauseAsync(SweepHarness sweep)
    {
        var workspace = sweep.Workspace;
        if (workspace.State == IdeState.Paused)
        {
            return true;
        }

        if (workspace.Breakpoints.Find(sweep.ProgramPath, 16) is null)
        {
            workspace.Breakpoints.Toggle(sweep.ProgramPath, 16);
        }

        AnswerInput(workspace);
        _ = workspace.StartDebuggingCommand.ExecuteAsync(null);
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (workspace.State != IdeState.Paused && DateTime.UtcNow < deadline)
        {
            sweep.Pump();
            await Task.Delay(25);
        }

        await sweep.SettleAsync(100);
        return workspace.State == IdeState.Paused;
    }

    private static void AnswerInput(WorkspaceViewModel workspace)
    {
        void Answer(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(OutputViewModel.IsWaitingForInput) && workspace.Output.IsWaitingForInput)
            {
                workspace.Output.InputText = "Ada";
                workspace.Output.SubmitInputCommand.Execute(null);
                workspace.Output.PropertyChanged -= Answer;
            }
        }

        workspace.Output.PropertyChanged += Answer;
    }

    /// <summary>Starts a command as its menu item does, and returns the task of one that runs asynchronously.</summary>
    private static Task? Start(ICommand command, object? parameter)
    {
        if (!command.CanExecute(parameter))
        {
            return null;
        }

        command.Execute(parameter);
        return command is IAsyncRelayCommand asynchronous ? asynchronous.ExecutionTask : null;
    }

    /// <summary>
    /// Records an action that changed nothing a person can see, after a moment more for one that answers late: a
    /// command that does nothing, or does it where nobody sees it.
    /// </summary>
    private static async Task NoEffectAsync(SweepHarness sweep, string where, SweepOutcome outcome)
    {
        if (outcome.Changed || !outcome.Completed)
        {
            return;
        }

        await sweep.SettleAsync(400);
        if (sweep.OpenOverlay() is null)
        {
            sweep.Add("no-effect", where, "It changed nothing a person can see: no panel, dialog, text, selection or status.");
        }
    }

    private static string Title(CommandEntry entry) => entry.Path is null ? entry.Title : $"{entry.Path} > {entry.Title}";

    /// <summary>Puts the keyboard focus in the editor, with the caret just after a piece of its text.</summary>
    private static CodeEditorView? FocusEditor(SweepHarness sweep, string at)
    {
        var editor = sweep.Window.GetVisualDescendants().OfType<CodeEditorView>().FirstOrDefault(e => e.IsEffectivelyVisible);
        if (editor is null)
        {
            return null;
        }

        var offset = editor.Editor.Document.Text.IndexOf(at, StringComparison.Ordinal);
        editor.Editor.CaretOffset = offset < 0 ? 0 : offset + 1;
        editor.Editor.TextArea.Focus();
        sweep.Pump();
        return editor;
    }

    /// <summary>
    /// Every key gesture the IDE shows a person, with the command it stands for: in the menus (as a menu item's
    /// gesture or written in its header), in the toolbar's tooltips and in the activity bar's.
    /// </summary>
    private static List<(string Gesture, string Title, ICommand? Command, object? Parameter)> Gestures(SweepHarness sweep)
    {
        var found = new List<(string, string, ICommand?, object?)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in sweep.Workspace.Commands.Commands)
        {
            if (entry.Gesture is { Length: > 0 } gesture && seen.Add(gesture))
            {
                found.Add((gesture, Title(entry), entry.Command, entry.Parameter));
            }
        }

        foreach (var item in sweep.Window.GetVisualDescendants().OfType<AdaptiveBarItem>())
        {
            if (item.Gesture is { Length: > 0 } gesture && seen.Add(gesture))
            {
                found.Add((gesture, item.Label ?? "?", item.Command, item.CommandParameter));
            }
        }

        foreach (var item in sweep.Workspace.Activity.Items)
        {
            if (item.Gesture is { Length: > 0 } gesture && seen.Add(gesture))
            {
                found.Add((gesture, item.Title, item.Command ?? sweep.Workspace.SelectSideBarPanelCommand,
                    Enum.TryParse<SideBarPanel>(item.Id, out var panel) ? panel : null));
            }
        }

        return found;
    }

    /// <summary>The keys of a gesture as the IDE writes it, such as "Ctrl+K, Ctrl+I" or "Alt+Num *".</summary>
    private static IEnumerable<(Key Key, KeyModifiers Modifiers)> Keys(string gesture)
    {
        foreach (var part in gesture.Split(", ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var modifiers = KeyModifiers.None;
            var keys = part.Split('+');
            var name = keys[^1];
            if (name.Length == 0 && part.EndsWith('+'))
            {
                name = "+";
            }

            foreach (var modifier in keys[..^1])
            {
                modifiers |= modifier switch
                {
                    "Ctrl" => KeyModifiers.Control,
                    "Shift" => KeyModifiers.Shift,
                    "Alt" => KeyModifiers.Alt,
                    "Cmd" or "Meta" or "Win" => KeyModifiers.Meta,
                    _ => KeyModifiers.None,
                };
            }

            var key = name switch
            {
                "/" => Key.OemQuestion,
                "`" => Key.OemTilde,
                "." => Key.OemPeriod,
                "," => Key.OemComma,
                "Num *" or "*" => Key.Multiply,
                "Break" => Key.Pause,
                "Left" => Key.Left,
                "Right" => Key.Right,
                _ when Enum.TryParse<Key>(name, ignoreCase: true, out var parsed) => parsed,
                _ when name.Length == 1 && char.IsLetterOrDigit(name[0]) => Enum.Parse<Key>(char.IsDigit(name[0]) ? "D" + name : name.ToUpperInvariant()),
                _ => Key.None,
            };
            yield return (key, modifiers);
        }
    }

    /// <summary>
    /// Taps every button that shows now (after an action that shows a tool window), once across the sweep: a finger on
    /// its middle. A button whose middle another control covers cannot be reached.
    /// </summary>
    private static async Task TapEverythingAsync(SweepHarness sweep, HeadlessTouchScreen finger, string state, HashSet<string> done, Action show)
    {
        await sweep.ResetAsync();
        show();
        await sweep.SettleAsync();
        sweep.Audit(state);
        for (var round = 0; round < 80; round++)
        {
            var next = Buttons(sweep.Window).FirstOrDefault(b => !done.Contains(NameOf(b)));
            if (next is null)
            {
                return;
            }

            var key = NameOf(next);
            done.Add(key);
            if (Destructive().IsMatch(key) || next.Name is "RunButton" or "MenuButton")
            {
                sweep.Note("skipped\t" + key);
                continue;
            }

            var where = $"tap ({state}): {key}";
            AnswerInput(sweep.Workspace);
            var outcome = await sweep.RunAsync(where, () =>
            {
                Tap(sweep, finger, next, where);
                return null;
            });
            sweep.Audit(where);
            await NoEffectAsync(sweep, where, outcome);
            await sweep.DismissAsync(where);
            await sweep.ResetAsync();
            show();
            await sweep.SettleAsync(60);
        }
    }

    /// <summary>What the toolbar and the status bar fold into More, run from its menu.</summary>
    private static async Task TapOverflowAsync(SweepHarness sweep, string state)
    {
        foreach (var name in new[] { "Toolbar", "StatusBar" })
        {
            for (var index = 0; index < 30; index++)
            {
                await sweep.ResetAsync();
                var bar = sweep.Window.GetVisualDescendants().OfType<AdaptiveBar>().FirstOrDefault(b => b.Name == name);
                if (bar is null || !bar.ShowsMore || bar.MoreButton.Flyout is not MenuFlyout menu)
                {
                    break;
                }

                menu.ShowAt(bar.MoreButton);
                sweep.Pump();
                if (index == 0)
                {
                    sweep.Audit($"{state}: {name}'s More");
                }

                var items = menu.Items.OfType<MenuItem>().Where(i => i.IsVisible).ToList();
                if (index >= items.Count)
                {
                    menu.Hide();
                    break;
                }

                var item = items[index];
                var header = item.Header as string ?? "?";
                if (!item.IsEnabled || item.Items.Count > 0)
                {
                    menu.Hide();
                    continue;
                }

                var where = $"{state}: {name} > More > {header}";
                if (item.Command is null)
                {
                    sweep.Add("no-effect", where, "It is a menu item with nothing to run.");
                    menu.Hide();
                    continue;
                }

                var outcome = await sweep.RunAsync(where, () =>
                {
                    item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    menu.Hide();
                    return null;
                });
                await NoEffectAsync(sweep, where, outcome);
                await sweep.DismissAsync(where);
            }
        }
    }

    /// <summary>Opens a control's context flyout as a long press does and runs each of its items, one per opening.</summary>
    private static async Task RunFlyoutAsync(SweepHarness sweep, string where, Control owner)
    {
        for (var index = 0; index < 20; index++)
        {
            if (owner.ContextFlyout is not MenuFlyout flyout || !owner.IsEffectivelyVisible)
            {
                return;
            }

            owner.RaiseEvent(new ContextRequestedEventArgs());
            sweep.Pump();
            if (index == 0)
            {
                sweep.Audit(where + " context menu");
            }

            var items = flyout.Items.OfType<MenuItem>().Where(i => i.IsVisible && i.Items.Count == 0).ToList();
            if (index >= items.Count)
            {
                flyout.Hide();
                return;
            }

            var item = items[index];
            var header = item.Header as string ?? "?";
            if (!item.IsEffectivelyEnabled || Destructive().IsMatch(header))
            {
                flyout.Hide();
                continue;
            }

            var outcome = await sweep.RunAsync($"{where} context menu: {header}", () =>
            {
                item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                flyout.Hide();
                return null;
            });
            await NoEffectAsync(sweep, $"{where} context menu: {header}", outcome);
            await sweep.DismissAsync($"{where} context menu: {header}");
        }
    }

    /// <summary>The buttons a finger can tap now: shown, enabled, inside the window, and not a row of a list.</summary>
    private static List<Button> Buttons(Window window) =>
        [.. window.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible && b.IsEffectivelyEnabled
            && b.Bounds.Width >= 1 && b.Bounds.Height >= 1
            && b.TranslatePoint(new Point(b.Bounds.Width / 2, b.Bounds.Height / 2), window) is { } middle
            && middle.X >= 0 && middle.Y >= 0 && middle.X <= window.Bounds.Width && middle.Y <= window.Bounds.Height
            && !b.GetVisualAncestors().Any(a => a is ListBoxItem or TreeViewItem or ScrollBar or AvaloniaEdit.TextEditor))];

    /// <summary>A button's name for the report, the same wherever it shows: the view it is in and what it says.</summary>
    private static string NameOf(Button button)
    {
        var view = button.GetVisualAncestors().OfType<UserControl>().FirstOrDefault()?.GetType().Name ?? "Window";
        var label = button switch
        {
            AdaptiveBarItem { Label: { Length: > 0 } text } => text,
            { Content: string text } => text,
            _ => ToolTip.GetTip(button) as string ?? Avalonia.Automation.AutomationProperties.GetName(button)
                ?? button.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => !string.IsNullOrEmpty(t.Text))?.Text
                ?? button.Name ?? button.GetType().Name,
        };
        return $"{view} > {(string.IsNullOrEmpty(button.Name) ? string.Empty : button.Name + " ")}\"{label}\"";
    }

    /// <summary>A finger's tap on a button's middle, after checking that nothing covers it there.</summary>
    private static void Tap(SweepHarness sweep, HeadlessTouchScreen finger, Button button, string where)
    {
        var middle = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), sweep.Window)
            ?? throw new InvalidOperationException("The button left the window.");
        if (sweep.Window.InputHitTest(middle) is Visual hit && !ReferenceEquals(hit, button) && !button.IsVisualAncestorOf(hit))
        {
            sweep.Add("covered", where, $"A tap on its middle reaches {hit.GetType().Name}{(hit is Control { Name: { Length: > 0 } name } ? "#" + name : string.Empty)} instead.");
        }

        finger.Down(middle);
        finger.Wait(60);
        finger.Up(middle);
        finger.Wait(400);
    }
}
