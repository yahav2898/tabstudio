namespace TabStudio.Forms.Design.Model;

/// <summary>
/// One component of a designed form, as InitializeComponent declares it: its field, its type, and the statements
/// that set it up, kept as C# so that what the designer does not understand is written back as it was.
/// </summary>
internal sealed class DesignComponent
{
    /// <summary>Creates a component.</summary>
    /// <param name="name">Its field's name, such as <c>button1</c>; for the form itself, the class name.</param>
    /// <param name="typeName">Its type as the code names it, such as <c>Button</c> or <c>System.Windows.Forms.Button</c>.</param>
    /// <param name="isRoot">Whether it is the form or user control being designed.</param>
    public DesignComponent(string name, string typeName, bool isRoot = false)
    {
        Name = name;
        TypeName = typeName;
        IsRoot = isRoot;
    }

    /// <summary>The field's name; for the root, the designed class's name.</summary>
    public string Name { get; set; }

    /// <summary>The type as the code names it.</summary>
    public string TypeName { get; set; }

    /// <summary>The type, once resolved; <see langword="null"/> for a type the designer does not know, such as the app's own user control.</summary>
    public Type? Type { get; set; }

    /// <summary>Whether it is the form or user control being designed.</summary>
    public bool IsRoot { get; }

    /// <summary>The arguments it is created with, as C#, such as <c>components</c> for a timer.</summary>
    public string ConstructorArguments { get; set; } = string.Empty;

    /// <summary>The modifiers of the component's field, such as <c>private</c> or <c>Friend WithEvents</c>, kept as the file had them.</summary>
    public string FieldModifiers { get; set; } = "private";

    /// <summary>
    /// Where the component's field is among the designer file's fields, which a Visual Basic file keeps; <see langword="null"/>
    /// for a component without a field there, such as a new one, whose field goes last.
    /// </summary>
    public int? FieldIndex { get; set; }

    /// <summary>The properties set, by property path, such as <c>Location</c> or <c>FlatAppearance.BorderSize</c>, with their values as C#.</summary>
    public List<DesignAssignment> Properties { get; } = [];

    /// <summary>The events handled, with the handlers' names.</summary>
    public List<DesignEvent> Events { get; } = [];

    /// <summary>What is added to the component's collections, such as its child controls or its menu items.</summary>
    public List<DesignCollectionAdd> Adds { get; } = [];

    /// <summary>Statements about the component the designer keeps as they are, such as <c>toolTip1.SetToolTip(...)</c>.</summary>
    public List<string> OtherStatements { get; } = [];

    /// <summary>The live object on the design surface.</summary>
    public object? Instance { get; set; }

    // Visual Basic names members in any case: Me.Button1.text is the Text the designer sets, not a second property.
    // No Windows Forms component has two members whose names differ only by case.
    private static bool SamePath(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    /// <summary>The value of a property as C#, or <see langword="null"/> when it is not set.</summary>
    /// <param name="path">The property's path.</param>
    public string? GetCode(string path) => Properties.Find(p => SamePath(p.Path, path))?.Code;

    /// <summary>Sets a property's value as C#, or removes it with <see langword="null"/>.</summary>
    /// <param name="path">The property's path.</param>
    /// <param name="code">The value as C#.</param>
    public void SetCode(string path, string? code)
    {
        var index = Properties.FindIndex(p => SamePath(p.Path, path));
        if (code is null)
        {
            if (index >= 0)
            {
                Properties.RemoveAt(index);
            }
        }
        else if (index >= 0)
        {
            Properties[index] = new DesignAssignment(path, code);
        }
        else
        {
            Properties.Add(new DesignAssignment(path, code));
        }
    }

    /// <summary>The handler of an event, or <see langword="null"/>.</summary>
    /// <param name="eventName">The event.</param>
    public string? GetHandler(string eventName) => Events.Find(e => SamePath(e.Event, eventName))?.Handler;

    /// <summary>Sets an event's handler, or removes it with <see langword="null"/>.</summary>
    /// <param name="eventName">The event.</param>
    /// <param name="handler">The handler method's name.</param>
    public void SetHandler(string eventName, string? handler)
    {
        Events.RemoveAll(e => SamePath(e.Event, eventName));
        if (!string.IsNullOrEmpty(handler))
        {
            Events.Add(new DesignEvent(eventName, handler));
        }
    }

    /// <summary>The adds to one of the component's collections, such as <c>Controls</c>.</summary>
    /// <param name="collection">The collection's path.</param>
    public DesignCollectionAdd Collection(string collection)
    {
        if (Adds.Find(a => a.Collection == collection) is { } existing)
        {
            return existing;
        }

        var added = new DesignCollectionAdd(collection);
        Adds.Add(added);
        return added;
    }

    /// <inheritdoc />
    public override string ToString() => $"{Name} ({TypeName})";
}

/// <summary>A property set in InitializeComponent.</summary>
/// <param name="Path">The property, or a property of a property, such as <c>FlatAppearance.BorderSize</c>.</param>
/// <param name="Code">The value as C#.</param>
internal sealed record DesignAssignment(string Path, string Code);

/// <summary>An event handled in InitializeComponent, or by a Visual Basic handler's <c>Handles</c> clause.</summary>
/// <param name="Event">The event, such as <c>Click</c>.</param>
/// <param name="Handler">The method, such as <c>button1_Click</c>.</param>
internal sealed record DesignEvent(string Event, string Handler)
{
    /// <summary>
    /// Whether the handler is wired by a <c>Handles</c> clause in the Visual Basic code file, such as
    /// <c>Handles Button1.Click</c>, rather than in the designer file, which then does not name it.
    /// </summary>
    public bool IsHandlesClause { get; init; }
}

/// <summary>
/// What InitializeComponent adds to a collection of a component, such as <c>groupBox1.Controls.Add(button1)</c> or
/// <c>menuStrip1.Items.AddRange(new ToolStripItem[] { fileMenuItem })</c>.
/// </summary>
/// <param name="collection">The collection's path, such as <c>Controls</c>, <c>Items</c> or <c>Panel1.Controls</c>.</param>
internal sealed class DesignCollectionAdd(string collection)
{
    /// <summary>The collection's path.</summary>
    public string Collection { get; } = collection;

    /// <summary>
    /// Whether the items are added with one <c>AddRange</c> call, as menus and strips are, rather than one <c>Add</c>
    /// call each, as controls are.
    /// </summary>
    public bool IsRange { get; set; }

    /// <summary>The element type of an <c>AddRange</c> array, as the code names it, such as <c>ToolStripItem</c>.</summary>
    public string? RangeType { get; set; }

    /// <summary>The arguments of each item, as C#: a component's name, or a value, with more arguments for some, such as a table's cell.</summary>
    public List<string> Items { get; } = [];
}
