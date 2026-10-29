using System.Text.Json.Nodes;

namespace TabStudio.LanguageServers.Configuration;

/// <summary>
/// A language server: which languages it serves, how to start it, and what to tell it. Built-in definitions come
/// from TabStudio's registry; a workspace changes or adds them in <c>.tabstudio/settings.json</c>.
/// </summary>
public sealed record LanguageServerDefinition
{
    /// <summary>The identifier, such as <c>pyright</c>, which settings use to change it.</summary>
    public required string Id { get; init; }

    /// <summary>The name shown to the user, such as <c>Pyright</c>.</summary>
    public required string Name { get; init; }

    /// <summary>The language identifiers it serves, such as <c>python</c>.</summary>
    public required IReadOnlyList<string> Languages { get; init; }

    /// <summary>The program to start, such as <c>pyright-langserver</c>: a name found on the path, or a full path.</summary>
    public required string Command { get; init; }

    /// <summary>Other names of the program, tried in order when <see cref="Command"/> is not installed.</summary>
    public IReadOnlyList<string> AlternativeCommands { get; init; } = [];

    /// <summary>The program's arguments, such as <c>--stdio</c>.</summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    /// <summary>Environment variables to set for it; a <see langword="null"/> value removes one.</summary>
    public IReadOnlyDictionary<string, string?> Environment { get; init; } = new Dictionary<string, string?>();

    /// <summary>
    /// Files that mark the root of a project, such as <c>pyproject.toml</c>: a file opens with the server of the
    /// nearest folder above it that has one, or else of the workspace folder.
    /// </summary>
    public IReadOnlyList<string> RootMarkers { get; init; } = [];

    /// <summary>The <c>initializationOptions</c> of <c>initialize</c>, if any.</summary>
    public JsonObject? InitializationOptions { get; init; }

    /// <summary>The settings it reads with <c>workspace/configuration</c>, before the workspace's own.</summary>
    public JsonObject? Settings { get; init; }

    /// <summary>The Toolchain Store package that installs it, such as <c>pyright</c>.</summary>
    public string? Package { get; init; }

    /// <summary>Whether to use it; a workspace can turn a built-in server off.</summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// Whether <c>initialize</c> names TabStudio's process (<c>processId</c>), which a server watches so it can exit
    /// when its client dies. Eclipse JDT stops itself when, after 30 seconds without messages, it cannot see that
    /// process; under PRoot that check can fail while TabStudio is running, so JDT is told no process and watches its
    /// own parent, which ends with TabStudio, instead.
    /// </summary>
    public bool SendsClientProcessId { get; init; } = true;

    /// <summary>The program names to try, in order.</summary>
    public IEnumerable<string> Commands => [Command, .. AlternativeCommands];
}
