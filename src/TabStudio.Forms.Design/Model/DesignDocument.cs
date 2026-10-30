namespace TabStudio.Forms.Design.Model;

/// <summary>How the designer file writes code, which the designer keeps when it writes the file again.</summary>
/// <param name="QualifyWithThis">Whether members are written as <c>this.button1</c> (<c>Me.Button1</c> in Visual Basic), as older designer files are.</param>
/// <param name="FullTypeNames">Whether types are written as <c>System.Windows.Forms.Button</c> rather than <c>Button</c>.</param>
/// <param name="Indent">The indentation of the statements in InitializeComponent.</param>
/// <param name="NewLine">The file's line ending.</param>
internal sealed record CodeStyle(bool QualifyWithThis, bool FullTypeNames, string Indent, string NewLine)
{
    /// <summary>The style Visual Studio writes for a project with implicit usings: <c>button1.Text = "OK";</c>.</summary>
    public static CodeStyle Simplified { get; } = new(false, false, "            ", "\n");

    /// <summary>The language of the designer file: C# unless it is a Visual Basic one, such as Form1.Designer.vb.</summary>
    public DesignerLanguage Language { get; init; }

    /// <summary>A member of the form, as this style writes it.</summary>
    /// <param name="name">The member's name.</param>
    public string Member(string name) => QualifyWithThis ? (Language == DesignerLanguage.VisualBasic ? "Me." : "this.") + name : name;
}

/// <summary>
/// A designed form: the root, which is the form or user control itself, and its components, in the order
/// InitializeComponent creates them, with the statements the designer keeps as they are.
/// </summary>
internal sealed class DesignDocument
{
    /// <summary>Creates a document.</summary>
    /// <param name="root">The form or user control being designed.</param>
    /// <param name="style">How the file writes code.</param>
    public DesignDocument(DesignComponent root, CodeStyle style)
    {
        Root = root;
        Style = style;
    }

    /// <summary>The form or user control being designed; its name is the class's name.</summary>
    public DesignComponent Root { get; }

    /// <summary>The components, in the order they are created.</summary>
    public List<DesignComponent> Components { get; } = [];

    /// <summary>How the file writes code.</summary>
    public CodeStyle Style { get; set; }

    /// <summary>Whether InitializeComponent creates the <c>components</c> container, which timers and tooltips join.</summary>
    public bool HasContainer { get; set; }

    /// <summary>
    /// The statement that creates the <c>components</c> container as the file writes it, which the designer keeps, as
    /// Visual Basic's template writes <c>components = New System.ComponentModel.Container()</c> in a file that otherwise
    /// writes <c>Me.</c>; <see langword="null"/> to write it in the file's style.
    /// </summary>
    public string? ContainerStatement { get; set; }

    /// <summary>Statements before the components are created that the designer keeps, such as a resource manager's.</summary>
    public List<string> LeadingStatements { get; } = [];

    /// <summary>The root and the components.</summary>
    public IEnumerable<DesignComponent> All => [Root, .. Components];

    /// <summary>A component by name, the root included; in Visual Basic, whose names ignore case, in any case.</summary>
    /// <param name="name">The name.</param>
    public DesignComponent? Find(string name)
    {
        var comparison = Style.Language == DesignerLanguage.VisualBasic ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return string.Equals(name, Root.Name, comparison) ? Root : Components.Find(c => string.Equals(c.Name, name, comparison));
    }

    /// <summary>
    /// A name for a new component of a type, as Visual Studio picks it: <c>button1</c>, then <c>button2</c>; in
    /// Visual Basic, <c>Button1</c>, then <c>Button2</c>.
    /// </summary>
    /// <param name="type">The type.</param>
    public string NewName(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        var stem = Style.Language == DesignerLanguage.VisualBasic ? type.Name : char.ToLowerInvariant(type.Name[0]) + type.Name[1..];
        for (var i = 1; ; i++)
        {
            var name = stem + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (Find(name) is null)
            {
                return name;
            }
        }
    }

    /// <summary>The component a live object belongs to.</summary>
    /// <param name="instance">The object.</param>
    public DesignComponent? Of(object? instance) => instance is null ? null : All.FirstOrDefault(c => ReferenceEquals(c.Instance, instance));
}
