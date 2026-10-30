using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.ViewModels;
using TabStudio.App.ViewModels.DesktopMode;
using TabStudio.App.Views;
using TabStudio.App.Views.DesktopMode;
using TabStudio.Testing;

namespace TabStudio.App.Tests.DesktopMode;

/// <summary>
/// Desktop mode in the real app, with a fake monitor that shows what is presented on it in a second window: the IDE
/// moves there as it is and back with nothing lost, the tablet's keyboard types on the big screen, and the tablet
/// becomes a companion with Run and debugger buttons.
/// </summary>
public sealed class DesktopModeTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly FakeExternalDisplay _display = new();
    private readonly ServiceProvider _services;
    private readonly MainViewModel _main;

    public DesktopModeTests()
    {
        _services = AppServices.Create(new AppPaths(_temp.Path), services => services.AddSingleton<IExternalDisplayService>(_display));
        _main = _services.GetRequiredService<MainViewModel>();
    }

    private DesktopModeViewModel Desktop => _main.DesktopMode!;

    [AvaloniaFact]
    public async Task The_IDE_moves_to_the_display_and_back_with_its_editors_caret_and_scroll_position()
    {
        var tablet = Show(1280, 800);
        var workspace = await OpenSampleAsync();
        var document = workspace.ActiveDocument!;
        var ide = tablet.GetVisualDescendants().OfType<WorkspaceView>().Single();
        var editor = ide.GetVisualDescendants().OfType<CodeEditorView>().Single(view => ReferenceEquals(view.DataContext, document));
        document.NavigateTo(33, 1);
        await UiTestHelpers.WaitUntilAsync(() => document.CaretLine == 33 && editor.Editor.VerticalOffset > 0, because: "the caret on line 33, scrolled into view");
        var scroll = editor.Editor.VerticalOffset;
        var touchBar = ide.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "FoldTouchBarButton");
        Assert.True(touchBar.IsEffectivelyVisible);

        _display.Connect();
        await UiTestHelpers.WaitUntilAsync(() => true);
        Assert.True(Desktop.IsOfferVisible);
        var offer = tablet.GetVisualDescendants().OfType<DesktopModeOfferView>().Single();
        Assert.True(offer.IsEffectivelyVisible);

        // It keeps clear of the bottom panel, where the Error List and the program's output are.
        var card = offer.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "OfferCard");
        var dock = ide.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "BottomDock");
        var cardBottom = card.TranslatePoint(new Point(0, card.Bounds.Height), tablet)!.Value.Y;
        var dockTop = dock.TranslatePoint(default, tablet)!.Value.Y;
        Assert.True(cardBottom <= dockTop, $"The offer ends at {cardBottom}, over the bottom panel at {dockTop}.");
        UiTestHelpers.SaveScreenshot(tablet, "desktop-01-offer");

        Desktop.EnterCommand.Execute(null);
        var monitor = await WaitForMonitorAsync();

        // The same views, on the display.
        Assert.Same(ide, monitor.GetVisualDescendants().OfType<WorkspaceView>().Single());
        Assert.Same(editor, monitor.GetVisualDescendants().OfType<CodeEditorView>().Single(view => ReferenceEquals(view.DataContext, document)));
        Assert.Empty(tablet.GetVisualDescendants().OfType<WorkspaceView>());
        Assert.True(tablet.GetVisualDescendants().OfType<DesktopCompanionView>().Single().IsEffectivelyVisible);
        Assert.True(workspace.IsSideBarVisible);
        Assert.Equal(33, document.CaretLine);
        Assert.Equal(1, _display.PresentedScale);
        Assert.Contains(monitor.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Desktop mode" && t.IsEffectivelyVisible);

        // A keyboard and a mouse are used there, and the tablet has the big buttons: no floating touch buttons.
        Assert.False(touchBar.IsEffectivelyVisible);
        UiTestHelpers.SaveScreenshot(monitor, "desktop-02-big-screen");
        UiTestHelpers.SaveScreenshot(tablet, "desktop-03-tablet-companion");

        // A keyboard connected to the tablet types into the editor on the display, and its shortcuts work there.
        await UiTestHelpers.WaitUntilAsync(() => editor.Editor.TextArea.IsKeyboardFocusWithin, because: "the editor on the display to have the keyboard");
        tablet.KeyTextInput("/* typed */");
        Assert.Contains("/* typed */", document.Document.Text, StringComparison.Ordinal);
        tablet.KeyPress(Key.F9, RawInputModifiers.None, PhysicalKey.F9, null);
        tablet.KeyRelease(Key.F9, RawInputModifiers.None, PhysicalKey.F9, null);
        Assert.NotNull(workspace.Breakpoints.Find(document.FilePath, 33));

        // Back on the tablet: the same views, caret and scroll position.
        var caret = document.CaretOffset;
        Desktop.ExitCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => _display.Window is null && tablet.GetVisualDescendants().OfType<WorkspaceView>().Any(), because: "the IDE back on the tablet");
        Assert.Same(ide, tablet.GetVisualDescendants().OfType<WorkspaceView>().Single());
        Assert.Same(editor, tablet.GetVisualDescendants().OfType<CodeEditorView>().Single(view => ReferenceEquals(view.DataContext, document)));
        Assert.Equal(caret, document.CaretOffset);
        Assert.Equal(scroll, editor.Editor.VerticalOffset);
        Assert.False(tablet.GetVisualDescendants().OfType<DesktopCompanionView>().Single().IsEffectivelyVisible);
        Assert.True(touchBar.IsEffectivelyVisible);
        UiTestHelpers.SaveScreenshot(tablet, "desktop-05-back-on-the-tablet");
    }

    [AvaloniaFact]
    public async Task Without_a_keyboard_the_tablet_s_on_screen_keyboard_types_into_the_editor_on_the_big_screen()
    {
        var tablet = Show(1280, 800);
        var workspace = await OpenSampleAsync();
        var document = workspace.ActiveDocument!;
        _display.SetKeyboard(false);
        _display.Connect();
        Desktop.EnterCommand.Execute(null);
        var monitor = await WaitForMonitorAsync();
        var editor = monitor.GetVisualDescendants().OfType<CodeEditorView>().Single(view => ReferenceEquals(view.DataContext, document));
        await UiTestHelpers.WaitUntilAsync(() => editor.Editor.TextArea.IsKeyboardFocusWithin, because: "the editor on the display to have the keyboard");
        await MoveCaretAsync(document, 0);
        var bar = tablet.GetVisualDescendants().OfType<DesktopKeyboardView>().Single();
        Assert.True(bar.IsEffectivelyVisible);

        // A tap on the field: the on-screen keyboard types there, and the text goes to the editor's caret.
        var field = bar.GetVisualDescendants().OfType<TextBox>().Single();
        field.Focus();
        Assert.Same(editor.Editor.TextArea, bar.Target);
        Assert.Equal("Type into Program.cs on the big screen", field.Watermark);
        field.Text = "// typed";
        field.Text = "// typed on a tablet";
        field.Text = "// Typed on a tablet";
        Assert.Equal("// Typed on a tablet// Welcome to TabStudio!", Line(document, 1));
        UiTestHelpers.SaveScreenshot(tablet, "desktop-11-tablet-typing");
        UiTestHelpers.SaveScreenshot(monitor, "desktop-11-big-screen-typing");

        // Enter goes to the editor, and the field starts again.
        tablet.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        tablet.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Assert.Equal("// Typed on a tablet", Line(document, 1));
        Assert.Equal(string.Empty, field.Text);

        // The bar's keys: Tab indents; Backspace on the empty field deletes on the big screen.
        Button(bar, "Tab").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        var indented = Line(document, 2);
        Assert.True(indented.Length > "// Welcome to TabStudio!".Length && indented.TrimStart() == "// Welcome to TabStudio!", indented);
        tablet.KeyPress(Key.Back, RawInputModifiers.None, PhysicalKey.Backspace, null);
        tablet.KeyRelease(Key.Back, RawInputModifiers.None, PhysicalKey.Backspace, null);
        Assert.True(Line(document, 2).Length < indented.Length);

        // A keyboard connected: its keys type on the big screen, and the bar goes.
        _display.SetKeyboard(true);
        await UiTestHelpers.WaitUntilAsync(() => true);
        Assert.False(bar.IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public async Task The_tablet_s_mouse_has_a_pointer_of_its_own_on_the_big_screen()
    {
        var tablet = Show(1280, 800);
        var workspace = await OpenSampleAsync();
        _display.Connect(1920, 1080, 1.5);
        Desktop.EnterCommand.Execute(null);
        var monitor = await WaitForMonitorAsync();
        var pointer = monitor.GetVisualDescendants().OfType<DesktopPointerView>().Single();
        Assert.IsType<OverlayLayer>(pointer.GetVisualParent());
        Assert.Equal(int.MaxValue, pointer.ZIndex);
        Assert.False(pointer.IsVisible);

        // The platform has the mouse and moves the pointer over the Output tab, where the IDE gets its events.
        workspace.SelectPanelCommand.Execute(BottomPanel.ErrorList);
        await UiTestHelpers.WaitUntilAsync(() => true);
        var output = monitor.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == "Output" && b.IsEffectivelyVisible);
        var center = output.TranslatePoint(new Point(output.Bounds.Width / 2, output.Bounds.Height / 2), monitor)!.Value;
        _display.Cursor.MoveTo(center.X, center.Y);
        _display.Cursor.Show();
        monitor.MouseMove(center);
        await UiTestHelpers.WaitUntilAsync(() => true);

        Assert.True(pointer.IsVisible);
        Assert.Equal(center.X, Canvas.GetLeft(pointer), 3);
        Assert.Equal(center.Y, Canvas.GetTop(pointer), 3);
        Assert.True(output.IsPointerOver);
        UiTestHelpers.SaveScreenshot(monitor, "desktop-10-big-screen-pointer");

        // The mouse goes back to the tablet, and the pointer goes with it.
        _display.Cursor.Hide();
        Assert.False(pointer.IsVisible);
        Desktop.ExitCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => _display.Window is null, because: "the IDE back on the tablet");
        Assert.False(_display.Cursor.IsVisible);
    }

    [AvaloniaFact]
    public async Task The_companion_s_big_buttons_run_and_step_the_debugger_on_the_big_screen()
    {
        var tablet = Show(1280, 800);
        var workspace = await OpenSampleAsync();
        AnswerInput(workspace, "Ada");
        var program = workspace.ActiveDocument!;
        workspace.Breakpoints.Toggle(program.FilePath, 15);
        _display.Connect();
        Desktop.EnterCommand.Execute(null);
        var monitor = await WaitForMonitorAsync();
        var actions = tablet.GetVisualDescendants().OfType<CompanionActionsView>().Single();

        Tap(actions, "Start Debugging (F5)");
        await DebuggingTests.WaitForPauseAsync(workspace, 15);
        await UiTestHelpers.WaitUntilAsync(() => true);
        Assert.True(Desktop.Companion.ShowsSteps);
        Assert.True(Button(actions, "Step Over (F10)").IsEffectivelyVisible);
        Assert.False(Button(actions, "Build Solution (Ctrl+Shift+B)").IsEffectivelyVisible);
        Assert.False(monitor.GetVisualDescendants().OfType<DebugTouchBarView>().Single().IsEffectivelyVisible);
        UiTestHelpers.SaveScreenshot(tablet, "desktop-04-companion-debugging");
        UiTestHelpers.SaveScreenshot(monitor, "desktop-04-big-screen-debugging");

        Tap(actions, "Step Over (F10)");
        await DebuggingTests.WaitForPauseAsync(workspace, 16);

        Tap(actions, "Stop (Shift+F5)");
        await DebuggingTests.WaitForExitAsync(workspace);
        Assert.True(Button(actions, "Start Debugging (F5)").IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public async Task Unplugging_the_display_brings_the_IDE_back_and_the_menu_and_status_bar_turn_desktop_mode_on()
    {
        var tablet = Show(1280, 800);
        var workspace = await OpenSampleAsync();
        Assert.False(workspace.DesktopMode!.HasDisplay);
        var statusButton = tablet.GetVisualDescendants().OfType<Button>().Single(b => ReferenceEquals(b.Command, Desktop.ToggleCommand));
        Assert.False(statusButton.IsEffectivelyVisible);

        _display.Connect();
        await UiTestHelpers.WaitUntilAsync(() => statusButton.IsEffectivelyVisible, because: "the status bar button");
        statusButton.Command!.Execute(null);
        await WaitForMonitorAsync();

        _display.Disconnect();
        await UiTestHelpers.WaitUntilAsync(() => tablet.GetVisualDescendants().OfType<WorkspaceView>().Any(), because: "the IDE back on the tablet");
        Assert.False(Desktop.IsActive);
        Assert.Contains("was disconnected", workspace.StatusText, StringComparison.Ordinal);

        // View > Desktop Mode is a check mark bound to desktop mode.
        _display.Connect();
        workspace.DesktopMode.IsActive = true;
        await WaitForMonitorAsync();
        _display.EndPresentationBySystem();
        await UiTestHelpers.WaitUntilAsync(() => _display.Window is not null, because: "the IDE shown again after the system ended the presentation");
        Assert.Equal(3, _display.Presentations);

        _display.Refuses = true;
        Desktop.SetScaleCommand.Execute(DesktopScale.Percent150);
        await UiTestHelpers.WaitUntilAsync(() => !Desktop.IsActive, because: "desktop mode to end when the display refuses");
        Assert.Contains(tablet.GetVisualDescendants().OfType<WorkspaceView>(), view => view.IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public async Task The_tablet_s_Toolbox_and_Properties_panel_edit_the_XAML_on_the_big_screen()
    {
        var folder = Path.Combine(_temp.Path, "Forms");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "SignIn.axaml");
        await File.WriteAllTextAsync(path, Form);
        var tablet = Show(1280, 800);
        await _main.OpenWorkspaceAsync(folder);
        var workspace = _main.Workspace!;
        var document = (await workspace.OpenDocumentAsync(path))!;
        _display.Connect();
        Desktop.EnterCommand.Execute(null);
        var monitor = await WaitForMonitorAsync();
        var companion = Desktop.Companion;
        Assert.True(companion.Toolbox.IsAvailable);
        Assert.Equal("Avalonia controls", companion.Toolbox.Title);

        // With the caret in the StackPanel's start tag, a tap on Button puts one last in the panel and selects it.
        await MoveCaretAsync(document, Form.IndexOf("Spacing", StringComparison.Ordinal));
        Assert.Equal("StackPanel", companion.Element.ElementName);
        Item(companion.Toolbox, "Button").Insert.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => companion.Element.ElementName == "Button", because: "the new button's properties");
        Assert.Contains("    <TextBox x:Name=\"Email\" Watermark=\"Email\" />\n    <Button Content=\"Button\" />\n  </StackPanel>", document.Document.Text, StringComparison.Ordinal);
        Assert.StartsWith("Added a Button at line 6", companion.Toolbox.Status, StringComparison.Ordinal);

        // Typed, picked from a list, added and reset: each change is written to the XAML.
        Row(companion.Element, "Content").Value = "Sign in";
        Row(companion.Element, "Content").Commit();
        Row(companion.Element, "HorizontalAlignment").SelectedChoice = "Stretch";
        Row(companion.Element, "Width").Value = "200";
        Row(companion.Element, "Width").CommitOnLeave();
        Assert.Contains("<Button Content=\"Sign in\" HorizontalAlignment=\"Stretch\" Width=\"200\" />", document.Document.Text, StringComparison.Ordinal);
        Row(companion.Element, "Width").ResetCommand.Execute(null);
        companion.Element.NewName = "2Columns";
        companion.Element.NewValue = "2";
        companion.Element.AddPropertyCommand.Execute(null);
        Assert.Contains("is not a property name", companion.Element.Error, StringComparison.Ordinal);
        companion.Element.NewName = "Classes";
        companion.Element.NewValue = "accent";
        companion.Element.AddPropertyCommand.Execute(null);
        Assert.Null(companion.Element.Error);
        Assert.Contains("<Button Content=\"Sign in\" HorizontalAlignment=\"Stretch\" Classes=\"accent\" />", document.Document.Text, StringComparison.Ordinal);
        Assert.Equal(["Content", "HorizontalAlignment", "Classes"], companion.Element.Groups[0].Rows.Select(r => r.Name));

        companion.Element.SearchText = "align";
        await UiTestHelpers.WaitUntilAsync(() => true);
        Assert.All(companion.Element.Groups.Where(g => g.IsVisible).SelectMany(g => g.Rows).Where(r => r.IsVisible), r => Assert.Contains("Align", r.Name, StringComparison.Ordinal));
        UiTestHelpers.SaveScreenshot(tablet, "desktop-07-properties");
        UiTestHelpers.SaveScreenshot(monitor, "desktop-07-big-screen-xaml");
        companion.Element.SearchText = string.Empty;

        // The edits are the editor's on the big screen: Ctrl+Z on the tablet's keyboard takes the last one back.
        await UiTestHelpers.WaitUntilAsync(() => monitor.GetVisualDescendants().OfType<AvaloniaEdit.Editing.TextArea>().Any(t => t.IsKeyboardFocusWithin), because: "the keyboard back on the big screen");
        tablet.KeyPress(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, null);
        tablet.KeyRelease(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, null);
        Assert.DoesNotContain("Classes=", document.Document.Text, StringComparison.Ordinal);

        // A narrow tablet shows the panes one at a time; the Toolbox's search keeps the controls that match.
        tablet.Width = 800;
        tablet.Height = 1280;
        companion.Toolbox.SearchText = "check";
        await UiTestHelpers.WaitUntilAsync(() => true);
        Assert.Equal(["CheckBox"], companion.Toolbox.Categories.Where(c => c.IsVisible).SelectMany(c => c.Items).Select(i => i.Name));
        Assert.True(tablet.GetVisualDescendants().OfType<ToolboxView>().Single().IsEffectivelyVisible);
        Assert.False(tablet.GetVisualDescendants().OfType<XamlPropertiesView>().Single().IsEffectivelyVisible);
        UiTestHelpers.SaveScreenshot(tablet, "desktop-08-toolbox-portrait");
        companion.ShowPropertiesCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => true);
        Assert.True(tablet.GetVisualDescendants().OfType<XamlPropertiesView>().Single().IsEffectivelyVisible);
        UiTestHelpers.SaveScreenshot(tablet, "desktop-09-properties-portrait");
    }

    [AvaloniaFact]
    public async Task The_desktop_self_test_passes_with_a_fake_display()
    {
        var tablet = Show(1280, 800);
        var templates = _services.GetRequiredService<ProjectTemplates>();
        var solution = templates.Create(ProjectTemplates.AvaloniaId, "SelfTestDesktop");
        await _main.OpenWorkspaceAsync(solution);

        // As in the emulator's self test, the designer has built the app and previewed its view before.
        var document = await _main.Workspace!.OpenDocumentAsync(Path.Combine(Path.GetDirectoryName(solution)!, "SelfTestDesktop", "Views", "MainView.axaml"));
        await UiTestHelpers.WaitUntilAsync(
            () => document!.Designer!.Preview?.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == "Hello from Avalonia!") == true,
            TimeSpan.FromMinutes(4),
            "the first preview");
        // A tablet without a keyboard, so the self test types with the companion's typing bar as well.
        _display.SetKeyboard(false);
        _display.Connect(1920, 1080, 1.5, "Overlay #1");
        var input = new HeadlessInput(tablet, _display);

        var result = await SelfTest.RunDesktopModeAsync(_main, (MainView)tablet.Content!, input, Path.Combine(_temp.Path, "Shots"), CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.Contains("the Toolbox put a Button", result.Message, StringComparison.Ordinal);
        Assert.Contains("the companion's typing bar typed on the display", result.Message, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(_temp.Path, "Shots", "desktop-display.png")));
        Assert.True(File.Exists(Path.Combine(_temp.Path, "Shots", "desktop-tablet.png")));
    }

    private const string Form = """
        <UserControl xmlns="https://github.com/avaloniaui"
                     xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
          <StackPanel Margin="24" Spacing="12">
            <TextBlock Text="Sign in" FontSize="22" />
            <TextBox x:Name="Email" Watermark="Email" />
          </StackPanel>
        </UserControl>

        """;

    public void Dispose()
    {
        _display.Disconnect();
        _main.Workspace?.Dispose();
        _services.Dispose();
        _temp.Dispose();
    }

    private static ToolboxItemViewModel Item(ToolboxViewModel toolbox, string name) =>
        toolbox.Categories.SelectMany(c => c.Items).Single(i => i.Name == name);

    private static XamlPropertyRowViewModel Row(XamlPropertiesViewModel properties, string name) =>
        properties.Groups.SelectMany(g => g.Rows).Single(r => r.Name == name);

    private static async Task MoveCaretAsync(DocumentViewModel document, int offset)
    {
        var location = document.Document.GetLocation(offset);
        document.NavigateTo(location.Line, location.Column);
        await UiTestHelpers.WaitUntilAsync(() => document.CaretOffset == offset, because: "the caret to move");
        await UiTestHelpers.WaitUntilAsync(() => true);
    }

    private static string Line(DocumentViewModel document, int number) =>
        document.Document.GetText(document.Document.GetLineByNumber(number));

    private static Button Button(Control root, string toolTip) =>
        root.GetVisualDescendants().OfType<Button>().Single(b => ToolTip.GetTip(b) as string == toolTip);

    private static void Tap(Control root, string toolTip)
    {
        var button = Button(root, toolTip);
        Assert.True(button.IsEffectivelyVisible && button.Command?.CanExecute(button.CommandParameter) == true, $"'{toolTip}' cannot be tapped.");
        button.Command!.Execute(button.CommandParameter);
    }

    private static void AnswerInput(WorkspaceViewModel workspace, string line) =>
        workspace.Output.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(OutputViewModel.IsWaitingForInput) && workspace.Output.IsWaitingForInput)
            {
                workspace.Output.InputText = line;
                workspace.Output.SubmitInputCommand.Execute(null);
            }
        };

    private async Task<Window> WaitForMonitorAsync()
    {
        await UiTestHelpers.WaitUntilAsync(
            () => _display.Window is { } window && window.GetVisualDescendants().OfType<DesktopScreenView>().Any(s => s.Bounds.Width > 0),
            because: "the IDE on the display");
        await UiTestHelpers.WaitUntilAsync(() => true);
        return _display.Window!;
    }

    private MainWindow Show(double width, double height)
    {
        var window = new MainWindow { DataContext = _main, Width = width, Height = height };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return window;
    }

    private async Task<WorkspaceViewModel> OpenSampleAsync()
    {
        var solution = await _services.GetRequiredService<SampleLibrary>().EnsureExtractedAsync();
        await _main.OpenWorkspaceAsync(solution);
        var workspace = _main.Workspace!;
        var document = workspace.ActiveDocument!;
        await document.AnalyzeNowAsync();
        await UiTestHelpers.WaitUntilAsync(() => document.Classifications.Count > 0, because: "syntax colors");
        return workspace;
    }

    /// <summary>The self test's keyboard and mouse, as Avalonia's headless input: keys to the tablet, clicks to the display.</summary>
    private sealed class HeadlessInput(Window tablet, FakeExternalDisplay display) : IDesktopSelfTestInput
    {
        public void Type(string text) => tablet.KeyTextInput(text);

        public Task ClickAsync(int displayId, PixelPoint point, CancellationToken cancellationToken)
        {
            var (window, at) = MoveTo(displayId, point);
            window.MouseDown(at, MouseButton.Left);
            window.MouseUp(at, MouseButton.Left);
            return Task.CompletedTask;
        }

        public Task ScrollAsync(int displayId, PixelPoint point, double notches, CancellationToken cancellationToken)
        {
            var (window, at) = MoveTo(displayId, point);
            window.MouseWheel(at, new Vector(0, notches));
            return Task.CompletedTask;
        }

        /// <summary>The mouse as TabStudio's own pointer moves it, captured from the tablet.</summary>
        private (Window Window, Point At) MoveTo(int displayId, PixelPoint point)
        {
            Assert.Equal(FakeExternalDisplay.DisplayId, displayId);
            var window = display.Window!;
            display.Cursor.MoveTo(point.X, point.Y);
            display.Cursor.Show();
            var at = point.ToPoint(window.RenderScaling);
            window.MouseMove(at);
            return (window, at);
        }

        public Task ScreenshotAsync(CancellationToken cancellationToken)
        {
            UiTestHelpers.SaveScreenshot(tablet, "desktop-06-self-test-tablet");
            UiTestHelpers.SaveScreenshot(display.Window!, "desktop-06-self-test-display");
            return Task.CompletedTask;
        }
    }
}
