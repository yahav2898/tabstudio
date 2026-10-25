namespace TabStudio.Porting.Forms;

/// <summary>
/// What the converter learned about a form while it wrote its XAML, for its code: the type of each control in
/// Avalonia, and the code the designer's events and components need.
/// </summary>
internal sealed class FormsClassInfo
{
    /// <summary>Each component's Windows Forms type and what it is in Avalonia (null for components that are fields in code).</summary>
    public Dictionary<string, (string Forms, string? Avalonia)> Components { get; } = new(StringComparer.Ordinal);

    /// <summary>Fields the form needs in code, such as a DispatcherTimer or a grid's rows.</summary>
    public List<string> Fields { get; } = [];

    /// <summary>Statements that connect the events and components, run after InitializeComponent.</summary>
    public List<string> Statements { get; } = [];

    /// <summary>Notes for the code, written as TODO comments with the statements: what and why.</summary>
    public List<(string What, string Why, string? Original)> Todos { get; } = [];

    /// <summary>Grids filled in code: the grid's name to the field of its rows.</summary>
    public Dictionary<string, string> GridRows { get; } = new(StringComparer.Ordinal);

    /// <summary>Handlers whose event arguments have another type in Avalonia: method to the arguments' type.</summary>
    public Dictionary<string, string> HandlerArguments { get; } = new(StringComparer.Ordinal);

    /// <summary>Closing handlers that must complete before the form closes.</summary>
    public HashSet<string> AsyncClosingHandlers { get; } = new(StringComparer.Ordinal);

    /// <summary>The namespaces the statements and fields need.</summary>
    public HashSet<string> Usings { get; } = new(StringComparer.Ordinal);
}
