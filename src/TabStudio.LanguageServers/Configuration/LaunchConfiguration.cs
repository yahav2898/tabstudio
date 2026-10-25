using System.Text.Json.Nodes;

namespace TabStudio.LanguageServers.Configuration;

/// <summary>
/// A launch configuration, as in VS Code's <c>launch.json</c>: which debug adapter runs it (<c>type</c>), whether it
/// starts a program or attaches to one (<c>request</c>), and the arguments the adapter reads, such as
/// <c>program</c>, <c>args</c>, <c>cwd</c>, <c>env</c> and <c>stopOnEntry</c>.
/// </summary>
/// <remarks>
/// <para><c>.tabstudio/launch.json</c> has the shape of VS Code's:</para>
/// <code>
/// {
///   "version": "0.2.0",
///   "configurations": [
///     {
///       "name": "Python: main.py",
///       "type": "debugpy",
///       "request": "launch",
///       "program": "${workspaceFolder}/main.py",
///       "args": [ "--verbose" ],
///       "cwd": "${workspaceFolder}",
///       "env": { "APP_MODE": "debug" },
///       "stopOnEntry": false
///     }
///   ]
/// }
/// </code>
/// <para>
/// Every property goes to the adapter as it is, after the variables <c>${workspaceFolder}</c>,
/// <c>${workspaceFolderBasename}</c>, <c>${file}</c>, <c>${fileBasename}</c>, <c>${fileBasenameNoExtension}</c>,
/// <c>${fileDirname}</c>, <c>${fileExtname}</c>, <c>${relativeFile}</c>, <c>${relativeFileDirname}</c>,
/// <c>${cwd}</c>, <c>${userHome}</c>, <c>${pathSeparator}</c> and <c>${env:NAME}</c> are replaced, and on top of the
/// adapter's defaults. <c>debugServer</c> connects to an adapter that is already running on that port.
/// </para>
/// </remarks>
public sealed record LaunchConfiguration
{
    /// <summary>The name shown in the Run menu.</summary>
    public required string Name { get; init; }

    /// <summary>The adapter type, such as <c>debugpy</c>.</summary>
    public required string Type { get; init; }

    /// <summary><c>launch</c> to start a program, <c>attach</c> to debug one that runs.</summary>
    public required string Request { get; init; }

    /// <summary>Every property of the configuration, as the adapter receives them.</summary>
    public required JsonObject Properties { get; init; }

    /// <summary>Whether it attaches to a running program.</summary>
    public bool IsAttach => string.Equals(Request, "attach", StringComparison.OrdinalIgnoreCase);

    /// <summary>The program to start, if it names one.</summary>
    public string? Program => JsonSettings.GetString(Properties, "program");

    /// <summary>The program's arguments.</summary>
    public IReadOnlyList<string> Arguments => JsonSettings.GetStrings(Properties, "args");

    /// <summary>The folder the program starts in, if it names one.</summary>
    public string? WorkingDirectory => JsonSettings.GetString(Properties, "cwd");

    /// <summary>Environment variables for the program.</summary>
    public IReadOnlyDictionary<string, string?> Environment => JsonSettings.GetStringMap(Properties, "env");

    /// <summary>Whether the program stops before its first line.</summary>
    public bool StopOnEntry => JsonSettings.GetBoolean(Properties, "stopOnEntry") ?? false;

    /// <summary>The port of an adapter that is already running, from <c>debugServer</c>, if any.</summary>
    public int? DebugServerPort => Properties["debugServer"] is JsonValue port && port.TryGetValue(out int value) && value > 0 ? value : null;

    /// <summary>Reads the configurations of a <c>launch.json</c> file.</summary>
    /// <param name="file">The file's object.</param>
    /// <param name="problems">Receives what is wrong with configurations that are left out.</param>
    public static IReadOnlyList<LaunchConfiguration> ParseFile(JsonObject file, ICollection<string> problems)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(problems);
        var configurations = new List<LaunchConfiguration>();
        if (file["configurations"] is not JsonArray entries)
        {
            problems.Add("launch.json has no \"configurations\" list.");
            return configurations;
        }

        foreach (var entry in entries)
        {
            if (entry is not JsonObject node)
            {
                continue;
            }

            var name = JsonSettings.GetString(node, "name");
            var type = JsonSettings.GetString(node, "type");
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(type))
            {
                problems.Add("A launch configuration needs a \"name\" and a \"type\".");
                continue;
            }

