using CommunityToolkit.Mvvm.ComponentModel;
using TabStudio.Debugging;

namespace TabStudio.App.ViewModels;

/// <summary>A root of a <see cref="VariableTreeViewModel"/>: a value to show, or why there is none.</summary>
/// <param name="Name">The name shown: a variable name, or the expression of a watch.</param>
/// <param name="Path">The path that finds the value again, such as <c>order.Items[0]</c>.</param>
/// <param name="Value">The value, or <see langword="null"/> when it could not be found.</param>
/// <param name="Error">Why there is no value, shown instead of it.</param>
/// <param name="IsRemovable">Whether the row has a remove button, as watches do.</param>
public sealed record VariableSource(string Name, string Path, DebugValue? Value, string? Error = null, bool IsRemovable = false);

/// <summary>One row of the Locals, Watch or DataTip tree: a variable, member or item of the paused program.</summary>
public sealed partial class VariableViewModel : ObservableObject
{
    private const double IndentPerLevel = 16;

    private VariableViewModel(string name, string value, string type, string path, int depth, DebugValue? source, bool hasChanged, bool isError, bool isRemovable)
    {
        Name = name;
        Value = value;
        Type = type;
        Path = path;
        Depth = depth;
        Source = source;
        IsExpandable = source?.HasChildren == true;
        HasChanged = hasChanged;
        IsError = isError;
        IsRemovable = isRemovable;
    }

    /// <summary>The name: a variable, a member, an index such as <c>[0]</c>, or the expression of a watch.</summary>
    public string Name { get; }

    /// <summary>The value as the debugger formats it, or an error message.</summary>
    public string Value { get; }

    /// <summary>The type as C# writes it.</summary>
    public string Type { get; }

    /// <summary>The path that finds the value again, such as <c>order.Items[0]</c>, for Add Watch.</summary>
    public string Path { get; }

    /// <summary>How deep the row is in the tree; roots are 0.</summary>
    public int Depth { get; }

    /// <summary>The left indentation, in pixels.</summary>
    public double IndentWidth => 4 + (Depth * IndentPerLevel);

    /// <summary>Whether the value has members or items to show.</summary>
    public bool IsExpandable { get; }

    /// <summary>Whether the value differs from the last time the program paused, which colors it, as in Visual Studio.</summary>
    public bool HasChanged { get; }

    /// <summary>Whether <see cref="Value"/> is an error message rather than a value.</summary>
    public bool IsError { get; }

    /// <summary>Whether the row has a remove button.</summary>
    public bool IsRemovable { get; }

    /// <summary>Whether the children are shown.</summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    /// <summary>The tree that shows the row, whose commands the row's buttons and menu use.</summary>
    public VariableTreeViewModel? Owner { get; internal set; }

    /// <summary>The value this row shows.</summary>
    internal DebugValue? Source { get; }

    /// <summary>The children, once the row was first expanded.</summary>
    internal IReadOnlyList<VariableViewModel>? Children { get; set; }

    /// <summary>
    /// Reads a value of the paused program into a row. Reading may run the program's code (a <c>ToString</c>
    /// override, a property getter), so call it off the UI thread.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="name">The name to show.</param>
    /// <param name="path">The path that finds it again.</param>
    /// <param name="depth">How deep the row is.</param>
    /// <param name="previousValue">Its value at the last pause, if it was shown then.</param>
    /// <param name="isRemovable">Whether the row has a remove button.</param>
    internal static VariableViewModel Read(DebugValue value, string name, string path, int depth, string? previousValue, bool isRemovable = false)
    {
        var text = value.Value;
        var type = value.Type;
        var changed = previousValue is not null && !string.Equals(previousValue, text, StringComparison.Ordinal);
        return new VariableViewModel(name, text, type, path, depth, value, changed, isError: false, isRemovable);
    }

    /// <summary>A row that says why there is no value.</summary>
    /// <param name="name">The name to show.</param>
    /// <param name="message">The reason.</param>
    /// <param name="depth">How deep the row is.</param>
    /// <param name="isRemovable">Whether the row has a remove button.</param>
    internal static VariableViewModel Failed(string name, string message, int depth, bool isRemovable = false) =>
        new(name, message, string.Empty, name, depth, source: null, hasChanged: false, isError: true, isRemovable);

    /// <summary>The path of a child: <c>order.Total</c> for a member, <c>items[0]</c> for an item.</summary>
    /// <param name="parent">The parent's path.</param>
    /// <param name="child">The child's name.</param>
    internal static string ChildPath(string parent, string child) =>
        child.StartsWith('[') ? $"{parent}{child}" : $"{parent}.{child}";
}
