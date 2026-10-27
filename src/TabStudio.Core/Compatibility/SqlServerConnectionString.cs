using System.Globalization;

namespace TabStudio.Core.Compatibility;

/// <summary>
/// Where a SQL Server connection string points, as far as TabStudio needs to know it: the host and port to probe and
/// whether it signs in with Windows.
/// </summary>
/// <param name="Server">The server as written, such as <c>.</c>, <c>tcp:db.example.com,1433</c> or <c>(localdb)\MSSQLLocalDB</c>.</param>
/// <param name="Host">The host to connect to: <c>127.0.0.1</c> for <c>.</c>, <c>(local)</c> and <c>localhost</c>.</param>
/// <param name="Port">The TCP port: the one given after a comma, otherwise 1433.</param>
/// <param name="Transport">How the server is reached.</param>
/// <param name="Database">The database (<c>Initial Catalog</c> or <c>Database</c>), if it names one.</param>
/// <param name="UsesWindowsAuthentication">
/// Whether it signs in with Windows (<c>Trusted_Connection</c> or <c>Integrated Security</c>), which a tablet cannot.
/// </param>
/// <param name="InstanceName">A named instance (<c>host\SQLEXPRESS</c>), whose port SQL Server Browser assigns.</param>
public sealed record SqlServerConnectionString(
    string Server,
    string Host,
    int Port,
    SqlServerTransport Transport,
    string? Database,
    bool UsesWindowsAuthentication,
    string? InstanceName = null)
{
    /// <summary>The port SQL Server listens on by default.</summary>
    public const int DefaultPort = 1433;

    private static readonly string[] ServerKeys = ["server", "data source", "address", "addr", "network address"];
    private static readonly string[] DatabaseKeys = ["initial catalog", "database"];

    /// <summary>Keys only SQL Server's connection strings use, which make a string SQL Server's without other evidence.</summary>
    private static readonly string[] SqlServerOnlyKeys =
    [
        "initial catalog", "trusted_connection", "integrated security", "multipleactiveresultsets", "trustservercertificate",
        "attachdbfilename", "applicationintent", "multisubnetfailover", "persist security info", "user instance",
    ];

    /// <summary>Keys that belong to other databases' connection strings, such as PostgreSQL's <c>Host</c>.</summary>
    private static readonly string[] OtherDatabaseKeys = ["host", "port", "uid", "pwd", "sslmode", "ssl mode", "mode", "cache", "filename"];

    /// <summary>Where the server is, for messages: <c>db.example.com:1433</c>, or the LocalDB instance.</summary>
    public string Endpoint => Transport == SqlServerTransport.Tcp
        ? string.Create(CultureInfo.InvariantCulture, $"{Host}:{Port}")
        : Server;

    /// <summary>
    /// Reads a connection string as SQL Server's, when it looks like one: it names a server and a database, or uses a
    /// key only SQL Server has, such as <c>Initial Catalog</c> or <c>Trusted_Connection</c>.
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="projectUsesSqlServer">
    /// Whether the project references a SQL Server client, which makes an ambiguous <c>Server=…;Database=…</c> string
    /// SQL Server's rather than MySQL's.
    /// </param>
    /// <returns>The server, or <see langword="null"/> when the string is not SQL Server's.</returns>
    public static SqlServerConnectionString? TryParse(string? connectionString, bool projectUsesSqlServer)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        var values = Split(connectionString);
        var server = ServerKeys.Select(k => values.GetValueOrDefault(k)).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
        if (server is null)
        {
            return null;
        }

        var database = DatabaseKeys.Select(k => values.GetValueOrDefault(k)).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
        var sqlServerOnly = SqlServerOnlyKeys.Any(values.ContainsKey) || server.StartsWith("(localdb)", StringComparison.OrdinalIgnoreCase);
        var otherDatabase = OtherDatabaseKeys.Any(values.ContainsKey);
        var looksLikeFile = database is null && (server.EndsWith(".db", StringComparison.OrdinalIgnoreCase)
            || server.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase) || server.Contains(':', StringComparison.Ordinal) && server.Contains('/', StringComparison.Ordinal)
            || server == ":memory:");
        if (looksLikeFile || (!sqlServerOnly && (otherDatabase || database is null || !projectUsesSqlServer)))
        {
            return null;
        }

        var integrated = IsTrue(values.GetValueOrDefault("trusted_connection")) || IsTrue(values.GetValueOrDefault("integrated security"));
        return Describe(server, database, integrated);
    }

    private static SqlServerConnectionString Describe(string server, string? database, bool integrated)
    {
        if (server.StartsWith("(localdb)", StringComparison.OrdinalIgnoreCase))
        {
            return new SqlServerConnectionString(server, server, 0, SqlServerTransport.LocalDb, database, integrated);
        }

        var address = server;
        if (address.StartsWith("np:", StringComparison.OrdinalIgnoreCase) || address.StartsWith("lpc:", StringComparison.OrdinalIgnoreCase)
            || address.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return new SqlServerConnectionString(server, server, 0, SqlServerTransport.WindowsOnly, database, integrated);
        }

        if (address.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
        {
            address = address[4..];
        }

        var port = DefaultPort;
        var comma = address.LastIndexOf(',');
        if (comma >= 0)
        {
            if (int.TryParse(address[(comma + 1)..].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var given) && given is > 0 and < 65536)
            {
                port = given;
            }

            address = address[..comma];
        }

        string? instance = null;
        var slash = address.IndexOf('\\', StringComparison.Ordinal);
        if (slash >= 0)
        {
            instance = address[(slash + 1)..].Trim();
            address = address[..slash];
        }

        var host = address.Trim();
        if (host is "." or "(local)" or "" || host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            host = "127.0.0.1";
        }

        return new SqlServerConnectionString(server, host, port, SqlServerTransport.Tcp, database, integrated, instance);
    }

    /// <summary>Splits <c>key=value;</c> pairs, lower-casing keys and honouring quoted values.</summary>
    private static Dictionary<string, string> Split(string connectionString)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var index = 0;
        while (index < connectionString.Length)
        {
            var equals = connectionString.IndexOf('=', index);
            if (equals < 0)
            {
                break;
            }

            var key = connectionString[index..equals].Trim().ToLowerInvariant();
            index = equals + 1;
            while (index < connectionString.Length && connectionString[index] == ' ')
            {
                index++;
            }

            string value;
            if (index < connectionString.Length && connectionString[index] is '"' or '\'')
            {
                var quote = connectionString[index];
                var end = connectionString.IndexOf(quote, index + 1);
                end = end < 0 ? connectionString.Length : end;
                value = connectionString[(index + 1)..end];
                index = connectionString.IndexOf(';', Math.Min(end, connectionString.Length));
                index = index < 0 ? connectionString.Length : index + 1;
            }
            else
            {
                var semicolon = connectionString.IndexOf(';', index);
                semicolon = semicolon < 0 ? connectionString.Length : semicolon;
                value = connectionString[index..semicolon];
                index = semicolon + 1;
            }

            if (key.Length > 0)
            {
                values[key] = value;
            }
        }

        return values;
    }

    private static bool IsTrue(string? value) =>
        value is not null && (value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase)
            || value.Trim().Equals("sspi", StringComparison.OrdinalIgnoreCase));
}
