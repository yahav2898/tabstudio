using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using TabStudio.App.Controls;
using TabStudio.App.Tests.Theming;
using TabStudio.App.Tests.Views.Scrolling;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;

namespace TabStudio.App.Tests.Sweep;

/// <summary>
/// What a tablet does to the IDE that a desktop does not: split screen and pop-up windows of every size, rotation in
/// the middle of work, a hardware keyboard attached and taken away, the on-screen keyboard over the caret, going to the
/// background during a build or a debug session, and right-to-left text and IME composition in the editor, in Find and
/// Replace and in the Terminal. Recorded like every command (<see cref="SweepHarness"/>).
/// </summary>
[Collection(AppearanceTestGroup.Name)]
public sealed class TabletSituationTests
{
    /// <summary>A tablet's screens and the windows Android gives an app in split screen and as a pop-up.</summary>
    private static readonly (string Name, double Width, double Height)[] Screens =
    [
        ("landscape", 1280, 800),
        ("portrait", 800, 1280),
        ("split screen, landscape half", 640, 800),
        ("split screen, portrait half", 800, 640),
        ("split screen, a third", 426, 800),
        ("pop-up window", 520, 620),
    ];

    [AvaloniaFact]
    public async Task Every_tool_window_fits_every_screen_and_window_size()
    {
        using var sweep = new SweepHarness("screens");
        sweep.Show(1280, 800);
        await sweep.OpenSampleAsync();
        foreach (var (name, width, height) in Screens)
        {
            sweep.Resize(width, height);
            await sweep.ResetAsync();
            sweep.Audit(name);
            foreach (var panel in Enum.GetValues<SideBarPanel>())
            {
                await sweep.RunAsync($"{name}: {panel}", () =>
                {
                    sweep.Workspace.SelectSideBarPanelCommand.Execute(panel);
                    return null;
                });
                sweep.Audit($"{name}: {panel}");
            }

            foreach (var panel in Enum.GetValues<BottomPanel>())
            {
                await sweep.RunAsync($"{name}: {panel}", () =>
                {
                    sweep.Workspace.SelectPanelCommand.Execute(panel);
                    return null;
                });
                sweep.Audit($"{name}: {panel}");
            }

            sweep.Workspace.Search.OpenCommand.Execute(null);
            sweep.Workspace.Search.Query = "gree";
            await sweep.Workspace.Search.Completion;
            await sweep.SettleAsync();
            sweep.Audit($"{name}: the search");
            await sweep.DismissAsync($"{name}: the search");

            _ = sweep.Workspace.Prompt.AskAsync("Rename", "The new name of the symbol:", "Rename", "Greeter");
            await sweep.SettleAsync();
            sweep.Audit($"{name}: a prompt");
            await sweep.DismissAsync($"{name}: a prompt");
        }

        Finish(sweep);
    }

    [AvaloniaFact]
    public async Task Rotating_and_going_to_the_background_during_a_build_and_a_debug_session()
    {
        var activity = new SweepAppActivity();
        using var sweep = new SweepHarness("lifecycle", activity);
        sweep.Show(1280, 800);
        var workspace = await sweep.OpenSampleAsync();

        // A build, sent to the background and rotated while it runs, then brought back.
        var build = await sweep.RunAsync("a build, to the background and back", () =>
        {
            var task = workspace.BuildCommand.ExecuteAsync(null);
            Background(sweep, activity);
            sweep.Resize(800, 1280);
            Foreground(activity);
            return task;
        }, TimeSpan.FromSeconds(90));
        if (!build.Completed || !workspace.Output.Text.Contains("Build succeeded", StringComparison.OrdinalIgnoreCase))
        {
            sweep.Add("lifecycle", "a build, to the background and back", "The build did not succeed after the app went to the background and came back: " + Last(workspace.Output.Text));
        }

        sweep.Audit("portrait, after the build");
        sweep.Resize(1280, 800);

        // A debug session paused at a breakpoint: to the background, rotated, back, and stepped on.
        workspace.Breakpoints.Toggle(sweep.ProgramPath, 16);
        Answer(workspace);
        _ = workspace.StartDebuggingCommand.ExecuteAsync(null);
        await WaitAsync(sweep, () => workspace.State == IdeState.Paused, TimeSpan.FromSeconds(60));
        if (workspace.State != IdeState.Paused)
        {
            sweep.Add("lifecycle", "debugging", "The sample did not stop at its breakpoint.");
            Finish(sweep);
            return;
        }

        await sweep.RunAsync("a paused debug session, to the background and back", () =>
        {
            Background(sweep, activity);
            sweep.Resize(800, 1280);
            Foreground(activity);
            return null;
        });
        sweep.Audit("portrait, paused at a breakpoint");
        if (workspace.State != IdeState.Paused)
        {
            sweep.Add("lifecycle", "a paused debug session, to the background and back", $"The session is {workspace.State} after coming back, not paused.");
        }

        await sweep.RunAsync("stepping after coming back", () => workspace.StepOverCommand.ExecuteAsync(null), TimeSpan.FromSeconds(30));
        await WaitAsync(sweep, () => workspace.State == IdeState.Paused, TimeSpan.FromSeconds(30));
        if (workspace.State != IdeState.Paused || workspace.Locals.Tree.Rows.Count == 0)
        {
            sweep.Add("lifecycle", "stepping after coming back", $"Step Over after coming back left the session {workspace.State} with {workspace.Locals.Tree.Rows.Count} locals.");
        }

        // Going to the background while the program runs, then stopping it from the foreground.
        await sweep.RunAsync("a running program, to the background and back", () =>
        {
            workspace.StartDebuggingCommand.Execute(null);
            Background(sweep, activity);
            sweep.Resize(1280, 800);
            Foreground(activity);
            return null;
        });
        await sweep.RunAsync("Stop after coming back", () =>
        {
            workspace.StopDebuggingCommand.Execute(null);
            return null;
        });
        await WaitAsync(sweep, () => !workspace.IsDebugging && !workspace.IsRunning, TimeSpan.FromSeconds(30));
        if (workspace.IsDebugging || workspace.IsRunning)
        {
            sweep.Add("lifecycle", "Stop after coming back", "The program was still running 30 s after Stop.");
        }

        Finish(sweep);
    }

