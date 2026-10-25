using System.Xml.Linq;
using TabStudio.Core.Workspaces;
using TabStudio.Porting.Forms;
using TabStudio.Porting.Wpf;
using TabStudio.Testing;

namespace TabStudio.Porting.Tests;

/// <summary>Independent conversion rules and safety properties, beyond the four end-to-end goldens.</summary>
public sealed class RuleTests
{
    [Theory]
    [InlineData("Label", "TextBlock")]
    [InlineData("Button", "Button")]
    [InlineData("TextBox", "TextBox")]
    [InlineData("CheckBox", "CheckBox")]
    [InlineData("NumericUpDown", "NumericUpDown")]
    [InlineData("ProgressBar", "ProgressBar")]
    [InlineData("TrackBar", "Slider")]
    [InlineData("DateTimePicker", "CalendarDatePicker")]
    [InlineData("PictureBox", "Image")]
    [InlineData("GroupBox", "HeaderedContentControl")]
    public void Forms_control_rules(string source, string target)
    {
        var result = Forms(source);
        var root = XDocument.Parse(result.Files.Single(f => f.Path == "Form1.axaml").Text!);
        Assert.Contains(root.Descendants(), e => e.Name.LocalName == target && (string?)e.Attribute("{http://schemas.microsoft.com/winfx/2006/xaml}Name") == "control");
    }

