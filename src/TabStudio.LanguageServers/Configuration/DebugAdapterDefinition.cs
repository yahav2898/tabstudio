using System.Text.Json.Nodes;

namespace TabStudio.LanguageServers.Configuration;

/// <summary>How TabStudio talks to a debug adapter.</summary>
public enum DebugAdapterTransport
{
    /// <summary>Over the adapter's standard input and output.</summary>
    Stdio,

    /// <summary>
    /// Over a TCP connection to a port on this device: TabStudio picks a free port, passes it in place of
    /// <c>${port}</c> in the arguments, and connects once the adapter listens.
    /// </summary>
    Tcp,
}

/// <summary>
/// A debug adapter: the launch configuration types it handles, how to start it, and the values every launch
/// configuration of its type starts from.
/// </summary>
public sealed record DebugAdapterDefinition
{
    /// <summary>The type launch configurations name, such as <c>debugpy</c>.</summary>
    public required string Id { get; init; }

    /// <summary>The name shown to the user, such as <c>Python (debugpy)</c>.</summary>
    public required string Name { get; init; }

    /// <summary>Other types that mean this adapter, such as VS Code's older <c>python</c>.</summary>
    public IReadOnlyList<string> Aliases { get; init; } = [];

    /// <summary>The language identifiers whose files it debugs, such as <c>python</c>.</summary>
    public IReadOnlyList<string> Languages { get; init; } = [];

    /// <summary>The program to start, such as <c>python3</c>: a name found on the path, or a full path.</summary>
    public required string Command { get; init; }

    /// <summary>Other names of the program, tried in order when <see cref="Command"/> is not installed.</summary>
    public IReadOnlyList<string> AlternativeCommands { get; init; } = [];

    /// <summary>The program's arguments, such as <c>-m debugpy.adapter</c>.</summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    /// <summary>Environment variables to set for it; a <see langword="null"/> value removes one.</summary>
    public IReadOnlyDictionary<string, string?> Environment { get; init; } = new Dictionary<string, string?>();

    /// <summary>How to talk to it.</summary>
    public DebugAdapterTransport Transport { get; init; }

    /// <summary>Values every launch configuration of this type starts from, such as <c>"console": "internalConsole"</c>.</summary>
    public JsonObject? Defaults { get; init; }

    /// <summary>
    /// Values that run the program in TabStudio's console, through the adapter's <c>runInTerminal</c> request, so
    /// it reads its input from the user, such as <c>"console": "integratedTerminal"</c>. They apply over
    /// <see cref="Defaults"/> when the client runs programs for the adapter, and under the configuration's own values.
    /// </summary>
    public JsonObject? TerminalDefaults { get; init; }

    /// <summary>The Toolchain Store package that installs it, such as <c>debugpy</c>.</summary>
    public string? Package { get; init; }

    /// <summary>Whether to use it; a workspace can turn a built-in adapter off.</summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>The program names to try, in order.</summary>
    public IEnumerable<string> Commands => [Command, .. AlternativeCommands];

    /// <summary>Whether a launch configuration's type means this adapter.</summary>
    /// <param name="type">The type.</param>
    public bool Handles(string type) =>
        string.Equals(Id, type, StringComparison.OrdinalIgnoreCase) || Aliases.Contains(type, StringComparer.OrdinalIgnoreCase);
}
