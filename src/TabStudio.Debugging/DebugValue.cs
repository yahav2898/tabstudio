using TabStudio.Debugging.Inspection;

namespace TabStudio.Debugging;

/// <summary>
/// A variable, field, property or collection item of the paused program, as the Locals panel shows it.
/// </summary>
/// <remarks>
/// <para>
/// Everything is computed lazily and cached: <see cref="Value"/> and <see cref="Type"/> on first read, children on
/// the first <see cref="Expand"/>. Reading them may run code of the program (a <c>ToString</c> override, a property
/// getter), which happens on a pool thread with a time limit, so call them from the IDE thread while the program
/// is paused. Values read after the program resumed may be out of date.
/// </para>
/// <para>Instances are thread-safe.</para>
/// </remarks>
public sealed class DebugValue
{
    private readonly Lazy<string> _value;
    private readonly Lazy<string> _type;
    private readonly Lazy<bool> _hasChildren;
    private readonly Lazy<IReadOnlyList<DebugValue>> _children;

    internal DebugValue(string name, string? declaredType, object? value, ValueInspector inspector)
    {
        Name = name;
        Object = value;
        _value = new Lazy<string>(() => inspector.FormatValue(value));
        _type = new Lazy<string>(() => ValueInspector.FormatType(value, declaredType, inspector.IsVisualBasic));
        _hasChildren = new Lazy<bool>(() => ValueInspector.HasChildren(value));
        _children = new Lazy<IReadOnlyList<DebugValue>>(() => inspector.GetChildren(value));
    }

    // Historical values contain no debuggee references and never invoke getters or ToString.
    internal DebugValue(string name, string type, string display, IReadOnlyList<DebugValue> children)
    {
        Name = name;
        IsHistorical = true;
        _value = new(() => display);
        _type = new(() => type);
        _hasChildren = new(() => children.Count > 0);
        _children = new(() => children);
    }

    /// <summary>Whether this is an immutable recorded value rather than a live object.</summary>
    public bool IsHistorical { get; }

    /// <summary>
    /// The name: a variable name, <c>this</c>, a member name or an index such as <c>[0]</c>; in Visual Basic code
    /// <c>Me</c> and indexes such as <c>(0)</c>.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The type as C# writes it, or as Visual Basic does for a value of Visual Basic code. When the runtime type is more
    /// specific than the declared one, it follows in braces, as in <c>object {string}</c> or <c>Object {String}</c>.
    /// </summary>
    public string Type => _type.Value;

    /// <summary>
    /// The value: strings and characters as C# literals, numbers and dates in the invariant culture, <c>null</c>,
    /// enum member names, <c>Count = n</c> for collections, <c>Length = n</c> for arrays, otherwise the
    /// <c>ToString</c> override or the type name in braces. A value of Visual Basic code reads as Visual Basic:
    /// <c>Nothing</c>, <c>True</c>, <c>"x"c</c>.
    /// </summary>
    public string Value => _value.Value;

    /// <summary>Whether <see cref="Expand"/> has anything to show. Never runs the program's code.</summary>
    public bool HasChildren => _hasChildren.Value;

    /// <summary>The object itself, for the debugger's own use.</summary>
    internal object? Object { get; }

    /// <summary>
    /// Returns the children: the first 100 items of an array or collection, the entries of a dictionary, or the
    /// public fields and properties of an object. Properties that throw, and indexers, are left out.
    /// </summary>
    public IReadOnlyList<DebugValue> Expand() => _children.Value;

    /// <inheritdoc />
    public override string ToString() => Name + " = " + Value;
}
