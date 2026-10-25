using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using TabStudio.Agent.Anthropic;
using TabStudio.Core.Diagnostics;

namespace TabStudio.Agent.Tools;

/// <summary>
/// The agent's built-in tools over the workspace: list, read and search files; create, edit and delete them as
/// pending changes; check for errors; and run tests. Paths are relative to the workspace root.
/// </summary>
public sealed class WorkspaceTools
{
    private const int MaxListedFiles = 1000;
    private const int MaxReadLines = 2000;
    private const int MaxSearchMatches = 200;
    private const int MaxDiagnostics = 100;
    private const long MaxSearchedFileLength = 1024 * 1024;

    private readonly IAgentWorkspace _workspace;
    private readonly ChangeSet _changes;

    /// <summary>Creates the tools.</summary>
    /// <param name="workspace">The workspace.</param>
    /// <param name="changes">Where edits go until they are approved.</param>
    public WorkspaceTools(IAgentWorkspace workspace, ChangeSet changes)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(changes);
        _workspace = workspace;
        _changes = changes;
        All =
        [
            Tool("list_files", "List the files in the workspace, or in one folder of it. Use this to find your way around the solution.",
                """{"type":"object","properties":{"folder":{"type":"string","description":"A folder relative to the workspace root, such as src/App. Omit for the whole workspace."},"pattern":{"type":"string","description":"A glob such as **/*.cs."}}}""",
                input => $"List {Text(input, "folder") ?? "files"}", ListFilesAsync),
            Tool("read_file", "Read a file, with line numbers. Read a range of lines for long files.",
                """{"type":"object","properties":{"path":{"type":"string"},"start_line":{"type":"integer","minimum":1},"end_line":{"type":"integer","minimum":1}},"required":["path"]}""",
                input => $"Read {Text(input, "path")}", ReadFileAsync),
            Tool("search", "Search the workspace's files with a regular expression (.NET syntax). Returns path:line: text for each match.",
                """{"type":"object","properties":{"pattern":{"type":"string"},"folder":{"type":"string"},"file_pattern":{"type":"string","description":"A glob such as **/*.cs."},"ignore_case":{"type":"boolean"}},"required":["pattern"]}""",
                input => $"Search for {Text(input, "pattern")}", SearchAsync),
            Tool("edit_file", "Replace text in a file. old_text must match exactly once (include enough surrounding lines to make it unique) unless replace_all is true. The change waits for the user's approval; your later reads see it.",
                """{"type":"object","properties":{"path":{"type":"string"},"old_text":{"type":"string"},"new_text":{"type":"string"},"replace_all":{"type":"boolean"}},"required":["path","old_text","new_text"]}""",
                input => $"Edit {Text(input, "path")}", EditFileAsync),
            Tool("create_file", "Create a file, or replace a file's whole content. The change waits for the user's approval.",
                """{"type":"object","properties":{"path":{"type":"string"},"content":{"type":"string"}},"required":["path","content"]}""",
                input => $"Create {Text(input, "path")}", CreateFileAsync),
            Tool("delete_file", "Delete a file. The change waits for the user's approval.",
                """{"type":"object","properties":{"path":{"type":"string"}},"required":["path"]}""",
                input => $"Delete {Text(input, "path")}", DeleteFileAsync),
            Tool("get_diagnostics", "Compile the solution with your pending changes and list its errors and warnings, like the Error List. Run it after editing.",
                """{"type":"object","properties":{"path":{"type":"string","description":"Only diagnostics in this file or folder."}}}""",
                _ => "Check for errors", GetDiagnosticsAsync),
            Tool("run_tests", "Build the test projects with your pending changes and run their tests (xUnit, NUnit or MSTest).",
                """{"type":"object","properties":{"filter":{"type":"string","description":"A test, class or namespace name. Omit to run all tests."}}}""",
                input => Text(input, "filter") is { } filter ? $"Run tests in {filter}" : "Run all tests", RunTestsAsync),
        ];
    }

    /// <summary>The tools.</summary>
    public IReadOnlyList<AgentTool> All { get; }

    private async Task<ToolOutcome> ListFilesAsync(JsonObject input, CancellationToken cancellationToken)
    {
        var folder = Normalize(Text(input, "folder"));
        var glob = Text(input, "pattern") is { Length: > 0 } pattern ? Glob(pattern) : null;
        var files = (await AllFilesAsync(cancellationToken).ConfigureAwait(false))
            .Where(f => folder.Length == 0 || f.StartsWith(folder + "/", StringComparison.Ordinal))
            .Where(f => glob?.IsMatch(f) != false)
            .ToList();

        if (files.Count == 0)
        {
            return new ToolOutcome("No files match.");
        }

        var listed = string.Join('\n', files.Take(MaxListedFiles));
        return new ToolOutcome(files.Count > MaxListedFiles ? $"{listed}\n... and {files.Count - MaxListedFiles} more files" : listed);
    }

    private async Task<ToolOutcome> ReadFileAsync(JsonObject input, CancellationToken cancellationToken)
    {
        if (Resolve(Text(input, "path")) is not { } path)
        {
            return ToolOutcome.Error("The path must be inside the workspace.");
        }

        if (await ReadAsync(path.Full, cancellationToken).ConfigureAwait(false) is not { } text)
        {
            return ToolOutcome.Error($"{path.Relative} does not exist or is not a text file.");
        }

        var lines = text.Split('\n');
        var start = Math.Max(Integer(input, "start_line") ?? 1, 1);
        var end = Math.Min(Integer(input, "end_line") ?? lines.Length, lines.Length);
        if (end - start + 1 > MaxReadLines)
        {
            end = start + MaxReadLines - 1;
        }

        var builder = new StringBuilder();
        for (var i = start; i <= end; i++)
        {
            builder.Append(CultureInfo.InvariantCulture, $"{i,6}\t{lines[i - 1].TrimEnd('\r')}\n");
        }

        if (end < lines.Length)
        {
            builder.Append(CultureInfo.InvariantCulture, $"... {lines.Length - end} more lines (the file has {lines.Length})\n");
        }

        return new ToolOutcome(builder.Length == 0 ? "(empty file)" : builder.ToString());
    }

    private async Task<ToolOutcome> SearchAsync(JsonObject input, CancellationToken cancellationToken)
    {
        Regex regex;
        try
        {
            var options = RegexOptions.CultureInvariant | (input["ignore_case"]?.GetValue<bool>() == true ? RegexOptions.IgnoreCase : RegexOptions.None);
            regex = new Regex(Text(input, "pattern") ?? string.Empty, options, TimeSpan.FromSeconds(1));
        }
        catch (ArgumentException ex)
        {
            return ToolOutcome.Error($"The pattern is not a valid regular expression: {ex.Message}");
        }

        var folder = Normalize(Text(input, "folder"));
        var glob = Text(input, "file_pattern") is { Length: > 0 } pattern ? Glob(pattern) : null;
        var matches = new List<string>();
        foreach (var file in await AllFilesAsync(cancellationToken).ConfigureAwait(false))
        {
            if ((folder.Length > 0 && !file.StartsWith(folder + "/", StringComparison.Ordinal)) || glob?.IsMatch(file) == false)
            {
                continue;
            }

            var full = Path.Combine(_workspace.RootDirectory, file);
            if (!_changes.TryGetPending(full, out _) && File.Exists(full) && new FileInfo(full).Length > MaxSearchedFileLength)
            {
                continue;
            }

            if (await ReadAsync(full, cancellationToken).ConfigureAwait(false) is not { } text)
            {
                continue;
            }

            var lines = text.Split('\n');
            for (var i = 0; i < lines.Length && matches.Count < MaxSearchMatches; i++)
            {
                try
                {
                    if (regex.IsMatch(lines[i]))
                    {
                        var line = lines[i].TrimEnd('\r').Trim();
                        matches.Add($"{file}:{(i + 1).ToString(CultureInfo.InvariantCulture)}: {(line.Length > 200 ? line[..200] + "..." : line)}");
                    }
                }
                catch (RegexMatchTimeoutException)
                {
                    return ToolOutcome.Error("The pattern took too long to match. Use a simpler pattern.");
                }
            }

            if (matches.Count >= MaxSearchMatches)
            {
                matches.Add($"... stopped after {MaxSearchMatches} matches");
                break;
            }
        }

        return new ToolOutcome(matches.Count == 0 ? "No matches." : string.Join('\n', matches));
    }

    private async Task<ToolOutcome> EditFileAsync(JsonObject input, CancellationToken cancellationToken)
    {
        if (Resolve(Text(input, "path")) is not { } path)
        {
            return ToolOutcome.Error("The path must be inside the workspace.");
        }

        if (await ReadAsync(path.Full, cancellationToken).ConfigureAwait(false) is not { } text)
        {
            return ToolOutcome.Error($"{path.Relative} does not exist. Use create_file to create it.");
        }

        var oldText = Text(input, "old_text") ?? string.Empty;
        var newText = Text(input, "new_text") ?? string.Empty;
        if (oldText.Length == 0)
        {
            return ToolOutcome.Error("old_text is empty. To replace the whole file, use create_file.");
        }

        // Match the file's line endings, so edits to Windows-style files apply.
        if (text.Contains("\r\n", StringComparison.Ordinal) && !oldText.Contains('\r', StringComparison.Ordinal))
        {
            oldText = oldText.Replace("\n", "\r\n", StringComparison.Ordinal);
            newText = newText.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal);
        }

        var count = CountOccurrences(text, oldText);
        var replaceAll = input["replace_all"]?.GetValue<bool>() == true;
        if (count == 0)
        {
            return ToolOutcome.Error($"old_text was not found in {path.Relative}. Read the file again and copy the text exactly, including whitespace.");
        }

        if (count > 1 && !replaceAll)
        {
            return ToolOutcome.Error($"old_text occurs {count} times in {path.Relative}. Include more surrounding lines so it matches once, or set replace_all.");
        }

        var updated = text.Replace(oldText, newText, StringComparison.Ordinal);
        await ProposeAsync(path, updated, cancellationToken).ConfigureAwait(false);
        var replaced = replaceAll && count > 1 ? $"{count} occurrences" : "1 occurrence";
        return new ToolOutcome($"Edited {path.Relative} ({replaced}). The change is pending the user's approval; your later reads include it.");
    }

    private async Task<ToolOutcome> CreateFileAsync(JsonObject input, CancellationToken cancellationToken)
    {
        if (Resolve(Text(input, "path")) is not { } path)
        {
            return ToolOutcome.Error("The path must be inside the workspace.");
        }

        var existed = await ReadAsync(path.Full, cancellationToken).ConfigureAwait(false) is not null;
        await ProposeAsync(path, Text(input, "content") ?? string.Empty, cancellationToken).ConfigureAwait(false);
        return new ToolOutcome($"{(existed ? "Replaced" : "Created")} {path.Relative}. The change is pending the user's approval.");
    }

    private async Task<ToolOutcome> DeleteFileAsync(JsonObject input, CancellationToken cancellationToken)
    {
        if (Resolve(Text(input, "path")) is not { } path)
        {
            return ToolOutcome.Error("The path must be inside the workspace.");
        }

        if (await ReadAsync(path.Full, cancellationToken).ConfigureAwait(false) is null)
        {
            return ToolOutcome.Error($"{path.Relative} does not exist.");
        }

        await ProposeAsync(path, null, cancellationToken).ConfigureAwait(false);
        return new ToolOutcome($"Deleted {path.Relative}. The change is pending the user's approval.");
    }

    private async Task<ToolOutcome> GetDiagnosticsAsync(JsonObject input, CancellationToken cancellationToken)
    {
        var filter = Resolve(Text(input, "path"));
        var diagnostics = await _workspace.GetDiagnosticsAsync(_changes.Overlay, cancellationToken).ConfigureAwait(false);
        var relevant = diagnostics
            .Where(d => d.Severity >= DiagnosticSeverityLevel.Warning)
            .Where(d => filter is null || (d.Location?.FilePath is { } file && (file == filter.Value.Full || file.StartsWith(filter.Value.Full + Path.DirectorySeparatorChar, StringComparison.Ordinal))))
            .OrderByDescending(d => d.Severity)
            .ToList();

        if (relevant.Count == 0)
        {
            return new ToolOutcome("No errors or warnings.");
        }

        var errors = relevant.Count(d => d.Severity == DiagnosticSeverityLevel.Error);
        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture, $"{errors} errors, {relevant.Count - errors} warnings\n");
        foreach (var diagnostic in relevant.Take(MaxDiagnostics))
        {
            var location = diagnostic.Location is { } at
                ? $"{Relative(at.FilePath)}({at.Line.ToString(CultureInfo.InvariantCulture)},{at.Column.ToString(CultureInfo.InvariantCulture)}): "
                : string.Empty;
            var severity = diagnostic.Severity == DiagnosticSeverityLevel.Error ? "error" : "warning";
            builder.Append(CultureInfo.InvariantCulture, $"{location}{severity} {diagnostic.Id}: {diagnostic.Message}\n");
        }

        return new ToolOutcome(builder.ToString());
    }

    private async Task<ToolOutcome> RunTestsAsync(JsonObject input, CancellationToken cancellationToken) =>
        new(await _workspace.RunTestsAsync(Text(input, "filter"), _changes.Overlay, cancellationToken).ConfigureAwait(false));

    /// <summary>A file's text as the agent sees it: its pending change if it has one, otherwise the workspace's.</summary>
    private async Task<string?> ReadAsync(string fullPath, CancellationToken cancellationToken) =>
        _changes.TryGetPending(fullPath, out var pending)
            ? pending
            : await _workspace.ReadFileAsync(fullPath, cancellationToken).ConfigureAwait(false);

    private async Task ProposeAsync((string Full, string Relative) path, string? text, CancellationToken cancellationToken)
    {
        var original = _changes.TryGetPending(path.Full, out _)
            ? null
            : await _workspace.ReadFileAsync(path.Full, cancellationToken).ConfigureAwait(false);
        _changes.Propose(path.Full, path.Relative, original, text);
    }

    /// <summary>The workspace's files with the pending changes applied: new files added, deleted files left out.</summary>
    private async Task<List<string>> AllFilesAsync(CancellationToken cancellationToken)
    {
        var files = new SortedSet<string>(await _workspace.ListFilesAsync(cancellationToken).ConfigureAwait(false), StringComparer.Ordinal);
        foreach (var change in _changes.Pending)
        {
            if (change.IsDeletion)
            {
                files.Remove(change.RelativePath);
            }
            else
            {
                files.Add(change.RelativePath);
            }
        }

        return [.. files];
    }

    /// <summary>Turns a path from Claude into a full path inside the root, or null when it would leave the root.</summary>
    private (string Full, string Relative)? Resolve(string? path)
    {
        var relative = Normalize(path);
        if (relative.Length == 0)
        {
            return null;
        }

        var root = Path.GetFullPath(_workspace.RootDirectory).TrimEnd(Path.DirectorySeparatorChar);
        var full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        return full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? (full, Path.GetRelativePath(root, full).Replace(Path.DirectorySeparatorChar, '/'))
            : null;
    }

    private string Relative(string fullPath) =>
        Path.GetRelativePath(_workspace.RootDirectory, fullPath).Replace(Path.DirectorySeparatorChar, '/');

    private static string Normalize(string? path)
    {
        var normalized = (path ?? string.Empty).Replace('\\', '/').Trim().Trim('/');
        return normalized is "." ? string.Empty : normalized.StartsWith("./", StringComparison.Ordinal) ? normalized[2..] : normalized;
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        for (var index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    /// <summary>A glob as a regular expression: <c>**</c> crosses folders, <c>*</c> and <c>?</c> do not; a pattern without a slash matches file names anywhere.</summary>
    private static Regex Glob(string pattern)
    {
        var builder = new StringBuilder(pattern.Contains('/', StringComparison.Ordinal) ? "^" : "(^|/)");
        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            if (c == '*' && i + 1 < pattern.Length && pattern[i + 1] == '*')
            {
                var slash = i + 2 < pattern.Length && pattern[i + 2] == '/';
                builder.Append(slash ? "(.*/)?" : ".*");
                i += slash ? 2 : 1;
            }
            else
            {
                builder.Append(c switch
                {
                    '*' => "[^/]*",
                    '?' => "[^/]",
                    _ => Regex.Escape(c.ToString()),
                });
            }
        }

        return new Regex(builder.Append('$').ToString(), RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    }

    private static string? Text(JsonObject input, string name) =>
        input[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static int? Integer(JsonObject input, string name) =>
        input[name] is JsonValue value && value.TryGetValue<int>(out var number) ? number : null;

    private static DelegateTool Tool(string name, string description, string schema, Func<JsonObject, string> describe, Func<JsonObject, CancellationToken, Task<ToolOutcome>> invoke) =>
        new(new ToolDefinition(name, description, (JsonObject)JsonNode.Parse(schema)!), describe, invoke);
}
