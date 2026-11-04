using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.Tests.Accessibility;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Sweep;

/// <summary>What the sweep found wrong.</summary>
/// <param name="Kind">
/// What kind of problem: <c>exception</c>, <c>unobserved</c>, <c>error-log</c>, <c>freeze</c>, <c>slow</c>,
/// <c>no-effect</c>, <c>dead-end</c>, <c>shortcut</c>, or an accessibility rule (<c>target</c>, <c>name</c>,
/// <c>contrast</c>, <c>icon-contrast</c>, <c>clipped</c>, <c>off-screen</c>, <c>overlap</c>).
/// </param>
/// <param name="Where">The command or state where it was first seen, such as "menu: Build &gt; Build Solution".</param>
/// <param name="What">What is wrong, in a sentence.</param>
internal sealed record SweepProblem(string Kind, string Where, string What);

/// <summary>What running one action did.</summary>
/// <param name="Changed">Whether anything a person can see or the IDE's state changed.</param>
/// <param name="Change">The first things that changed, for the report.</param>
/// <param name="Completed">Whether the action finished within its time.</param>
internal sealed record SweepOutcome(bool Changed, string Change, bool Completed);

/// <summary>
/// Drives the real IDE through every command a person can reach, on Avalonia's headless platform with Skia: opens the
/// sample solution, runs an action (a menu command, a tap, a key press), waits for it, and records what went wrong
/// (an exception on the UI thread or in a task, an error written to the Output, a UI thread blocked long enough to be a
/// freeze, a dialog that cannot be closed, an action that changed nothing) and what each new state of the screen
/// breaks of the accessibility and layout rules (<see cref="AccessibilityAudit"/>). Everything goes to
/// <c>sweep/&lt;name&gt;.tsv</c> beside the screenshots, so CI publishes it.
/// </summary>
internal sealed class SweepHarness : IDisposable
{
    /// <summary>How long the UI thread may be blocked by one action or one job before it counts as a freeze.</summary>
    public static readonly TimeSpan FreezeLimit = TimeSpan.FromSeconds(2.5);

    /// <summary>How long an action may run before the sweep goes on without it.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);