    [AvaloniaFact]
    public async Task A_hardware_keyboard_attached_and_detached_and_the_on_screen_keyboard_over_the_caret()
    {
        using var sweep = new SweepHarness("keyboards");
        var window = sweep.Show(1280, 800);
        var workspace = await sweep.OpenSampleAsync();
        var finger = new HeadlessTouchScreen(window);
        var editor = window.GetVisualDescendants().OfType<CodeEditorView>().First(e => e.IsEffectivelyVisible);
        var textArea = editor.Editor.TextArea;

        // Typing on a keyboard, then a finger, then the keyboard again: the IDE follows each.
        textArea.Focus();
        await sweep.RunAsync("typing on a hardware keyboard", () =>
        {
            sweep.PressKey(Key.End);
            window.KeyTextInput(" ");
            return null;
        });
        if (workspace.IsTouchInput)
        {
            sweep.Add("keyboard", "typing on a hardware keyboard", "The IDE still counts the input as touch after keys were typed.");
        }

        var middle = textArea.TranslatePoint(new Point(textArea.Bounds.Width / 2, 40), window) ?? default;
        await sweep.RunAsync("a tap on the editor after typing", () =>
        {
            finger.Down(middle);
            finger.Wait(60);
            finger.Up(middle);
            return null;
        });
        if (!workspace.IsTouchInput)
        {
            sweep.Add("keyboard", "a tap on the editor after typing", "The IDE still counts the input as a keyboard's after a finger tapped.");
        }

        sweep.Audit("touch again after a keyboard");
        await sweep.RunAsync("F9 after a tap", () =>
        {
            textArea.Focus();
            sweep.PressKey(Key.F9);
            return null;
        });
        if (workspace.Breakpoints.All.Count == 0)
        {
            sweep.Add("keyboard", "F9 after a tap", "F9 set no breakpoint after the input switched from touch to the keyboard.");
        }

        // The on-screen keyboard: the window loses its lower part, as Android resizes it, while the caret is near the
        // bottom of the editor. The caret must stay in view, above the keyboard.
        foreach (var (name, width, height) in new[] { ("landscape", 1280.0, 800.0), ("portrait", 800.0, 1280.0) })
        {
            sweep.Resize(width, height);
            await sweep.ResetAsync();
            editor = window.GetVisualDescendants().OfType<CodeEditorView>().First(e => e.IsEffectivelyVisible);
            textArea = editor.Editor.TextArea;
            var document = editor.Editor.Document;
            if (document.LineCount < 120)
            {
                document.Insert(document.TextLength, string.Concat(Enumerable.Range(1, 120).Select(i => string.Create(CultureInfo.InvariantCulture, $"// line {i}\n"))));
            }

            await sweep.SettleAsync();
            var view = textArea.TextView;
            view.EnsureVisualLines();
            var last = view.VisualLines.Count > 2 ? view.VisualLines[^2].FirstDocumentLine.LineNumber : 10;
            editor.Editor.TextArea.Caret.Line = last;
            textArea.Focus();
            await sweep.SettleAsync();
            await sweep.RunAsync($"{name}: the on-screen keyboard opens over the caret", () =>
            {
                sweep.Resize(width, height * 0.55);
                return null;
            });
            await sweep.SettleAsync(200);
            if (!CaretInView(editor))
            {
                sweep.Add("keyboard", $"{name}: the on-screen keyboard opens over the caret",
                    $"The caret's line ({editor.Editor.TextArea.Caret.Line}) is under the keyboard: the editor did not scroll it into what is left of the screen.");
            }

            sweep.Audit($"{name}: with the on-screen keyboard");
            sweep.Resize(width, height);
            await sweep.SettleAsync();
        }

        Finish(sweep);
    }

