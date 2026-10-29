using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using TabStudio.App.Services;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Core.Diagnostics;
using TabStudio.Forms.Design.Surface;
using TabStudio.Forms.Design.View;
using DrawingPoint = System.Drawing.Point;
using FormsControl = System.Windows.Forms.Control;

namespace TabStudio.App.Tests.Views;

/// <summary>
/// The Windows Forms designer in the IDE, driven as a user does: Form1.cs opens on the designer, a drag moves a
/// control on the grid and a handle resizes it, the Toolbox adds controls by tap-then-tap and by drag, the Properties
/// change them, and a double-tap writes a Click handler and shows it in the code; each change is in Form1.Designer.cs.
/// </summary>
public sealed class FormsDesignerTests
{
    private const string ProjectName = "HelloForms";

    [AvaloniaFact]
    public async Task Form1_opens_on_the_designer_where_a_drag_moves_a_button_and_its_handle_resizes_it()
    {
        using var fixture = new WorkspaceFixture();
        var designed = await DesignedForm.OpenAsync(fixture, WindowsFormsAppTests.HelloForm.Designer, WindowsFormsAppTests.HelloForm.Code("Hello"));
        var designer = designed.Designer;
        Assert.True(designer.IsDesign);
        Assert.False(designer.HasLoadError, designer.LoadError);
        Assert.False(designed.Document.ShowsEditorActions);
        Assert.Equal("Form1", designer.SelectionTitle);

        // A tap selects the button.
        designed.Tap(designed.CenterOf("button1"));
        Assert.Equal("button1", designer.SelectionTitle);
        Assert.Equal("System.Windows.Forms.Button", designer.SelectionType);

        // A drag moves it on the 8-pixel grid.
        var scale = designer.Canvas.Scale;
        var start = designed.CenterOf("button1") + new Point(10, 0);
        designed.Drag(start, start + new Point(144, 8));
        var x = DesignSurface.Snap(24 + (int)Math.Round(144 / scale));
        var y = DesignSurface.Snap(24 + (int)Math.Round(8 / scale));
        Assert.Contains($"button1.Location = new Point({x}, {y});", designed.DesignerText, StringComparison.Ordinal);
        Assert.Equal(new DrawingPoint(x, y), designed.ControlNamed("button1").Location);

        // Its bottom-right handle resizes it, the dragged edges on the grid.
        var handle = designed.HandleOf("button1", Handle.BottomRight);
        designed.Drag(handle, handle + new Point(40, 16));
        var width = DesignSurface.Snap(x + 120 + (int)Math.Round(40 / scale)) - x;
        var height = DesignSurface.Snap(y + 32 + (int)Math.Round(16 / scale)) - y;
        Assert.Contains($"button1.Size = new Size({width}, {height});", designed.DesignerText, StringComparison.Ordinal);
        UiTestHelpers.SaveScreenshot(designed.Window, "winforms-designer-01-selection");

        // Undo takes the resize back, and Redo does it again; the statements the designer does not change stay.
        designer.UndoCommand.Execute(null);
        Assert.Contains("button1.Size = new Size(120, 32);", designed.DesignerText, StringComparison.Ordinal);
        designer.RedoCommand.Execute(null);
        Assert.Contains($"button1.Size = new Size({width}, {height});", designed.DesignerText, StringComparison.Ordinal);
        Assert.Contains("button1.Click += button1_Click;", designed.DesignerText, StringComparison.Ordinal);
        Assert.Contains("label1.Text = \"Not clicked yet\";", designed.DesignerText, StringComparison.Ordinal);
        designed.Window.Close();
    }

