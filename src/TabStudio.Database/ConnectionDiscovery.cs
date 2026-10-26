using System.Text.Json;
using System.Xml.Linq;

namespace TabStudio.Database;

public sealed record DiscoveredConnection(string Name, string Source, DatabaseProvider? Provider, string Value)
{
    public override string ToString() => $"{Name} ({Source})";
}

public static class ConnectionDiscovery
{
    public static Task<IReadOnlyList<DiscoveredConnection>> FindAsync(string root, string? secretsRoot = null, CancellationToken token = default) => Task.Run(async () =>
    {
        var found = new List<DiscoveredConnection>();
        var pending = new Stack<string>(); pending.Push(root);
        var options = new EnumerationOptions { AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = true };
        secretsRoot ??= OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "UserSecrets")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".microsoft", "usersecrets");
        var secrets = new HashSet<string>(StringComparer.Ordinal);
        while (pending.TryPop(out var directory))
        {
            token.ThrowIfCancellationRequested();
            foreach (var child in Directory.EnumerateDirectories(directory, "*", options))
                if (Path.GetFileName(child) is not (".git" or "bin" or "obj" or "node_modules" or ".venv" or "packages" or ".nuget")) pending.Push(child);
            foreach (var file in Directory.EnumerateFiles(directory, "appsettings*.json", options))
                await ReadJsonAsync(file, Path.GetRelativePath(root, file), directory, found, token).ConfigureAwait(false);
            foreach (var project in Directory.EnumerateFiles(directory, "*.csproj", options))
            {
                try
                {
                    var xml = XDocument.Load(project);
                    foreach (var id in xml.Descendants().Where(e => e.Name.LocalName == "UserSecretsId").Select(e => e.Value.Trim()))
                    {
                        // IDs are folder names, never paths. Do not let an imported project escape the secrets root.
                        if (id.Length == 0 || id is "." or ".." || id.IndexOfAny(['/', '\\', ':']) >= 0 || !secrets.Add(id)) continue;
                        var file = Path.Combine(secretsRoot, id, "secrets.json");
                        if (File.Exists(file)) await ReadJsonAsync(file, $"user secrets: {Path.GetFileName(project)}", directory, found, token).ConfigureAwait(false);
                    }
                }
                catch (Exception ex) when (ex is System.Xml.XmlException or IOException or UnauthorizedAccessException) { }
            }
        }
        return (IReadOnlyList<DiscoveredConnection>)found;
    }, token);

    private static async Task ReadJsonAsync(string file, string source, string directory, List<DiscoveredConnection> found, CancellationToken token)
    {
        try
        {
            // Configuration supports comments and trailing commas, unlike strict JSON.
            using var stream = File.OpenRead(file);
            using var json = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }, token).ConfigureAwait(false);
            if (json.RootElement.ValueKind != JsonValueKind.Object) return;
            foreach (var property in json.RootElement.EnumerateObject())
            {
                if (property.Name.Equals("ConnectionStrings", StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.Object)
                    foreach (var item in property.Value.EnumerateObject()) Add(item.Name, item.Value);
                else if (property.Name.StartsWith("ConnectionStrings:", StringComparison.OrdinalIgnoreCase)) Add(property.Name[18..], property.Value);
            }
            void Add(string name, JsonElement item)
            {
                if (item.ValueKind != JsonValueKind.String || item.GetString() is not { Length: > 0 } value) return;
                var provider = DatabaseConnections.Detect(value);
                if (provider == DatabaseProvider.SQLite) value = DatabaseConnections.ResolveSqlitePath(value, directory);
                found.Add(new DiscoveredConnection(name, source, provider, value));
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or ArgumentException) { }
    }
}
