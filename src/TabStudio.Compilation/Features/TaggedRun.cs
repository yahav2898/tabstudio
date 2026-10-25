namespace TabStudio.Compilation.Features;

/// <summary>
/// A run of text in a tooltip, signature or completion description, with what it is so it can be colored like
/// the code in the editor.
/// </summary>
/// <param name="Text">The text.</param>
/// <param name="Kind">What the text is.</param>
public sealed record TaggedRun(string Text, TaggedTextKind Kind)
{
    /// <summary>
    /// The Roslyn classification name to color the run with, the same names
    /// <see cref="RoslynWorkspaceHost.GetClassificationsAsync"/> uses, or <see langword="null"/> for the default
    /// text color.
    /// </summary>
    public string? Classification => TaggedTextMapper.ToClassification(Kind);
}
