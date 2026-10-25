namespace TabStudio.Compilation.Features;

/// <summary>One overload shown by signature help.</summary>
/// <param name="Prefix">What comes before the parameters, such as <c>string Greeter.Greet(</c>.</param>
/// <param name="Parameters">The parameters. Show them separated by <c>", "</c>.</param>
/// <param name="Suffix">What comes after the parameters, such as <c>)</c>.</param>
/// <param name="Documentation">The summary from the documentation comment, or an empty string.</param>
/// <param name="ActiveParameter">The parameter the caret is on in this overload, or -1 when there is none.</param>
/// <param name="IsVariadic">Whether the last parameter is a <c>params</c> array.</param>
public sealed record SignatureItem(
    IReadOnlyList<TaggedRun> Prefix,
    IReadOnlyList<SignatureParameter> Parameters,
    IReadOnlyList<TaggedRun> Suffix,
    string Documentation,
    int ActiveParameter,
    bool IsVariadic)
{
    /// <summary>The whole signature as plain text.</summary>
    public string Label =>
        string.Concat(Prefix.Select(r => r.Text))
        + string.Join(", ", Parameters.Select(p => string.Concat(p.Display.Select(r => r.Text))))
        + string.Concat(Suffix.Select(r => r.Text));
}
