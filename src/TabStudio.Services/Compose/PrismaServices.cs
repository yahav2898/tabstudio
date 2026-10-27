using System.Text;
using TabStudio.Toolchains.Projects;

namespace TabStudio.Services.Compose;

/// <summary>
/// The database a Prisma schema names, offered in Services for a repository with no Compose file, as
/// gothinkster/node-express-realworld-example-app: its schema's PostgreSQL datasource reads <c>DATABASE_URL</c>, which
/// nothing in the repository sets. TabStudio offers a PostgreSQL (or MySQL) service, F5 starts it, and the run gets the
/// variable pointing at it. A project whose <c>.env</c> already names a database, its own or one on the network, is
/// left alone.
/// </summary>
public static class PrismaServices
{
    /// <summary>The service's name, which the run's connection URL uses as its host until it is rewritten to 127.0.0.1.</summary>
    public const string ServiceName = "db";

    /// <summary>The user and password of the database TabStudio runs, which the app's URL carries.</summary>
    public const string Credential = "tabstudio";

    /// <summary>The Prisma project in a folder, or in a folder one level down with <c>package.json</c>.</summary>
    /// <param name="directory">The repository.</param>
    public static PrismaProject? Find(string directory)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        if (!Directory.Exists(directory))
        {
            return null;
        }

        if (PrismaProject.Detect(directory) is { } project)
        {
            return project;
        }

        try
        {
            return Directory.EnumerateDirectories(directory)
                .Where(d => Path.GetFileName(d) is not ("node_modules" or ".git") && File.Exists(Path.Combine(d, "package.json")))
                .Order(StringComparer.Ordinal)
                .Select(PrismaProject.Detect)
                .FirstOrDefault(p => p is not null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// The services for a Prisma project's database, or <see langword="null"/> when there is none to offer: no Prisma,
    /// SQLite or a database TabStudio does not run, a <c>datasource</c> whose URL is not read from a variable, or a
    /// <c>.env</c> that points the variable at a database that is not on this device.
    /// </summary>
    /// <param name="directory">The repository.</param>
    public static ComposeProject? Discover(string directory)
    {
        if (Find(directory) is not { DatabaseImage: { } image, UrlVariable: { } variable } prisma)
        {
            return null;
        }

        if (prisma.EnvironmentFileUrl() is { } configured && !IsOwn(configured))
        {
            // The project chose its database: TabStudio leaves it alone.
            return null;
        }

        var database = DatabaseName(prisma.Directory);
        var mysql = image.StartsWith("mysql", StringComparison.Ordinal);
        var target = mysql ? 3306 : 5432;
        var environment = mysql
            ? new Dictionary<string, string?>(StringComparer.Ordinal) { ["MYSQL_ROOT_PASSWORD"] = Credential, ["MYSQL_DATABASE"] = database }
            : new Dictionary<string, string?>(StringComparer.Ordinal) { ["POSTGRES_USER"] = Credential, ["POSTGRES_PASSWORD"] = Credential, ["POSTGRES_DB"] = database };
        var url = mysql
            ? $"mysql://root:{Credential}@{ServiceName}:{target}/{database}"
            : $"postgresql://{Credential}:{Credential}@{ServiceName}:{target}/{database}?schema=public";
        var schema = Path.GetRelativePath(directory, Path.Combine(prisma.Directory, prisma.Schema)).Replace('\\', '/');
        var service = new ComposeService(ServiceName, image)
        {
            Environment = environment,
            Ports = [new ServicePort(target, target)],
        };
        return new ComposeProject(Path.GetFullPath(directory), [], new Dictionary<string, ComposeService>(StringComparer.Ordinal) { [ServiceName] = service })
        {
            RunServices = [ServiceName],
            RunEnvironment = new Dictionary<string, string?>(StringComparer.Ordinal) { [variable] = url },
            Origin = schema,
        };
    }

    /// <summary>Whether a connection URL points at this device, where TabStudio's database listens.</summary>
    /// <param name="url">The URL.</param>
    public static bool IsLocal(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Host is "localhost" or "127.0.0.1" or "::1" or "[::1]" or ServiceName;

    /// <summary>
    /// Whether a connection URL is the one TabStudio wrote for its own database: on this device, with TabStudio's
    /// password. A later F5 may give the database another port, so this URL is TabStudio's to update.
    /// </summary>
    /// <param name="url">The URL.</param>
    public static bool IsOwn(string url) =>
        IsLocal(url) && Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.UserInfo.Split(':') is [_, var password] && Uri.UnescapeDataString(password) == Credential;

    /// <summary>A database name from the project folder's name: letters, digits and underscores.</summary>
    private static string DatabaseName(string directory)
    {
        var name = new StringBuilder();
        foreach (var c in Path.GetFileName(Path.TrimEndingDirectorySeparator(directory)).ToLowerInvariant())
        {
            name.Append(char.IsAsciiLetterOrDigit(c) ? c : '_');
        }

        var text = name.ToString().Trim('_');
        return text.Length == 0 || char.IsAsciiDigit(text[0]) ? "app" : text[..Math.Min(text.Length, 48)];
    }
}