    [AvaloniaFact]
    public async Task The_Toolbox_adds_controls_by_tap_and_by_drag_and_the_Properties_change_them()
    {
        using var fixture = new WorkspaceFixture();
        var designed = await DesignedForm.OpenAsync(fixture);
        var designer = designed.Designer;

        // Tap a tool, then tap the form: the control is added there, on the grid.
        var buttonTool = designed.ToolButton("Button");
        designed.Tap(designed.Center(buttonTool));
        Assert.True(((FormsToolboxItemViewModel)buttonTool.DataContext!).IsArmed);
        designed.Tap(designed.OnForm(new DrawingPoint(40, 40)));
        Assert.False(((FormsToolboxItemViewModel)buttonTool.DataContext!).IsArmed);
        Assert.Equal("button1", designer.SelectionTitle);
        var text = designed.DesignerText;
        Assert.Contains("button1 = new Button();", text, StringComparison.Ordinal);
        Assert.Contains("button1.Location = new Point(40, 40);", text, StringComparison.Ordinal);
        Assert.Contains("button1.Text = \"button1\";", text, StringComparison.Ordinal);
        Assert.Contains("Controls.Add(button1);", text, StringComparison.Ordinal);
        Assert.Contains("private Button button1;", text, StringComparison.Ordinal);

        // Drag a tool onto the form: it is added where it is dropped.
        var labelTool = designed.ToolButton("Label");
        var from = designed.Center(labelTool);
        designed.Drag(from, designed.OnForm(new DrawingPoint(40, 120)), through: from + new Point(40, 0));
        Assert.False(((FormsToolboxItemViewModel)labelTool.DataContext!).IsArmed);
        Assert.Equal("label1", designer.SelectionTitle);
        text = designed.DesignerText;
        Assert.Contains("label1.AutoSize = true;", text, StringComparison.Ordinal);
        Assert.Contains("label1.Location = new Point(40, 120);", text, StringComparison.Ordinal);
        Assert.Contains("Controls.Add(label1);", text, StringComparison.Ordinal);

        // The Properties show the selected button's; its text is typed, and Enter commits it.
        designed.Tap(designed.CenterOf("button1"));
        await DesignedForm.SettleAsync();
        var textBox = designed.Window.GetVisualDescendants().OfType<TextBox>().Single(t => t.DataContext is TextPropertyViewModel { Path: "Text" });
        Assert.Equal("button1", textBox.Text);
        textBox.Focus();
        textBox.SelectAll();
        designed.Window.KeyTextInput("Place order");
        designed.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Assert.Contains("button1.Text = \"Place order\";", designed.DesignerText, StringComparison.Ordinal);
        Assert.Equal("Place order", designed.ControlNamed("button1").Text);

        // A check box turns a property off, and a swatch picks a color.
        var enabled = designed.Window.GetVisualDescendants().OfType<CheckBox>().Single(c => c.DataContext is BooleanPropertyViewModel { Path: "Enabled" });
        enabled.IsChecked = false;
        Assert.Contains("button1.Enabled = false;", designed.DesignerText, StringComparison.Ordinal);
        var backColor = designer.Properties.OfType<ColorPropertyViewModel>().Single(p => p.Path == "BackColor");
        backColor.Palette.Single(c => c.Name == "LightYellow").PickCommand.Execute(null);
        Assert.Contains("button1.BackColor = Color.LightYellow;", designed.DesignerText, StringComparison.Ordinal);
        await DesignedForm.SettleAsync();
        UiTestHelpers.SaveScreenshot(designed.Window, "winforms-designer-02-toolbox-and-properties");

        // The designer's Properties are beside the form, so the side bar's hide: two never show at once.
        Assert.False(designed.Workspace.ShowsSideBarProperties);
        Assert.False(designed.Window.GetVisualDescendants().OfType<PropertiesView>().Single().IsVisible);

        // Renaming the button renames it in the designer file.
        var name = designer.Properties.OfType<TextPropertyViewModel>().Single(p => p.Path == "Name");
        name.Text = "orderButton";
        Assert.Equal("orderButton", designer.SelectionTitle);
        Assert.Contains("orderButton.Text = \"Place order\";", designed.DesignerText, StringComparison.Ordinal);
        Assert.DoesNotContain("button1", designed.DesignerText, StringComparison.Ordinal);
        designed.Window.Close();
    }

