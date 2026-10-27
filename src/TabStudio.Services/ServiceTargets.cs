using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using TabStudio.Services.Compose;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace TabStudio.Services;

/// <summary>
/// Which Compose services a run actually uses: those its configuration points at with a connection string or a
/// URL. A repository's Compose file often lists every database its app can use (PetClinic's has MySQL and
/// PostgreSQL, while the app runs on in-memory H2 by default), so F5 starts a service only when the run reaches it:
/// through its environment, <c>appsettings.json</c>, the Spring profiles that are active, or <c>.env</c> files.
/// </summary>
public static partial class ServiceTargets
{
    private static readonly string[] LocalHosts = ["localhost", "127.0.0.1", "::1", "[::1]", "0.0.0.0", "host.docker.internal"];

    /// <summary>The services a run of a project points at, in the Compose file's order.</summary>
    /// <param name="project">The Compose model.</param>
    /// <param name="projectDirectory">The project that runs.</param>
    /// <param name="environment">The run's environment variables, such as a launch profile's, or <see langword="null"/>.</param>
    /// <param name="environmentName">The .NET environment, for <c>appsettings.{name}.json</c>.</param>
    public static IReadOnlyList<string> Referenced(ComposeProject project, string projectDirectory, IReadOnlyDictionary<string, string?>? environment = null, string environmentName = "Development")
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(projectDirectory);
        var variables = new Dictionary<string, string?>(environment ?? new Dictionary<string, string?>(), StringComparer.Ordinal);
        var values = new List<string>();
        values.AddRange(variables.Values.OfType<string>());
        foreach (var file in new[] { ".env", ".flaskenv", ".env.local", ".env.development" })
        {
            foreach (var (name, value) in ReadDotEnv(Path.Combine(projectDirectory, file)))
            {
                values.Add(value);
                variables.TryAdd(name, value);
            }
        }

