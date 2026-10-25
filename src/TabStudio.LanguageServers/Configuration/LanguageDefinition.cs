namespace TabStudio.LanguageServers.Configuration;

/// <summary>A language, as the protocols name it, and the files that are written in it.</summary>
/// <param name="Id">The language identifier the protocols use, such as <c>python</c> or <c>typescriptreact</c>.</param>
/// <param name="Name">The name shown to the user, such as <c>Python</c>.</param>
/// <param name="Extensions">The file extensions, with their dot, such as <c>.py</c>.</param>
public sealed record LanguageDefinition(string Id, string Name, IReadOnlyList<string> Extensions)
{
    /// <summary>Whether a file is in this language, by its extension.</summary>
    /// <param name="filePath">A file name or path.</param>
    public bool Matches(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        var extension = Path.GetExtension(filePath);
        return extension.Length > 0 && Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }
}
