using System.Buffers;
using System.Text;
using TabStudio.Core.Diagnostics;
using TabStudio.Link.Language;

namespace TabStudio.WindowsBridge.Builds;

/// <summary>
/// Maps paths between the tablet's workspace and the bridge's mirror of it, in paths, in free text such as build
/// output and debugger messages, and in MSBuild's errors and warnings, which become Error List entries on the
/// tablet's files. Windows paths are compared without case, as Windows does.
/// </summary>
public sealed class BridgePathMapper
{
    private static readonly SearchValues<char> PathEnd = SearchValues.Create("\"'()[]<>|\r\n\t*?:");

    private readonly LinkPathMap _map;
    private readonly StringComparison _hostComparison;
    private readonly string _hostPrefix;
    private readonly string _clientPrefix;
    private readonly Dictionary<string, string?> _texts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates a mapper.</summary>
    /// <param name="map">The workspace's path map.</param>
    /// <param name="hostIgnoresCase">Whether the host's file system ignores case, as Windows does.</param>
    public BridgePathMapper(LinkPathMap map, bool hostIgnoresCase)
    {
        ArgumentNullException.ThrowIfNull(map);
        _map = map;
        _hostComparison = hostIgnoresCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        _hostPrefix = map.CompilerPathMap.Key;
        _clientPrefix = map.CompilerPathMap.Value;
    }

    /// <summary>Creates a mapper for this machine's file system.</summary>
    /// <param name="map">The workspace's path map.</param>
    public static BridgePathMapper ForThisMachine(LinkPathMap map) => new(map, OperatingSystem.IsWindows() || OperatingSystem.IsMacOS());

    /// <summary>The workspace's path map.</summary>
    public LinkPathMap Map => _map;

    /// <summary>The mirror's root on this machine.</summary>
    public string HostRoot => _map.HostRoot;

    /// <summary>The bridge's path for a tablet path, or <see langword="null"/> when it is outside the workspace or unsafe.</summary>
    /// <param name="clientPath">A full path on the tablet.</param>
    public string? ToHost(string clientPath) => _map.ToHost(clientPath);

    /// <summary>The tablet's path for a bridge path; paths outside the mirror are returned as they are.</summary>
    /// <param name="hostPath">A full path on the bridge.</param>
    public string ToClient(string hostPath)
    {
        ArgumentNullException.ThrowIfNull(hostPath);
        if (string.Equals(Path.TrimEndingDirectorySeparator(hostPath), _map.HostRoot, _hostComparison))
        {
            return _map.ClientRoot;
        }

        return hostPath.StartsWith(_hostPrefix, _hostComparison)
            ? _clientPrefix + ToClientSeparators(hostPath[_hostPrefix.Length..])
            : hostPath;
    }

    /// <summary>
    /// Free text, such as a line of build output, with the mirror's paths in it replaced by the tablet's. A path runs
    /// from the mirror's root to the first character that cannot be in one here, such as a parenthesis or a quote.
    /// </summary>
    /// <param name="text">The text.</param>
    public string ToClientText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var start = text.IndexOf(_hostPrefix, _hostComparison);
        if (start < 0)
        {
            return text;
        }

        var result = new StringBuilder(text.Length);
        var copied = 0;
        while (start >= 0)
        {
            result.Append(text, copied, start - copied);
            var rest = start + _hostPrefix.Length;
            var end = EndOfPath(text, rest);
            result.Append(_clientPrefix).Append(ToClientSeparators(text[rest..end]));
            copied = end;
            start = end < text.Length ? text.IndexOf(_hostPrefix, end, _hostComparison) : -1;
        }

