using System.Buffers;
using TabStudio.Compilation.Features;
using TabStudio.Core.Diagnostics;
using TabStudio.Debugging.Symbols;

namespace TabStudio.Link.Language;

/// <summary>
/// Maps paths between the tablet's workspace and the host's mirror of it. The host keeps the mirror in a folder of
/// its own, possibly on another operating system (an Android tablet's <c>/data/…</c> paths on a Windows PC), so
/// every path that crosses the link is mapped: requests on the way in, results on the way out.
/// </summary>
public sealed class LinkPathMap
{
    private static readonly SearchValues<char> InvalidNameCharacters = SearchValues.Create(Path.GetInvalidFileNameChars());

    private readonly string _clientPrefix;
    private readonly string _hostPrefix;

    /// <summary>Creates a map.</summary>
    /// <param name="clientRoot">The folder on the tablet that contains the whole workspace.</param>
    /// <param name="clientSeparator">The tablet's directory separator.</param>
    /// <param name="hostRoot">The folder on the host that mirrors it.</param>
    public LinkPathMap(string clientRoot, char clientSeparator, string hostRoot)
    {
        ArgumentException.ThrowIfNullOrEmpty(clientRoot);
        ArgumentException.ThrowIfNullOrEmpty(hostRoot);
        ClientRoot = clientRoot;
        ClientSeparator = clientSeparator;
        HostRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(hostRoot));
        _clientPrefix = WithSeparator(clientRoot, clientSeparator);
        _hostPrefix = WithSeparator(HostRoot, Path.DirectorySeparatorChar);
    }

    /// <summary>The folder on the tablet that contains the whole workspace.</summary>
    public string ClientRoot { get; }

    /// <summary>The tablet's directory separator.</summary>
    public char ClientSeparator { get; }

    /// <summary>The folder on the host that mirrors it.</summary>
    public string HostRoot { get; }

    /// <summary>
    /// The compiler's path map, so PDBs and <c>[CallerFilePath]</c> name the tablet's files: the host's prefix and the
    /// tablet's.
    /// </summary>
    public KeyValuePair<string, string> CompilerPathMap => new(_hostPrefix, _clientPrefix);

    /// <summary>The host's path for a tablet path.</summary>
    /// <param name="clientPath">A full path on the tablet.</param>
    /// <returns>The path in the mirror, or <see langword="null"/> when the path is outside the workspace or unsafe.</returns>
    public string? ToHost(string clientPath)
    {
        ArgumentNullException.ThrowIfNull(clientPath);
        if (string.Equals(clientPath, ClientRoot, StringComparison.Ordinal) || string.Equals(clientPath, _clientPrefix, StringComparison.Ordinal))
        {
            return HostRoot;
        }

        return clientPath.StartsWith(_clientPrefix, StringComparison.Ordinal)
            ? Resolve(clientPath[_clientPrefix.Length..].Split(ClientSeparator))
            : null;
    }

    /// <summary>The host's path for a path relative to the workspace's root, with '/' as separator, as file lists carry it.</summary>
    /// <param name="relativePath">The relative path.</param>
    /// <returns>The path in the mirror, or <see langword="null"/> when the path would leave the mirror or is invalid on the host.</returns>
    public string? RelativeToHost(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        return Resolve(relativePath.Split('/'));
    }

    /// <summary>The tablet's path for a host path. Paths outside the mirror, such as metadata, are returned as they are.</summary>
    /// <param name="hostPath">A full path on the host.</param>
    public string ToClient(string hostPath)
    {
        ArgumentNullException.ThrowIfNull(hostPath);
        if (string.Equals(hostPath, HostRoot, StringComparison.Ordinal))
        {
            return ClientRoot;
        }

        if (!hostPath.StartsWith(_hostPrefix, StringComparison.Ordinal))
        {
            return hostPath;
        }

        var relative = hostPath[_hostPrefix.Length..];
        if (Path.DirectorySeparatorChar != ClientSeparator)
        {
            relative = relative.Replace(Path.DirectorySeparatorChar, ClientSeparator);
        }

        if (Path.AltDirectorySeparatorChar != ClientSeparator && Path.AltDirectorySeparatorChar != Path.DirectorySeparatorChar)
        {
            relative = relative.Replace(Path.AltDirectorySeparatorChar, ClientSeparator);
        }

        return _clientPrefix + relative;
    }

    /// <summary>A diagnostic with its file and message on the tablet's paths.</summary>
    /// <param name="diagnostic">A diagnostic from the host.</param>
    public BuildDiagnostic ToClient(BuildDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        var message = diagnostic.Message.Replace(_hostPrefix, _clientPrefix, StringComparison.Ordinal);
        return diagnostic with
        {
            Message = message,
            Location = diagnostic.Location is { } location ? location with { FilePath = ToClient(location.FilePath) } : null,
        };
    }

    /// <summary>Diagnostics on the tablet's paths.</summary>
    /// <param name="diagnostics">Diagnostics from the host.</param>
    public IReadOnlyList<BuildDiagnostic> ToClient(IReadOnlyList<BuildDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        return [.. diagnostics.Select(d => ToClient(d))];
    }

    /// <summary>A definition on the tablet's paths.</summary>
    /// <param name="definition">A definition from the host.</param>
    public DefinitionResult ToClient(DefinitionResult definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition with { Locations = [.. definition.Locations.Select(l => l with { FilePath = ToClient(l.FilePath) })] };
    }

    /// <summary>A reference on the tablet's paths.</summary>
    /// <param name="reference">A reference from the host.</param>
    public ReferenceItem ToClient(ReferenceItem reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return reference with { FilePath = ToClient(reference.FilePath) };
    }

    /// <summary>A document edit on the tablet's paths.</summary>
    /// <param name="edit">An edit from the host.</param>
    public DocumentEdit ToClient(DocumentEdit edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        return edit with { FilePath = ToClient(edit.FilePath) };
    }

    /// <summary>A rename on the tablet's paths.</summary>
    /// <param name="rename">A rename from the host.</param>
    public RenameResult ToClient(RenameResult rename)
    {
        ArgumentNullException.ThrowIfNull(rename);
        return rename with { Edits = [.. rename.Edits.Select(e => ToClient(e))], Conflicts = ToClient(rename.Conflicts) };
    }

    /// <summary>A debugger statement on the tablet's paths.</summary>
    /// <param name="statement">A statement from the host's debug build.</param>
    public DebugStatement ToClient(DebugStatement statement)
    {
        ArgumentNullException.ThrowIfNull(statement);
        return statement with { Location = statement.Location with { FilePath = ToClient(statement.Location.FilePath) } };
    }

    /// <summary>A debugger method on the tablet's paths.</summary>
    /// <param name="method">A method from the host's debug build.</param>
    public DebugMethod ToClient(DebugMethod method)
    {
        ArgumentNullException.ThrowIfNull(method);
        return method with { Location = method.Location with { FilePath = ToClient(method.Location.FilePath) } };
    }

    /// <summary>A path with a separator at its end.</summary>
    private static string WithSeparator(string path, char separator) =>
        path.EndsWith(separator) ? path : path + separator;

    /// <summary>The mirror path of relative segments, refusing any that would leave the mirror or are invalid here.</summary>
    private string? Resolve(string[] segments)
    {
        foreach (var segment in segments)
        {
            if (segment.Length == 0 || segment is "." or ".." || segment.AsSpan().IndexOfAny(InvalidNameCharacters) >= 0)
            {
                return null;
            }
        }

        return Path.Combine([HostRoot, .. segments]);
    }
}