    [AvaloniaFact]
    public async Task Hebrew_and_IME_composition_in_the_editor_Find_Replace_and_the_Terminal()
    {
        using var sweep = new SweepHarness("rtl-ime");
        var window = sweep.Show(1280, 800);
        var workspace = await sweep.OpenSampleAsync();
        var editor = window.GetVisualDescendants().OfType<CodeEditorView>().First(e => e.IsEffectivelyVisible);
        var textArea = editor.Editor.TextArea;
        const string Hebrew = "שלום עולם";

        // Typed into a comment, word by word, as an on-screen keyboard commits it, then composed as an IME does.
        textArea.Focus();
        editor.Editor.CaretOffset = 0;
        await sweep.RunAsync("Hebrew typed in the editor", () =>
        {
            window.KeyTextInput("// ");
            window.KeyTextInput("שלום");
            window.KeyTextInput(" ");
            window.KeyTextInput("עולם");
            sweep.PressKey(Key.Enter);
            return null;
        });
        if (!editor.Editor.Document.Text.StartsWith("// " + Hebrew + "\n", StringComparison.Ordinal))
        {
            sweep.Add("rtl", "Hebrew typed in the editor", "The editor's first line is \"" + editor.Editor.Document.GetText(editor.Editor.Document.GetLineByNumber(1)) + "\".");
        }

        foreach (var key in new[] { Key.Home, Key.End, Key.Left, Key.Right, Key.Up, Key.Down })
        {
            editor.Editor.CaretOffset = 5;
            await sweep.RunAsync($"the caret through Hebrew: {key}", () =>
            {
                sweep.PressKey(key);
                sweep.PressKey(key, KeyModifiers.Shift);
                return null;
            });
        }

        sweep.Audit("Hebrew in the editor");
        await ComposeAsync(sweep, textArea, "the editor", "ב", "בר", "ברכה", () => editor.Editor.Document.Text);

        // Find and Replace, with Hebrew in their boxes.
        workspace.FindCommand.Execute(null);
        await sweep.SettleAsync();
        var search = editor.Search;
        await sweep.RunAsync("Find with Hebrew", () =>
        {
            search.SearchPattern = "עולם";
            return null;
        });
        sweep.Audit("Find with Hebrew");
        if (!editor.Editor.Document.Text.Contains("עולם", StringComparison.Ordinal))
        {
            sweep.Add("rtl", "Find with Hebrew", "The Hebrew text was gone before Find.");
        }

        search.Close();
        workspace.ReplaceCommand.Execute(null);
        await sweep.SettleAsync();
        await sweep.RunAsync("Replace All with Hebrew", () =>
        {
            search.SearchPattern = "עולם";
            search.ReplacePattern = "חברים";
            editor.ReplaceAll();
            return null;
        });
        if (!editor.Editor.Document.Text.Contains("שלום חברים", StringComparison.Ordinal))
        {
            sweep.Add("rtl", "Replace All with Hebrew", "Replace All did not turn \"שלום עולם\" into \"שלום חברים\": \"" + editor.Editor.Document.GetText(editor.Editor.Document.GetLineByNumber(1)) + "\".");
        }

        var findBox = search.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(t => t.IsEffectivelyVisible);
        if (findBox is not null)
        {
            findBox.Focus();
            findBox.Text = string.Empty;
            await ComposeAsync(sweep, findBox, "the find box", "ש", "של", "שלום", () => findBox.Text ?? string.Empty);
        }

        search.Close();

        // The Terminal: Hebrew typed and echoed back.
        workspace.ShowTerminalCommand.Execute(null);
        await WaitAsync(sweep, () => workspace.Terminal.IsRunning, TimeSpan.FromSeconds(30));
        if (workspace.Terminal.IsRunning)
        {
            var terminal = window.GetVisualDescendants().OfType<TerminalView>().FirstOrDefault(t => t.IsEffectivelyVisible);
            terminal?.Focus();
            await sweep.RunAsync("Hebrew in the Terminal", () =>
            {
                window.KeyTextInput("echo ");
                window.KeyTextInput("שלום");
                window.KeyTextInput("-");
                window.KeyTextInput("עולם");
                sweep.PressKey(Key.Enter);
                return null;
            });
            await WaitAsync(sweep, () => workspace.Terminal.Emulator.ScreenText.Contains("שלום-עולם\n", StringComparison.Ordinal)
                || workspace.Terminal.Emulator.ScreenText.Split('\n').Count(l => l.Contains("שלום-עולם", StringComparison.Ordinal)) >= 2, TimeSpan.FromSeconds(10));
            var echoed = workspace.Terminal.Emulator.ScreenText.Split('\n').Count(l => l.Contains("שלום-עולם", StringComparison.Ordinal));
            if (echoed < 2)
            {
                sweep.Add("rtl", "Hebrew in the Terminal", "The shell did not echo the Hebrew it was given: " + Last(workspace.Terminal.Emulator.ScreenText));
            }

            sweep.Audit("Hebrew in the Terminal");
            if (terminal is not null)
            {
                await ComposeAsync(sweep, terminal, "the Terminal", "ת", "תו", "תודה", () => workspace.Terminal.Emulator.ScreenText);
            }
        }
        else
        {
            sweep.Note("the Terminal did not start here: " + workspace.Terminal.Message);
        }

        Finish(sweep);
    }

