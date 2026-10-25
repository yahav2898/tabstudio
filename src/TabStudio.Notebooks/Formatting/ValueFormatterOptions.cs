namespace TabStudio.Notebooks.Formatting;

/// <summary>
/// Limits that keep formatting a value fast and its output readable, even for huge or endless collections.
/// </summary>
public sealed record ValueFormatterOptions
{
    /// <summary>The most items of a collection to show as table rows. The rest are counted, not shown.</summary>
    public int MaxRows { get; init; } = 100;

    /// <summary>The most properties and fields of a type to show as table columns.</summary>
    public int MaxColumns { get; init; } = 50;

    /// <summary>The most items of a nested collection to list inside one table cell.</summary>
    public int MaxInlineItems { get; init; } = 10;

    /// <summary>The longest text a table cell may hold before it is cut short.</summary>
    public int MaxCellLength { get; init; } = 1000;
}