            configurations.Add(new LaunchConfiguration
            {
                Name = name,
                Type = type,
                Request = JsonSettings.GetString(node, "request") ?? "launch",
                Properties = (JsonObject)node.DeepClone(),
            });
        }

        return configurations;
    }

    /// <summary>The configuration TabStudio offers for a file of a language that has a debug adapter, when <c>launch.json</c> has none.</summary>
    /// <param name="adapter">The adapter.</param>
    /// <param name="languageName">The language's name, such as <c>Python</c>.</param>
    public static LaunchConfiguration ForCurrentFile(DebugAdapterDefinition adapter, string languageName)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(languageName);
        var name = $"{languageName}: Current File";
        return new LaunchConfiguration
        {
            Name = name,
            Type = adapter.Id,
            Request = "launch",
            Properties = new JsonObject
            {
                ["name"] = name,
                ["type"] = adapter.Id,
                ["request"] = "launch",
                ["program"] = "${file}",
                ["cwd"] = "${workspaceFolder}",
            },
        };
    }

    /// <summary>The configuration with its variables replaced.</summary>
    /// <param name="variables">The values of the variables.</param>
    public LaunchConfiguration Substitute(LaunchVariables variables)
    {
        ArgumentNullException.ThrowIfNull(variables);
        var properties = (JsonObject)Substitute(Properties.DeepClone(), variables)!;
        return this with { Name = variables.Substitute(Name), Properties = properties };
    }

    /// <summary>
    /// The arguments of the <c>launch</c> or <c>attach</c> request: the adapter's defaults, with its terminal defaults
    /// over them when TabStudio runs the program, under the configuration's properties.
    /// </summary>
    /// <param name="adapter">The adapter that runs it.</param>
    /// <param name="inTerminal">Whether TabStudio runs the program for the adapter (<c>runInTerminal</c>).</param>
    public JsonObject ToRequestArguments(DebugAdapterDefinition adapter, bool inTerminal = false)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        var defaults = inTerminal && adapter.TerminalDefaults is { } terminal ? JsonSettings.Merge(adapter.Defaults, terminal) : adapter.Defaults;
        return JsonSettings.Merge(defaults, Properties);
    }

    private static JsonNode? Substitute(JsonNode? node, LaunchVariables variables)
    {
        switch (node)
        {
            case JsonObject value:
                foreach (var name in value.Select(p => p.Key).ToList())
                {
                    value[name] = Substitute(value[name]?.DeepClone(), variables);
                }

                return value;
            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    array[i] = Substitute(array[i]?.DeepClone(), variables);
                }

                return array;
            case JsonValue text when text.TryGetValue(out string? value):
                return JsonValue.Create(variables.Substitute(value));
            default:
                return node;
        }
    }
}

/// <summary>The values of the variables in launch configurations, such as <c>${workspaceFolder}</c> and <c>${file}</c>.</summary>
/// <param name="WorkspaceFolder">The workspace's root folder.</param>
/// <param name="File">The file in the active editor, if any.</param>
public sealed record LaunchVariables(string WorkspaceFolder, string? File)
{
    /// <summary>Replaces every variable TabStudio knows in a text; others are left as they are.</summary>
    /// <param name="text">The text.</param>
    public string Substitute(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var start = text.IndexOf("${", StringComparison.Ordinal);
        if (start < 0)
        {
            return text;
        }

        var result = new System.Text.StringBuilder(text.Length);
        var position = 0;
        while (start >= 0)
        {
            var end = text.IndexOf('}', start + 2);
            if (end < 0)
            {
                break;
            }

            result.Append(text, position, start - position);
            var name = text[(start + 2)..end];
            result.Append(Resolve(name) ?? text[start..(end + 1)]);
            position = end + 1;
            start = text.IndexOf("${", position, StringComparison.Ordinal);
        }

        result.Append(text, position, text.Length - position);
        return result.ToString();
    }

    private string? Resolve(string name)
    {
        if (name.StartsWith("env:", StringComparison.Ordinal))
        {
            return System.Environment.GetEnvironmentVariable(name[4..]) ?? string.Empty;
        }

        var file = File ?? string.Empty;
        return name switch
        {
            "workspaceFolder" or "workspaceRoot" => WorkspaceFolder,
            "workspaceFolderBasename" => Path.GetFileName(WorkspaceFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            "file" => file,
            "fileBasename" => Path.GetFileName(file),
            "fileBasenameNoExtension" => Path.GetFileNameWithoutExtension(file),
            "fileDirname" => Path.GetDirectoryName(file) ?? string.Empty,
            "fileExtname" => Path.GetExtension(file),
            "relativeFile" => file.Length == 0 ? string.Empty : Path.GetRelativePath(WorkspaceFolder, file),
            "relativeFileDirname" => file.Length == 0 ? string.Empty : Path.GetRelativePath(WorkspaceFolder, Path.GetDirectoryName(file) ?? WorkspaceFolder),
            "cwd" => WorkspaceFolder,
            "userHome" => System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
            "pathSeparator" => new string(Path.DirectorySeparatorChar, 1),
            _ => null,
        };
    }
}
