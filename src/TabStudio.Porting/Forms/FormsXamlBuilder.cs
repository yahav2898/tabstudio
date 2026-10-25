using System.Globalization;
using System.Xml.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TabStudio.Porting.Output;
using TabStudio.Porting.Xaml;

namespace TabStudio.Porting.Forms;

/// <summary>
/// Writes a form's Avalonia XAML from its designer layout: a DockPanel for docked strips, a Canvas for controls at
/// fixed places, a Grid where controls are anchored to the right or bottom, and Windows Forms' controls as their
/// Avalonia counterparts. Event handlers stay in code; what has no counterpart becomes a TODO comment in the XAML
/// and a port note.
/// </summary>
/// <remarks>The controls are in <c>FormsXamlBuilder.Controls.cs</c>, the layout in <c>FormsXamlBuilder.Layout.cs</c>.</remarks>
internal sealed partial class FormsXamlBuilder
{
    private static readonly XName Name = XamlNamespaces.Xaml + "Name";

    private readonly FormsDesign _design;
    private readonly PortContext _context;
    private readonly FileReport _report;
    private readonly HashSet<string> _projectTypes;
    private readonly XNamespace _app;
    private readonly XNamespace _local;
    private readonly double _scale;
    private readonly List<(XElement Element, string What, string Why)> _notes = [];
    private XElement _root = null!;

    /// <summary>Prepares the XAML of a form.</summary>
    /// <param name="design">The form's layout.</param>
    /// <param name="context">The port.</param>
    /// <param name="report">The XAML file's report.</param>
    /// <param name="projectTypes">The types the project declares, which can be used as controls.</param>
    public FormsXamlBuilder(FormsDesign design, PortContext context, FileReport report, HashSet<string> projectTypes)
    {
        _design = design;
        _context = context;
        _report = report;
        _projectTypes = projectTypes;
        _app = XamlNamespaces.Using(context.SupportNamespace);
        _local = XamlNamespaces.Using(design.Namespace.Length > 0 ? design.Namespace : context.RootNamespace);
        _scale = VerticalScale(design);
    }

    /// <summary>What the code of the form needs, filled while the XAML is built.</summary>
    public FormsClassInfo Info { get; } = new();

    /// <summary>Builds the XAML.</summary>
    public XDocument Build()
    {
        var form = _design.Form;
        var isForm = _design.BaseType is "Form";
        _root = new XElement(isForm ? _app + "AppForm" : XamlNamespaces.Avalonia + "UserControl",
            new XAttribute("xmlns", XamlNamespaces.Avalonia.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "x", XamlNamespaces.Xaml.NamespaceName));
        if (isForm)
        {
            _root.Add(new XAttribute(XNamespace.Xmlns + "app", _app.NamespaceName));
            _context.Needs(SupportNeeds.Form);
            _report.Converted("Form → AppForm, a window on the desktop and the screen on phones");
        }
        else
        {
            _report.Converted("UserControl → Avalonia UserControl");
        }

        _root.Add(new XAttribute(XamlNamespaces.Xaml + "Class", _design.ClassName));
        var (width, height) = FormsValues.Pair(form.Property("ClientSize")) ?? FormsValues.Pair(form.Property("Size")) ?? (800, 450);
        if (isForm)
        {
            if (FormsValues.String(form.Property("Text")) is { } title)
            {
                _root.Add(new XAttribute("Title", title));
            }

            _root.Add(new XAttribute("Width", Round(width)));
            _root.Add(new XAttribute("Height", Round(height * _scale + StripAllowance(form))));
            AddFormProperties(form);
        }

        AddCommon(_root, form, isRoot: true);
        if (Layout(form, form.Children, width, height, 0, 0) is { } content)
        {
            _root.Add(content);
        }

        foreach (var statement in _design.Unread)
        {
            Todo(statement.ToString(), "The converter does not know what this line of InitializeComponent does; set it in the XAML or in code", null);
        }

        // The TODO comments go in front of their elements, now that every element is in place.
        foreach (var (element, what, why) in _notes)
        {
            var comment = new XComment($" TODO(port): {what.TrimEnd('.')}. {why.TrimEnd('.')}. ".Replace("--", "- -", StringComparison.Ordinal));
            if (element == _root || element.Parent is null)
            {
                _root.AddFirst(comment);
            }
            else
            {
                element.AddBeforeSelf(comment);
            }

            _report.Note(comment, what, why);
        }

        return new XDocument(_root);
    }

