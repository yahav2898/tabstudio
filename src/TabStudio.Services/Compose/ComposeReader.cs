using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace TabStudio.Services.Compose;

/// <summary>Reads Compose YAML (including anchors) and devcontainer JSONC without executing repository code.</summary>
public static partial class ComposeReader
{
    private static readonly string[] ComposeFileNames = ["compose.yaml", "compose.yml", "docker-compose.yml", "docker-compose.yaml"];
    private static readonly string[] ContainerOnlyKeys = ["privileged", "devices", "cap_add", "network_mode", "pid", "ipc", "extends", "secrets", "configs", "links", "volumes_from"];

    public static ComposeProject? Discover(string directory, IReadOnlyDictionary<string, string?>? environment = null)
    {
        directory = Path.GetFullPath(directory);
        var devcontainer = Path.Combine(directory, ".devcontainer", "devcontainer.json");
        var files = new List<string>();
        string? developmentService = null;
        IReadOnlyList<string> runServices = [];
        if (File.Exists(devcontainer))
        {
            using var json = JsonDocument.Parse(File.ReadAllText(devcontainer), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            var root = json.RootElement;
            if (root.TryGetProperty("dockerComposeFile", out var compose))
            {
                var paths = compose.ValueKind == JsonValueKind.Array ? compose.EnumerateArray().Select(x => x.GetString()!) : [compose.GetString()!];
                files.AddRange(paths.Select(path => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(devcontainer)!, path))));
            }
            if (root.TryGetProperty("service", out var service)) developmentService = service.GetString();
            if (root.TryGetProperty("runServices", out var run)) runServices = run.EnumerateArray().Select(x => x.GetString()!).ToArray();
        }
        if (files.Count == 0)
        {
            // A backend opened below the repository root uses the repository's Compose file, just as
            // `docker compose` does. A nested repository must never inherit its parent's services.
            string? file = null;
            for (var folder = new DirectoryInfo(directory); folder is not null; folder = folder.Parent)
            {
                file = ComposeFileNames.Select(name => Path.Combine(folder.FullName, name)).FirstOrDefault(File.Exists);
                if (file is not null || Path.Exists(Path.Combine(folder.FullName, ".git"))) break;
            }
            if (file is null) return null;
            files.Add(file);
            var extension = Path.GetExtension(file);
            var over = Path.Combine(Path.GetDirectoryName(file)!, Path.GetFileNameWithoutExtension(file) + ".override" + extension);
            if (File.Exists(over)) files.Add(over);
        }
        return Read(files, environment) with { DevelopmentService = developmentService, RunServices = runServices };
    }

    public static ComposeProject Read(IReadOnlyList<string> files, IReadOnlyDictionary<string, string?>? environment = null)
    {
        if (files.Count == 0) throw new ComposeException("No Compose files were specified.");
        var directory = Path.GetDirectoryName(Path.GetFullPath(files[0]))!;
        var supplied = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (System.Collections.DictionaryEntry variable in System.Environment.GetEnvironmentVariables()) supplied[(string)variable.Key] = (string?)variable.Value;
        foreach (var (name, value) in environment ?? new Dictionary<string, string?>()) supplied[name] = value;
        var variables = ReadEnvironmentFile(Path.Combine(directory, ".env"), supplied, required: false);
        foreach (var (name, value) in supplied) variables[name] = value;
        var merged = new Dictionary<string, YamlMappingNode>(StringComparer.Ordinal);
        var unsupportedVolumes = new Dictionary<string, bool>(StringComparer.Ordinal);
        var broken = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            if (!File.Exists(file)) throw new ComposeException($"Compose file not found: {file}");
            try
            {
                using var reader = File.OpenText(file);
                var stream = new YamlStream();
                stream.Load(reader);
                if (stream.Documents.Count != 1 || stream.Documents[0].RootNode is not YamlMappingNode root) throw new ComposeException($"Expected one Compose mapping in {file}.");
                if (Get(root, "include") is not null) throw new ComposeException("Compose 'include' is not supported locally. Use a remote host.");
                var expanded = new HashSet<YamlNode>(ReferenceEqualityComparer.Instance);
                ResolveMerges(root, new HashSet<YamlNode>(ReferenceEqualityComparer.Instance));
                foreach (var (key, value) in Mapping(Get(root, "volumes")).Children)
                {
                    var settings = Mapping(value);
                    unsupportedVolumes[Text(key)] = Text(Get(settings, "external")) == "true" || Get(settings, "driver_opts") is not null || Text(Get(settings, "driver")) is not ("" or "local");
                }
                foreach (var pair in Mapping(Get(root, "services")).Children)
                {
                    var name = Text(pair.Key);
                    if (!NamePattern().IsMatch(name)) throw new ComposeException("A Compose service name is invalid.");
                    var node = Mapping(pair.Value);
                    try
                    {
                        // Interpolate each file before merging, as Compose does.
                        ExpandNode(node, variables, expanded);
                    }
                    catch (ComposeException ex)
                    {
                        // A service's own error stays on its row; the other services still run.
                        broken[name] = ex.Message;
                    }

                    merged[name] = merged.TryGetValue(name, out var existing) ? Merge(existing, node) : node;
                }
            }
            catch (YamlException ex) { throw new ComposeException($"Invalid Compose YAML in {Path.GetFileName(file)} at line {ex.Start.Line + 1}."); }
        }
        var services = merged.ToDictionary(p => p.Key, p =>
        {
            ComposeService service;
            try
            {
                if (broken.TryGetValue(p.Key, out var error)) throw new ComposeException(error);
                service = ReadService(p.Key, p.Value, directory, variables);
            }
            catch (ComposeException ex)
            {
                // One service's invalid or unsupported setting makes that service unavailable, not the whole file.
                return new ComposeService(p.Key, Get(p.Value, "image") is YamlScalarNode image ? image.Value : null) { Unsupported = [ex.Message] };
            }

            return service with { Unsupported = [.. service.Unsupported, .. service.Volumes.Where(v => !v.IsBind && v.Source is { } name && unsupportedVolumes.GetValueOrDefault(name)).Select(_ => "External or driver-backed volumes require a remote host.")] };
        }, StringComparer.Ordinal);
        return new ComposeProject(directory, files, services);
    }

    private static ComposeService ReadService(string name, YamlMappingNode node, string directory, Dictionary<string, string?> variables)
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var envFile in SequenceOrOne(Get(node, "env_file")))
        {
            var path = envFile is YamlMappingNode map ? Text(Get(map, "path")) : Text(envFile);
            var required = envFile is not YamlMappingNode settings || Text(Get(settings, "required")) != "false";
            foreach (var pair in ReadEnvironmentFile(Path.Combine(directory, path), variables, required)) environment[pair.Key] = pair.Value;
        }
        var env = Get(node, "environment");
        if (env is YamlMappingNode envMap)
        {
            foreach (var pair in envMap.Children)
            {
                var key = Text(pair.Key);
                environment[key] = IsNull(pair.Value) ? variables.GetValueOrDefault(key) : Text(pair.Value);
            }
        }
        else
        {
            foreach (var entry in SequenceOrOne(env))
            {
                var parts = Text(entry).Split('=', 2);
                environment[parts[0]] = parts.Length == 2 ? parts[1] : variables.GetValueOrDefault(parts[0]);
            }
        }
        var dependencies = new List<ServiceDependency>();
        if (Get(node, "depends_on") is YamlMappingNode depends)
        {
            foreach (var pair in depends.Children)
            {
                var options = Mapping(pair.Value);
                var condition = Get(options, "condition") is { } value ? Text(value) : "service_started";
                if (condition is not ("service_started" or "service_healthy" or "service_completed_successfully")) throw new ComposeException($"Invalid depends_on condition for {name}.");
                dependencies.Add(new ServiceDependency(Text(pair.Key), condition, Text(Get(options, "required")) != "false"));
            }
        }
        else
        {
            dependencies.AddRange(SequenceOrOne(Get(node, "depends_on")).Select(x => new ServiceDependency(Text(x))));
        }
        var volumes = SequenceOrOne(Get(node, "volumes")).Select(x => ReadVolume(x, directory)).ToArray();
        var health = Get(node, "healthcheck") as YamlMappingNode;
        var unsupported = new List<string>();
        foreach (var key in ContainerOnlyKeys)
        {
            if (Get(node, key) is not null) unsupported.Add($"Compose '{key}' requires container semantics; use a remote host.");
        }
        return new ComposeService(name, Get(node, "image") is { } image ? Text(image) : null)
        {
            Environment = environment,
            Ports = SequenceOrOne(Get(node, "ports")).SelectMany(ReadPorts).ToArray(),
            Volumes = volumes,
            Dependencies = dependencies,
            Command = ReadCommand(Get(node, "command")),
            Entrypoint = ReadCommand(Get(node, "entrypoint")),
            BuildContext = Get(node, "build") is { } build ? Path.GetFullPath(Path.Combine(directory, build is YamlMappingNode b ? Text(Get(b, "context")) : Text(build))) : null,
            WorkingDirectory = Get(node, "working_dir") is { } workdir ? Text(workdir) : null,
            User = Get(node, "user") is { } user ? Text(user) : null,
            Platform = Get(node, "platform") is { } platform ? Text(platform) : null,
            Restart = Get(node, "restart") is { } restart ? Text(restart) : null,
            Profiles = SequenceOrOne(Get(node, "profiles")).Select(Text).ToArray(),
            Unsupported = unsupported,
            Healthcheck = health is null ? null : new ServiceHealthcheck(Get(health, "test") is YamlSequenceNode tests ? tests.Children.Select(Text).ToArray() : Get(health, "test") is { } shell ? ["CMD-SHELL", Text(shell)] : [])
            {
                Disabled = Text(Get(health, "disable")) == "true" || Text((Get(health, "test") as YamlSequenceNode)?.Children.FirstOrDefault()) == "NONE",
                Interval = Duration(Get(health, "interval"), TimeSpan.FromSeconds(30)),
                Timeout = Duration(Get(health, "timeout"), TimeSpan.FromSeconds(10)),
                StartPeriod = Duration(Get(health, "start_period"), TimeSpan.Zero),
                StartInterval = Duration(Get(health, "start_interval"), TimeSpan.FromSeconds(5)),
                Retries = Get(health, "retries") is { } retry && int.TryParse(Text(retry), out var count) && count > 0 ? count : 3,
            },
        };
    }

    private static IEnumerable<ServicePort> ReadPorts(YamlNode node)
    {
        if (node is YamlMappingNode map)
        {
            var target = Port(Text(Get(map, "target")));
            var hostPort = Get(map, "published") is { } value ? Port(Text(value), allowZero: true) : 0;
            yield return new ServicePort(target, hostPort, Get(map, "protocol") is { } protocol ? Text(protocol) : "tcp", Get(map, "host_ip") is { } ip ? Text(ip) : null);
            yield break;
        }
        var text = Text(node);
        var protocolParts = text.Split('/');
        var pieces = protocolParts[0].Split(':');
        if (pieces.Length > 3) throw new ComposeException("IPv6 Compose port bindings require long syntax.");
        var targetRange = pieces[^1].Split('-');
        var publishedRange = pieces.Length > 1 ? pieces[^2].Split('-') : ["0"];
        var first = Port(targetRange[0]);
        var last = targetRange.Length == 2 ? Port(targetRange[1]) : first;
        var published = Port(publishedRange[0], allowZero: true);
        if (last < first || last - first > 100 || (publishedRange.Length == 2 && Port(publishedRange[1]) - published != last - first)) throw new ComposeException("Invalid or oversized Compose port range.");
        for (var i = first; i <= last; i++) yield return new ServicePort(i, published == 0 ? 0 : published + i - first, protocolParts.Length == 2 ? protocolParts[1] : "tcp", pieces.Length == 3 ? pieces[0] : null);
    }

    private static int Port(string value, bool allowZero = false) => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port >= (allowZero ? 0 : 1) && port <= 65535 ? port : throw new ComposeException("A Compose port must be between 1 and 65535.");

    private static ServiceVolume ReadVolume(YamlNode node, string directory)
    {
        string? source; string target; bool readOnly; bool bind; var create = true;
        if (node is YamlMappingNode map)
        {
            var type = Text(Get(map, "type"));
            if (type is not ("" or "volume" or "bind")) throw new ComposeException($"Volume type '{type}' is not supported locally. Use a remote host.");
            source = Get(map, "source") is { } value ? Text(value) : null;
            target = Text(Get(map, "target"));
            readOnly = Text(Get(map, "read_only")) == "true";
            bind = type == "bind";
            create = Text(Get(Mapping(Get(map, "bind")), "create_host_path")) != "false";
        }
        else
        {
            var parts = Text(node).Split(':');
            if (parts.Length > 3) throw new ComposeException("Invalid Compose volume.");
            source = parts.Length > 1 ? parts[0] : null;
            target = parts.Length > 1 ? parts[1] : parts[0];
            readOnly = parts.Length == 3 && parts[2].Split(',').Contains("ro");
            bind = source is not null && (source.StartsWith('.') || Path.IsPathRooted(source) || source.StartsWith('~'));
        }
        if (!target.StartsWith('/') || target.Split('/').Contains("..") || target.Contains(':')) throw new ComposeException("A volume target must be an absolute guest path without '..' or ':'.");
        if (bind && source is not null) source = Path.GetFullPath(Path.Combine(directory, source));
        return new ServiceVolume(source, target, readOnly, bind, create);
    }

    internal static Dictionary<string, string?> ReadEnvironmentFile(string path, IReadOnlyDictionary<string, string?> variables, bool required)
    {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (!File.Exists(path))
        {
            if (required) throw new ComposeException($"Environment file not found: {Path.GetFileName(path)}");
            return result;
        }
        foreach (var line in File.ReadLines(path))
        {
            var text = line.Trim();
            if (text.Length == 0 || text.StartsWith('#')) continue;
            if (text.StartsWith("export ", StringComparison.Ordinal)) text = text[7..];
            var pair = text.Split('=', 2);
            var key = pair[0].Trim();
            var value = pair.Length == 2 ? pair[1].Trim() : "";
            var single = value.StartsWith('\'');
            if ((single || value.StartsWith('"')) && value.Length >= 2 && value[^1] == value[0]) value = value[1..^1];
            else if (value.IndexOf(" #", StringComparison.Ordinal) is var comment && comment >= 0) value = value[..comment].TrimEnd();
            var scope = new Dictionary<string, string?>(variables, StringComparer.Ordinal);
            foreach (var previous in result) scope[previous.Key] = previous.Value;
            result[key] = single ? value : ComposeInterpolation.Expand(value, scope);
        }
        return result;
    }

    public static IReadOnlyList<string> SplitCommand(string text)
    {
        var words = new List<string>(); var word = new StringBuilder(); char quote = '\0'; bool escaped = false, present = false;
        foreach (var c in text)
        {
            if (escaped) { word.Append(c); escaped = false; present = true; }
            else if (c == '\\' && quote != '\'') { escaped = true; present = true; }
            else if (quote != '\0') { if (c == quote) quote = '\0'; else word.Append(c); }
            else if (c is '\'' or '"') { quote = c; present = true; }
            else if (char.IsWhiteSpace(c)) { if (present) { words.Add(word.ToString()); word.Clear(); present = false; } }
            else { word.Append(c); present = true; }
        }
        if (quote != '\0' || escaped) throw new ComposeException("Unclosed quote or escape in a Compose command.");
        if (present) words.Add(word.ToString());
        return words;
    }

    private static IReadOnlyList<string>? ReadCommand(YamlNode? node) => IsNull(node) ? null : node is YamlSequenceNode sequence ? sequence.Children.Select(Text).ToArray() : SplitCommand(Text(node));
    private static TimeSpan Duration(YamlNode? node, TimeSpan fallback)
    {
        if (node is null) return fallback;
        var text = Text(node); var matches = DurationPattern().Matches(text);
        if (matches.Count == 0 || string.Concat(matches.Select(x => x.Value)) != text) throw new ComposeException("Invalid healthcheck duration.");
        var seconds = matches.Sum(x => double.Parse(x.Groups[1].Value, CultureInfo.InvariantCulture) * (x.Groups[2].Value switch { "h" => 3600, "m" => 60, "ms" => 0.001, "us" => 0.000001, "ns" => 0.000000001, _ => 1 }));
        return seconds >= 0 && seconds <= 86400 ? TimeSpan.FromSeconds(seconds) : throw new ComposeException("Healthcheck durations must be at most one day.");
    }

    private static void ExpandNode(YamlNode node, IReadOnlyDictionary<string, string?> variables, HashSet<YamlNode> visited)
    {
        if (!visited.Add(node)) return;
        if (node is YamlScalarNode scalar) scalar.Value = ComposeInterpolation.Expand(scalar.Value ?? "", variables);
        else if (node is YamlMappingNode map) foreach (var child in map.Children.Values) ExpandNode(child, variables, visited);
        else if (node is YamlSequenceNode seq) foreach (var child in seq.Children) ExpandNode(child, variables, visited);
    }
    private static void ResolveMerges(YamlNode node, HashSet<YamlNode> visited)
    {
        if (!visited.Add(node)) return;
        if (node is YamlMappingNode map)
        {
            foreach (var value in map.Children.Values.ToArray()) ResolveMerges(value, visited);
            if (Get(map, "<<") is { } merge)
            {
                foreach (var inherited in SequenceOrOne(merge).OfType<YamlMappingNode>())
                {
                    foreach (var pair in inherited.Children)
                    {
                        if (!map.Children.ContainsKey(pair.Key)) map.Children.Add(pair.Key, pair.Value);
                    }
                }

                map.Children.Remove(new YamlScalarNode("<<"));
            }
        }
        else if (node is YamlSequenceNode sequence)
        {
            foreach (var child in sequence.Children) ResolveMerges(child, visited);
        }
    }

    private static YamlMappingNode Merge(YamlMappingNode first, YamlMappingNode next)
    {
        var result = new YamlMappingNode(first.Children);
        foreach (var (key, value) in next.Children)
        {
            var name = Text(key);
            if (name is "environment" or "healthcheck" or "depends_on" && result.Children.TryGetValue(key, out var previous) && previous is YamlMappingNode a && value is YamlMappingNode b)
            {
                result.Children[key] = Merge(a, b);
            }
            else if (name == "volumes" && result.Children.TryGetValue(key, out previous) && previous is YamlSequenceNode av && value is YamlSequenceNode bv)
            {
                var all = av.Children.Concat(bv.Children).GroupBy(v => v is YamlMappingNode m ? Text(Get(m, "target")) : Text(v).Split(':') is var p && p.Length > 1 ? p[1] : p[0]);
                result.Children[key] = new YamlSequenceNode(all.Select(g => g.Last()));
            }
            else if (name is "ports" or "env_file" && result.Children.TryGetValue(key, out previous) && previous is YamlSequenceNode aps && value is YamlSequenceNode bps)
            {
                result.Children[key] = new YamlSequenceNode(aps.Children.Concat(bps.Children).Distinct());
            }
            else
            {
                result.Children[key] = value;
            }
        }
        return result;
    }
    private static bool IsNull(YamlNode? node) => node is null || node is YamlScalarNode s && s.Value is null or "null" or "~";
    private static YamlNode? Get(YamlMappingNode map, string key) => map.Children.TryGetValue(new YamlScalarNode(key), out var value) ? value : null;
    private static YamlMappingNode Mapping(YamlNode? node) => node as YamlMappingNode ?? new YamlMappingNode();
    private static IEnumerable<YamlNode> SequenceOrOne(YamlNode? node) => node is YamlSequenceNode sequence ? sequence.Children : IsNull(node) ? [] : [node!];
    private static string Text(YamlNode? node) => node is YamlScalarNode scalar ? scalar.Value ?? "" : node?.ToString() ?? "";
    [GeneratedRegex("^[a-zA-Z0-9][a-zA-Z0-9_.-]*$")] private static partial Regex NamePattern();
    [GeneratedRegex("([0-9]+(?:\\.[0-9]+)?)(ms|us|ns|h|m|s)")] private static partial Regex DurationPattern();
}