    [AvaloniaFact]
    public async Task Double_tapping_a_button_writes_its_Click_handler_shows_it_in_the_code_and_the_project_builds()
    {
        using var fixture = new WorkspaceFixture();
        var designed = await DesignedForm.OpenAsync(fixture);
        var designer = designed.Designer;
        designed.Tap(designed.Center(designed.ToolButton("Button")));
        designed.Tap(designed.OnForm(new DrawingPoint(24, 24)));
        Assert.Equal("button1", designer.SelectionTitle);

        // A double-tap on the button writes its default event's handler, as Visual Studio does, and shows it.
        var center = designed.CenterOf("button1");
        designed.Tap(center);
        designed.Tap(center);
        Assert.True(designer.IsCode);
        Assert.True(designed.Document.ShowsEditorActions);
        var code = designed.Document.Document.Text;
        Assert.Contains("private void button1_Click(object sender, EventArgs e)", code, StringComparison.Ordinal);
        Assert.Contains("button1.Click += button1_Click;", designed.DesignerText, StringComparison.Ordinal);
        var bodyLine = designed.Document.Document.GetLocation(code.IndexOf("private void button1_Click", StringComparison.Ordinal)).Line + 2;
        await UiTestHelpers.WaitUntilAsync(() => designed.Document.CaretLine == bodyLine, because: "the caret in the handler");
        UiTestHelpers.SaveScreenshot(designed.Window, "winforms-designer-03-click-handler");

        // The Properties' events show the handler, and Shift+F7 goes back to the designer.
        Assert.Equal("button1_Click", designer.Events.Single(e => e.Name == "Click").Handler);
        designed.Window.KeyPressQwerty(PhysicalKey.F7, RawInputModifiers.Shift);
        Assert.True(designer.IsDesign);

        // What the designer wrote builds without errors or warnings.
        await designed.Workspace.BuildCommand.ExecuteAsync(null);
        var problems = designed.Workspace.ErrorList.Items.Where(i => i.Diagnostic.Severity >= DiagnosticSeverityLevel.Warning).Select(i => i.Diagnostic.ToString()).ToList();
        Assert.True(problems.Count == 0, string.Join('\n', problems));
        Assert.Contains("========== Build: 1 succeeded, 0 failed ==========", designed.Workspace.Output.Text, StringComparison.Ordinal);
        designed.Window.Close();
    }

    [AvaloniaFact]
    public async Task A_Visual_Basic_form_is_designed_by_touch_and_a_double_tap_writes_a_Handles_handler()
    {
        using var fixture = new WorkspaceFixture();
        var designed = await DesignedForm.OpenAsync(fixture, visualBasic: true);
        var designer = designed.Designer;
        designed.Tap(designed.Center(designed.ToolButton("Button")));
        designed.Tap(designed.OnForm(new DrawingPoint(24, 24)));
        Assert.Equal("Button1", designer.SelectionTitle);
        Assert.Contains("Friend WithEvents Button1 As Button", designed.DesignerText, StringComparison.Ordinal);
        UiTestHelpers.SaveScreenshot(designed.Window, "winforms-designer-05-visual-basic");

        // A double-tap writes the handler as Visual Studio does in Visual Basic: with a Handles clause.
        var center = designed.CenterOf("Button1");
        designed.Tap(center);
        designed.Tap(center);
        Assert.True(designer.IsCode);
        Assert.Contains("Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click", designed.Document.Document.Text, StringComparison.Ordinal);
        UiTestHelpers.SaveScreenshot(designed.Window, "winforms-designer-06-visual-basic-handler");

        await designed.Workspace.BuildCommand.ExecuteAsync(null);
        var problems = designed.Workspace.ErrorList.Items.Where(i => i.Diagnostic.Severity >= DiagnosticSeverityLevel.Error).Select(i => i.Diagnostic.ToString()).ToList();
        Assert.True(problems.Count == 0, string.Join('\n', problems));
        designed.Window.Close();
    }

