namespace TabStudio.Debugging.Inspection;

/// <summary>
/// The language a statement is written in, which decides how its values, types and breakpoint conditions read. A debug
/// build may mix C# and Visual Basic projects, so it is decided per source file, which also holds for builds made
/// elsewhere, such as by a TabStudio Link host.
/// </summary>
internal static class SourceLanguage
{
    /// <summary>The extension of Visual Basic source files.</summary>
    private const string VisualBasicExtension = ".vb";

    /// <summary>Whether a source file is Visual Basic.</summary>
    /// <param name="filePath">The file, or <see langword="null"/> for an unknown location, which counts as C#.</param>
    public static bool IsVisualBasic(string? filePath) =>
        filePath is not null && filePath.EndsWith(VisualBasicExtension, StringComparison.OrdinalIgnoreCase);
}
