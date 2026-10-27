using System.Net.Sockets;
using System.Text.Json;
using TabStudio.Core.Workspaces;

namespace TabStudio.Core.Compatibility;

/// <summary>
/// Finds projects that use SQL Server, which has no engine for ARM64 Linux and so cannot run on the tablet, and explains
/// in one message what to do instead.
/// </summary>
/// <remarks>
/// A project uses SQL Server when it (or a project it references) references EF Core's SQL Server provider,
/// Microsoft.Data.SqlClient or System.Data.SqlClient, or when its <c>appsettings*.json</c> has a SQL Server connection
/// string. Reading is local and quick; probing the server is <see cref="IsReachableAsync"/>.
/// </remarks>
public static class SqlServerDetector
{
    private static readonly string[] SqlServerPackages =
    [
        "Microsoft.EntityFrameworkCore.SqlServer", "Microsoft.Data.SqlClient", "System.Data.SqlClient", "Hangfire.SqlServer",
        "Dapper.SqlBuilder", "Microsoft.Extensions.Caching.SqlServer", "Aspire.Microsoft.EntityFrameworkCore.SqlServer",
    ];

    private static readonly string[] PostgreSqlPackages = ["Npgsql.EntityFrameworkCore.PostgreSQL", "Npgsql", "Aspire.Npgsql.EntityFrameworkCore.PostgreSQL"];
    private static readonly string[] SqlitePackages = ["Microsoft.EntityFrameworkCore.Sqlite", "Microsoft.EntityFrameworkCore.Sqlite.Core", "Microsoft.Data.Sqlite"];

    /// <summary>The longest a reachability probe waits for a server to accept a connection.</summary>
    public static TimeSpan ProbeTimeout { get; } = TimeSpan.FromSeconds(2);

    /// <summary>Finds whether a project uses SQL Server.</summary>
    /// <param name="project">The project, usually the startup project.</param>
    /// <param name="workspaceProjects">The workspace's projects, to follow project references; may be empty.</param>
    /// <returns>The usage, or <see langword="null"/> when the project does not use SQL Server.</returns>
    public static SqlServerUsage? Detect(ProjectInfo project, IEnumerable<ProjectInfo> workspaceProjects)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(workspaceProjects);
        var packages = PackagesWithReferences(project, workspaceProjects.ToList());
        var evidence = packages.Where(p => SqlServerPackages.Contains(p, StringComparer.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var settings = ReadConnectionStrings(project.DirectoryPath, out var providerSetting);
        var connections = settings
            .Select(s => SqlServerConnectionString.TryParse(s.Value, evidence.Count > 0) is { } server ? new SqlServerConnection(s.Name, s.Value, s.File, server) : null)
            .OfType<SqlServerConnection>()
            .DistinctBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (evidence.Count == 0 && connections.Count == 0)
        {
            return null;
        }

        if (connections.Count > 0)
        {
            evidence.Add($"a SQL Server connection string in {Path.GetFileName(connections[0].SourceFile)}");
        }

        return new SqlServerUsage(
            project.Name,
            evidence,
            connections,
            packages.Any(p => PostgreSqlPackages.Contains(p, StringComparer.OrdinalIgnoreCase)),
            packages.Any(p => SqlitePackages.Contains(p, StringComparer.OrdinalIgnoreCase)),
            providerSetting);
    }

    /// <summary>
    /// Whether anything answers at a SQL Server's address: a TCP connection within <paramref name="timeout"/>. LocalDB,
    /// named pipes and shared memory never answer on a tablet.
    /// </summary>
    /// <param name="server">The server.</param>
    /// <param name="timeout">How long to wait; see <see cref="ProbeTimeout"/>.</param>
    /// <param name="cancellationToken">Cancels the probe.</param>
    public static async Task<bool> IsReachableAsync(SqlServerConnectionString server, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        if (server.Transport != SqlServerTransport.Tcp)
        {
            return false;
        }

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout);
        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync(server.Host, server.Port, limit.Token).ConfigureAwait(false);
            return client.Connected;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    /// <summary>
    /// The one message that explains why the project's SQL Server cannot be used here, and what to do instead.
    /// </summary>
    /// <param name="usage">The project's use of SQL Server.</param>
    /// <param name="unreachable">The connection string nothing answered for, or <see langword="null"/> when there is none to name.</param>
    public static string Explain(SqlServerUsage usage, SqlServerConnection? unreachable)
    {
        ArgumentNullException.ThrowIfNull(usage);
        var where = unreachable is null
            ? "and no SQL Server connection string was found in its appsettings"
            : unreachable.Server.Transport switch
            {
                SqlServerTransport.LocalDb => $"and '{unreachable.Name}' in {Path.GetFileName(unreachable.SourceFile)} uses {unreachable.Server.Server}, which exists only on Windows",
                SqlServerTransport.WindowsOnly => $"and '{unreachable.Name}' in {Path.GetFileName(unreachable.SourceFile)} uses {unreachable.Server.Server}, which works only on the server's own Windows machine",
                _ => $"and nothing answers at {unreachable.Server.Endpoint} ('{unreachable.Name}' in {Path.GetFileName(unreachable.SourceFile)})",
            };
        var choices = "Connect to a SQL Server on your network or in the cloud";
        if (unreachable?.Server.UsesWindowsAuthentication == true)
        {
            choices += " with a SQL login (Windows sign-in is not available here)";
        }

        var alternatives = (usage.SupportsPostgreSql, usage.SupportsSqlite) switch
        {
            (true, true) => ", or switch to PostgreSQL, which runs here in Services, or SQLite",
            (true, false) => ", or switch to PostgreSQL, which runs here in Services",
            (false, true) => ", or switch to SQLite, which runs here",
            _ => string.Empty,
        };
        return $"{usage.ProjectName} uses SQL Server, which has no engine for ARM64 Linux, so it cannot run on this tablet, {where}. {choices}{alternatives}.";
    }

    /// <summary>
    /// Whether an exception from a run comes from SQL Server's client: a <c>SqlException</c>, or a
    /// <c>PlatformNotSupportedException</c> from SqlClient, anywhere in its chain.
    /// </summary>
    /// <param name="exception">The run's unhandled exception.</param>
    public static bool IsSqlServerFailure(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current.GetType().Name == "SqlException"
                || (current is PlatformNotSupportedException && (current.Message.Contains("SqlClient", StringComparison.Ordinal) || current.StackTrace?.Contains("SqlClient", StringComparison.Ordinal) == true))
                || current.StackTrace?.Contains("Microsoft.Data.SqlClient.SqlConnection", StringComparison.Ordinal) == true
                || current.StackTrace?.Contains("System.Data.SqlClient.SqlConnection", StringComparison.Ordinal) == true)
            {
                return true;
            }

            if (current is AggregateException { InnerExceptions.Count: > 0 } aggregate && aggregate.InnerExceptions.Any(IsSqlServerFailure))
            {
                return true;
            }
        }

