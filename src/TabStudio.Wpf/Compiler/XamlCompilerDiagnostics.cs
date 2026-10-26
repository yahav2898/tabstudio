using TabStudio.Compilation.Wpf;
using TabStudio.Core.Diagnostics;

namespace TabStudio.Wpf.Compiler;

/// <summary>Turns what OpenSilver's XAML compiler logged into Error List entries in the project's XAML files.</summary>
internal static class XamlCompilerDiagnostics
{
    /// <summary>The compiler's errors and warnings, in the files the project has, with their offsets.</summary>
    /// <param name="problems">What the compiler logged.</param>
    /// <param name="request">The pass, whose copies of files are reported as the project's files.</param>
    /// <param name="projectName">The project.</param>
    public static IReadOnlyList<BuildDiagnostic> Convert(IReadOnlyList<LoggedProblem> problems, XamlCompilerRequest request, string projectName)
    {
        var originals = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in request.Files)
        {
            originals[file.Item.FilePath] = file.Item.FilePath;
            if (file.ContentPath is { } copy)
            {
                originals[copy] = file.Item.FilePath;
            }
        }

        var texts = new Dictionary<string, string?>(StringComparer.Ordinal);
        var diagnostics = new List<BuildDiagnostic>();
        foreach (var problem in problems)
        {
            var severity = problem.IsError ? DiagnosticSeverityLevel.Error : DiagnosticSeverityLevel.Warning;
            var id = problem.Code is { Length: > 0 } code ? code : WpfDiagnosticIds.XamlError;
            var message = Clean(problem.Message);
            DiagnosticLocation? location = null;
            if (problem.File is { Length: > 0 } file)
            {
                var path = Path.IsPathRooted(file) ? file : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(request.ProjectFilePath)!, file));
                path = originals.GetValueOrDefault(path, path);
                if (!texts.TryGetValue(path, out var text))
                {
                    text = Read(path);
                    texts[path] = text;
                }

                location = Locate(path, text, problem.Line, problem.Column);
            }

            diagnostics.Add(new BuildDiagnostic(id, severity, message, location, projectName));
        }

        return diagnostics;
    }

    /// <summary>A one-based line and column as a location with its offset, or the file's start when they are not given.</summary>
    internal static DiagnosticLocation Locate(string path, string? text, int line, int column)
    {
        line = Math.Max(line, 1);
        column = Math.Max(column, 1);
        if (text is null)
        {
            return new DiagnosticLocation(path, 0, 0, line, column);
        }

        var offset = 0;
        for (var current = 1; current < line && offset < text.Length; current++)
        {
            var next = text.IndexOf('\n', offset);
            if (next < 0)
            {
                break;
            }

            offset = next + 1;
        }

        offset = Math.Min(offset + column - 1, text.Length);

        // The span is the name or word at the position, so the squiggle has something to underline.
        var end = offset;
        while (end < text.Length && !char.IsWhiteSpace(text[end]) && text[end] is not ('>' or '/' or '=' or '"' or '\''))
        {
            end++;
        }

        return new DiagnosticLocation(path, offset, end - offset, line, column);
    }

    /// <summary>The message without the trailing line breaks and the inner exceptions' repeats the task joins in.</summary>
    private static string Clean(string message)
    {
        var lines = message.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(" ", lines.Distinct(StringComparer.Ordinal));
    }

    private static string? Read(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
