using TabStudio.Notebooks.Outputs;

namespace TabStudio.Notebooks.Formatting;

/// <summary>
/// A value as a notebook shows it: its representations by MIME type, and a table when it is a collection or an
/// object.
/// </summary>
/// <param name="Data">The representations. <c>text/plain</c> is always there; tables also have <c>text/html</c>.</param>
/// <param name="Table">The value as a table, for native views, or <see langword="null"/> for simple values.</param>
public sealed record FormattedValue(MimeBundle Data, TableOutput? Table = null);
