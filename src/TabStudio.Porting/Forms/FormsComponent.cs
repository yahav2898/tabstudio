using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TabStudio.Porting.Forms;

/// <summary>An event the designer connected: <c>button1.Click += button1_Click</c>.</summary>
/// <param name="Event">The event.</param>
/// <param name="Handler">The handler method.</param>
internal sealed record FormsEvent(string Event, string Handler);

/// <summary>A column or row style of a TableLayoutPanel.</summary>
/// <param name="SizeType">Absolute, Percent or AutoSize.</param>
/// <param name="Value">The size.</param>
internal sealed record FormsTrack(string SizeType, double Value);

/// <summary>
/// A control or component as the Windows Forms designer creates it in InitializeComponent: its type, the properties
/// it sets, its children, and the events it connects.
/// </summary>
/// <param name="name">The field's name, such as <c>button1</c>; <c>this</c> for the form.</param>
/// <param name="type">The Windows Forms type, without its namespace.</param>
internal sealed class FormsComponent(string name, string type)
{
    /// <summary>The field's name.</summary>
    public string Name { get; } = name;

    /// <summary>The Windows Forms type, without its namespace, such as <c>Button</c>.</summary>
    public string Type { get; set; } = type;

    /// <summary>The arguments the designer created it with, such as a ToolTip's container.</summary>
    public List<ExpressionSyntax> Arguments { get; } = [];

    /// <summary>The properties the designer set, in order, by name (nested ones as <c>FlatAppearance.BorderSize</c>).</summary>
    public Dictionary<string, ExpressionSyntax> Properties { get; } = new(StringComparer.Ordinal);

    /// <summary>The controls it contains, in the order the designer added them.</summary>
    public List<FormsComponent> Children { get; } = [];

    /// <summary>For a SplitContainer, the controls of its second panel; its first panel's are <see cref="Children"/>.</summary>
    public List<FormsComponent> SecondPanel { get; } = [];

    /// <summary>The items of a menu, toolbar or status strip, or a menu item's drop-down items.</summary>
    public List<FormsComponent> Items { get; } = [];

    /// <summary>The items of a list or combo box, as written: string literals and other expressions.</summary>
    public List<ExpressionSyntax> ListItems { get; } = [];

    /// <summary>The columns of a grid or a list view.</summary>
    public List<FormsComponent> Columns { get; } = [];

    /// <summary>The events the designer connected.</summary>
    public List<FormsEvent> Events { get; } = [];

    /// <summary>The column and row of a control in a TableLayoutPanel, when the designer placed it.</summary>
    public (int Column, int Row)? Cell { get; set; }

    /// <summary>The columns a control spans in a TableLayoutPanel.</summary>
    public int ColumnSpan { get; set; } = 1;

    /// <summary>The rows a control spans in a TableLayoutPanel.</summary>
    public int RowSpan { get; set; } = 1;

    /// <summary>A TableLayoutPanel's column styles.</summary>
    public List<FormsTrack> ColumnStyles { get; } = [];

    /// <summary>A TableLayoutPanel's row styles.</summary>
    public List<FormsTrack> RowStyles { get; } = [];

    /// <summary>The tooltip a ToolTip component gives it.</summary>
    public string? ToolTip { get; set; }

    /// <summary>The control it is in.</summary>
    public FormsComponent? Parent { get; set; }

    /// <summary>A property's expression, or <see langword="null"/>.</summary>
    public ExpressionSyntax? Property(string property) => Properties.GetValueOrDefault(property);
}

/// <summary>A form as the designer laid it out, and what the converter could not read.</summary>
/// <param name="ClassName">The form's class, with its namespace.</param>
/// <param name="Form">The form itself.</param>
/// <param name="Components">Every component the designer created, by field name.</param>
/// <param name="Unread">Statements of InitializeComponent the converter does not understand.</param>
internal sealed record FormsDesign(string ClassName, FormsComponent Form, Dictionary<string, FormsComponent> Components, List<StatementSyntax> Unread)
{
    /// <summary>The form's class without its namespace.</summary>
    public string ShortName => ClassName[(ClassName.LastIndexOf('.') + 1)..];

    /// <summary>The form's namespace.</summary>
    public string Namespace => ClassName.Contains('.', StringComparison.Ordinal) ? ClassName[..ClassName.LastIndexOf('.')] : string.Empty;

    /// <summary>Whether the designer file declared the form's namespace with braces rather than file-scoped.</summary>
    public bool BlockNamespace { get; init; }

    /// <summary>The designer's base type: Form, or UserControl for a user control.</summary>
    public string BaseType { get; init; } = "Form";
}