        values.AddRange(AppSettings(projectDirectory, environmentName));
        values.AddRange(SpringValues(projectDirectory, variables));
        var targets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            foreach (var name in Targets(value, project))
            {
                targets.Add(name);
            }
        }

        return [.. project.Services.Keys.Where(targets.Contains)];
    }

    /// <summary>The services one configuration value points at, such as <c>postgres</c> for <c>jdbc:postgresql://localhost/petclinic</c>.</summary>
    /// <param name="value">A connection string, URL or <c>host:port</c>.</param>
    /// <param name="project">The Compose model.</param>
    public static IReadOnlyList<string> Targets(string value, ComposeProject project)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(project);
        var result = new List<string>();
        foreach (var (host, port) in Endpoints(value))
        {
            foreach (var service in project.Services.Values)
            {
                var named = host.Equals(service.Name, StringComparison.OrdinalIgnoreCase);
                var local = LocalHosts.Contains(host, StringComparer.OrdinalIgnoreCase) && port is { } p && Listens(service, p);
                if ((named || local) && !result.Contains(service.Name))
                {
                    result.Add(service.Name);
                }
            }
        }

        return result;
    }

    /// <summary>The hosts and ports a value names: URLs, <c>Host=…;Port=…</c> fields and <c>host:port</c> pairs.</summary>
    internal static IEnumerable<(string Host, int? Port)> Endpoints(string value)
    {
        foreach (Match url in Url().Matches(value))
        {
            var scheme = (url.Groups["inner"].Success ? url.Groups["inner"].Value : url.Groups["scheme"].Value).ToLowerInvariant();
            yield return (url.Groups["host"].Value.Trim('[', ']'), url.Groups["port"].Success ? Port(url.Groups["port"].Value) : DefaultPort(scheme));
        }

        string? fieldHost = null;
        int? fieldPort = null;
        foreach (var field in value.Split(';'))
        {
            var pair = field.Split('=', 2);
            if (pair.Length != 2)
            {
                continue;
            }

            var key = pair[0].Trim();
            var text = pair[1].Trim().Trim('\'', '"');
            if (key.Equals("Host", StringComparison.OrdinalIgnoreCase) || key.Equals("Server", StringComparison.OrdinalIgnoreCase)
                || key.Equals("Data Source", StringComparison.OrdinalIgnoreCase) || key.Equals("Address", StringComparison.OrdinalIgnoreCase))
            {
                var host = text.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase) ? text[4..] : text;
                var split = host.IndexOfAny([',', ':']);
                fieldHost = split > 0 ? host[..split] : host;
                if (split > 0)
                {
                    fieldPort = Port(host[(split + 1)..]);
                }
            }
            else if (key.Equals("Port", StringComparison.OrdinalIgnoreCase))
            {
                fieldPort = Port(text);
            }
        }

        if (fieldHost is { Length: > 0 })
        {
            yield return (fieldHost, fieldPort);
        }

        foreach (Match pair in HostPort().Matches(value))
        {
            yield return (pair.Groups["host"].Value, Port(pair.Groups["port"].Value));
        }
    }

    private static bool Listens(ComposeService service, int port) =>
        service.Ports.Any(p => p.Published == port || (p.Published == 0 && p.Target == port))
        || (service.Ports.Count == 0 && Native.ServiceCatalog.Map(service).Mapping?.Ports.Contains(port) == true);

    private static int? Port(string text) =>
        int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port is > 0 and <= 65535 ? port : null;

    private static int? DefaultPort(string scheme) => scheme switch
    {
        "postgres" or "postgresql" or "postgresql+asyncpg" or "postgresql+psycopg2" or "postgresql+psycopg" => 5432,
        "mysql" or "mariadb" or "mysql+pymysql" or "mysql+mysqldb" or "mysqlx" => 3306,
        "redis" or "rediss" or "valkey" => 6379,
        "mongodb" => 27017,
        "amqp" or "amqps" => 5672,
        "sqlserver" => 1433,
        "memcached" => 11211,
        "nats" => 4222,
        _ => null,
    };

    private static IEnumerable<KeyValuePair<string, string>> ReadDotEnv(string path)
    {
        if (!File.Exists(path))
        {
            yield break;
        }

        string[] lines;
        try
        {
            lines = File.ReadAllLines(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (var line in lines)
        {
            var text = line.Trim();
            if (text.StartsWith("export ", StringComparison.Ordinal))
            {
                text = text[7..];
            }

            var pair = text.Split('=', 2);
            if (text.StartsWith('#') || pair.Length != 2)
            {
                continue;
            }

            yield return new(pair[0].Trim(), pair[1].Trim().Trim('"', '\''));
        }
    }

    private static IEnumerable<string> AppSettings(string projectDirectory, string environmentName)
    {
        foreach (var file in new[] { "appsettings.json", $"appsettings.{environmentName}.json" })
        {
            var path = Path.Combine(projectDirectory, file);
            if (!File.Exists(path))
            {
                continue;
            }

            var values = new List<string>();
            try
            {
                using var json = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
                Strings(json.RootElement, values);
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                // A file that cannot be read points at nothing; the app reports it when it starts.
            }

            foreach (var value in values)
            {
                yield return value;
            }
        }
    }

    private static void Strings(JsonElement element, List<string> values)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                Strings(property.Value, values);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                Strings(item, values);
            }
        }
        else if (element.ValueKind == JsonValueKind.String && element.GetString() is { } text)
        {
            values.Add(text);
        }
    }

    /// <summary>
    /// The values of a Spring Boot application's configuration for the profiles that are active: its
    /// <c>application.properties</c> or <c>application.yml</c>, and <c>application-{profile}</c> for each profile
    /// that <c>SPRING_PROFILES_ACTIVE</c> or <c>spring.profiles.active</c> activates, with <c>${NAME:default}</c>
    /// placeholders resolved.
    /// </summary>
    internal static IReadOnlyList<string> SpringValues(string projectDirectory, IReadOnlyDictionary<string, string?> environment)
    {
        var resources = Path.Combine(projectDirectory, "src", "main", "resources");
        if (!Directory.Exists(resources))
        {
            return [];
        }

        var settings = ReadSpring(resources, "application");
        var profiles = (environment.GetValueOrDefault("SPRING_PROFILES_ACTIVE") ?? settings.GetValueOrDefault("spring.profiles.active") ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var profile in profiles.Where(p => SafeProfile().IsMatch(p)))
        {
            foreach (var (key, value) in ReadSpring(resources, "application-" + profile))
            {
                settings[key] = value;
            }
        }

        return [.. settings.Values.Select(v => Resolve(v, environment, settings))];
    }

    private static Dictionary<string, string> ReadSpring(string resources, string name)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var properties = Path.Combine(resources, name + ".properties");
        try
        {
            if (File.Exists(properties))
            {
                foreach (var line in File.ReadAllLines(properties))
                {
                    var text = line.Trim();
                    if (text.Length == 0 || text[0] is '#' or '!')
                    {
                        continue;
                    }

                    var split = text.IndexOfAny(['=', ':']);
                    if (split > 0)
                    {
                        result[text[..split].Trim()] = text[(split + 1)..].Trim();
                    }
                }
            }

            foreach (var yaml in new[] { name + ".yml", name + ".yaml" }.Select(f => Path.Combine(resources, f)).Where(File.Exists))
            {
                using var reader = File.OpenText(yaml);
                var stream = new YamlStream();
                stream.Load(reader);
                foreach (var document in stream.Documents)
                {
                    Flatten(document.RootNode, string.Empty, result);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or YamlException)
        {
            // Configuration that cannot be read points at nothing.
        }

        return result;
    }

    private static void Flatten(YamlNode node, string prefix, Dictionary<string, string> result)
    {
        switch (node)
        {
            case YamlMappingNode map:
                foreach (var (key, value) in map.Children)
                {
                    var name = key is YamlScalarNode scalar ? scalar.Value ?? string.Empty : string.Empty;
                    Flatten(value, prefix.Length == 0 ? name : prefix + "." + name, result);
                }

                break;
            case YamlSequenceNode sequence:
                var index = 0;
                foreach (var item in sequence.Children)
                {
                    Flatten(item, $"{prefix}[{index++}]", result);
                }

                break;
            case YamlScalarNode { Value: { } text }:
                result[prefix] = text;
                break;
        }
    }

    /// <summary>Resolves Spring's <c>${NAME:default}</c> placeholders from the environment and the other settings.</summary>
    private static string Resolve(string value, IReadOnlyDictionary<string, string?> environment, Dictionary<string, string> settings) =>
        Placeholder().Replace(value, m =>
        {
            var name = m.Groups["name"].Value;
            return environment.GetValueOrDefault(name) ?? settings.GetValueOrDefault(name)
                ?? environment.GetValueOrDefault(name.ToUpperInvariant().Replace('.', '_').Replace('-', '_'))
                ?? (m.Groups["default"].Success ? m.Groups["default"].Value : string.Empty);
        });

    [GeneratedRegex(@"(?<scheme>[A-Za-z][A-Za-z0-9+.-]*):(?:(?<inner>[A-Za-z][A-Za-z0-9+.-]*):)?//(?:[^@/\s]*@)?(?<host>\[[^\]]+\]|[^:/\s?#,;@]+)(?::(?<port>\d{1,5}))?", RegexOptions.CultureInvariant)]
    private static partial Regex Url();

    [GeneratedRegex(@"(?<![\w./@:-])(?<host>[A-Za-z][\w.-]*):(?<port>\d{2,5})(?![\w.:/])", RegexOptions.CultureInvariant)]
    private static partial Regex HostPort();

    [GeneratedRegex(@"\$\{(?<name>[A-Za-z0-9_.-]+)(:(?<default>[^}]*))?\}", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"^[A-Za-z0-9_-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeProfile();
}