    [AvaloniaFact]
    public async Task A_menu_strip_gets_its_menus_typed_in_the_Properties_and_a_menu_item_gets_its_Click_handler()
    {
        using var fixture = new WorkspaceFixture();
        var designed = await DesignedForm.OpenAsync(fixture);
        var designer = designed.Designer;
        var tool = designed.ToolButton("MenuStrip");
        tool.BringIntoView();
        await DesignedForm.SettleAsync();
        designed.Tap(designed.Center(tool));
        designed.Tap(designed.OnForm(new DrawingPoint(200, 200)));
        Assert.Equal("menuStrip1", designer.SelectionTitle);
        Assert.True(designer.HasStripItems);
        Assert.Equal("Add menu item", designer.AddItemLabel);
        await DesignedForm.SettleAsync();

        // Type a menu's text and press Enter, as in Visual Studio's "Type Here".
        designed.TypeNewItem("&File");
        Assert.Equal("&File", Assert.Single(designer.StripItems).Text);
        Assert.Equal(string.Empty, designer.NewItemText);

        // Tap the menu to give it items; - is a separator.
        designed.Tap(designed.Center(designed.StripItemButton("fileToolStripMenuItem")));
        Assert.Equal("fileToolStripMenuItem", designer.SelectionTitle);
        designed.TypeNewItem("&Open...");
        designed.TypeNewItem("-");
        designed.TypeNewItem("E&xit");
        Assert.Equal(["&Open...", "—", "E&xit"], designer.StripItems.Select(i => i.Text));
        var text = designed.DesignerText;
        Assert.Contains("menuStrip1.Items.AddRange(new ToolStripItem[] { fileToolStripMenuItem });", text, StringComparison.Ordinal);
        Assert.Contains("fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { openToolStripMenuItem, toolStripSeparator1, exitToolStripMenuItem });", text, StringComparison.Ordinal);
        Assert.Contains("MainMenuStrip = menuStrip1;", text, StringComparison.Ordinal);
        await DesignedForm.SettleAsync();
        UiTestHelpers.SaveScreenshot(designed.Window, "winforms-designer-04-menu");

        // A menu item's Click handler, from its events.
        designed.Tap(designed.Center(designed.StripItemButton("exitToolStripMenuItem")));
        designer.ShowEventsCommand.Execute(null);
        designer.Events.Single(e => e.Name == "Click").OpenCommand.Execute(null);
        Assert.True(designer.IsCode);
        Assert.Contains("private void exitToolStripMenuItem_Click(object sender, EventArgs e)", designed.Document.Document.Text, StringComparison.Ordinal);
        Assert.Contains("exitToolStripMenuItem.Click += exitToolStripMenuItem_Click;", designed.DesignerText, StringComparison.Ordinal);

        await designed.Workspace.BuildCommand.ExecuteAsync(null);
        var problems = designed.Workspace.ErrorList.Items.Where(i => i.Diagnostic.Severity >= DiagnosticSeverityLevel.Warning).Select(i => i.Diagnostic.ToString()).ToList();
        Assert.True(problems.Count == 0, string.Join('\n', problems));
        designed.Window.Close();
    }

    /// <summary>A form of a new Windows Forms project, open on its designer in the IDE.</summary>
    private sealed class DesignedForm(WorkspaceViewModel workspace, Window window, DocumentViewModel document, string designerPath)
    {
        public WorkspaceViewModel Workspace { get; } = workspace;

        public Window Window { get; } = window;

        public DocumentViewModel Document { get; } = document;

        public FormsDesignerViewModel Designer { get; } = document.FormsDesigner!;

        public string DesignerText => File.ReadAllText(designerPath);

