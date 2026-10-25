namespace TabStudio.Notebooks.Outputs;

/// <summary>
/// A value laid out as a table of text, so a native view can show collections and objects without HTML.
/// </summary>
/// <param name="Columns">The column headers.</param>
/// <param name="Rows">The rows. Each row has one cell per column.</param>
public sealed record TableOutput(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows)
{
    /// <summary>Whether the value had more rows than were formatted.</summary>
    public bool IsTruncated { get; init; }

    /// <summary>The number of rows the value had, when it is known; it is not known for a lazy sequence that was cut short.</summary>
    public int? TotalRowCount { get; init; }
}