        return result.Append(text, copied, text.Length - copied).ToString();
    }

    /// <summary>Free text with the tablet's paths in it replaced by the mirror's, such as a debugger's expression.</summary>
    /// <param name="text">The text.</param>
    public string ToHostText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var start = text.IndexOf(_clientPrefix, StringComparison.Ordinal);
        if (start < 0)
        {
            return text;
        }

        var result = new StringBuilder(text.Length);
        var copied = 0;
        while (start >= 0)
        {
            result.Append(text, copied, start - copied);
            var rest = start + _clientPrefix.Length;
            var end = EndOfPath(text, rest);
            result.Append(_hostPrefix).Append(text.AsSpan(rest, end - rest).ToString().Replace(_map.ClientSeparator, Path.DirectorySeparatorChar));
            copied = end;
            start = end < text.Length ? text.IndexOf(_clientPrefix, end, StringComparison.Ordinal) : -1;
        }

        return result.Append(text, copied, text.Length - copied).ToString();
    }

    /// <summary>
    /// An MSBuild error or warning as an Error List entry on the tablet's file: its path mapped, its line and column
    /// turned into a position in the file's text, its message's paths mapped.
    /// </summary>
    /// <param name="message">The error or warning.</param>
    /// <param name="projectDirectory">The folder of the project that was built, on the bridge, for relative paths.</param>
    /// <param name="projectName">The project's name, for the Error List's project column.</param>
    public BuildDiagnostic ToDiagnostic(MsBuildMessage message, string projectDirectory, string? projectName)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(projectDirectory);
        var code = message.Code.Length > 0 ? message.Code : (message.HasFile ? "MSBUILD" : message.Origin.Length > 0 ? message.Origin.ToUpperInvariant() : "MSBUILD");
        var text = ToClientText(message.Text);
        DiagnosticLocation? location = null;
        if (message.HasFile)
        {
            var hostFile = Path.IsPathRooted(message.Origin) ? message.Origin : Path.GetFullPath(Path.Combine(projectDirectory, message.Origin));
            var clientFile = ToClient(hostFile);
            var line = Math.Max(1, message.Line);
            var column = Math.Max(1, message.Column);
            location = new DiagnosticLocation(clientFile, OffsetOf(hostFile, line, column), 0, line, column);
        }

        return new BuildDiagnostic(code, message.Severity, text, location, projectName);
    }

    /// <summary>
    /// Where a path ends in free text: before a character no path in the mirror has, such as a colon, or a comma and a
    /// space. Parentheses and apostrophes can be in a folder's name ("My App (x86)", "Yahav's"): a parenthesis ends
    /// the path when it is MSBuild's "(line,column)" or closes one opened before the path; an apostrophe ends it when
    /// no letter or digit follows, as a closing quote does.
    /// </summary>
    private static int EndOfPath(string text, int from)
    {
        var depth = 0;
        for (var i = from; i < text.Length; i++)
        {
            var c = text[i];
            switch (c)
            {
                case '(' when !IsLocation(text, i + 1):
                    depth++;
                    continue;
                case ')' when depth > 0:
                    depth--;
                    continue;
                case '\'' when i + 1 < text.Length && char.IsLetterOrDigit(text[i + 1]):
                    continue;
            }

            if (PathEnd.Contains(c) || (c == ',' && i + 1 < text.Length && text[i + 1] == ' '))
            {
                return i;
            }
        }

        return text.Length;
    }

    // "12)" or "12,5)" or "12,5,12,9)": the position MSBuild writes after a file.
    private static bool IsLocation(string text, int from)
    {
        var i = from;
        var digits = 0;
        for (; i < text.Length && (char.IsAsciiDigit(text[i]) || (text[i] == ',' && digits > 0)); i++)
        {
            digits += char.IsAsciiDigit(text[i]) ? 1 : 0;
        }

        return digits > 0 && i < text.Length && text[i] == ')';
    }

    private string ToClientSeparators(string relative)
    {
        var result = relative;
        if (Path.DirectorySeparatorChar != _map.ClientSeparator)
        {
            result = result.Replace(Path.DirectorySeparatorChar, _map.ClientSeparator);
        }

        if (Path.AltDirectorySeparatorChar != _map.ClientSeparator)
        {
            result = result.Replace(Path.AltDirectorySeparatorChar, _map.ClientSeparator);
        }

        return result;
    }

    /// <summary>The character offset of a line and column in a file of the mirror, which holds the tablet's text.</summary>
    private int OffsetOf(string hostFile, int line, int column)
    {
        if (!_texts.TryGetValue(hostFile, out var text))
        {
            try
            {
                text = File.Exists(hostFile) ? File.ReadAllText(hostFile) : null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                text = null;
            }

            _texts[hostFile] = text;
        }

        if (text is null)
        {
            return 0;
        }

        var offset = 0;
        for (var current = 1; current < line; current++)
        {
            var next = text.IndexOf('\n', offset);
            if (next < 0)
            {
                return text.Length;
            }

            offset = next + 1;
        }

        var lineEnd = text.IndexOf('\n', offset);
        var lineLength = (lineEnd < 0 ? text.Length : lineEnd) - offset;
        return offset + Math.Min(column - 1, Math.Max(0, lineLength));
    }
}