    [Theory]
    [InlineData("Click", "Click", "EventArgs")]
    [InlineData("MouseDown", "PointerPressed", "PointerPressedEventArgs")]
    [InlineData("KeyDown", "KeyDown", "KeyEventArgs")]
    [InlineData("TextChanged", "TextChanged", "EventArgs")]
    public void Designer_event_rules_keep_handlers_and_adapt_arguments(string source, string target, string args)
    {
        var result = Forms("Button", $"control.{source} += Handle;", "private void Handle(object sender, EventArgs e) { }");
        Assert.Contains($"control.{target} += Handle;", Text(result, "Form1.Events.cs"), StringComparison.Ordinal);
        Assert.Contains(args + " e", Text(result, "Form1.cs"), StringComparison.Ordinal);
        Assert.Contains("InitializePortedEvents();", Text(result, "Form1.cs"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Enabled", "IsEnabled")]
    [InlineData("Visible", "IsVisible")]
    [InlineData("Text", "Content")]
    public void Code_member_rules(string source, string target)
    {
        var result = Forms("Button", "", $"void Update() {{ control.{source} = default; }}");
        Assert.Contains("control." + target, Text(result, "Form1.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void Async_closing_waits_and_numeric_and_checked_reads_preserve_value_semantics()
    {
        var sample = SampleProjects.Port("WinFormsCustomers");
        Assert.Contains("FormClosingAsync += MainForm_FormClosing", Text(sample, "MainForm.Events.cs"), StringComparison.Ordinal);
        Assert.Contains("System.Threading.Tasks.Task MainForm_FormClosing", Text(sample, "MainForm.cs"), StringComparison.Ordinal);
        Assert.Contains("await handler(this, args)", Text(sample, "Porting/AppForm.cs"), StringComparison.Ordinal);
        Assert.Contains("IsChecked==true", Text(sample, "MainForm.cs"), StringComparison.Ordinal);
        Assert.Contains("ageUpDown.Value??0", Text(sample, "MainForm.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void Unmapped_events_properties_and_components_have_navigable_structured_notes()
    {
        var result = Forms("AxHost", "control.MysteryProperty = 7; control.Paint += Handle;", "void Handle(object sender, EventArgs e) { }");
        Assert.NotEmpty(result.Notes);
        foreach (var note in result.Notes)
        {
            Assert.False(string.IsNullOrWhiteSpace(note.What));
            Assert.False(string.IsNullOrWhiteSpace(note.Why));
            var lines = Text(result, note.File).Split('\n');
            Assert.InRange(note.Line, 1, lines.Length);
            Assert.Contains("TODO(port)", lines[note.Line - 1], StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("DatePicker", "CalendarDatePicker")]
    [InlineData("ListViewItem", "ListBoxItem")]
    [InlineData("AdornerDecorator", "Decorator")]
    public void Wpf_element_rules(string source, string target)
    {
        var result = Wpf($"<{source} />");
        Assert.Contains("<" + target, Text(result, "MainWindow.axaml"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("RichTextBox")]
    [InlineData("WebBrowser")]
    [InlineData("MediaElement")]
    [InlineData("Viewport3D")]
    public void Wpf_unmapped_controls_have_notes(string control)
    {
        var result = Wpf($"<{control} />");
        Assert.Contains(result.Notes, n => n.What.Contains(control, StringComparison.Ordinal) && n.Why.Length > 0 && n.Line > 0);
    }

    [Fact]
    public void Designer_reader_accepts_this_qualified_and_delegate_event_wiring()
    {
        var design = DesignerReader.Read("namespace Example { partial class F { System.Windows.Forms.Button b; void InitializeComponent() { this.b = new System.Windows.Forms.Button(); this.b.Click += new System.EventHandler(this.OnClick); this.Controls.Add(this.b); } } }");
        Assert.NotNull(design);
        Assert.Equal("OnClick", Assert.Single(design.Components["b"].Events).Handler);
        Assert.Equal("b", Assert.Single(design.Form.Children).Name);
        Assert.Empty(design.Unread);
    }

    [Fact]
    public void Write_refuses_to_overwrite_existing_files_and_originals_stay_byte_identical()
    {
        using var temp = new TempDirectory();
        var project = Path.Combine(temp.Path, "App.csproj");
        File.WriteAllText(project, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><UseWindowsForms>true</UseWindowsForms></PropertyGroup></Project>");
        var source = ProjectParser.Parse(project, File.ReadAllText(project));
        var before = File.ReadAllBytes(project);
        var result = AvaloniaPorter.Port(source, new PortOptions("Converted", Path.Combine(temp.Path, "Converted"), SampleProjects.AvaloniaVersion));
        result.Write();
        Assert.Throws<IOException>(result.Write);
        Assert.Equal(before, File.ReadAllBytes(project));
    }

    [Theory]
    [InlineData("")]
    [InlineData("../Source")]
    [InlineData("A/B")]
    [InlineData("App..Next")]
    public void Invalid_project_names_are_rejected(string name) => Assert.NotNull(AvaloniaPorter.ValidateName(name));

    private static string Text(PortResult result, string path) => result.Files.Single(f => f.Path == path).Text!;

    private static PortResult Forms(string type, string setup = "", string members = "") => AvaloniaPorter.Port(new PortSource
    {
        Name = "Example", Kind = PortSourceKind.WindowsForms,
        Properties = new Dictionary<string, string> { ["ImplicitUsings"] = "enable" },
        Files = new Dictionary<string, string>
        {
            ["Form1.Designer.cs"] = "namespace Example { partial class Form1 { " + type + " control; void InitializeComponent() { control = new " + type + "(); control.Location = new Point(10, 20); control.Size = new Size(100, 30); Controls.Add(control); " + setup + " } } }",
            ["Form1.cs"] = "namespace Example\n{\n    public partial class Form1 : Form\n    {\n        public Form1()\n        {\n            InitializeComponent();\n        }\n        " + members + "\n    }\n}\n",
        },
    }, new PortOptions("Converted", Path.Combine(Path.GetTempPath(), "port-rules"), SampleProjects.AvaloniaVersion));

    private static PortResult Wpf(string content) => AvaloniaPorter.Port(new PortSource
    {
        Name = "Example", Kind = PortSourceKind.Wpf,
        Files = new Dictionary<string, string> { ["MainWindow.xaml"] = "<Window xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" x:Class=\"Example.MainWindow\">" + content + "</Window>" },
    }, new PortOptions("Converted", Path.Combine(Path.GetTempPath(), "port-rules"), SampleProjects.AvaloniaVersion));
}
