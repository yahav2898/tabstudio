using TabStudio.Toolchains.Projects;

namespace TabStudio.Services.Compose;

/// <summary>
/// The database a Laravel app's <c>.env</c> names (<c>DB_CONNECTION=mysql</c>, <c>mariadb</c> or <c>pgsql</c> on this
/// device), offered in Services for a repository with no Compose file, unless a database already answers there. F5 starts it, and the app gets <c>DB_HOST</c>,
/// <c>DB_PORT</c>, <c>DB_DATABASE</c>, <c>DB_USERNAME</c> and <c>DB_PASSWORD</c> for it; Laravel reads the process's
/// environment before its <c>.env</c>, so the file is never changed. SQLite and a database on the network are the
/// app's own.
/// </summary>
public static class LaravelServices
{
    /// <summary>The service's name, which makes Services set <c>DB_HOST</c> and <c>DB_PORT</c> for it.</summary>
    public const string ServiceName = "db";

    /// <summary>The password of the database TabStudio runs when the app's <c>.env</c> leaves it empty and the server needs one.</summary>
    public const string Credential = "tabstudio";

    private static bool IsListening(string host, int port)
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            return client.ConnectAsync(host == "localhost" ? "127.0.0.1" : host, port).Wait(TimeSpan.FromSeconds(2)) && client.Connected;
        }
        catch (Exception ex) when (ex is AggregateException or System.Net.Sockets.SocketException or ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// A Laravel app's Compose file, as Laravel Sail writes it, with its run pointed at the database its <c>.env</c>
    /// names: F5 starts only that service (the app's own container is the IDE's run), and the app gets
    /// <c>DB_HOST</c> and <c>DB_PORT</c> for it. Any other Compose file is returned as it is.
    /// </summary>
    /// <param name="compose">The repository's Compose model, if it has one.</param>
    /// <param name="directory">The app's folder.</param>
    public static ComposeProject? WithCompose(ComposeProject? compose, string directory)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        if (compose is null || compose.RunServices.Count > 0 || !Directory.Exists(directory) || PhpProject.Detect(directory) != PhpProjectKind.Laravel)
        {
            return compose;
        }

        var settings = Path.Combine(directory, ".env");
        if (!File.Exists(settings))
        {
            settings = Path.Combine(directory, ".env.example");
        }

        if (PhpProject.EnvValue(settings, "DB_HOST") is not { Length: > 0 } host || !compose.Services.ContainsKey(host)
            || PhpProject.EnvValue(settings, "DB_CONNECTION") is "sqlite")
        {
            return compose;
        }

        var environment = new Dictionary<string, string?>(compose.RunEnvironment, StringComparer.Ordinal) { ["DB_HOST"] = host };
        return compose with
        {
            RunServices = [host],
            RunEnvironment = environment,
            RunPorts = new Dictionary<string, string>(compose.RunPorts, StringComparer.Ordinal) { ["DB_PORT"] = host },
        };
    }

    /// <summary>
    /// The services for a Laravel app's database, or <see langword="null"/> when there is none to offer: not a
    /// Laravel app, SQLite or another connection TabStudio does not run, or a host that is not on this device.
    /// </summary>
    /// <param name="directory">The app's folder.</param>
    public static ComposeProject? Discover(string directory)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        if (!Directory.Exists(directory) || PhpProject.Detect(directory) != PhpProjectKind.Laravel)
        {
            return null;
        }

        var settings = Path.Combine(directory, ".env");
        if (!File.Exists(settings))
        {
            settings = Path.Combine(directory, ".env.example");
        }

        var connection = PhpProject.EnvValue(settings, "DB_CONNECTION");
        var image = connection switch
        {
            "mysql" or "mariadb" => "mariadb:11",
            "pgsql" => "postgres:17",
            _ => null,
        };
        var host = PhpProject.EnvValue(settings, "DB_HOST") is { Length: > 0 } configured ? configured : "127.0.0.1";
        if (image is null || host is not ("127.0.0.1" or "localhost" or "::1" or ServiceName or "mysql" or "mariadb" or "pgsql" or "postgres" or "database"))
        {
            return null;
        }

        var mysql = image.StartsWith("mariadb", StringComparison.Ordinal);
        var target = mysql ? 3306 : 5432;

        // A database already answering where .env points, such as one the user runs, is the app's own.
        var port = int.TryParse(PhpProject.EnvValue(settings, "DB_PORT"), out var configuredPort) ? configuredPort : target;
        if (host is "127.0.0.1" or "localhost" or "::1" && IsListening(host, port))
        {
            return null;
        }

        var database = PhpProject.EnvValue(settings, "DB_DATABASE") is { Length: > 0 } name ? name : "laravel";
        var user = PhpProject.EnvValue(settings, "DB_USERNAME") is { Length: > 0 } username ? username : "root";
        var password = PhpProject.EnvValue(settings, "DB_PASSWORD") ?? string.Empty;
        Dictionary<string, string?> environment;
        if (mysql && string.Equals(user, "root", StringComparison.Ordinal))
        {
            environment = password.Length == 0
                ? new(StringComparer.Ordinal) { ["MYSQL_ALLOW_EMPTY_PASSWORD"] = "yes", ["MYSQL_DATABASE"] = database }
                : new(StringComparer.Ordinal) { ["MYSQL_ROOT_PASSWORD"] = password, ["MYSQL_DATABASE"] = database };
        }
        else if (mysql)
        {
            password = password.Length == 0 ? Credential : password;
            environment = new(StringComparer.Ordinal)
            {
                ["MYSQL_ROOT_PASSWORD"] = Credential, ["MYSQL_USER"] = user, ["MYSQL_PASSWORD"] = password, ["MYSQL_DATABASE"] = database,
            };
        }
        else
        {
            // PostgreSQL's server needs a password.
            password = password.Length == 0 ? Credential : password;
            environment = new(StringComparer.Ordinal) { ["POSTGRES_USER"] = user, ["POSTGRES_PASSWORD"] = password, ["POSTGRES_DB"] = database };
        }

        var service = new ComposeService(ServiceName, image)
        {
            Environment = environment,
            Ports = [new ServicePort(target, target)],
        };
        return new ComposeProject(Path.GetFullPath(directory), [], new Dictionary<string, ComposeService>(StringComparer.Ordinal) { [ServiceName] = service })
        {
            RunServices = [ServiceName],
            RunEnvironment = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["DB_CONNECTION"] = connection,
                ["DB_HOST"] = ServiceName,
                ["DB_DATABASE"] = database,
                ["DB_USERNAME"] = user,
                ["DB_PASSWORD"] = password,
            },
            Origin = Path.GetFileName(settings),
        };
    }
}
