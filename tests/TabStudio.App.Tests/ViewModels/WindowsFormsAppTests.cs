using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Core.Diagnostics;
using TabStudio.Forms.Hosting;
using AvaloniaPoint = Avalonia.Point;
using DrawingPoint = System.Drawing.Point;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>
/// Windows Forms apps in the IDE: the Visual Studio template builds against the Forms layer, F5 shows its form in the
/// app preview in a Windows frame, a click reaches its button, its Console output goes to the Output panel, saving a
/// file restarts it, closing the main form ends the run, and an exception in a handler ends the app, not the IDE.
/// </summary>
public sealed class WindowsFormsAppTests
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(2);

    [AvaloniaFact]
    public async Task F5_runs_a_form_in_the_app_preview_a_click_changes_its_label_and_saving_restarts_it()
    {
        using var fixture = new WorkspaceFixture();
        var solution = new ProjectTemplates(fixture.Paths).Create(ProjectTemplates.WindowsFormsId, "HelloForms");
        var project = Path.Combine(Path.GetDirectoryName(solution)!, "HelloForms");
        File.WriteAllText(Path.Combine(project, "Form1.cs"), HelloForm.Code("Hello from Windows Forms!"));
        File.WriteAllText(Path.Combine(project, "Form1.Designer.cs"), HelloForm.Designer);
        var workspace = fixture.Workspace;
        await workspace.LoadAsync(solution);
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = workspace } };
        window.Show();

        var run = workspace.StartDebuggingCommand.ExecuteAsync(null);
        var app = await FormsPreview.WaitForAsync(workspace, window, run);
        Assert.True(workspace.IsAppPreviewSelected);
        Assert.Contains("HelloForms is running in the app preview.", workspace.Output.Text, StringComparison.Ordinal);
        Assert.Contains("Started without the debugger: a Windows Forms app runs its own message loop", workspace.Output.Text, StringComparison.Ordinal);
        await UiTestHelpers.WaitUntilAsync(() => workspace.Output.Text.Contains("Form1 is ready.", StringComparison.Ordinal), because: "the form's Console output");

        var form = app.Desktop.Windows[0];
        Assert.Equal("Hello Forms", form.Title);
        Assert.Equal(new System.Drawing.Size(400, 200), form.Client.Size);
        app.Click(new DrawingPoint(form.Client.X + 84, form.Client.Y + 40));
        await app.WaitForTextAsync("label1", "Hello from Windows Forms!", "the click to change the label");
        await UiTestHelpers.WaitUntilAsync(() => app.Desktop.Windows[0].Content.Texts.Contains("Hello from Windows Forms!"), because: "the new label on the screen");
        UiTestHelpers.SaveScreenshot(window, "winforms-01-running-app");

        // The project builds without warnings, its designer file included.
        Assert.DoesNotContain("warning", workspace.Output.Text, StringComparison.Ordinal);
        var errors = workspace.ErrorList.Items.Where(i => i.Diagnostic.Severity >= DiagnosticSeverityLevel.Warning).Select(i => i.Diagnostic.ToString()).ToList();
        Assert.True(errors.Count == 0, string.Join('\n', errors));

        // Saving the form's code builds the app again and runs the change.
        var document = await workspace.OpenDocumentAsync(Path.Combine(project, "Form1.cs"));
        var text = document!.Document.Text;
        var greeting = text.IndexOf("Hello from Windows Forms!", StringComparison.Ordinal);
        document.Document.Replace(greeting, "Hello from Windows Forms!".Length, "Changed on a tablet");
        await workspace.SaveCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(
            () => workspace.Output.Text.Contains("Restarting 'HelloForms' with the saved changes.", StringComparison.Ordinal) || run.IsCompleted,
            StartTimeout,
            "the app to restart");
        app = await FormsPreview.WaitForAsync(workspace, window, run);
        form = app.Desktop.Windows[0];
        app.Click(new DrawingPoint(form.Client.X + 84, form.Client.Y + 40));
        await app.WaitForTextAsync("label1", "Changed on a tablet", "the restarted app's click");

        // Closing the main form ends the run.
        app.ClickCaption(WindowPart.Close);
        await run.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.False(workspace.AppPreview.IsRunning);
        Assert.Contains("The program 'HelloForms' has exited with code 0", workspace.Output.Text, StringComparison.Ordinal);
        window.Close();
    }

    [AvaloniaFact]
    public async Task An_exception_a_click_handler_does_not_handle_ends_the_app_and_not_the_IDE()
    {
        using var fixture = new WorkspaceFixture();
        var solution = new ProjectTemplates(fixture.Paths).Create(ProjectTemplates.WindowsFormsId, "Crashy");
        var project = Path.Combine(Path.GetDirectoryName(solution)!, "Crashy");
        File.WriteAllText(Path.Combine(project, "Form1.cs"), HelloForm.Code("unused").Replace("HelloForms", "Crashy", StringComparison.Ordinal)
            .Replace("label1.Text = \"unused\";", "throw new InvalidOperationException(\"The order has no lines.\");", StringComparison.Ordinal));
        File.WriteAllText(Path.Combine(project, "Form1.Designer.cs"), HelloForm.Designer.Replace("HelloForms", "Crashy", StringComparison.Ordinal));
        var workspace = fixture.Workspace;
        await workspace.LoadAsync(solution);
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = workspace } };
        window.Show();

        var run = workspace.RunCommand.ExecuteAsync(null);
        var app = await FormsPreview.WaitForAsync(workspace, window, run);
        var form = app.Desktop.Windows[0];
        app.Click(new DrawingPoint(form.Client.X + 84, form.Client.Y + 40));
        await run.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.False(workspace.AppPreview.IsRunning);
        Assert.Contains("Unhandled exception. System.InvalidOperationException: The order has no lines.", workspace.Output.Text, StringComparison.Ordinal);
        Assert.Contains("The program 'Crashy' stopped because of an unhandled exception.", workspace.Output.Text, StringComparison.Ordinal);
        Assert.Equal(IdeState.Failed, workspace.State);
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_business_app_with_a_menu_a_grid_inputs_and_message_boxes_builds_and_runs()
    {
        using var fixture = new WorkspaceFixture();
        var solution = new ProjectTemplates(fixture.Paths).Create(ProjectTemplates.WindowsFormsId, "Orders");
        var project = Path.Combine(Path.GetDirectoryName(solution)!, "Orders");
        File.WriteAllText(Path.Combine(project, "Form1.cs"), BusinessApp.Code);
        File.WriteAllText(Path.Combine(project, "Form1.Designer.cs"), BusinessApp.Designer);
        File.WriteAllText(Path.Combine(project, "Customer.cs"), BusinessApp.Customer);
        var workspace = fixture.Workspace;
        await workspace.LoadAsync(solution);
        var window = new Window { Width = 1600, Height = 1000, Content = new WorkspaceView { DataContext = workspace } };
        window.Show();

        var run = workspace.RunCommand.ExecuteAsync(null);
        var app = await FormsPreview.WaitForAsync(workspace, window, run);
        Assert.DoesNotContain("warning", workspace.Output.Text, StringComparison.Ordinal);
        var form = app.Desktop.Windows[0];
        Assert.Equal("Orders", form.Title);
        await UiTestHelpers.WaitUntilAsync(() => app.Read(w => w.Form.Controls.Find("customersGrid", true)[0] is System.Windows.Forms.DataGridView { RowCount: > 2 }), because: "the grid's rows");

        // Widen the side bar by its splitter, as a user does to see a big form at its own size.
        var splitter = window.GetVisualDescendants().OfType<GridSplitter>().Single(s => s.Name == "SideSplitter");
        var grip = splitter.TranslatePoint(new AvaloniaPoint(splitter.Bounds.Width / 2, splitter.Bounds.Height / 2), window)!.Value;
        window.MouseMove(grip);
        window.MouseDown(grip, MouseButton.Left);
        for (var step = 1; step <= 6; step++)
        {
            window.MouseMove(new AvaloniaPoint(grip.X - (100 * step), grip.Y), RawInputModifiers.LeftMouseButton);
        }

        window.MouseUp(new AvaloniaPoint(grip.X - 600, grip.Y), MouseButton.Left);
        await UiTestHelpers.WaitUntilAsync(() => app.Desktop.Scale > 0.9, because: "the form at its own size in the wider app preview");
        UiTestHelpers.SaveScreenshot(window, "winforms-02-business-app");

        // The Save button asks before saving, in a message box over the form.
        var save = app.BoundsOf("saveButton");
        var client = app.Desktop.Windows[0].Client;
        app.Click(new DrawingPoint(client.X + save.X + (save.Width / 2), client.Y + save.Y + (save.Height / 2)));
        await UiTestHelpers.WaitUntilAsync(() => app.Desktop.Windows.Count == 2, because: "the message box");
        Assert.True(app.Desktop.Windows[1].IsModal);
        UiTestHelpers.SaveScreenshot(window, "winforms-03-message-box");
        window.KeyTextInput("y");
        await UiTestHelpers.WaitUntilAsync(() => app.TextOf("statusLabel") == "Saved 3 customers.", because: "the answer to the message box");

        workspace.StopCommand.Execute(null);
        await run.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Contains("The program 'Orders' was stopped.", workspace.Output.Text, StringComparison.Ordinal);
        window.Close();
    }

    /// <summary>A running Windows Forms app in the IDE's app preview, which tests press as a user does.</summary>
    private sealed class FormsPreview(FormsDesktop desktop, Window window)
    {
        public FormsDesktop Desktop { get; } = desktop;

        /// <summary>
        /// Waits until the app preview shows a running app's form, laid out and drawn, as a user sees it before tapping
        /// it: a click sent before the form is drawn may land where its button is not yet.
        /// </summary>
        public static async Task<FormsPreview> WaitForAsync(WorkspaceViewModel workspace, Window window, Task run)
        {
            await UiTestHelpers.WaitUntilAsync(
                () => (workspace.AppPreview.View is FormsDesktop { Windows.Count: > 0 } && workspace.AppPreview.IsRunning) || run.IsCompleted,
                StartTimeout,
                "the app's form");
            Assert.True(workspace.AppPreview.IsRunning, workspace.Output.Text);
            var desktop = (FormsDesktop)workspace.AppPreview.View!;
            await UiTestHelpers.WaitUntilAsync(() => desktop.IsSizeKnown && desktop.IsAttachedToVisualTree(), because: "the desktop's layout");
            await UiTestHelpers.WaitUntilAsync(() => desktop.Windows.Count > 0 && desktop.Windows[0].Content.Texts.Any(), because: "the form drawn on the desktop");
            return new FormsPreview(desktop, window);
        }

        /// <summary>Waits until a control shows a text, and says what it shows instead when it does not.</summary>
        public async Task WaitForTextAsync(string name, string expected, string because)
        {
            string? shown = null;
            try
            {
                await UiTestHelpers.WaitUntilAsync(() => (shown = TextOf(name)) == expected, because: because);
            }
            catch (Xunit.Sdk.FailException)
            {
                Assert.Fail($"Timed out waiting for {because}: {name} shows \"{shown}\" instead of \"{expected}\".");
            }
        }

        public T Read<T>(Func<TabStudio.Forms.Runtime.FormWindow, T> read)
        {
            var runtime = Desktop.Runtime ?? throw new InvalidOperationException("The app ended.");
            T value = default!;
            Assert.True(runtime.Send(() => value = read(runtime.Windows[0]), TimeSpan.FromSeconds(10)), "The app is busy.");
            return value;
        }

        /// <summary>The text of a control, or of a strip's item, such as a status label.</summary>
        public string? TextOf(string name) => Read(w =>
            w.Form.Controls.Find(name, true).FirstOrDefault()?.Text
            ?? Descendants(w.Form).OfType<System.Windows.Forms.ToolStrip>()
                .SelectMany(strip => strip.Items.Find(name, true)).FirstOrDefault()?.Text);

        private static IEnumerable<System.Windows.Forms.Control> Descendants(System.Windows.Forms.Control control) =>
            control.Controls.Cast<System.Windows.Forms.Control>().SelectMany(child => Descendants(child).Prepend(child));

        public System.Drawing.Rectangle BoundsOf(string name) => Read(w =>
        {
            var control = w.Form.Controls.Find(name, true)[0];
            var origin = control.Parent!.PointToScreen(control.Location);
            var client = w.Form.PointToScreen(System.Drawing.Point.Empty);
            return new System.Drawing.Rectangle(origin.X - client.X, origin.Y - client.Y, control.Width, control.Height);
        });

        public void Click(DrawingPoint point)
        {
            var at = Desktop.TranslatePoint(new AvaloniaPoint((point.X + 0.5) * Desktop.Scale, (point.Y + 0.5) * Desktop.Scale), window)
                ?? throw new InvalidOperationException("The desktop is not in the window.");
            window.MouseMove(at);
            window.MouseDown(at, MouseButton.Left);
            window.MouseUp(at, MouseButton.Left);
        }

        public void ClickCaption(WindowPart part)
        {
            var button = ChromeGeometry.Button(Desktop.Windows[0], part);
            Click(new DrawingPoint(button.X + (button.Width / 2), button.Y + (button.Height / 2)));
        }
    }

    /// <summary>A form with a button that changes a label, as the designer writes it.</summary>
    internal static class HelloForm
    {
        public const string Designer = """
            namespace HelloForms
            {
                partial class Form1
                {
                    /// <summary>
                    ///  Required designer variable.
                    /// </summary>
                    private System.ComponentModel.IContainer components = null;

                    /// <summary>
                    ///  Clean up any resources being used.
                    /// </summary>
                    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
                    protected override void Dispose(bool disposing)
                    {
                        if (disposing && (components != null))
                        {
                            components.Dispose();
                        }
                        base.Dispose(disposing);
                    }

                    #region Windows Form Designer generated code

                    /// <summary>
                    ///  Required method for Designer support - do not modify
                    ///  the contents of this method with the code editor.
                    /// </summary>
                    private void InitializeComponent()
                    {
                        button1 = new Button();
                        label1 = new Label();
                        SuspendLayout();
                        //
                        // button1
                        //
                        button1.Location = new Point(24, 24);
                        button1.Name = "button1";
                        button1.Size = new Size(120, 32);
                        button1.TabIndex = 0;
                        button1.Text = "Say hello";
                        button1.UseVisualStyleBackColor = true;
                        button1.Click += button1_Click;
                        //
                        // label1
                        //
                        label1.AutoSize = true;
                        label1.Location = new Point(24, 72);
                        label1.Name = "label1";
                        label1.Size = new Size(84, 15);
                        label1.TabIndex = 1;
                        label1.Text = "Not clicked yet";
                        //
                        // Form1
                        //
                        AutoScaleDimensions = new SizeF(7F, 15F);
                        AutoScaleMode = AutoScaleMode.Font;
                        ClientSize = new Size(400, 200);
                        Controls.Add(label1);
                        Controls.Add(button1);
                        Name = "Form1";
                        Text = "Hello Forms";
                        ResumeLayout(false);
                        PerformLayout();
                    }

                    #endregion

                    private Button button1;
                    private Label label1;
                }
            }
            """;

        public static string Code(string greeting) => $$"""
            namespace HelloForms
            {
                public partial class Form1 : Form
                {
                    public Form1()
                    {
                        InitializeComponent();
                        Console.WriteLine("Form1 is ready.");
                    }

                    private void button1_Click(object sender, EventArgs e)
                    {
                        label1.Text = "{{greeting}}";
                    }
                }
            }
            """;
    }

    /// <summary>A small business app: a menu, a grid of customers, fields to edit one, and a message box to confirm.</summary>
    private static class BusinessApp
    {
        public const string Customer = """
            using System.ComponentModel;

            namespace Orders;

            public sealed class Customer
            {
                public string Name { get; set; } = string.Empty;

                public string City { get; set; } = string.Empty;

                [DisplayName("Credit limit")]
                public decimal CreditLimit { get; set; }

                public DateTime Since { get; set; }

                public bool Active { get; set; }
            }
            """;

        public const string Code = """
            using System.ComponentModel;

            namespace Orders
            {
                public partial class Form1 : Form
                {
                    private readonly BindingList<Customer> _customers =
                    [
                        new() { Name = "Contoso", City = "Seattle", CreditLimit = 25000m, Since = new DateTime(2019, 4, 1), Active = true },
                        new() { Name = "Fabrikam", City = "Tel Aviv", CreditLimit = 12500m, Since = new DateTime(2021, 9, 15), Active = true },
                        new() { Name = "Northwind", City = "London", CreditLimit = 8000m, Since = new DateTime(2016, 1, 20), Active = false },
                    ];

                    public Form1()
                    {
                        InitializeComponent();
                        customerBindingSource.DataSource = _customers;
                        cityComboBox.Items.AddRange(["Seattle", "Tel Aviv", "London", "Berlin"]);
                        nameTextBox.DataBindings.Add("Text", customerBindingSource, nameof(Customer.Name));
                        cityComboBox.DataBindings.Add("Text", customerBindingSource, nameof(Customer.City));
                        limitUpDown.DataBindings.Add("Value", customerBindingSource, nameof(Customer.CreditLimit));
                        sinceDatePicker.DataBindings.Add("Value", customerBindingSource, nameof(Customer.Since));
                        activeCheckBox.DataBindings.Add("Checked", customerBindingSource, nameof(Customer.Active));
                        statusLabel.Text = $"{_customers.Count} customers";
                    }

                    private void saveButton_Click(object sender, EventArgs e)
                    {
                        var answer = MessageBox.Show(this, $"Save {_customers.Count} customers?", "Orders", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (answer == DialogResult.Yes)
                        {
                            statusLabel.Text = $"Saved {_customers.Count} customers.";
                        }
                    }

                    private void exitMenuItem_Click(object sender, EventArgs e) => Close();

                    private void aboutMenuItem_Click(object sender, EventArgs e) =>
                        MessageBox.Show(this, "Orders 1.0, written on a tablet.", "About Orders", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            """;

        public const string Designer = """
            namespace Orders
            {
                partial class Form1
                {
                    private System.ComponentModel.IContainer components = null;

                    protected override void Dispose(bool disposing)
                    {
                        if (disposing && (components != null))
                        {
                            components.Dispose();
                        }
                        base.Dispose(disposing);
                    }

                    #region Windows Form Designer generated code

                    private void InitializeComponent()
                    {
                        components = new System.ComponentModel.Container();
                        menuStrip1 = new MenuStrip();
                        fileMenuItem = new ToolStripMenuItem();
                        saveMenuItem = new ToolStripMenuItem();
                        exitMenuItem = new ToolStripMenuItem();
                        helpMenuItem = new ToolStripMenuItem();
                        aboutMenuItem = new ToolStripMenuItem();
                        customersGrid = new DataGridView();
                        customerBindingSource = new BindingSource(components);
                        detailsGroup = new GroupBox();
                        nameLabel = new Label();
                        nameTextBox = new TextBox();
                        cityLabel = new Label();
                        cityComboBox = new ComboBox();
                        limitLabel = new Label();
                        limitUpDown = new NumericUpDown();
                        sinceLabel = new Label();
                        sinceDatePicker = new DateTimePicker();
                        activeCheckBox = new CheckBox();
                        saveButton = new Button();
                        statusStrip1 = new StatusStrip();
                        statusLabel = new ToolStripStatusLabel();
                        menuStrip1.SuspendLayout();
                        ((System.ComponentModel.ISupportInitialize)customersGrid).BeginInit();
                        ((System.ComponentModel.ISupportInitialize)customerBindingSource).BeginInit();
                        detailsGroup.SuspendLayout();
                        ((System.ComponentModel.ISupportInitialize)limitUpDown).BeginInit();
                        statusStrip1.SuspendLayout();
                        SuspendLayout();
                        //
                        // menuStrip1
                        //
                        menuStrip1.Items.AddRange(new ToolStripItem[] { fileMenuItem, helpMenuItem });
                        menuStrip1.Location = new Point(0, 0);
                        menuStrip1.Name = "menuStrip1";
                        menuStrip1.Size = new Size(760, 24);
                        menuStrip1.TabIndex = 0;
                        //
                        // fileMenuItem
                        //
                        fileMenuItem.DropDownItems.AddRange(new ToolStripItem[] { saveMenuItem, exitMenuItem });
                        fileMenuItem.Name = "fileMenuItem";
                        fileMenuItem.Size = new Size(37, 20);
                        fileMenuItem.Text = "&File";
                        //
                        // saveMenuItem
                        //
                        saveMenuItem.Name = "saveMenuItem";
                        saveMenuItem.ShortcutKeys = Keys.Control | Keys.S;
                        saveMenuItem.Size = new Size(138, 22);
                        saveMenuItem.Text = "&Save";
                        saveMenuItem.Click += saveButton_Click;
                        //
                        // exitMenuItem
                        //
                        exitMenuItem.Name = "exitMenuItem";
                        exitMenuItem.Size = new Size(138, 22);
                        exitMenuItem.Text = "E&xit";
                        exitMenuItem.Click += exitMenuItem_Click;
                        //
                        // helpMenuItem
                        //
                        helpMenuItem.DropDownItems.AddRange(new ToolStripItem[] { aboutMenuItem });
                        helpMenuItem.Name = "helpMenuItem";
                        helpMenuItem.Size = new Size(44, 20);
                        helpMenuItem.Text = "&Help";
                        //
                        // aboutMenuItem
                        //
                        aboutMenuItem.Name = "aboutMenuItem";
                        aboutMenuItem.Size = new Size(107, 22);
                        aboutMenuItem.Text = "&About";
                        aboutMenuItem.Click += aboutMenuItem_Click;
                        //
                        // customersGrid
                        //
                        customersGrid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
                        customersGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                        customersGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
                        customersGrid.DataSource = customerBindingSource;
                        customersGrid.Location = new Point(12, 36);
                        customersGrid.Name = "customersGrid";
                        customersGrid.Size = new Size(440, 330);
                        customersGrid.TabIndex = 1;
                        //
                        // detailsGroup
                        //
                        detailsGroup.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                        detailsGroup.Controls.Add(nameLabel);
                        detailsGroup.Controls.Add(nameTextBox);
                        detailsGroup.Controls.Add(cityLabel);
                        detailsGroup.Controls.Add(cityComboBox);
                        detailsGroup.Controls.Add(limitLabel);
                        detailsGroup.Controls.Add(limitUpDown);
                        detailsGroup.Controls.Add(sinceLabel);
                        detailsGroup.Controls.Add(sinceDatePicker);
                        detailsGroup.Controls.Add(activeCheckBox);
                        detailsGroup.Location = new Point(464, 36);
                        detailsGroup.Name = "detailsGroup";
                        detailsGroup.Size = new Size(284, 280);
                        detailsGroup.TabIndex = 2;
                        detailsGroup.TabStop = false;
                        detailsGroup.Text = "Customer";
                        //
                        // nameLabel
                        //
                        nameLabel.AutoSize = true;
                        nameLabel.Location = new Point(12, 28);
                        nameLabel.Name = "nameLabel";
                        nameLabel.Text = "&Name:";
                        //
                        // nameTextBox
                        //
                        nameTextBox.Location = new Point(96, 24);
                        nameTextBox.Name = "nameTextBox";
                        nameTextBox.Size = new Size(172, 23);
                        nameTextBox.TabIndex = 1;
                        //
                        // cityLabel
                        //
                        cityLabel.AutoSize = true;
                        cityLabel.Location = new Point(12, 64);
                        cityLabel.Name = "cityLabel";
                        cityLabel.Text = "&City:";
                        //
                        // cityComboBox
                        //
                        cityComboBox.Location = new Point(96, 60);
                        cityComboBox.Name = "cityComboBox";
                        cityComboBox.Size = new Size(172, 23);
                        cityComboBox.TabIndex = 3;
                        //
                        // limitLabel
                        //
                        limitLabel.AutoSize = true;
                        limitLabel.Location = new Point(12, 100);
                        limitLabel.Name = "limitLabel";
                        limitLabel.Text = "Credit &limit:";
                        //
                        // limitUpDown
                        //
                        limitUpDown.Increment = new decimal(new int[] { 500, 0, 0, 0 });
                        limitUpDown.Location = new Point(96, 96);
                        limitUpDown.Maximum = new decimal(new int[] { 1000000, 0, 0, 0 });
                        limitUpDown.Name = "limitUpDown";
                        limitUpDown.Size = new Size(172, 23);
                        limitUpDown.TabIndex = 5;
                        limitUpDown.ThousandsSeparator = true;
                        //
                        // sinceLabel
                        //
                        sinceLabel.AutoSize = true;
                        sinceLabel.Location = new Point(12, 136);
                        sinceLabel.Name = "sinceLabel";
                        sinceLabel.Text = "Customer &since:";
                        //
                        // sinceDatePicker
                        //
                        sinceDatePicker.Format = DateTimePickerFormat.Short;
                        sinceDatePicker.Location = new Point(110, 132);
                        sinceDatePicker.Name = "sinceDatePicker";
                        sinceDatePicker.Size = new Size(158, 23);
                        sinceDatePicker.TabIndex = 7;
                        //
                        // activeCheckBox
                        //
                        activeCheckBox.AutoSize = true;
                        activeCheckBox.Location = new Point(96, 172);
                        activeCheckBox.Name = "activeCheckBox";
                        activeCheckBox.TabIndex = 8;
                        activeCheckBox.Text = "&Active";
                        activeCheckBox.UseVisualStyleBackColor = true;
                        //
                        // saveButton
                        //
                        saveButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
                        saveButton.Location = new Point(648, 328);
                        saveButton.Name = "saveButton";
                        saveButton.Size = new Size(100, 32);
                        saveButton.TabIndex = 3;
                        saveButton.Text = "Save";
                        saveButton.UseVisualStyleBackColor = true;
                        saveButton.Click += saveButton_Click;
                        //
                        // statusStrip1
                        //
                        statusStrip1.Items.AddRange(new ToolStripItem[] { statusLabel });
                        statusStrip1.Location = new Point(0, 378);
                        statusStrip1.Name = "statusStrip1";
                        statusStrip1.Size = new Size(760, 22);
                        statusStrip1.TabIndex = 4;
                        //
                        // statusLabel
                        //
                        statusLabel.Name = "statusLabel";
                        statusLabel.Size = new Size(70, 17);
                        statusLabel.Text = "Ready";
                        //
                        // Form1
                        //
                        AutoScaleDimensions = new SizeF(7F, 15F);
                        AutoScaleMode = AutoScaleMode.Font;
                        ClientSize = new Size(760, 400);
                        Controls.Add(saveButton);
                        Controls.Add(detailsGroup);
                        Controls.Add(customersGrid);
                        Controls.Add(statusStrip1);
                        Controls.Add(menuStrip1);
                        MainMenuStrip = menuStrip1;
                        Name = "Form1";
                        StartPosition = FormStartPosition.CenterScreen;
                        Text = "Orders";
                        menuStrip1.ResumeLayout(false);
                        menuStrip1.PerformLayout();
                        ((System.ComponentModel.ISupportInitialize)customersGrid).EndInit();
                        ((System.ComponentModel.ISupportInitialize)customerBindingSource).EndInit();
                        detailsGroup.ResumeLayout(false);
                        detailsGroup.PerformLayout();
                        ((System.ComponentModel.ISupportInitialize)limitUpDown).EndInit();
                        statusStrip1.ResumeLayout(false);
                        statusStrip1.PerformLayout();
                        ResumeLayout(false);
                        PerformLayout();
                    }

                    #endregion

                    private MenuStrip menuStrip1;
                    private ToolStripMenuItem fileMenuItem;
                    private ToolStripMenuItem saveMenuItem;
                    private ToolStripMenuItem exitMenuItem;
                    private ToolStripMenuItem helpMenuItem;
                    private ToolStripMenuItem aboutMenuItem;
                    private DataGridView customersGrid;
                    private BindingSource customerBindingSource;
                    private GroupBox detailsGroup;
                    private Label nameLabel;
                    private TextBox nameTextBox;
                    private Label cityLabel;
                    private ComboBox cityComboBox;
                    private Label limitLabel;
                    private NumericUpDown limitUpDown;
                    private Label sinceLabel;
                    private DateTimePicker sinceDatePicker;
                    private CheckBox activeCheckBox;
                    private Button saveButton;
                    private StatusStrip statusStrip1;
                    private ToolStripStatusLabel statusLabel;
                }
            }
            """;
    }
}