        public static async Task<DesignedForm> OpenAsync(WorkspaceFixture fixture, string? designer = null, string? code = null, bool visualBasic = false)
        {
            var extension = visualBasic ? ".vb" : ".cs";
            var solution = new ProjectTemplates(fixture.Paths).Create(visualBasic ? ProjectTemplates.VisualBasicWindowsFormsId : ProjectTemplates.WindowsFormsId, ProjectName);
            var project = Path.Combine(Path.GetDirectoryName(solution)!, ProjectName);
            var designerPath = Path.Combine(project, "Form1.Designer" + extension);
            if (designer is not null)
            {
                File.WriteAllText(designerPath, designer);
            }

            if (code is not null)
            {
                File.WriteAllText(Path.Combine(project, "Form1" + extension), code);
            }

            var workspace = fixture.Workspace;
            await workspace.LoadAsync(solution);
            var window = new Window { Width = 1600, Height = 1000, Content = new WorkspaceView { DataContext = workspace } };
            window.Show();
            var document = await workspace.OpenDocumentAsync(Path.Combine(project, "Form1" + extension));
            Assert.NotNull(document);
            Assert.NotNull(document.FormsDesigner);
            var designed = new DesignedForm(workspace, window, document, designerPath);
            await UiTestHelpers.WaitUntilAsync(
                () => designed.Designer.Canvas.IsEffectivelyVisible && designed.Designer.Canvas.Bounds.Width > 0,
                because: "the designer's layout");
            designed.Designer.IsToolboxOpen = true;
            designed.Designer.IsPropertiesOpen = true;
            await DesignedForm.SettleAsync();
            return designed;
        }

        public FormsControl ControlNamed(string name) => (FormsControl)Designer.Find(name)!.Instance!;

        /// <summary>The center of a component on the canvas, in the window.</summary>
        public Point CenterOf(string name) => InWindow(Designer.Canvas, Designer.Canvas.BoundsOnCanvas(Designer.Find(name)!).Center);

        /// <summary>The center of a selection handle of a component, in the window.</summary>
        public Point HandleOf(string name, Handle handle) =>
            InWindow(Designer.Canvas, Designer.Canvas.Handles(Designer.Find(name)!).Single(h => h.Handle == handle).Center);

        /// <summary>A point of the form's client area, in the window.</summary>
        public Point OnForm(DrawingPoint point) => InWindow(Designer.Canvas, Designer.Canvas.ToCanvas(point));

        public Button ToolButton(string name) =>
            Window.GetVisualDescendants().OfType<Button>().Single(b => b.DataContext is FormsToolboxItemViewModel tool && tool.Name == name);

        public Button StripItemButton(string name) =>
            Window.GetVisualDescendants().OfType<Button>().Single(b => b.DataContext is StripItemViewModel item && item.Name == name);

        public Point Center(Visual visual) => InWindow(visual, new Rect(visual.Bounds.Size).Center);

        /// <summary>Types an item's text where the Properties say "Type Here", and presses Enter.</summary>
        public void TypeNewItem(string text)
        {
            var box = Window.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "NewItemBox");
            box.Focus();
            Window.KeyTextInput(text);
            Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        }

        public void Tap(Point at)
        {
            Window.MouseMove(at);
            Window.MouseDown(at, MouseButton.Left);
            Window.MouseUp(at, MouseButton.Left);
        }

        public void Drag(Point from, Point to, Point? through = null)
        {
            Window.MouseMove(from);
            Window.MouseDown(from, MouseButton.Left);
            if (through is { } waypoint)
            {
                Window.MouseMove(waypoint, RawInputModifiers.LeftMouseButton);
            }

            var last = through ?? from;
            for (var step = 1; step <= 4; step++)
            {
                Window.MouseMove(new Point(last.X + ((to.X - last.X) * step / 4), last.Y + ((to.Y - last.Y) * step / 4)), RawInputModifiers.LeftMouseButton);
            }

            Window.MouseUp(to, MouseButton.Left);
        }

        /// <summary>Lets layout and rendering catch up.</summary>
        public static async Task SettleAsync()
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            await UiTestHelpers.WaitUntilAsync(() => true);
        }

        private Point InWindow(Visual visual, Point point) =>
            visual.TranslatePoint(point, Window) ?? throw new InvalidOperationException("The control is not in the window.");
    }
}