        return false;
    }

    private static List<string> PackagesWithReferences(ProjectInfo project, List<ProjectInfo> workspaceProjects)
    {
        var byPath = workspaceProjects.GroupBy(p => Path.GetFullPath(p.FilePath), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var packages = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<ProjectInfo>([project]);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!seen.Add(Path.GetFullPath(current.FilePath)))
            {
                continue;
            }

            packages.AddRange(current.PackageReferences.Select(p => p.Name));
            foreach (var reference in current.ProjectReferences)
            {
                var path = Path.GetFullPath(Path.IsPathRooted(reference) ? reference : Path.Combine(current.DirectoryPath, reference));
                if (byPath.TryGetValue(path, out var referenced))
                {
                    pending.Push(referenced);
                }
            }
        }

        return packages;
    }

    /// <summary>Reads <c>ConnectionStrings</c> from <c>appsettings.json</c> and <c>appsettings.*.json</c>, the base file first.</summary>
    private static List<(string Name, string Value, string File)> ReadConnectionStrings(string directory, out KeyValuePair<string, string>? providerSetting)
    {
        providerSetting = null;
        var result = new List<(string, string, string)>();
        if (!Directory.Exists(directory))
        {
            return result;
        }

        var files = Directory.EnumerateFiles(directory, "appsettings*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(f => Path.GetFileName(f).Equals("appsettings.json", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(f => f, StringComparer.Ordinal);
        foreach (var file in files)
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(file), new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (property.Name.Equals("ConnectionStrings", StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.Object)
                    {
                        result.AddRange(property.Value.EnumerateObject()
                            .Where(c => c.Value.ValueKind == JsonValueKind.String)
                            .Select(c => (c.Name, c.Value.GetString()!, file)));
                    }
                    else if (property.Name.StartsWith("ConnectionStrings:", StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.String)
                    {
                        result.Add((property.Name["ConnectionStrings:".Length..], property.Value.GetString()!, file));
                    }
                    else if (providerSetting is null && IsProviderSetting(property))
                    {
                        providerSetting = new KeyValuePair<string, string>(property.Name, property.Value.GetString()!);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                // A configuration file that cannot be read is reported by the app itself when it runs.
            }
        }

        return result;
    }

    /// <summary>A top-level setting such as <c>"DatabaseProvider": "SqlServer"</c> that chooses the database.</summary>
    private static bool IsProviderSetting(JsonProperty property) =>
        property.Value.ValueKind == JsonValueKind.String
        && (property.Name.Contains("Provider", StringComparison.OrdinalIgnoreCase) || property.Name.Contains("DatabaseType", StringComparison.OrdinalIgnoreCase)
            || property.Name.Contains("DbType", StringComparison.OrdinalIgnoreCase))
        && property.Value.GetString() is { } value
        && (value.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) || value.Contains("MsSql", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) || value.Contains("Postgre", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Npgsql", StringComparison.OrdinalIgnoreCase));
}