    private static readonly PropertyInfo[] WorkspaceProperties = [.. typeof(WorkspaceViewModel)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && IsSimple(p.PropertyType))];

    private readonly TempDirectory _temp = new();
    private readonly string _name;
    private readonly List<SweepProblem> _problems = [];
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
    private readonly HashSet<Exception> _exceptions = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, string> _originals = new(StringComparer.Ordinal);
    private int _shots;
    private readonly List<string> _log = [];
    private WorkspaceViewModel? _watched;
    private string _where = "opening the sample";

    /// <summary>Creates the IDE, with its services in a folder of its own.</summary>
    /// <param name="name">The report's name, such as "menus".</param>
    /// <param name="activity">Whether the app is in the background, for a test that sends it there; Avalonia's lifetime when <see langword="null"/>.</param>
    public SweepHarness(string name, IAppActivity? activity = null)
    {
        _name = name;
        Services = AppServices.Create(new AppPaths(_temp.Path), services =>
        {
            if (activity is not null)
            {
                services.AddSingleton(activity);
            }
        });
        Main = Services.GetRequiredService<MainViewModel>();
        Dispatcher.UIThread.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    /// <summary>The IDE's services.</summary>
    public ServiceProvider Services { get; }

    /// <summary>The IDE.</summary>
    public MainViewModel Main { get; }

    /// <summary>The window it shows in, once shown.</summary>
    public MainWindow Window { get; private set; } = null!;

    /// <summary>The open workspace.</summary>
    public WorkspaceViewModel Workspace => Main.Workspace ?? throw new InvalidOperationException("No workspace is open.");

    /// <summary>The path of the sample's Program.cs, once opened.</summary>
    public string ProgramPath { get; private set; } = string.Empty;

    /// <summary>The sample solution's path, once opened.</summary>
    public string SolutionPath { get; private set; } = string.Empty;

    /// <summary>What was found.</summary>
    public IReadOnlyList<SweepProblem> Problems => _problems;

    /// <summary>How many actions ran.</summary>
    public int Actions { get; private set; }

    /// <summary>Whether the dark theme shows.</summary>
    public static bool IsDark => Application.Current?.ActualThemeVariant != ThemeVariant.Light;

    /// <summary>The folder the reports go to: <c>sweep</c> in the screenshots' folder.</summary>
    public static string ReportFolder => Directory.CreateDirectory(Path.Combine(
        Environment.GetEnvironmentVariable("TABSTUDIO_SCREENSHOTS") ?? Path.Combine(AppContext.BaseDirectory, "screenshots"), "sweep")).FullName;

    /// <summary>Shows the IDE in a window of a size, as a tablet's screen or a window in split screen.</summary>
    public MainWindow Show(double width, double height)
    {
        Window = new MainWindow { DataContext = Main, Width = width, Height = height, MinWidth = 0, MinHeight = 0 };
        Window.Show();
        Pump();
        return Window;
    }

    /// <summary>Opens the sample solution, with Program.cs in the editor, and waits until its commands are registered.</summary>
    public async Task<WorkspaceViewModel> OpenSampleAsync()
    {
        _where = "opening the sample";
        SolutionPath = await Services.GetRequiredService<SampleLibrary>().EnsureExtractedAsync();
        await Main.OpenWorkspaceAsync(SolutionPath);
        var workspace = Workspace;
        ProgramPath = Directory.EnumerateFiles(Path.GetDirectoryName(SolutionPath)!, "Program.cs", SearchOption.AllDirectories).First();
        if (workspace.ActiveDocument?.FilePath != ProgramPath)
        {
            await workspace.OpenDocumentAsync(ProgramPath);
        }

        foreach (var file in Directory.EnumerateFiles(Path.GetDirectoryName(SolutionPath)!, "*.cs", SearchOption.AllDirectories))
        {
            _originals[file] = await File.ReadAllTextAsync(file);
        }

        var document = workspace.ActiveDocument!;
        await document.AnalyzeNowAsync();
        await UiTestHelpers.WaitUntilAsync(() => document.Classifications.Count > 0 && workspace.Commands.Commands.Count > 0, because: "syntax colors and the commands");
        Watch(workspace);
        return workspace;
    }

    /// <summary>Gives the window another size, as rotating the tablet or resizing a split screen does.</summary>
    public void Resize(double width, double height)
    {
        Window.Width = width;
        Window.Height = height;
        Pump();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Pump();
    }

    /// <summary>Runs the UI thread's pending work, recording what it throws.</summary>
    public void Pump()
    {
        var clock = Stopwatch.StartNew();
        try
        {
            Dispatcher.UIThread.RunJobs();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            RecordException(ex);
        }

        if (clock.Elapsed > FreezeLimit)
        {
            Add("freeze", _where, string.Create(CultureInfo.InvariantCulture, $"UI work blocked the UI thread for {clock.Elapsed.TotalSeconds:0.0} s."));
        }
    }

    /// <summary>Lets the IDE settle: runs its work for a while and renders a frame.</summary>
    public async Task SettleAsync(int milliseconds = 120)
    {
        var clock = Stopwatch.StartNew();
        do
        {
            Pump();
            await Task.Delay(20);
        }
        while (clock.ElapsedMilliseconds < milliseconds);
        try
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            RecordException(ex);
        }

        Pump();
    }

    /// <summary>
    /// Runs an action and records what went wrong: what it threw, at once or from its task, what it wrote to the
    /// Output as an error, how long it blocked the UI thread, and whether it changed anything.
    /// </summary>
    /// <param name="where">What the action is, for the report.</param>
    /// <param name="action">The action; it returns the task of an asynchronous command, or <see langword="null"/>.</param>
    /// <param name="timeout">How long to wait for its task.</param>
    public async Task<SweepOutcome> RunAsync(string where, Func<Task?> action, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        _where = where;
        Actions++;
        Note("run\t" + where);
        var (before, moving) = await StableSnapshotAsync();
        Task? task = null;
        var clock = Stopwatch.StartNew();
        try
        {
            task = action();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            RecordException(ex);
        }

        if (clock.Elapsed > FreezeLimit)
        {
            Add("freeze", where, string.Create(CultureInfo.InvariantCulture, $"It blocked the UI thread for {clock.Elapsed.TotalSeconds:0.0} s."));
        }

        var limit = timeout ?? DefaultTimeout;
        var waited = Stopwatch.StartNew();
        while (task is { IsCompleted: false } && waited.Elapsed < limit)
        {
            Pump();
            await Task.Delay(20);
        }

        var completed = task is null || task.IsCompleted;
        if (!completed)
        {
            Note(string.Create(CultureInfo.InvariantCulture, $"slow\t{where}\tstill running after {limit.TotalSeconds:0} s"));
        }
        else if (task is { IsFaulted: true, Exception: { } failure })
        {
            foreach (var inner in failure.InnerExceptions)
            {
                RecordException(inner);
            }
        }

        // A task the action left faulted and unobserved is reported only when it is collected: collect now, so the
        // report names this action rather than a later one, or none at all.
        CollectUnobserved();
        await SettleAsync();
        var after = Snapshot();
        var changes = Diff(before, after, moving);
        var change = string.Join(", ", changes.Take(4));
        Note($"done\t{where}\t{(changes.Count > 0 ? change : "no change")}");
        return new SweepOutcome(changes.Count > 0, change, completed);
    }

    /// <summary>Records a problem, once for each kind, place and text.</summary>
    public void Add(string kind, string where, string what)
    {
        if (_seen.Add(kind + "\t" + where + "\t" + what))
        {
            _problems.Add(new SweepProblem(kind, where, what));
            Note($"problem\t{kind}\t{where}\t{what}");
        }
    }

    /// <summary>Notes a line in the run's log, which goes beside the report.</summary>
    public void Note(string line)
    {
        _log.Add(line.ReplaceLineEndings(" "));

        // Written as the sweep goes, so a run that hangs still says where.
        try
        {
            File.AppendAllText(Path.Combine(ReportFolder, _name + (IsDark ? "" : ".light") + ".log"), line.ReplaceLineEndings(" ") + "\n");
        }
        catch (IOException)
        {
            // The log is for people reading CI; the report has the problems.
        }
    }

    /// <summary>
    /// Checks the screen as it is now against the accessibility and layout rules: touch targets, accessible names,
    /// contrast, clipped text, controls off the screen and controls lying over each other, in the window and in every
    /// popup it shows. Each finding is recorded once, with the first state that showed it.
    /// </summary>
    public void Audit(string where)
    {
        IReadOnlyList<AccessibilityFinding> found;
        try
        {
            found = AccessibilityAudit.Everything(Window, IsDark, overlaps: OpenOverlay() is null && !HasLightDismissPopup() && !IsDrawerOpen());
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            RecordException(ex);
            return;
        }

        string? shot = null;
        foreach (var finding in found)
        {
            if (_seen.Add("a11y\t" + finding.Kind + "\t" + finding.Path))
            {
                shot ??= Shoot();
                _problems.Add(new SweepProblem(finding.Kind, where, $"{finding.Path}: {finding.Text} [{shot}]"));
                Note($"problem\t{finding.Kind}\t{where}\t{finding.Path}: {finding.Text} [{shot}]");
            }
        }
    }

    /// <summary>Saves what the window shows now, for a finding, as <c>sweep/shots/&lt;name&gt;-&lt;n&gt;.png</c>.</summary>
    public string Shoot()
    {
        var name = string.Create(CultureInfo.InvariantCulture, $"{_name}-{(IsDark ? "dark" : "light")}-{++_shots:000}.png");
        try
        {
            var folder = Directory.CreateDirectory(Path.Combine(ReportFolder, "shots")).FullName;
            UiTestHelpers.SaveFrame(Window, Path.Combine(folder, name));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return "no picture: " + ex.Message;
        }

        return name;
    }

    /// <summary>
    /// Whether the side bar is open as a drawer over the editor, as in portrait: it lies over the editor and the bottom
    /// panel by design, and a tap beside it closes it.
    /// </summary>
    public bool IsDrawerOpen() =>
        Main.Workspace is { IsSideBarVisible: true } && Window.GetVisualDescendants().OfType<WorkspaceView>().FirstOrDefault() is { IsSideBarDocked: false };

    /// <summary>Whether a flyout, context menu or other light-dismiss popup is open: the first tap only closes it.</summary>
    public bool HasLightDismissPopup() =>
        Window.GetVisualDescendants().OfType<LightDismissOverlayLayer>().Any(layer => layer.IsVisible);

    /// <summary>
    /// Closes whatever an action opened over the IDE, as a person would with the keyboard (Escape) and by touch (the
    /// dialog's Cancel or Close, or a tap outside it), and records a dialog that one of them cannot close.
    /// </summary>
    public async Task DismissAsync(string where)
    {
        if (HasLightDismissPopup())
        {
            // A tap outside a flyout or a context menu closes it, as on a tablet.
            Window.MouseDown(new Point(Window.Bounds.Width - 2, Window.Bounds.Height - 2), MouseButton.Left);
            Window.MouseUp(new Point(Window.Bounds.Width - 2, Window.Bounds.Height - 2), MouseButton.Left);
            await SettleAsync(60);
            if (HasLightDismissPopup())
            {
                PressKey(Key.Escape);
                await SettleAsync(60);
                if (HasLightDismissPopup())
                {
                    Add("dead-end", where, "Neither a tap outside its flyout or menu nor Escape closes it.");
                }
            }
        }

        for (var round = 0; round < 4; round++)
        {
            var overlay = OpenOverlay();
            if (overlay is null)
            {
                return;
            }

            var (name, close) = overlay.Value;
            PressKey(Key.Escape);
            await SettleAsync(80);
            // A menu or a box inside the dialog may take the first Escape, as it does on every platform, and a
            // dialog an unfinished command opened again (Share session, once its host starts) closes on the next.
            for (var again = 0; again < 3 && OpenOverlay() is { } first && first.Name == name; again++)
            {
                await SettleAsync(400);
                PressKey(Key.Escape);
                await SettleAsync(150);
            }

            if (OpenOverlay() is { } still && still.Name == name)
            {
                Add("dead-end", where, $"Escape does not close {name}: a keyboard user is left in it.");
                close();
                await SettleAsync(80);
            }
        }

        if (OpenOverlay() is { } stuck)
        {
            Add("dead-end", where, $"{stuck.Name} cannot be closed.");
            stuck.Close();
            await SettleAsync(80);
        }
    }

    /// <summary>The dialog or popup that shows over the IDE now, by name, with a way to close it, or none.</summary>
    public (string Name, Action Close)? OpenOverlay()
    {
        if (Main.Workspace is { } workspace)
        {
            if (workspace.Prompt.IsOpen)
            {
                var prompt = workspace.Prompt;
                return ($"the \"{prompt.Title}\" prompt", () => prompt.CancelCommand.Execute(null));
            }

            if (workspace.Search.IsOpen)
            {
                return ("the search", () => workspace.Search.CloseCommand.Execute(null));
            }
        }

        foreach (var popup in Window.GetVisualDescendants().OfType<Popup>())
        {
            if (popup.IsOpen && popup.Child is { IsVisible: true } && !IsPassive(popup))
            {
                return ($"the popup {Describe(popup)}", () => popup.IsOpen = false);
            }
        }

        // A dialog over the IDE dims it with the scrim brush, whatever its panel is called.
        var scrim = Window.TryFindResource("Brush.Scrim", Window.ActualThemeVariant, out var found) ? found : null;
        foreach (var control in Window.GetVisualDescendants().OfType<Control>())
        {
            var dims = scrim is not null && control switch
            {
                Panel panel => ReferenceEquals(panel.Background, scrim),
                Border border => ReferenceEquals(border.Background, scrim),
                _ => false,
            };
            if ((dims || control.Name is { } name && (name.EndsWith("Scrim", StringComparison.Ordinal) || name == "OperationsOverlay"))
                && control.IsEffectivelyVisible && control.Bounds.Width > 1)
            {
                var shown = control;
                return ($"the dialog {Describe(control)}", () => CloseScrim(shown));
            }
        }

        return null;
    }

    /// <summary>Presses and releases a key, with modifiers, on the window, as a hardware keyboard does.</summary>
    public void PressKey(Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        var raw = ToRaw(modifiers);
        try
        {
            Window.KeyPress(key, raw, PhysicalKey.None, null);
            Window.KeyRelease(key, raw, PhysicalKey.None, null);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            RecordException(ex);
        }

        Pump();
    }

    /// <summary>Raw modifiers for key modifiers.</summary>
    public static RawInputModifiers ToRaw(KeyModifiers modifiers)
    {
        var raw = RawInputModifiers.None;
        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            raw |= RawInputModifiers.Control;
        }

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            raw |= RawInputModifiers.Shift;
        }

        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            raw |= RawInputModifiers.Alt;
        }

        if (modifiers.HasFlag(KeyModifiers.Meta))
        {
            raw |= RawInputModifiers.Meta;
        }

        return raw;
    }

    /// <summary>
    /// Puts the IDE back as the sweep expects it between actions: nothing over it, the sample open with Program.cs in
    /// the editor, Solution Explorer and the Output showing, no program running and no debug session.
    /// </summary>
    public async Task ResetAsync()
    {
        _where = "putting the IDE back";
        await DismissAsync("putting the IDE back");
        if (Main.Workspace is null)
        {
            await Main.OpenWorkspaceAsync(SolutionPath);
            await UiTestHelpers.WaitUntilAsync(() => Main.Workspace?.Commands.Commands.Count > 0, because: "the reopened solution's commands");
        }

        var workspace = Workspace;
        Watch(workspace);
        if (workspace.IsDebugging || workspace.IsRunning)
        {
            if (workspace.StopDebuggingCommand.CanExecute(null))
            {
                workspace.StopDebuggingCommand.Execute(null);
            }

            var clock = Stopwatch.StartNew();
            while ((workspace.IsDebugging || workspace.IsRunning) && clock.Elapsed < TimeSpan.FromSeconds(20))
            {
                Pump();
                await Task.Delay(25);
            }
        }

        if (workspace.ActiveDocument?.FilePath != ProgramPath)
        {
            await workspace.OpenDocumentAsync(ProgramPath);
        }

        // The sample as it was: commands edit, comment, cut and format it, and the next one must find it whole.
        foreach (var document in workspace.Documents.ToList())
        {
            if (_originals.TryGetValue(document.FilePath, out var original) && document.Document.Text != original)
            {
                workspace.ActiveDocument = document;
                document.Document.Text = original;
                await workspace.SaveCommand.ExecuteAsync(null);
            }
        }

        foreach (var (file, original) in _originals)
        {
            if (!workspace.Documents.Any(d => d.FilePath == file) && File.Exists(file) && await File.ReadAllTextAsync(file) != original)
            {
                await File.WriteAllTextAsync(file, original);
            }
        }

        if (workspace.ActiveDocument?.FilePath != ProgramPath)
        {
            await workspace.OpenDocumentAsync(ProgramPath);
        }

        if (workspace.SelectedSideBarPanel != SideBarPanel.SolutionExplorer)
        {
            workspace.SelectSideBarPanelCommand.Execute(SideBarPanel.SolutionExplorer);
        }

        // Docked beside the editor on a landscape tablet; folded away in portrait, where it is a drawer over it.
        var docked = Window.GetVisualDescendants().OfType<WorkspaceView>().FirstOrDefault()?.IsSideBarDocked ?? true;
        workspace.IsSideBarVisible = docked;

        if (!workspace.IsBottomPanelVisible)
        {
            workspace.IsBottomPanelVisible = true;
        }

        workspace.SelectPanelCommand.Execute(BottomPanel.Output);
        await SettleAsync(60);
    }

    /// <summary>Writes the report, and the run's log, beside the screenshots.</summary>
    public void Write()
    {
        var theme = IsDark ? "dark" : "light";
        var report = new StringBuilder();
        report.Append(CultureInfo.InvariantCulture, $"# {_name} ({theme}): {Actions} actions, {_problems.Count} problems\n");
        foreach (var problem in _problems.OrderBy(p => p.Kind, StringComparer.Ordinal))
        {
            report.Append(problem.Kind).Append('\t').Append(problem.Where.ReplaceLineEndings(" ")).Append('\t').Append(problem.What.ReplaceLineEndings(" ")).Append('\n');
        }

        File.WriteAllText(Path.Combine(ReportFolder, $"{_name}.{theme}.tsv"), report.ToString());
    }

    /// <summary>
    /// Fails with every problem found that is not on the list of those the sweep knows of and leaves to others, with
    /// the reason for each (<see cref="SweepAllowances"/>).
    /// </summary>
    public void AssertClean()
    {
        CollectUnobserved();
        Pump();
        Write();
        var open = _problems.Where(p => !SweepAllowances.IsAllowed(p)).ToList();
        Assert.True(open.Count == 0, $"The sweep \"{_name}\" found {open.Count} problems in {Actions} actions:\n"
            + string.Join("\n", open.Take(120).Select(p => $"{p.Kind}: {p.Where}: {p.What}")));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispatcher.UIThread.UnhandledException -= OnUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        if (_watched is not null)
        {
            _watched.Output.Written -= OnOutputWritten;
        }

        try
        {
            Window?.Close();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The window goes with the test.
        }

        Main.Workspace?.Dispose();
        Services.Dispose();
        _temp.Dispose();
    }

    /// <summary>Records what an error the IDE did not handle was, once.</summary>
    public void RecordException(Exception exception)
    {
        if (!_exceptions.Add(exception))
        {
            return;
        }

        Add("exception", _where, Describe(exception));
    }

    private static string Describe(Exception exception)
    {
        var frame = exception.StackTrace?.Split('\n').Select(l => l.Trim())
            .FirstOrDefault(l => l.Contains("TabStudio.", StringComparison.Ordinal)) ?? exception.StackTrace?.Split('\n').FirstOrDefault()?.Trim();
        return $"{exception.GetType().Name}: {exception.Message} ({frame})";
    }

    private static string Describe(Control control)
    {
        var named = control.GetVisualAncestors().Prepend(control).OfType<Control>().FirstOrDefault(c => !string.IsNullOrEmpty(c.Name));
        var view = control.GetVisualAncestors().OfType<UserControl>().FirstOrDefault();
        return $"{named?.Name ?? control.GetType().Name} in {view?.GetType().Name ?? "the window"}";
    }

    /// <summary>A popup that only shows something, such as a tooltip or the editor's adornments, rather than a dialog.</summary>
    private static bool IsPassive(Popup popup) =>
        popup.Child is ToolTip || (!popup.IsLightDismissEnabled && popup.Child?.GetVisualDescendants().OfType<Button>().Any() != true);

    /// <summary>Closes a dialog by touch: its Cancel or Close button, or a tap on the scrim around it.</summary>
    private void CloseScrim(Control scrim)
    {
        var button = scrim.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.IsEffectivelyVisible && b.IsEffectivelyEnabled
            && (b.Content as string ?? ToolTip.GetTip(b) as string ?? Avalonia.Automation.AutomationProperties.GetName(b)) is { } text
            && (text.StartsWith("Cancel", StringComparison.OrdinalIgnoreCase) || text.StartsWith("Close", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("Done", StringComparison.OrdinalIgnoreCase) || text.StartsWith("Not now", StringComparison.OrdinalIgnoreCase)));
        if (button?.Command is { } command && command.CanExecute(button.CommandParameter))
        {
            command.Execute(button.CommandParameter);
            return;
        }

        if (button is not null)
        {
            button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            return;
        }

        Add("dead-end", _where, $"The dialog {Describe(scrim)} has no Cancel or Close button for a finger.");
        var corner = scrim.TranslatePoint(new Point(4, 4), Window);
        if (corner is { } point)
        {
            Window.MouseDown(point, MouseButton.Left);
            Window.MouseUp(point, MouseButton.Left);
        }

        scrim.IsVisible = false;
    }

    private void Watch(WorkspaceViewModel workspace)
    {
        if (ReferenceEquals(_watched, workspace))
        {
            return;
        }

        if (_watched is not null)
        {
            _watched.Output.Written -= OnOutputWritten;
        }

        _watched = workspace;
        workspace.Output.Written += OnOutputWritten;
    }

    private void OnOutputWritten(object? sender, OutputWrittenEventArgs e)
    {
        if (e.Kind is OutputKind.Failure or OutputKind.Error && !string.IsNullOrWhiteSpace(e.Text))
        {
            Add("error-log", _where, e.Text.Trim());
        }
    }

    private void OnUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        RecordException(e.Exception);
        e.Handled = true;
    }

    /// <summary>Collects garbage and runs finalizers, which raises the unobserved exceptions of faulted tasks.</summary>
    public static void CollectUnobserved()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        // Raised on the finalizer thread; recorded on the UI thread, with the action that ran when it was raised.
        var exception = e.Exception;
        var where = _where;
        e.SetObserved();
        Dispatcher.UIThread.Post(() =>
        {
            foreach (var inner in exception.InnerExceptions)
            {
                if (_exceptions.Add(inner))
                {
                    Add("unobserved", "near " + where, Describe(inner));
                    Note("stack\t" + inner.ToString().ReplaceLineEndings(" | "));
                }
            }
        });
    }

    /// <summary>
    /// Two snapshots a moment apart, and what changed between them on its own, such as a spinner or a clock, which is
    /// left out of this action's comparison.
    /// </summary>
    private async Task<(Dictionary<string, string> Snapshot, HashSet<string> Moving)> StableSnapshotAsync()
    {
        var first = Snapshot();
        await SettleAsync(60);
        var second = Snapshot();
        var moving = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in first.Keys.Union(second.Keys))
        {
            if (!first.TryGetValue(key, out var a) || !second.TryGetValue(key, out var b) || a != b)
            {
                moving.Add(key);
            }
        }

        return (second, moving);
    }

    private static List<string> Diff(Dictionary<string, string> before, Dictionary<string, string> after, HashSet<string> moving)
    {
        var changes = new List<string>();
        foreach (var (key, value) in after)
        {
            if (moving.Contains(key))
            {
                continue;
            }

            if (!before.TryGetValue(key, out var old))
            {
                changes.Add("+" + key);
            }
            else if (old != value)
            {
                changes.Add(key + "=" + Short(value));
            }
        }

        foreach (var key in before.Keys)
        {
            if (!after.ContainsKey(key) && !moving.Contains(key))
            {
                changes.Add("-" + key);
            }
        }

        return changes;
    }

    private static string Short(string text) => text.Length <= 40 ? text : text[..39] + "…";

    /// <summary>
    /// What a person sees and what the IDE's state is now: the workspace's simple properties, the open dialogs, the
    /// editor's text, caret and selection, and every named control, view and text that shows, popups included.
    /// </summary>
    private Dictionary<string, string> Snapshot()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["page"] = Main.CurrentPage?.GetType().Name ?? "none",
        };
        if (Main.Workspace is { } workspace)
        {
            foreach (var property in WorkspaceProperties)
            {
                try
                {
                    map["vm." + property.Name] = Convert.ToString(property.GetValue(workspace), CultureInfo.InvariantCulture) ?? "null";
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    // A property that throws now is not part of the state.
                }
            }

            map["vm.Documents"] = string.Join("|", workspace.Documents.Select(d => d.FilePath));
            map["vm.Output"] = workspace.Output.Document.TextLength.ToString(CultureInfo.InvariantCulture);
            map["vm.Prompt"] = workspace.Prompt.IsOpen ? workspace.Prompt.Title : "closed";
            map["vm.Search"] = workspace.Search.IsOpen.ToString();
            map["vm.Breakpoints"] = workspace.Breakpoints.All.Count.ToString(CultureInfo.InvariantCulture);
        }

        if (Window is { } window)
        {
            map["focus"] = window.FocusManager?.GetFocusedElement()?.GetType().Name ?? "none";
            foreach (var editor in window.GetVisualDescendants().OfType<CodeEditorView>().Where(e => e.IsEffectivelyVisible))
            {
                var text = editor.Editor;
                map["editor"] = string.Create(CultureInfo.InvariantCulture,
                    $"{text.Document.TextLength}:{text.Document.Text.GetHashCode(StringComparison.Ordinal)}:{text.CaretOffset}:{text.SelectionStart}+{text.SelectionLength}:{text.TextArea.TextView.ScrollOffset.Y:0}");
                break;
            }

            Walk(window, "w", map, 0);
        }

        return map;
    }

    private static void Walk(Visual visual, string path, Dictionary<string, string> map, int depth)
    {
        if (!visual.IsVisible || depth > 80)
        {
            return;
        }

        var here = path;
        if (visual is Control control && (!string.IsNullOrEmpty(control.Name) || control is UserControl))
        {
            here = path + "/" + (string.IsNullOrEmpty(control.Name) ? control.GetType().Name : control.Name);
            map[here] = control.IsEffectivelyEnabled ? "1" : "0";
        }

        switch (visual)
        {
            case TextBlock { Text: { Length: > 0 } text }:
                map[here + "/text:" + text.GetHashCode(StringComparison.Ordinal).ToString(CultureInfo.InvariantCulture)] = "1";
                break;
            case ToggleButton toggle:
                map[here + "/toggle" + toggle.GetHashCode().ToString(CultureInfo.InvariantCulture)] = toggle.IsChecked?.ToString() ?? "null";
                break;
            case SelectingItemsControl list:
                map[here + "/list" + list.GetHashCode().ToString(CultureInfo.InvariantCulture)] = list.ItemCount.ToString(CultureInfo.InvariantCulture) + ":" + list.SelectedIndex.ToString(CultureInfo.InvariantCulture);
                break;
            case Popup { IsOpen: true, Child: { } child } popup:
                var name = "popup:" + (string.IsNullOrEmpty(popup.Name) ? child.GetType().Name : popup.Name);
                map[here + "/" + name] = "open";
                Walk(child, here + "/" + name, map, depth + 1);
                break;
        }

        foreach (var child in visual.GetVisualChildren())
        {
            Walk(child, here, map, depth + 1);
        }
    }

    private static bool IsSimple(Type type)
    {
        var inner = Nullable.GetUnderlyingType(type) ?? type;
        return inner == typeof(bool) || inner == typeof(int) || inner == typeof(string) || inner == typeof(double) || inner.IsEnum;
    }
}

/// <summary>The app's activity as a test sets it: in the foreground, or in the background as Android's onStop puts it.</summary>
internal sealed class SweepAppActivity : IAppActivity
{
    /// <inheritdoc />
    public bool IsInBackground { get; private set; }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <summary>Sends the app to the background or brings it back.</summary>
    public void Set(bool inBackground)
    {
        if (IsInBackground != inBackground)
        {
            IsInBackground = inBackground;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
