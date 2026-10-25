namespace TabStudio.LanguageServers.Documents;

/// <summary>Converts between file paths and the <c>file:</c> URIs the protocols name documents with.</summary>
public static class DocumentUri
{
    /// <summary>How file paths compare on this platform: ignoring case on Windows, exactly elsewhere.</summary>
    public static StringComparer PathComparer { get; } = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>The URI of a file, with every path segment percent-encoded, as VS Code writes them.</summary>
    /// <param name="filePath">A file or folder path.</param>
    public static string FromPath(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        var full = Path.GetFullPath(filePath).Replace('\\', '/');
        var encoded = string.Join('/', full.Split('/').Select(segment => Uri.EscapeDataString(segment)));
        return encoded.StartsWith('/') ? "file://" + encoded : "file:///" + encoded;
    }

    /// <summary>The path of a <c>file:</c> URI, or <see langword="null"/> for any other kind of URI.</summary>
    /// <param name="value">The URI.</param>
    public static string? ToPath(string? value)
    {
        if (value is null || !value.StartsWith("file:", StringComparison.OrdinalIgnoreCase) || !Uri.TryCreate(value, UriKind.Absolute, out var parsed))
        {
            return null;
        }

        var path = parsed.LocalPath;
        return OperatingSystem.IsWindows() || path.Length == 0 ? path : Path.GetFullPath(path);
    }

    /// <summary>Whether two paths name the same file.</summary>
    /// <param name="first">A path.</param>
    /// <param name="second">Another path.</param>
    public static bool SamePath(string? first, string? second) =>
        first is not null && second is not null && PathComparer.Equals(Path.GetFullPath(first), Path.GetFullPath(second));
}
