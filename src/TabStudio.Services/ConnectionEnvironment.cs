using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using TabStudio.Services.Compose;

namespace TabStudio.Services;

/// <summary>Rewrites service endpoints, without replacing credentials or unrelated text.</summary>
public static partial class ConnectionEnvironment
{
    public static string Rewrite(string value, IReadOnlyDictionary<string, IReadOnlyList<ServicePort>> endpoints)
    {
        foreach (var (name, ports) in endpoints.OrderByDescending(x => x.Key.Length))
        {
            if (ports.Count == 0) continue;
            var port = ports[0];
            var namePattern = Regex.Escape(name);
            var fields = SplitConnectionFields(value);
            var changedHost = false;
            for (var i = 0; i < fields.Count; i++)
            {
                var equals = fields[i].IndexOf('=');
                if (equals < 0) continue;
                var key = fields[i][..equals].Trim();
                if (!new[] { "Host", "Server", "Data Source" }.Contains(key, StringComparer.OrdinalIgnoreCase)) continue;
                var host = fields[i][(equals + 1)..].Trim().Trim('\'', '"');
                if (host.Equals(name, StringComparison.OrdinalIgnoreCase))
                { fields[i] = fields[i][..(equals + 1)] + "127.0.0.1"; changedHost = true; }
                else if (host.StartsWith(name + ",", StringComparison.OrdinalIgnoreCase) || host.StartsWith(name + ":", StringComparison.OrdinalIgnoreCase))
                { fields[i] = fields[i][..(equals + 1)] + "127.0.0.1" + host[name.Length] + port.Published.ToString(CultureInfo.InvariantCulture); changedHost = true; }
            }
            if (changedHost)
            {
                var index = fields.FindIndex(f => f.IndexOf('=') is var e && e > 0 && f[..e].Trim().Equals("Port", StringComparison.OrdinalIgnoreCase));
                if (index >= 0) fields[index] = fields[index][..(fields[index].IndexOf('=') + 1)] + port.Published.ToString(CultureInfo.InvariantCulture);
                else fields.Add("Port=" + port.Published.ToString(CultureInfo.InvariantCulture));
                value = string.Join(';', fields);
            }
            // A URI-looking password in a database connection string is still a password.
            if (fields.Any(f => f.IndexOf('=') is var e && e > 0 && new[] { "Host", "Server", "Data Source" }.Contains(f[..e].Trim(), StringComparer.OrdinalIgnoreCase))) continue;
            value = Regex.Replace(value, @"(://(?:[^/@\s]*@)?)" + namePattern + @"(?::([0-9]+))?(?=[/\s?;#]|$)", m =>
            {
                var target = m.Groups[2].Success ? int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) : port.Target;
                var mapped = ports.FirstOrDefault(p => p.Target == target) ?? port;
                return m.Groups[1].Value + "127.0.0.1:" + mapped.Published.ToString(CultureInfo.InvariantCulture);
            }, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
            value = Regex.Replace(value, "(^|[,\\s])" + namePattern + @"(?::([0-9]+))?(?=[,;\s]|$)", m =>
            {
                var target = m.Groups[2].Success ? int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) : port.Target;
                var mapped = ports.FirstOrDefault(p => p.Target == target) ?? port;
                return m.Groups[1].Value + "127.0.0.1" + (m.Groups[2].Success ? ":" + mapped.Published.ToString(CultureInfo.InvariantCulture) : "");
            }, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
        }
        return value;
    }