    /// <summary>The form's window properties: its sizes, where it opens, whether it resizes, and its default and cancel buttons.</summary>
    private void AddFormProperties(FormsComponent form)
    {
        if (FormsValues.Pair(form.Property("MinimumSize")) is { } minimum)
        {
            AddIfPositive(_root, "MinWidth", minimum.A);
            AddIfPositive(_root, "MinHeight", minimum.B * _scale);
        }

        if (FormsValues.Pair(form.Property("MaximumSize")) is { } maximum)
        {
            AddIfPositive(_root, "MaxWidth", maximum.A);
            AddIfPositive(_root, "MaxHeight", maximum.B * _scale);
        }

        switch (FormsValues.EnumMember(form.Property("StartPosition")))
        {
            case "CenterScreen":
                _root.Add(new XAttribute("WindowStartupLocation", "CenterScreen"));
                break;
            case "CenterParent":
                _root.Add(new XAttribute("WindowStartupLocation", "CenterOwner"));
                break;
        }

        if (FormsValues.EnumMember(form.Property("FormBorderStyle")) is { } border)
        {
            if (border is "FixedSingle" or "Fixed3D" or "FixedDialog" or "FixedToolWindow")
            {
                _root.Add(new XAttribute("CanResize", "False"));
                _report.Converted("FormBorderStyle → CanResize");
            }
            else if (border is "None")
            {
                Note(_root, "FormBorderStyle.None", "Avalonia windows without a frame set SystemDecorations=\"None\" on the desktop window (DesktopWindow)");
            }
        }

        if (FormsValues.Boolean(form.Property("ShowInTaskbar")) is false)
        {
            _root.Add(new XAttribute("ShowInTaskbar", "False"));
        }

        if (FormsValues.Boolean(form.Property("TopMost")) is true)
        {
            _root.Add(new XAttribute("Topmost", "True"));
        }

        if (FormsValues.EnumMember(form.Property("WindowState")) is { } state and not "Normal")
        {
            _root.Add(new XAttribute("WindowState", state));
        }

        if (form.Property("Icon") is not null)
        {
            Note(_root, "Form icon", "The icon is in the form's .resx; save it as an .ico in Assets and set Icon=\"/Assets/app.ico\"");
        }
    }

    /// <summary>The height Avalonia's menus and status bars take beyond the designer's strips.</summary>
    private static double StripAllowance(FormsComponent form) =>
        form.Children.Sum(c => c.Type switch
        {
            "MenuStrip" => 8,
            "ToolStrip" => 10,
            "StatusStrip" => 6,
            _ => 0,
        });

    /// <summary>
    /// The factor that makes rows of text boxes and buttons tall enough for Avalonia's controls: Fluent's are 32
    /// pixels tall, Windows Forms' 23.
    /// </summary>
    private static double VerticalScale(FormsDesign design)
    {
        var pitch = double.MaxValue;
        foreach (var container in design.Components.Values.Prepend(design.Form))
        {
            var rows = container.Children.Where(c => IsSingleLine(c) && Bounds(c) is not null).Select(c => Bounds(c)!.Value).ToList();
            foreach (var a in rows)
            {
                foreach (var b in rows)
                {
                    var below = b.Y > a.Y && b.Y >= a.Y + Math.Min(a.Height, 20) && b.X < a.X + a.Width && a.X < b.X + b.Width;
                    if (below)
                    {
                        pitch = Math.Min(pitch, b.Y - a.Y);
                    }
                }
            }
        }

        return pitch >= 32 || pitch == double.MaxValue ? 1 : Math.Min(32 / pitch, 1.6);
    }

    /// <summary>Adds a TODO comment and a note in front of an element.</summary>
    private void Note(XElement element, string what, string why) => _notes.Add((element, what, why));

    /// <summary>Records a TODO for the form's code (the generated designer file).</summary>
    private void Todo(string what, string why, string? original) => Info.Todos.Add((what, why, original));

    private static void AddIfPositive(XElement element, string name, double value)
    {
        if (value > 0)
        {
            element.Add(new XAttribute(name, Round(value)));
        }
    }

    private static string Round(double value) => Math.Round(value).ToString(CultureInfo.InvariantCulture);

    /// <summary>The form's original markup of a property, for notes.</summary>
    private static string Original(FormsComponent component, string property) =>
        $"{component.Name}.{property} = {component.Property(property)}";

    private static ExpressionSyntax? Get(FormsComponent component, string property) => component.Property(property);
}
