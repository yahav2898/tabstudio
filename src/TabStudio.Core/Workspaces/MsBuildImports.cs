using System.Text.RegularExpressions;

namespace TabStudio.Core.Workspaces;

/// <summary>
/// Works out which file an <c>&lt;Import Project="..." /&gt;</c> names without running MSBuild. The path may use
/// <c>$(MSBuildThisFile…)</c>, the properties known at the import, and the two property functions repositories chain
/// their <c>Directory.Build.props</c> and <c>Directory.Packages.props</c> files with:
/// <c>$([MSBuild]::GetPathOfFileAbove(file, startDirectory))</c> and
/// <c>$([MSBuild]::GetDirectoryNameOfFileAbove(startDirectory, file))</c>, as Refit's
/// <c>&lt;Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" /&gt;</c>.
/// </summary>
/// <remarks>
/// As in MSBuild, both functions look for the file in the start directory and then in each of its parents, and give an
/// empty string when there is none; <c>GetPathOfFileAbove</c> without a start directory starts in the folder of the
/// file that calls it. A property an argument names that nothing sets is empty there, as in MSBuild. Any other property
/// function, and a property the path itself names that nothing sets, make the path one that cannot be worked out.
/// </remarks>
public static partial class MsBuildImports
{
    private const int MaxExpansionDepth = 8;

    private const string FunctionPrefix = "$([MSBuild]::";

    private static readonly char[] Wildcards = ['*', '?'];

    private static readonly char[] Separators = ['/', '\\'];

    /// <summary>The full path an import's <c>Project</c> names.</summary>
    /// <param name="project">The import's <c>Project</c> attribute, as written.</param>
    /// <param name="importingFile">
    /// Full path of the file with the import: what <c>$(MSBuildThisFile…)</c> means, and the folder a relative path is
    /// relative to.
    /// </param>
    /// <param name="properties">The properties known at the import. Keys are case-insensitive.</param>
    /// <returns>
    /// The full path, which may not exist; <see langword="null"/> when the path cannot be worked out (see
    /// <see cref="Expand"/>), names files by a wildcard, or is empty, as when <c>GetPathOfFileAbove</c> finds nothing.
    /// </returns>
    public static string? ResolvePath(string project, string importingFile, IReadOnlyDictionary<string, string> properties)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(importingFile);
        ArgumentNullException.ThrowIfNull(properties);

        var expanded = Expand(project, importingFile, properties)?.Trim();
        if (string.IsNullOrEmpty(expanded) || expanded.IndexOfAny(Wildcards) >= 0)
        {
            return null;
        }