    public static IReadOnlyDictionary<string, string?> ForProject(ComposeProject project, string projectDirectory, IReadOnlyDictionary<string, IReadOnlyList<ServicePort>> endpoints, string environmentName = "Development")
    {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        var application = FindApplication(project, projectDirectory);
        foreach (var pair in application?.Environment ?? new Dictionary<string, string?>()) result[pair.Key] = pair.Value is { } value ? Rewrite(value, endpoints) : null;
        foreach (var pair in project.RunEnvironment) result.TryAdd(pair.Key, pair.Value is { } value ? Rewrite(value, endpoints) : null);
        foreach (var file in new[] { "appsettings.json", $"appsettings.{environmentName}.json" })
        {
            var path = Path.Combine(projectDirectory, file);
            if (!File.Exists(path)) continue;
            using var json = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            Visit(json.RootElement, "", (key, value) =>
            {
                var replaced = Rewrite(value, endpoints);
                if (value != replaced && !result.ContainsKey(key.Replace(":", "__", StringComparison.Ordinal))) result[key.Replace(":", "__", StringComparison.Ordinal)] = replaced;
            });
        }
        foreach (var (name, ports) in endpoints)
        {
            var service = project.Services[name];
            var env = service.Environment;
            var key = name.Replace('-', '_').Replace('.', '_');
            var first = ports[0];
            result[$"{key.ToUpperInvariant()}_HOST"] = "127.0.0.1";
            result[$"{key.ToUpperInvariant()}_PORT"] = first.Published.ToString(CultureInfo.InvariantCulture);
            var mapping = Native.ServiceCatalog.Map(service).Mapping;
            var connection = mapping?.Kind switch
            {
                Native.NativeServiceKind.Postgres => $"Host=127.0.0.1;Port={first.Published};Username={Quote(env.GetValueOrDefault("POSTGRES_USER") ?? "postgres")};Password={Quote(env.GetValueOrDefault("POSTGRES_PASSWORD") ?? "")};Database={Quote(env.GetValueOrDefault("POSTGRES_DB") ?? env.GetValueOrDefault("POSTGRES_USER") ?? "postgres")}",
                Native.NativeServiceKind.Redis or Native.NativeServiceKind.Valkey => $"127.0.0.1:{first.Published}" + (env.GetValueOrDefault("REDIS_PASSWORD") is { Length: > 0 } password ? ",password=" + password : ""),
                Native.NativeServiceKind.MariaDb => $"Server=127.0.0.1;Port={first.Published};User ID={Quote(env.GetValueOrDefault("MARIADB_USER") ?? env.GetValueOrDefault("MYSQL_USER") ?? "app")};Password={Quote(env.GetValueOrDefault("MARIADB_PASSWORD") ?? env.GetValueOrDefault("MYSQL_PASSWORD") ?? "")};Database={Quote(env.GetValueOrDefault("MARIADB_DATABASE") ?? env.GetValueOrDefault("MYSQL_DATABASE") ?? "app")}",
                _ => null,
            };
            if (connection is not null) result.TryAdd("ConnectionStrings__" + name, connection);
        }
        foreach (var (variable, service) in project.RunPorts)
        {
            if (endpoints.TryGetValue(service, out var published) && published.Count > 0) result[variable] = published[0].Published.ToString(CultureInfo.InvariantCulture);
        }
        return result;
    }

    public static ComposeService? FindApplication(ComposeProject project, string projectDirectory)
    {
        if (project.DevelopmentService is { } name && project.Services.TryGetValue(name, out var development)) return development;
        projectDirectory = Path.GetFullPath(projectDirectory);
        return project.Services.Values.Where(s => s.BuildContext is { } context && (projectDirectory == context || projectDirectory.StartsWith(context.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal)))
            .OrderByDescending(s => s.BuildContext!.Length).FirstOrDefault();
    }

    private static List<string> SplitConnectionFields(string value)
    {
        var fields = new List<string>(); char quote = '\0'; var start = 0;
        for (var i = 0; i < value.Length; i++)
        {
            if (quote != '\0')
            {
                if (value[i] == quote)
                {
                    if (i + 1 < value.Length && value[i + 1] == quote)
                    {
                        i++;
                    }
                    else
                    {
                        quote = '\0';
                    }
                }
            }
            else if (value[i] is '\'' or '"')
            {
                quote = value[i];
            }
            else if (value[i] == ';')
            {
                fields.Add(value[start..i]);
                start = i + 1;
            }
        }
        fields.Add(value[start..]);
        return fields;
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    private static void Visit(JsonElement element, string prefix, Action<string, string> value)
    {
        if (element.ValueKind == JsonValueKind.Object) foreach (var property in element.EnumerateObject()) Visit(property.Value, prefix.Length == 0 ? property.Name : prefix + ":" + property.Name, value);
        else if (element.ValueKind == JsonValueKind.String) value(prefix, element.GetString()!);
    }
}