    /// <summary>
    /// Composes a word as an IME does, its letters shown as they are typed (preedit), then commits it: the word must
    /// end up there once, without the composition's partial words.
    /// </summary>
    private static async Task ComposeAsync(SweepHarness sweep, Visual target, string where, string first, string second, string word, Func<string> text)
    {
        var request = new TextInputMethodClientRequestedEventArgs { RoutedEvent = InputElement.TextInputMethodClientRequestedEvent };
        (target as Interactive)?.RaiseEvent(request);
        var client = request.Client;
        if (client is null)
        {
            sweep.Add("ime", $"IME composition in {where}", "It offers the on-screen keyboard no text input client, so no IME can type into it.");
            return;
        }

        var before = Count(text(), word);
        await sweep.RunAsync($"IME composition in {where}", () =>
        {
            if (client.SupportsPreedit)
            {
                client.SetPreeditText(first);
                client.SetPreeditText(second);
                client.SetPreeditText(null);
            }

            sweep.Window.KeyTextInput(word);
            return null;
        });
        await sweep.SettleAsync(200);
        var after = text();
        if (Count(after, word) != before + 1)
        {
            sweep.Add("ime", $"IME composition in {where}", $"Composing \"{word}\" left it there {Count(after, word) - before} times.");
        }

        if (Count(after, second + word) > 0 || Count(after, first + word) > 0)
        {
            sweep.Add("ime", $"IME composition in {where}", $"The composition's partial word stayed before \"{word}\".");
        }
    }

    private static int Count(string text, string word)
    {
        var count = 0;
        for (var at = text.IndexOf(word, StringComparison.Ordinal); at >= 0; at = text.IndexOf(word, at + word.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    private static bool CaretInView(CodeEditorView editor)
    {
        var view = editor.Editor.TextArea.TextView;
        view.EnsureVisualLines();
        var caret = editor.Editor.TextArea.Caret;
        var top = view.GetVisualTopByDocumentLine(caret.Line) - view.ScrollOffset.Y;
        return top >= -1 && top + view.DefaultLineHeight <= view.Bounds.Height + 1;
    }

    private static void Background(SweepHarness sweep, SweepAppActivity activity)
    {
        // As Android's onStop does through the activatable lifetime (TabStudioApplication).
        sweep.Main.SaveSession();
        activity.Set(inBackground: true);
        sweep.Pump();
    }

    private static void Foreground(SweepAppActivity activity) => activity.Set(inBackground: false);

    private static void Answer(WorkspaceViewModel workspace)
    {
        void OnChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(OutputViewModel.IsWaitingForInput) && workspace.Output.IsWaitingForInput)
            {
                workspace.Output.InputText = "Ada";
                workspace.Output.SubmitInputCommand.Execute(null);
                workspace.Output.PropertyChanged -= OnChanged;
            }
        }

        workspace.Output.PropertyChanged += OnChanged;
    }

    private static async Task WaitAsync(SweepHarness sweep, Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition() && DateTime.UtcNow < deadline)
        {
            sweep.Pump();
            await Task.Delay(25);
        }

        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        sweep.Pump();
    }

    private static string Last(string text) =>
        string.Join(" | ", text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).TakeLast(4));

    private static void Finish(SweepHarness sweep)
    {
        sweep.Write();
        if (SweepAllowances.Enforced)
        {
            sweep.AssertClean();
        }
    }

}