        return FullPath(Path.GetDirectoryName(importingFile) ?? string.Empty, expanded);
    }

    /// <summary>
    /// Expands <c>$(MSBuildThisFile…)</c>, the known properties (a few levels deep) and calls of
    /// <c>GetPathOfFileAbove</c> and <c>GetDirectoryNameOfFileAbove</c> in a text such as an import's path.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="file">Full path of the file the text is in, which <c>$(MSBuildThisFile…)</c> means.</param>
    /// <param name="properties">The properties known there. Keys are case-insensitive.</param>
    /// <returns>
    /// The expanded text; <see langword="null"/> when it names a property nothing sets outside a function's arguments,
    /// or calls another property function.
    /// </returns>
    public static string? Expand(string text, string file, IReadOnlyDictionary<string, string> properties)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(properties);

        var expanded = EvaluateFunctions(ExpandProperties(ExpandThisFile(text, file), properties, unknownAsEmpty: false), file, properties);
        return expanded is null || expanded.Contains("$(", StringComparison.Ordinal) ? null : expanded;
    }

    /// <summary>
    /// Resolves the <c>$(MSBuildThisFile…)</c> properties, which mean the file that uses them:
    /// <c>$(MSBuildThisFileDirectory)</c> (with a trailing separator), <c>$(MSBuildThisFileFullPath)</c>,
    /// <c>$(MSBuildThisFileName)</c>, <c>$(MSBuildThisFileExtension)</c> and <c>$(MSBuildThisFile)</c>.
    /// </summary>
    /// <param name="text">The text, such as a condition or a path.</param>
    /// <param name="file">Full path of the file the text is in.</param>
    public static string ExpandThisFile(string text, string file)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(file);
        if (!text.Contains("$(MSBuildThisFile", StringComparison.OrdinalIgnoreCase))
        {
            return text;
        }

        var folder = (Path.GetDirectoryName(file) ?? string.Empty).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return text
            .Replace("$(MSBuildThisFileDirectory)", folder, StringComparison.OrdinalIgnoreCase)
            .Replace("$(MSBuildThisFileFullPath)", file, StringComparison.OrdinalIgnoreCase)
            .Replace("$(MSBuildThisFileName)", Path.GetFileNameWithoutExtension(file), StringComparison.OrdinalIgnoreCase)
            .Replace("$(MSBuildThisFileExtension)", Path.GetExtension(file), StringComparison.OrdinalIgnoreCase)
            .Replace("$(MSBuildThisFile)", Path.GetFileName(file), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// MSBuild's <c>GetPathOfFileAbove</c>: the full path of the file named <paramref name="fileName"/> in
    /// <paramref name="startDirectory"/> or the nearest of its parents that has one; empty when none does.
    /// </summary>
    /// <param name="fileName">A file name, without a folder.</param>
    /// <param name="startDirectory">The full path of the folder to start in.</param>
    internal static string GetPathOfFileAbove(string fileName, string startDirectory)
    {
        var directory = GetDirectoryNameOfFileAbove(startDirectory, fileName);
        return directory.Length == 0 ? string.Empty : Path.Combine(directory, fileName);
    }

    /// <summary>
    /// MSBuild's <c>GetDirectoryNameOfFileAbove</c>: <paramref name="startDirectory"/> or the nearest of its parents
    /// that has the file <paramref name="fileName"/>; empty when none does.
    /// </summary>
    /// <param name="startDirectory">The full path of the folder to start in.</param>
    /// <param name="fileName">The file, a name or a path relative to each folder.</param>
    internal static string GetDirectoryNameOfFileAbove(string startDirectory, string fileName)
    {
        for (string? directory = Path.TrimEndingDirectorySeparator(startDirectory); !string.IsNullOrEmpty(directory); directory = Path.GetDirectoryName(directory))
        {
            if (File.Exists(Path.Combine(directory, fileName)))
            {
                return directory;
            }
        }

        return string.Empty;
    }

    /// <summary>Replaces <c>$(Name)</c> with the known properties, a few levels deep; an unknown one stays or becomes empty.</summary>
    private static string ExpandProperties(string text, IReadOnlyDictionary<string, string> properties, bool unknownAsEmpty)
    {
        for (var depth = 0; depth < MaxExpansionDepth && text.Contains("$(", StringComparison.Ordinal); depth++)
        {
            var expanded = PropertyReference().Replace(text, match =>
                properties.TryGetValue(match.Groups["name"].Value, out var value) ? value : (unknownAsEmpty ? string.Empty : match.Value));
            if (string.Equals(expanded, text, StringComparison.Ordinal))
            {
                break;
            }

            text = expanded;
        }

        return text;
    }

    /// <summary>Replaces each <c>$([MSBuild]::Function(...))</c> with its value; <see langword="null"/> for one this does not evaluate.</summary>
    private static string? EvaluateFunctions(string text, string file, IReadOnlyDictionary<string, string> properties)
    {
        var start = text.IndexOf(FunctionPrefix, StringComparison.OrdinalIgnoreCase);
        while (start >= 0)
        {
            var nameStart = start + FunctionPrefix.Length;
            var open = text.IndexOf('(', nameStart);
            var close = open < 0 ? -1 : ClosingParenthesis(text, open);
            if (close < 0 || close + 1 >= text.Length || text[close + 1] != ')')
            {
                return null;
            }

            var arguments = Arguments(text[(open + 1)..close], properties);
            var value = arguments is null ? null : Call(text[nameStart..open].Trim(), arguments, file, properties);
            if (value is null)
            {
                return null;
            }

            text = string.Concat(text.AsSpan(0, start), value, text.AsSpan(close + 2));
            start = text.IndexOf(FunctionPrefix, start + value.Length, StringComparison.OrdinalIgnoreCase);
        }

        return text;
    }

    /// <summary>The index of the parenthesis that closes the one at <paramref name="open"/>, outside quotes; -1 when there is none.</summary>
    private static int ClosingParenthesis(string text, int open)
    {
        var depth = 0;
        var quote = '\0';
        for (var i = open; i < text.Length; i++)
        {
            var c = text[i];
            if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }
            }
            else if (c is '\'' or '"' or '`')
            {
                quote = c;
            }
            else if (c == '(')
            {
                depth++;
            }
            else if (c == ')' && --depth == 0)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// A function's arguments, split on the commas outside quotes and parentheses, with their properties expanded (an
    /// unknown one is empty, as in MSBuild) and their quotes removed; <see langword="null"/> when one calls a function.
    /// </summary>
    private static List<string>? Arguments(string text, IReadOnlyDictionary<string, string> properties)
    {
        var arguments = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return arguments;
        }

        var start = 0;
        var depth = 0;
        var quote = '\0';
        for (var i = 0; i <= text.Length; i++)
        {
            var c = i < text.Length ? text[i] : ',';
            if (i == text.Length && (quote != '\0' || depth != 0))
            {
                // An unclosed quote or parenthesis.
                return null;
            }

            if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }
            }
            else if (c is '\'' or '"' or '`')
            {
                quote = c;
            }
            else if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                depth--;
            }
            else if (c == ',' && depth == 0)
            {
                var argument = ExpandProperties(text[start..i].Trim(), properties, unknownAsEmpty: true);
                if (argument.Contains("$(", StringComparison.Ordinal))
                {
                    return null;
                }

                arguments.Add(Unquote(argument));
                start = i + 1;
            }
        }

        return arguments;
    }

    private static string Unquote(string argument) =>
        argument.Length >= 2 && argument[0] is '\'' or '"' or '`' && argument[^1] == argument[0] ? argument[1..^1] : argument;

    /// <summary>The value of a call of <c>GetPathOfFileAbove</c> or <c>GetDirectoryNameOfFileAbove</c>; <see langword="null"/> for anything else.</summary>
    private static string? Call(string name, List<string> arguments, string file, IReadOnlyDictionary<string, string> properties)
    {
        var folder = Path.GetDirectoryName(file) ?? string.Empty;
        if (string.Equals(name, "GetPathOfFileAbove", StringComparison.OrdinalIgnoreCase) && arguments.Count is 1 or 2)
        {
            // MSBuild accepts only a file name here.
            var fileName = arguments[0].Trim();
            var start = arguments.Count == 2 ? StartDirectory(arguments[1], properties, folder) : folder;
            return fileName.Length == 0 || fileName.IndexOfAny(Separators) >= 0 || start is null ? null : GetPathOfFileAbove(fileName, start);
        }

        if (string.Equals(name, "GetDirectoryNameOfFileAbove", StringComparison.OrdinalIgnoreCase) && arguments.Count == 2)
        {
            var fileName = arguments[1].Trim().Replace('\\', Path.DirectorySeparatorChar);
            var start = StartDirectory(arguments[0], properties, folder);
            return fileName.Length == 0 || start is null ? null : GetDirectoryNameOfFileAbove(start, fileName);
        }

        return null;
    }

    /// <summary>
    /// The full path of a function's start directory. A relative one is relative to the project's folder, which is
    /// MSBuild's current directory while it evaluates, or else to the file's.
    /// </summary>
    private static string? StartDirectory(string directory, IReadOnlyDictionary<string, string> properties, string fileFolder)
    {
        var trimmed = directory.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        var baseDirectory = properties.TryGetValue("MSBuildProjectDirectory", out var projectDirectory) && projectDirectory.Length > 0
            ? projectDirectory
            : fileFolder;
        return FullPath(baseDirectory, trimmed);
    }

    private static string? FullPath(string baseDirectory, string path)
    {
        try
        {
            return Path.GetFullPath(Path.Combine(baseDirectory, path.Replace('\\', Path.DirectorySeparatorChar)));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"\$\((?<name>[A-Za-z_][A-Za-z0-9_.\-]*)\)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex PropertyReference();
}
