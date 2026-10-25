using TabStudio.Core.Diagnostics;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.Services.Ci;

/// <summary>A CI annotation shown in the Error List: the diagnostic, and the check and commit it came from.</summary>
/// <param name="Diagnostic">The diagnostic, located in the workspace's copy of the file.</param>
/// <param name="CheckName">The check that reported it, such as "Build and test".</param>
/// <param name="CommitSha">The commit CI checked.</param>
/// <param name="CheckUri">The check's page, if it has one.</param>
public sealed record CiDiagnostic(BuildDiagnostic Diagnostic, string CheckName, string CommitSha, Uri? CheckUri)
{
    /// <summary>The first seven characters of the commit's hash.</summary>
    public string ShortSha => CommitSha.Length > 7 ? CommitSha[..7] : CommitSha;
}

/// <summary>
/// Turns CI annotations into Error List entries. Annotation paths are relative to the repository's root; the
/// workspace may be that folder or one below it, and only files in the workspace are listed.
/// </summary>
public static class CiDiagnostics
{
    /// <summary>Maps the failure and warning annotations whose file is in the workspace. Reads the files, so call it off the UI thread.</summary>
    /// <param name="annotations">The annotations, with their check runs.</param>
    /// <param name="commitSha">The commit CI checked.</param>
    /// <param name="repositoryRoot">The folder linked to the repository's root.</param>
    /// <param name="workspaceRoot">The workspace's folder: the linked folder or one inside it.</param>
    /// <param name="projectOf">Names the project a file belongs to, if any.</param>
    public static IReadOnlyList<CiDiagnostic> Map(
        IReadOnlyList<CiAnnotation> annotations,
        string commitSha,
        string repositoryRoot,
        string workspaceRoot,
        Func<string, string?> projectOf)
    {
        ArgumentNullException.ThrowIfNull(annotations);
        ArgumentNullException.ThrowIfNull(projectOf);
        var repository = Path.GetFullPath(repositoryRoot);
        var workspace = Path.GetFullPath(workspaceRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var texts = new Dictionary<string, string?>(StringComparer.Ordinal);
        var seen = new HashSet<(string, int, string, DiagnosticSeverityLevel)>();
        var mapped = new List<CiDiagnostic>();
        foreach (var (check, annotation) in annotations)
        {
            if (annotation.Level == GitHubAnnotationLevel.Notice || FullPathOf(repository, annotation.Path) is not { } path
                || !path.StartsWith(workspace, StringComparison.Ordinal))
            {
                continue;
            }

            if (!texts.TryGetValue(path, out var text))
            {
                text = ReadText(path);
                texts[path] = text;
            }

            if (text is null)
            {
                continue;
            }

            var compiler = annotation.Compiler;
            var message = string.IsNullOrWhiteSpace(compiler.Message) ? annotation.Message : compiler.Message;
            var severity = annotation.Level == GitHubAnnotationLevel.Failure ? DiagnosticSeverityLevel.Error : DiagnosticSeverityLevel.Warning;
            if (!seen.Add((path, annotation.StartLine, message, severity)))
            {
                // The same problem reported by several jobs, such as one per platform.
                continue;
            }

            var location = Locate(path, text, annotation.StartLine, annotation.StartColumn ?? compiler.Column, annotation.EndLine, annotation.EndColumn);
            var diagnostic = new BuildDiagnostic(compiler.Code ?? string.Empty, severity, message, location, projectOf(path));
            mapped.Add(new CiDiagnostic(diagnostic, check.Name, commitSha, check.DetailsUri ?? check.WebUri));
        }

        return mapped;
    }

    /// <summary>The full path of a repository-relative path, or <see langword="null"/> when it is not a file in the repository.</summary>
    private static string? FullPathOf(string repositoryRoot, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || relativePath.Contains('\0', StringComparison.Ordinal))
        {
            return null;
        }

        var full = Path.GetFullPath(Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        return full.StartsWith(repositoryRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal) && File.Exists(full)
            ? full
            : null;
    }

    private static string? ReadText(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The location of a 1-based line and column in the file's text, clamped to the text.</summary>
    private static DiagnosticLocation Locate(string path, string text, int line, int? column, int endLine, int? endColumn)
    {
        var lineStart = 0;
        var current = 1;
        while (current < line)
        {
            var next = text.IndexOf('\n', lineStart);
            if (next < 0)
            {
                break;
            }

            lineStart = next + 1;
            current++;
        }

        var lineEnd = text.IndexOf('\n', lineStart);
        lineEnd = lineEnd < 0 ? text.Length : lineEnd;
        if (lineEnd > lineStart && text[lineEnd - 1] == '\r')
        {
            lineEnd--;
        }

        var start = Math.Clamp(lineStart + Math.Max(0, (column ?? 1) - 1), lineStart, lineEnd);
        var end = endLine == line && endColumn is { } last && last > (column ?? 1) ? Math.Clamp(lineStart + last - 1, start, lineEnd) : start;
        return new DiagnosticLocation(path, start, end - start, current, start - lineStart + 1);
    }
}
