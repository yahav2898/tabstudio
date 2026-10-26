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
            var port = ports.FirstOrDefault();
            if (port is null) continue;
            var original = value;
            var namePattern = Regex.Escape(name);
            var changedHost = false;
            value = Regex.Replace(value, @"(^|;)(\s*(?:Host|Server|Data Source)\s*=\s*)([^;]+)", match =>
            {
                var host = match.Groups[3].Value.Trim();
                if (host.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    changedHost = true;
                    return match.Groups[1].Value + match.Groups[2].Value + "127.0.0.1";
                }
                if (host.StartsWith(name + ",", StringComparison.OrdinalIgnoreCase) || host.StartsWith(name + ":", StringComparison.OrdinalIgnoreCase))
                {
                    changedHost = true;
                    return match.Groups[1].Value + match.Groups[2].Value + "127.0.0.1" + host[name.Length] + port.Published.ToString(CultureInfo.InvariantCulture);
                }
                return match.Value;
            }, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            if (changedHost)
            {
                if (Regex.IsMatch(value, @"(^|;)\s*Port\s*=", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)))
                    value = Regex.Replace(value, @"(^|;)(\s*Port\s*=\s*)[0-9]+", m => m.Groups[1].Value + m.Groups[2].Value + port.Published.ToString(CultureInfo.InvariantCulture), RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
                else value += ";Port=" + port.Published.ToString(CultureInfo.InvariantCulture);
            }
            value = Regex.Replace(value, @"(://(?:[^/@\s]*@)?)" + namePattern + @"(?::([0-9]+))?(?=[/\s?;#]|$)", m =>
            {
                var target = m.Groups[2].Success ? int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) : port.Target;
                var mapped = ports.FirstOrDefault(p => p.Target == target) ?? port;
                return m.Groups[1].Value + "127.0.0.1:" + mapped.Published.ToString(CultureInfo.InvariantCulture);
            }, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
            value = Regex.Replace(value, "(^|[,\\s])" + namePattern + @"(?::([0-9]+))?(?=[,\s]|$)", m =>
            {
                var target = m.Groups[2].Success ? int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) : port.Target;
                var mapped = ports.FirstOrDefault(p => p.Target == target) ?? port;
                return m.Groups[1].Value + "127.0.0.1" + (m.Groups[2].Success ? ":" + mapped.Published.ToString(CultureInfo.InvariantCulture) : "");
            }, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
            _ = original;
        }
        return value;
    }

    public static IReadOnlyDictionary<string, string?> ForProject(ComposeProject project, string projectDirectory, IReadOnlyDictionary<string, IReadOnlyList<ServicePort>> endpoints, string environmentName = "Development")
    {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        var application = FindApplication(project, projectDirectory);
        foreach (var pair in application?.Environment ?? new Dictionary<string, string?>()) result[pair.Key] = pair.Value is { } value ? Rewrite(value, endpoints) : null;
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
            var first = ports.First();
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
        return result;
    }

    public static ComposeService? FindApplication(ComposeProject project, string projectDirectory)
    {
        if (project.DevelopmentService is { } name && project.Services.TryGetValue(name, out var development)) return development;
        projectDirectory = Path.GetFullPath(projectDirectory);
        return project.Services.Values.Where(s => s.BuildContext is { } context && (projectDirectory == context || projectDirectory.StartsWith(context.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal)))
            .OrderByDescending(s => s.BuildContext!.Length).FirstOrDefault();
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    private static void Visit(JsonElement element, string prefix, Action<string, string> value)
    {
        if (element.ValueKind == JsonValueKind.Object) foreach (var property in element.EnumerateObject()) Visit(property.Value, prefix.Length == 0 ? property.Name : prefix + ":" + property.Name, value);
        else if (element.ValueKind == JsonValueKind.String) value(prefix, element.GetString()!);
    }
}
